using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.Stages;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public sealed class OverlayHandoffObservationConfig
    {
        public int Version;
        public uint AppId;
        public ulong SteamId;
        public string PayloadManifestPath, EvidenceRoot;
    }

    public sealed class OverlayHandoffObservationRuntime : IOverlayHandoffObservationRuntime
    {
        private readonly OverlayHandoffObservationOptions options;
        private readonly ISavePathProvider paths;
        private readonly OverlayHandoffObservationConfig config;
        private readonly OverlayObservationContext context;
        private readonly string roleDirectory, configPath, saveManifestPath;
        private readonly ProcessIdentity self;
        private readonly ProcessIdentity steam;
        private readonly Func<ProcessStartInfo, Process> createProcess;
        private readonly double started = OverlayObservationEvidence.MonotonicSeconds();
        private ObservationSaveManifest initialSave;
        private string firstCoreError, originObservationHash;
        private ExperimentRequest request;
        private string requestPath;
        private int recordSequence;
        public string EvidenceDirectory => context.Directory;
        public double Now => OverlayObservationEvidence.MonotonicSeconds();
        public string ObservationFailure => firstCoreError ?? SteamOverlayObservationAccess.NativeFailure;

        public OverlayHandoffObservationRuntime(OverlayHandoffObservationOptions options, ISavePathProvider paths,
            Func<ProcessStartInfo, Process> createProcess = null)
        {
            this.options = options; this.paths = paths; this.createProcess = createProcess ?? Process.Start;
            if (options.Error != null) throw options.Error;
            self = CaptureSelf();
            if (options.Role == OverlayObservationRole.OriginObserver)
            {
                configPath = ResetOverlayTrialFiles.EvidencePath(options.Path);
                config = ResetOverlayTrialFiles.ReadPinned<OverlayHandoffObservationConfig>(configPath, null, out var configHash);
                ValidateConfig(config);
                var ready = ReadReady();
                string runId = Guid.NewGuid().ToString("N");
                context = new OverlayObservationContext { Version = 2, RunId = runId, Role = options.Role,
                    Directory = Path.Combine(ResetOverlayTrialFiles.EvidencePath(config.EvidenceRoot), DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + runId),
                    ConfigurationPath = configPath, ConfigurationSha256 = configHash, Origin = self,
                    OperationId = ready.OperationId, MappingVersion = ready.MappingVersion, ReadyState = ready.State,
                    AppId = config.AppId, SteamId = config.SteamId };
                if (Directory.Exists(context.Directory)) throw new IOException("Run evidence already exists.");
                var manifest = ResetOverlayTrialFiles.ReadPinned<TrialPayloadManifest>(ResetOverlayTrialFiles.EvidencePath(config.PayloadManifestPath), null, out var hash);
                context.ManifestSha256 = hash;
                ResetOverlayTrialFiles.VerifyPayload(Path.GetDirectoryName(self.Path), manifest);
                initialSave = OverlayObservationEvidence.CaptureSave(paths.SaveRootPath);
                steam = WindowsIdentityCapture.Steam(self);
                if (steam == null) throw new IOException("Original Steam client is unavailable.");
                Directory.CreateDirectory(context.Directory);
            }
            else
            {
                string contextPath = ResetOverlayTrialFiles.EvidencePath(options.Path);
                context = ExperimentFiles.Read<OverlayObservationContext>(contextPath);
                OverlayObservationWire.ValidateContext(context, options.Role);
                OverlayObservationEvidence.Under(ResetOverlayTrialFiles.EvidencePath(context.Directory), contextPath);
                configPath = ResetOverlayTrialFiles.EvidencePath(context.ConfigurationPath);
                config = ResetOverlayTrialFiles.ReadPinned<OverlayHandoffObservationConfig>(configPath, context.ConfigurationSha256, out _);
                ValidateConfig(config);
                requestPath = OverlayObservationEvidence.Under(context.Directory, ResetOverlayTrialFiles.EvidencePath(options.RequestPath));
                request = ExperimentFiles.Read<ExperimentRequest>(requestPath);
                context = OverlayObservationWire.ReadContext(request);
                if (!string.Equals(request.OverlayObservationContextPath, contextPath, StringComparison.OrdinalIgnoreCase) ||
                    context.AppId != config.AppId || context.SteamId != config.SteamId ||
                    request.OverlayObservationRunId != context.RunId || request.Parent.Sha256 != self.Sha256 ||
                    !string.Equals(request.Parent.Path, self.Path, StringComparison.OrdinalIgnoreCase) ||
                    !WindowsIdentityCapture.SameScope(request.Parent, self) || WindowsIdentityCapture.SameProcess(request.Parent, self))
                    throw new IOException("Observation child ownership mismatch.");
                steam = request.Steam;
            }
            roleDirectory = OverlayObservationEvidence.Under(context.Directory, Path.Combine(context.Directory, options.Role.ToString()));
            if (Directory.Exists(roleDirectory)) throw new IOException("This observation role was already claimed.");
            Directory.CreateDirectory(roleDirectory);
            saveManifestPath = Path.Combine(context.Directory, "participant-files.json");
            if (options.Role == OverlayObservationRole.OriginObserver)
            {
                ResetOverlayTrialFiles.Create(saveManifestPath, initialSave);
                context.SaveManifestSha256 = ExperimentFiles.Hash(saveManifestPath);
            }
            else
            {
                initialSave = ResetOverlayTrialFiles.ReadPinned<ObservationSaveManifest>(saveManifestPath, context.SaveManifestSha256, out _);
            }
            Record("RoleEntered", new { Process = PublicIdentity(self), Client = PublicIdentity(steam),
                ArgumentCounts = options.ArgumentCounts,
                JsonAndBackupBytesOnly = true, SessionLockExcluded = true });
        }

        public static void ValidateConfig(OverlayHandoffObservationConfig value)
        {
            if (value == null || value.Version != 1 || value.AppId != 5218360 || value.SteamId == 0)
                throw new IOException("Invalid observation configuration identity/version.");
            ResetOverlayTrialFiles.EvidencePath(value.PayloadManifestPath);
            ResetOverlayTrialFiles.EvidencePath(value.EvidenceRoot);
        }

        private ResetRecord ReadReady()
        {
            var row = new FileExhibitionResetJournal(Path.Combine(paths.SaveRootPath, "exhibition-reset.json")).Load();
            ValidateReady(row, config, context);
            return row;
        }

        public static void ValidateReady(ResetRecord row, OverlayHandoffObservationConfig config, OverlayObservationContext context)
        {
            if (row == null || row.State != ResetRecord.Ready || row.AppId != config.AppId || row.SteamId != config.SteamId ||
                row.MappingVersion != ExhibitionResetCoordinator.MappingVersion)
                throw new IOException("An unchanged Ready record for the configured account and mapping is required.");
            if (context != null && (row.OperationId != context.OperationId || row.State != context.ReadyState || row.MappingVersion != context.MappingVersion))
                throw new IOException("Initial Ready identity changed.");
        }

        public bool NativeReady()
        {
            if (PlatformRuntimeRegistry.HasSelection && !PlatformRuntimeRegistry.CurrentSelection.IsSuccess)
                throw new InvalidOperationException("Provider selection failed: " + PlatformRuntimeRegistry.CurrentSelection.FailureReason);
            var runtime = SteamOverlayObservationAccess.Runtime;
            if (runtime == null) return false;
            var state = runtime.Diagnostics.State;
            if (state == SteamPlatformRuntimeState.Faulted || state == SteamPlatformRuntimeState.Unavailable ||
                state == SteamPlatformRuntimeState.Shutdown || state == SteamPlatformRuntimeState.ShuttingDown)
                throw new InvalidOperationException("Native runtime failed: " + runtime.Diagnostics.LastFailureReason);
            return state == SteamPlatformRuntimeState.Available;
        }

        public bool ReceiptReady()
        {
            string path = OverlayObservationWire.ReceiptPath(request);
            if (!File.Exists(path)) return false;
            var receipt = ExperimentFiles.Read<OverlayObservationReceipt>(path);
            if (!OverlayObservationWire.Matches(receipt, request, context, self)) throw new IOException("Child receipt mismatch.");
            var environment = new WindowsCycleEnvironment(request, requestPath);
            if (environment.ParentAlive()) throw new IOException("Original process has not exited.");
            Record("ChildReceiptValidated", new { Ready = false, HelperTerminalReviewed = false });
            return true;
        }

        public async Task PrepareAsync(Action guard)
        {
            await ValidatePinsAsync(guard); guard();
            await RecordSaveAsync("Prepared", guard); guard();
            if (options.Role == OverlayObservationRole.OriginObserver)
            {
                OverlayObservationWire.ValidateContext(context, options.Role);
                ResetOverlayTrialFiles.Create(Path.Combine(context.Directory, "origin-context.json"), context);
                guard();
            }
        }

        public async Task RevalidateAsync(Action guard)
        {
            ValidateOriginReport(); guard();
            await ValidatePinsAsync(guard); guard();
            await RecordSaveAsync("BeforeHandoff", guard); guard();
            var check = MakeRequest();
            var environment = new WindowsCycleEnvironment(check, Path.Combine(context.Directory, "handoff-GameOnly", "request.json"));
            environment.EnsureNoOtherGame(); guard();
            if (!environment.OriginalSteamAlive()) throw new IOException("Original Steam client exited.");
            guard();
        }

        private void ValidateOriginReport()
        {
            var child = ReplacementContext();
            OverlayObservationWire.ValidateOriginReport(child);
        }

        private OverlayObservationContext ReplacementContext()
        {
            var child = ExperimentFiles.Parse<OverlayObservationContext>(ExperimentFiles.Json(context));
            child.Role = OverlayObservationRole.ReplacementObserver;
            child.OriginObservationPath = Path.Combine(context.Directory, OverlayObservationRole.OriginObserver.ToString(), "user-observation.json");
            child.OriginObservationSha256 = originObservationHash;
            return child;
        }

        private async Task ValidatePinsAsync(Action guard)
        {
            guard();
            await Task.Run(() => ReadReady()); guard();
            await Task.Run(() => ResetOverlayTrialFiles.ReadPinned<OverlayHandoffObservationConfig>(configPath, context.ConfigurationSha256, out _)); guard();
            var manifest = await Task.Run(() => ResetOverlayTrialFiles.ReadPinned<TrialPayloadManifest>(config.PayloadManifestPath, context.ManifestSha256, out _)); guard();
            await Task.Run(() => ResetOverlayTrialFiles.VerifyPayload(Path.GetDirectoryName(self.Path), manifest)); guard();
            var identity = SteamOverlayObservationAccess.Runtime.ReadObservationIdentity();
            guard();
            if (!identity.Valid || !identity.LoggedOn || identity.AppId != context.AppId || identity.SteamId != context.SteamId)
                throw new IOException("Live Steam account or AppID changed.");
            var currentSteam = WindowsIdentityCapture.Steam(self);
            guard();
            if (currentSteam == null || !WindowsIdentityCapture.SameProcess(steam, currentSteam) || steam.Sha256 != currentSteam.Sha256 ||
                !WindowsIdentityCapture.SameScope(steam, currentSteam) || !string.Equals(steam.Path, currentSteam.Path, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Steam client identity changed.");
            Record("PinsValidated", new { AccountMatches = true, ReadyMatches = true, PayloadMatches = true, ClientMatches = true });
        }

        private async Task RecordSaveAsync(string stage, Action guard)
        {
            var current = await Task.Run(() => OverlayObservationEvidence.CaptureSave(paths.SaveRootPath));
            guard();
            Record("ParticipantManifest:" + stage, current);
            OverlayObservationEvidence.RequireSame(initialSave, current);
        }

        private void RecordSave(string stage)
        {
            var current = OverlayObservationEvidence.CaptureSave(paths.SaveRootPath);
            Record("ParticipantManifest:" + stage, current);
            OverlayObservationEvidence.RequireSame(initialSave, current);
        }

        public void Claim()
        {
            ResetOverlayTrialFiles.Create(Path.Combine(context.Directory, "handoff.claimed.json"),
                new ObservationClaim { RunId = context.RunId, Utc = DateTime.UtcNow.ToString("o"), Pid = self.Pid, StartTicks = self.StartTicks });
        }

        private ExperimentRequest MakeRequest() => new ExperimentRequest { Nonce = Guid.NewGuid().ToString("N"), Parent = self, Steam = steam,
            Trial = Trial.GameOnly, AppId = context.AppId, SteamId = context.SteamId, OperationId = context.OperationId,
            Build = Application.buildGUID + "/" + Application.version, EvidenceDirectory = Path.Combine(context.Directory, "handoff-GameOnly"),
            ToolsDirectory = Path.Combine(Path.GetDirectoryName(self.Path), "RestartExperiment"), OverlayObservationWireVersion = 2,
            OverlayObservationRunId = context.RunId, OverlayObservationChildRole = OverlayObservationRole.ReplacementObserver,
            OverlayObservationContextPath = Path.Combine(context.Directory, "replacement-context.json") };

        public HandoffResult StartHelper(Action finalGuard)
        {
            request = MakeRequest();
            requestPath = Path.Combine(request.EvidenceDirectory, "request.json");
            Directory.CreateDirectory(request.EvidenceDirectory);
            var childContext = ReplacementContext();
            OverlayObservationWire.ValidateContext(childContext, childContext.Role);
            OverlayObservationWire.ValidateOriginReport(childContext);
            ResetOverlayTrialFiles.Create(request.OverlayObservationContextPath, childContext);
            request.OverlayObservationContextSha256 = ExperimentFiles.Hash(request.OverlayObservationContextPath);
            ResetOverlayTrialFiles.Create(requestPath, request);
            using (var process = LaunchEnvironment.Start(() =>
            {
                var environment = new WindowsCycleEnvironment(request, requestPath);
                environment.Validate(); environment.EnsureNoOtherGame();
                if (!environment.OriginalSteamAlive()) throw new IOException("Original Steam exited before helper request.");
                var start = ExperimentFiles.HostStart(request.ToolsDirectory, requestPath, false);
                Record("HelperLaunchPrepared");
                Record("HelperRequestPrepared", new { request.Nonce, RequestPath = requestPath, ActualCreationConfirmed = false });
                return start;
            }, null, start => { finalGuard(); return createProcess(start); }))
            {
                if (process == null) return HandoffResult.NotStarted;
                try
                {
                    ResetOverlayTrialFiles.Create(Path.Combine(request.EvidenceDirectory, "helper-created.json"),
                        new ObservationHelperCreation { Version = 1, Nonce = request.Nonce, Observer = WindowsIdentityCapture.Capture(process, false) });
                }
                catch (Exception e) { throw new OverlayHandoffAcceptedEvidenceException(e); }
                return HandoffResult.Accepted;
            }
        }

        public async Task SaveObservationAsync(OverlayVisibility visibility)
        {
            var row = new OverlayUserObservation { RunId = context.RunId, Role = options.Role, Process = self,
                Source = "User", AttemptReported = visibility != OverlayVisibility.Inconclusive,
                Visibility = visibility == OverlayVisibility.Opened ? "opened" : visibility == OverlayVisibility.NotVisible ? "not-visible" : "inconclusive",
                Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = Now };
            string path = Path.Combine(roleDirectory, "user-observation.json");
            originObservationHash = await Task.Run(() =>
            {
                ResetOverlayTrialFiles.Create(path, row);
                return ExperimentFiles.Hash(path);
            });
        }

        public Task DelayAsync() => Task.Delay(25);
        public void FinalSnapshot()
        {
            Exception first = null;
            try { RecordSave("BeforeExit"); } catch (Exception e) { first = e; }
            if (options.Role == OverlayObservationRole.ReplacementObserver)
            {
                try { ReadHelperTerminal(); } catch (Exception e) { if (first == null) first = e; }
            }
            if (first != null) throw first;
        }
        private void ReadHelperTerminal()
        {
            var creation = ExperimentFiles.Read<ObservationHelperCreation>(Path.Combine(request.EvidenceDirectory, "helper-created.json"));
            if (creation == null || creation.Version != 1 || creation.Nonce != request.Nonce || creation.Observer == null)
                throw new IOException("Helper creation identity is unavailable.");
            string path = Path.Combine(request.EvidenceDirectory, "events-" + creation.Observer.Pid + ".jsonl");
            if (!File.Exists(path) || new FileInfo(path).Length > 262144) throw new IOException("Helper terminal evidence is missing or oversized.");
            var terminal = File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(ExperimentFiles.Parse<ExperimentObservation>).Where(row => row.Stage == "HelperCompleted" || row.Stage == "HelperFailed").ToArray();
            if (terminal.Length != 1) throw new IOException("Exactly one helper terminal record is required at final review.");
            var row = terminal[0];
            if (row.Observer == null || row.Child == null || row.Nonce != request.Nonce || row.OperationId != context.OperationId ||
                !WindowsIdentityCapture.SameProcess(row.Observer, creation.Observer) || !WindowsIdentityCapture.SameScope(row.Observer, creation.Observer) ||
                row.Observer.Path != creation.Observer.Path || !WindowsIdentityCapture.SameProcess(row.Child, self) || row.Child.Path != self.Path)
                throw new IOException("Helper terminal identity mismatch.");
            Record("HelperTerminalObserved", new { row.Stage, row.Error, Observer = PublicIdentity(row.Observer), ChildExitConfirmed = false, SteamTrackingReleased = false });
            if (row.Stage != "HelperCompleted" || row.Error != null) throw new IOException("Helper failed; the comparison is ineligible.");
        }
        public sealed class ObservationHelperCreation { public int Version; public string Nonce; public ProcessIdentity Observer; }

        public void Quit() { Application.Quit(); }

        public void Record(string stage, object value = null)
        {
            var row = new { Utc = DateTime.UtcNow.ToString("o"), Elapsed = Now - started, context.RunId,
                Role = options.Role.ToString(), self.Pid, self.StartTicks, Sequence = ++recordSequence, Stage = stage, Value = value };
            try { File.AppendAllText(Path.Combine(roleDirectory, "events.jsonl"), OverlayObservationEvidence.Json(row) + Environment.NewLine); }
            catch (Exception e) { if (firstCoreError == null) firstCoreError = OverlayObservationEvidence.Limit(e.Message); throw; }
        }

        private static object PublicIdentity(ProcessIdentity identity) => new { identity.Pid, identity.StartTicks, identity.Path, identity.Sha256 };
        private static ProcessIdentity CaptureSelf() { using (var process = Process.GetCurrentProcess()) return WindowsIdentityCapture.Capture(process, true); }
        public sealed class ObservationClaim { public string RunId, Utc; public int Pid; public long StartTicks; }
    }
}
