using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.Stages;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using Game.Platform.Steam.ProductAchievements;
using Game.Product.Achievements.Infrastructure;
using Game.Product.Achievements;
using Game.Product.Achievements.Composition;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    public sealed class ResetOverlayTrialConfig
    {
        public int Version;
        public uint AppId;
        public ulong SteamId;
        public string PayloadManifestPath;
    }
    public sealed class ResetOverlayTrialObservation
    {
        public string Utc, TrialId, OperationId, Stage, Error;
        public ResetOverlayRole Role;
        public int Sequence;
        public double MonotonicSeconds;
        public ProcessIdentity Process;
        public uint AppId;
        public ulong SteamId;
    }
    public sealed class ResetOverlayResult
    {
        public int Version;
        public string TrialId, OperationId, MappingVersion, ReadyState, Utc, FilesPath, FilesSha256, VerificationError;
        public uint AppId;
        public ulong SteamId;
        public ProcessIdentity Process;
        public double MonotonicSeconds;
        public bool LocalVerified;
        public string[] Names;
        public bool[] QuerySucceeded, Achieved;
    }
    public delegate bool ResetOverlayAchievementRead(string name, out bool achieved);

    public sealed class TrialPayloadManifest
    {
        public int fileCount;
        public TrialPayloadFile[] files;
    }
    public sealed class TrialPayloadFile { public string relativePath, sha256; }

    public static class ResetOverlayTrialFiles
    {
        public static void Create<T>(string path, T value)
        {
            // CreateNew is a durable one-shot claim. A partial/failed write also consumes the step.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(ExperimentFiles.Json(value));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }
        public static string EvidencePath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new IOException("Missing trial evidence path.");
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(@"D:\J2M\evidence\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Trial evidence must be on D.");
            return full;
        }
        public static T ReadPinned<T>(string path, string expectedHash, out string hash)
        {
            var bytes = File.ReadAllBytes(path);
            using (var digest = System.Security.Cryptography.SHA256.Create())
                hash = BitConverter.ToString(digest.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            if (expectedHash != null && hash != expectedHash) throw new IOException("Trial input changed: " + Path.GetFileName(path));
            using (var stream = new MemoryStream(bytes))
                return (T)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        public static void VerifyPayload(string root, TrialPayloadManifest manifest)
        {
            if (manifest?.files == null || manifest.fileCount != manifest.files.Length || manifest.files.Length == 0) throw new IOException("Incomplete payload manifest.");
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var expected = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.files)
            {
                if (entry == null || string.IsNullOrEmpty(entry.relativePath) || string.IsNullOrEmpty(entry.sha256) || Path.IsPathRooted(entry.relativePath)) throw new IOException("Incomplete payload entry.");
                var file = Path.GetFullPath(Path.Combine(prefix, entry.relativePath));
                if (!file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !expected.Add(file) || !File.Exists(file) || ExperimentFiles.Hash(file) != entry.sha256)
                    throw new IOException("Trial payload mismatch: " + entry.relativePath);
            }
            var actual = new System.Collections.Generic.HashSet<string>(Directory.GetFiles(prefix, "*", SearchOption.AllDirectories), StringComparer.OrdinalIgnoreCase);
            if (!expected.SetEquals(actual)) throw new IOException("Installed payload file set differs from manifest.");
            foreach (var required in new[] { "VectorQuake.exe", "Exhibition-Relaunch.ps1", "VectorQuake_Data/Managed/Game.Exhibition.Integration.dll",
                "RestartExperiment/RestartExperiment.cs", "RestartExperiment/RestartExperimentWindows.cs", "RestartExperiment/RestartExperimentNativeProbe.cs", "RestartExperiment/Restart-Experiment.ps1" })
                if (!expected.Contains(Path.GetFullPath(Path.Combine(prefix, required)))) throw new IOException("Required payload file missing: " + required);
            if (File.Exists(Path.Combine(prefix, "steam_appid.txt"))) throw new IOException("Unexpected steam_appid.txt.");
        }
    }

    // The production runtime owns evidence and validation; native/process calls alone are injectable.
    public sealed class ResetOverlayTrialRuntime : IResetOverlayTrialRuntime
    {
        private readonly ResetOverlayRole role;
        private readonly ISavePathProvider paths;
        private readonly string contextPath, requestPath, roleDirectory, configPath;
        private readonly ProcessIdentity self, steam;
        private readonly Func<ResetIdentity> identity;
        private readonly Func<ProcessIdentity> currentSteam;
        private readonly ResetOverlayAchievementRead readAchievement;
        private readonly Func<ProcessStartInfo, Process> createProcess;
        private readonly Action<ExperimentRequest> validateEnvironment;
        private readonly Action quit;
        private readonly Func<bool> available;
        private readonly Func<string> nativeFailure;
        private readonly ResetOverlayTrialContext context;
        private readonly ResetOverlayTrialConfig config;
        private readonly ResetOverlayInitialIdentityPin initialIdentity = new ResetOverlayInitialIdentityPin();
        private ResetOverlaySdkBaseline baseline;
        private ObservationSaveManifest initialFiles, pendingFiles, exitFiles;
        private ExperimentRequest request;
        private string firstCoreError, resetResultHash;
        private int sequence;
        public string EvidenceDirectory => context.Directory;
        public bool Available => available();
        public string CoreFailure => firstCoreError ?? nativeFailure();
        public string[] Targets => baseline?.Names.Where((name, i) => baseline.Achieved[i]).ToArray() ?? Array.Empty<string>();
        public string BaselineSummary => baseline == null ? null : string.Join("\n", baseline.Names.Select((n, i) => n + ": " +
            (!baseline.QuerySucceeded[i] ? "query failed" : baseline.Achieved[i] ? "earned" : "unearned")));

        public ResetOverlayTrialRuntime(ResetOverlayRole role, string inputPath, string requestPath, ISavePathProvider paths,
            Func<ResetIdentity> identity = null, ResetOverlayAchievementRead readAchievement = null,
            Func<ProcessIdentity> captureSelf = null, Func<ProcessIdentity> currentSteam = null,
            Func<ProcessStartInfo, Process> createProcess = null, Action<ExperimentRequest> validateEnvironment = null,
            Action quit = null, Func<bool> available = null, Func<string> nativeFailure = null)
        {
            if (role != ResetOverlayRole.Initiator && role != ResetOverlayRole.ResetWorker) throw new IOException("Unsupported reset role.");
            this.role = role; this.paths = paths;
            this.identity = identity ?? (() => new SteamExhibitionResetAdapter().GetIdentity());
            this.readAchievement = readAchievement ?? Steamworks.SteamUserStats.GetAchievement;
            self = (captureSelf ?? Capture)();
            this.currentSteam = currentSteam ?? (() => WindowsIdentityCapture.Steam(self));
            this.createProcess = createProcess ?? Process.Start;
            this.quit = quit ?? Application.Quit;
            this.available = available ?? (() => SteamAchievementMaintenanceAccess.IsAvailable);
            this.nativeFailure = nativeFailure ?? (() => SteamOverlayObservationAccess.NativeFailure ??
                (PlatformRuntimeRegistry.HasSelection && !PlatformRuntimeRegistry.CurrentSelection.IsSuccess ? "Platform selection failed." : null));
            this.validateEnvironment = validateEnvironment ?? ValidateEnvironment;
            if (role == ResetOverlayRole.Initiator)
            {
                configPath = ResetOverlayTrialFiles.EvidencePath(inputPath);
                config = ReadConfig(configPath, null, out var configHash);
                steam = ExperimentFiles.Parse<ProcessIdentity>(ExperimentFiles.Json(this.currentSteam()));
                if (steam == null) throw new IOException("Original Steam client unavailable.");
                context = new ResetOverlayTrialContext { Version = 2, TrialId = Guid.NewGuid().ToString("N"),
                    MappingVersion = ExhibitionResetCoordinator.MappingVersion, ConfigurationPath = configPath,
                    ConfigurationSha256 = configHash, Origin = self, Steam = steam, AppId = config.AppId, SteamId = config.SteamId };
                context.Directory = Path.Combine(@"D:\J2M\evidence\reset-overlay-trial", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + context.TrialId);
                if (Directory.Exists(context.Directory)) throw new IOException("Trial directory already exists.");
                contextPath = Path.Combine(context.Directory, "context.json");
                this.requestPath = Path.Combine(context.Directory, "handoff-GameOnly", "request.json");
            }
            else
            {
                contextPath = ResetOverlayTrialFiles.EvidencePath(inputPath);
                this.requestPath = ResetOverlayTrialFiles.EvidencePath(requestPath);
                request = ExperimentFiles.Read<ExperimentRequest>(this.requestPath);
                context = ResetOverlayWire.ReadContext(request);
                if (!string.Equals(request.ResetOverlayContextPath, contextPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.Combine(request.EvidenceDirectory, "request.json"), this.requestPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(self.Path, context.Origin.Path, StringComparison.OrdinalIgnoreCase) || self.Sha256 != context.Origin.Sha256 ||
                    !WindowsIdentityCapture.SameScope(self, context.Origin) || WindowsIdentityCapture.SameProcess(self, context.Origin)) throw new IOException("Worker ownership mismatch.");
                configPath = ResetOverlayTrialFiles.EvidencePath(context.ConfigurationPath);
                config = ReadConfig(configPath, context.ConfigurationSha256, out _);
                steam = context.Steam;
                baseline = ResetOverlayTrialFiles.ReadPinned<ResetOverlaySdkBaseline>(context.BaselinePath, context.BaselineSha256, out _);
                initialFiles = ResetOverlayTrialFiles.ReadPinned<ObservationSaveManifest>(context.InitialFilesPath, context.InitialFilesSha256, out _);
                pendingFiles = ResetOverlayTrialFiles.ReadPinned<ObservationSaveManifest>(context.PendingFilesPath, context.PendingFilesSha256, out _);
                exitFiles = pendingFiles;
            }
            roleDirectory = Path.Combine(context.Directory, role.ToString());
            OverlayObservationEvidence.Under(context.Directory, roleDirectory);
            if (Directory.Exists(roleDirectory)) throw new IOException("This trial role has already entered; do not retry.");
            Directory.CreateDirectory(roleDirectory);
            Record("RoleEntered");
        }

        private static ResetOverlayTrialConfig ReadConfig(string path, string hash, out string actualHash)
        {
            var result = ResetOverlayTrialFiles.ReadPinned<ResetOverlayTrialConfig>(path, hash, out actualHash);
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path), new Newtonsoft.Json.Linq.JsonLoadSettings {
                DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
            if (json.Properties().Any(p => !new[] { "Version", "AppId", "SteamId", "PayloadManifestPath" }.Contains(p.Name)))
                throw new IOException("Unknown or legacy reset configuration field.");
            ValidateConfig(result);
            if (ExperimentFiles.Hash(path) != actualHash) throw new IOException("Configuration changed while reading.");
            return result;
        }
        public static void ValidateConfig(ResetOverlayTrialConfig c)
        {
            if (c == null || c.Version != 2 || c.AppId != 5218360 || c.SteamId == 0) throw new IOException("Reset config v2 with confirmed AppID/account is required.");
            ResetOverlayTrialFiles.EvidencePath(c.PayloadManifestPath);
        }
        public async Task WaitAvailableAsync(Action guard)
        {
            var clock = Stopwatch.StartNew();
            await ResetOverlayStartupWait.Run(() => Available,
                () => role == ResetOverlayRole.Initiator || ReceiptReady(), () => clock.ElapsedMilliseconds, ms => Task.Delay(ms), guard);
        }
        private bool ReceiptReady()
        {
            if (!File.Exists(ResetOverlayReceipt.PathFor(request))) return false;
            if (!ResetOverlayReceipt.Matches(ExperimentFiles.Read<ResetOverlayChildReceipt>(ResetOverlayReceipt.PathFor(request)), request, self, requestPath))
                throw new IOException("Owned child receipt mismatch.");
            if (new WindowsCycleEnvironment(request, requestPath).ParentAlive()) throw new IOException("Origin has not exited.");
            return HelperCompleted();
        }
        private bool HelperCompleted()
        {
            var creation = ResetOverlayWire.ReadCreation(request);
            string path = Path.Combine(request.EvidenceDirectory, "events-" + creation.Helper.Pid + ".jsonl");
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path);
            // The helper publishes newline-delimited complete records. Never accept a partial terminal.
            if (text.Length == 0 || !text.EndsWith("\n", StringComparison.Ordinal)) return false;
            var rows = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(ExperimentFiles.Parse<ExperimentObservation>)
                .Where(r => r.Stage == "HelperCompleted" || r.Stage == "HelperFailed").ToArray();
            if (rows.Length == 0) return false;
            var row = rows[0];
            if (rows.Length != 1 || row.Stage != "HelperCompleted" || row.Error != null || row.Nonce != request.Nonce || row.OperationId != context.OperationId ||
                !ResetOverlayWire.Same(row.Observer, creation.Helper) || !ResetOverlayWire.Same(row.Parent, context.Origin) ||
                !ResetOverlayWire.Same(row.OriginalSteam, steam) || !ResetOverlayWire.Same(row.Child, self)) throw new IOException("Owned helper terminal failed or mismatched.");
            return true;
        }
        public async Task PrepareAsync(ResetRecord record, Action guard)
        {
            guard();
            if (role == ResetOverlayRole.Initiator)
            {
                initialIdentity.Validate(record, identity());
                context.ReadyOperationId = record.OperationId;
                initialFiles = await Task.Run(() => CaptureParticipantFiles()); guard();
                exitFiles = initialFiles;
            }
            await ValidatePinsAsync(role == ResetOverlayRole.ResetWorker, guard); guard();
            await CheckFilesAsync(exitFiles, "Prepared", guard); guard();
            if (role == ResetOverlayRole.Initiator)
            {
                context.InitialFilesPath = Path.Combine(context.Directory, "initial-files.json");
                context.InitialFilesSha256 = await CreateAsync(context.InitialFilesPath, initialFiles); guard();
                baseline = ReadSdkBaseline(); guard();
                context.BaselinePath = Path.Combine(context.Directory, "sdk-baseline.json");
                context.BaselineSha256 = await CreateAsync(context.BaselinePath, baseline); guard();
                ResetOverlayWire.ValidateBaseline(baseline);
                Record("BaselineStored"); guard();
            }
            else { Record("ReceiptAndHelperTerminalValidated"); guard(); }
        }
        public void ValidateRecord(ResetRecord record, bool pending)
        {
            // Coordinator guards also run after preparation/claim and after the asynchronous Steam reset.
            // A valid account alone cannot preserve the same-client trial contract.
            ValidateClient();
            var actual = identity();
            string op = pending || role == ResetOverlayRole.ResetWorker ? context.OperationId : context.ReadyOperationId;
            if (record == null || record.SchemaVersion != 1 || record.State != (pending ? ResetRecord.Pending : ResetRecord.Ready) ||
                !Guid.TryParseExact(record.OperationId, "N", out _) || record.OperationId != op || record.MappingVersion != context.MappingVersion ||
                record.AppId != context.AppId || record.SteamId != context.SteamId || actual.AppId != context.AppId || actual.SteamId != context.SteamId)
                throw new IOException("Live account/journal/operation/mapping mismatch.");
            if (!pending && role == ResetOverlayRole.Initiator) initialIdentity.Validate(record, actual);
        }
        private ResetRecord ReadRecord() => new FileExhibitionResetJournal(Path.Combine(paths.SaveRootPath, "exhibition-reset.json")).Load();
        private async Task ValidatePinsAsync(bool pending, Action guard)
        {
            guard();
            var record = await Task.Run(ReadRecord); guard();
            ValidateRecord(record, pending); guard();
            await Task.Run(() => ReadConfig(configPath, context.ConfigurationSha256, out _)); guard();
            var payload = await Task.Run(() =>
            {
                var manifest = ResetOverlayTrialFiles.ReadPinned<TrialPayloadManifest>(config.PayloadManifestPath, context.ManifestSha256, out var hash);
                return (Manifest: manifest, Hash: hash);
            }); guard();
            context.ManifestSha256 = payload.Hash;
            await Task.Run(() => ResetOverlayTrialFiles.VerifyPayload(Path.GetDirectoryName(self.Path), payload.Manifest)); guard();
            if (ExperimentFiles.Hash(config.PayloadManifestPath) != context.ManifestSha256) throw new IOException("Payload manifest changed while checking.");
            guard(); ValidateRecord(ReadRecord(), pending); guard();
            ValidateClient(); guard();
            validateEnvironment(MakeRequest(false)); guard();
            Record("PinsValidated"); guard();
        }
        private void ValidateClient()
        {
            if (!ResetOverlayWire.Same(steam, currentSteam())) throw new IOException("Original Steam client identity changed.");
        }
        public async Task RevalidateAsync(Action guard)
        {
            guard();
            await Task.Run(() =>
            {
                var b = ResetOverlayTrialFiles.ReadPinned<ResetOverlaySdkBaseline>(context.BaselinePath, context.BaselineSha256, out _);
                var r = ResetOverlayTrialFiles.ReadPinned<ResetOverlayUserReport>(context.OriginReportPath, context.OriginReportSha256, out _);
                ResetOverlayWire.ValidateReport(r, b, ResetOverlayRole.Initiator, self);
                if (!ResetOverlayWire.BaselineAgrees(r) || r.BaselineSha256 != context.BaselineSha256) throw new IOException("Baseline report changed.");
            }); guard();
            await ValidatePinsAsync(false, guard); guard();
            await CheckFilesAsync(initialFiles, "BeforePending", guard); guard();
            var now = ReadSdkBaseline(); guard();
            ResetOverlayWire.ValidateBaseline(now);
            if (!baseline.Achieved.SequenceEqual(now.Achieved)) throw new IOException("SDK baseline changed before reset.");
            Record("BaselineRevalidated"); guard();
        }
        private ResetOverlaySdkBaseline ReadSdkBaseline()
        {
            ValidateRecord(ReadRecord(), role == ResetOverlayRole.ResetWorker && ReadRecord().State == ResetRecord.Pending);
            var names = ResetOverlayWire.Names();
            if (!names.SequenceEqual(SteamAchievementMapping.Production.Entries.Select(e => e.ExpectedSteamApiName.Value))) throw new IOException("Unexpected reset mapping.");
            var row = new ResetOverlaySdkBaseline { Version = 2, TrialId = context.TrialId, ReadyOperationId = context.ReadyOperationId,
                MappingVersion = context.MappingVersion, Role = ResetOverlayRole.Initiator, Source = "SDK", Process = self,
                AppId = context.AppId, SteamId = context.SteamId, Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = OverlayObservationEvidence.MonotonicSeconds(),
                Names = names, Achieved = new bool[5], QuerySucceeded = new bool[5] };
            for (int i = 0; i < names.Length; i++)
            {
                try { row.QuerySucceeded[i] = readAchievement(names[i], out row.Achieved[i]); }
                catch (Exception) { row.QuerySucceeded[i] = false; }
            }
            return row;
        }
        public void ConfirmScope()
        {
            if (role != ResetOverlayRole.Initiator || context.ScopeConfirmedUtc != null) throw new IOException("Scope confirmation already consumed.");
            context.HandoffNonce = Guid.NewGuid().ToString("N");
            context.ScopeConfirmedUtc = DateTime.UtcNow.ToString("o");
            Record("ScopeConfirmed:VQ_LEVEL_0_CLEAR..VQ_LEVEL_4_CLEAR;participant-campaign-and-achievements");
        }
        public void Claim(string step)
        {
            if (step != (role == ResetOverlayRole.Initiator ? "initial-request" : "reset-worker")) throw new IOException("Invalid reset claim.");
            ResetOverlayTrialFiles.Create(Path.Combine(context.Directory, step + ".claimed.json"), Event(step));
        }
        public async Task BindPendingAsync(ResetRecord record, Action guard)
        {
            guard();
            if (role != ResetOverlayRole.Initiator || record == null || record.OperationId == context.ReadyOperationId || string.IsNullOrWhiteSpace(context.ScopeConfirmedUtc)) throw new IOException("Invalid Pending binding.");
            context.OperationId = record.OperationId;
            ValidateRecord(record, true); guard();
            pendingFiles = await Task.Run(() => CaptureParticipantFiles()); guard();
            RequireUnchangedExcept(initialFiles, pendingFiles, IsJournalFile);
            exitFiles = pendingFiles;
            context.PendingFilesPath = Path.Combine(context.Directory, "pending-files.json");
            context.PendingFilesSha256 = await CreateAsync(context.PendingFilesPath, pendingFiles); guard();
            Record("PendingCommitted"); guard();
            await CreateAsync(contextPath, context); guard();
        }
        public async Task VerifyResetAsync(ResetRecord record, Action guard)
        {
            guard(); ValidateRecord(record, false); guard();
            Exception localFailure = null;
            try { await Task.Run(VerifyLocal); } catch (Exception e) { localFailure = e; }
            guard();
            var sdk = ReadSdkBaseline(); guard();
            var files = await Task.Run(() => CaptureParticipantFiles()); guard();
            Exception filesFailure = null;
            try { RequireUnchangedExcept(pendingFiles, files, IsResetFile); } catch (Exception e) { filesFailure = e; }
            string filesPath = Path.Combine(context.Directory, "reset-files.json");
            string filesHash = await CreateAsync(filesPath, files); guard();
            var result = new ResetOverlayResult { Version = 2, TrialId = context.TrialId, OperationId = context.OperationId,
                AppId = context.AppId, SteamId = context.SteamId, MappingVersion = context.MappingVersion, ReadyState = record.State,
                Process = self, LocalVerified = localFailure == null, VerificationError = (localFailure ?? filesFailure)?.ToString(), Names = sdk.Names, Achieved = sdk.Achieved, QuerySucceeded = sdk.QuerySucceeded,
                Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = OverlayObservationEvidence.MonotonicSeconds(), FilesPath = filesPath, FilesSha256 = filesHash };
            resetResultHash = await CreateAsync(Path.Combine(roleDirectory, "reset-result.json"), result); guard();
            if (localFailure != null || filesFailure != null) throw new IOException("Local reset/file verification failed; required result preserved.", localFailure ?? filesFailure);
            if (sdk.QuerySucceeded.Any(v => !v) || sdk.Achieved.Any(v => v)) throw new IOException("SDK reset readback failed; display comparison is ineligible.");
            exitFiles = files;
            Record("ResetCommittedAndVerified"); guard();
        }
        private void VerifyLocal()
        {
            var root = paths.SaveRootPath;
            foreach (var name in CampaignNames())
                if (File.Exists(Path.Combine(root, name)) || (Directory.Exists(root) && Directory.GetFiles(root, name + ".*").Length != 0)) throw new IOException("Participant campaign data remains: " + name);
            if (Directory.GetFiles(root).Select(Path.GetFileName).Any(n => n.StartsWith(FileProductAchievementRepository.AchievementFileName + ".", StringComparison.Ordinal) && n != FileProductAchievementRepository.AchievementFileName + ".bak") ||
                new[] { ".tmp", ".bak", ".rollback" }.Any(suffix => File.Exists(Path.Combine(root, "exhibition-reset.json" + suffix)))) throw new IOException("Participant recovery files remain.");
            // Existing destructive reset writes an empty canonical ledger and empty backup.
            // Inspect both directly: AtomicTextFileStore.ReadAllText performs cleanup and is not read-only.
            foreach (var name in new[] { FileProductAchievementRepository.AchievementFileName, FileProductAchievementRepository.AchievementFileName + ".bak" })
            {
                var loaded = FileProductAchievementRepository.LoadReadOnly(Path.Combine(root, name));
                if (loaded.Status != AchievementDocumentLoadStatus.Loaded || loaded.Document == null || loaded.Document.EarnedAchievementIds.Length != 0 || loaded.Document.PendingAchievementPublicationIds.Length != 0)
                    throw new IOException("Participant achievement ledger/backup is not empty: " + name);
            }
        }
        private static string[] CampaignNames() => new[] { FileCampaignProfileRepository.ProfileFileName, CampaignLocalLaunchStateRepository.FileName, CampaignSaveRecoveryService.PendingResetFileName };
        private static bool IsJournalFile(string name) => name == "exhibition-reset.json" || new[] { ".tmp", ".bak", ".rollback" }.Any(s => name == "exhibition-reset.json" + s);
        private static bool IsResetFile(string name) => IsJournalFile(name) || CampaignNames().Concat(new[] { FileProductAchievementRepository.AchievementFileName }).Any(n => name == n || name.StartsWith(n + ".", StringComparison.Ordinal));
        public static void RequireUnchangedExcept(ObservationSaveManifest before, ObservationSaveManifest after, Func<string, bool> allowed)
        {
            if (before?.Files == null || after?.Files == null) throw new IOException("Participant manifest missing.");
            OverlayObservationEvidence.RequireSame(new ObservationSaveManifest { Files = before.Files.Where(f => !allowed(f.Path)).ToArray() },
                new ObservationSaveManifest { Files = after.Files.Where(f => !allowed(f.Path)).ToArray() });
        }
        public async Task SaveReportAsync(string visibility, string[] judgments)
        {
            ValidateRecord(ReadRecord(), false); ValidateClient();
            if (role == ResetOverlayRole.ResetWorker && (resetResultHash == null || ExperimentFiles.Hash(Path.Combine(roleDirectory, "reset-result.json")) != resetResultHash)) throw new IOException("Reset result changed or missing.");
            var report = new ResetOverlayUserReport { Version = 2, TrialId = context.TrialId, Role = role, Source = "User", Process = self,
                Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = OverlayObservationEvidence.MonotonicSeconds(), BaselineSha256 = context.BaselineSha256,
                ResetResultSha256 = role == ResetOverlayRole.ResetWorker ? resetResultHash : null, Names = Targets, Judgments = (string[])judgments.Clone(),
                Visibility = visibility, AttemptReported = visibility != "inconclusive" };
            ResetOverlayWire.ValidateReport(report, baseline, role, self);
            var path = Path.Combine(roleDirectory, "user-report.json");
            var hash = await CreateAsync(path, report);
            if (role == ResetOverlayRole.Initiator) { context.OriginReportPath = path; context.OriginReportSha256 = hash; }
        }
        private ExperimentRequest MakeRequest(bool child)
        {
            return new ExperimentRequest { Nonce = child ? context.HandoffNonce : Guid.NewGuid().ToString("N"), Parent = self, Steam = steam, AppId = context.AppId, SteamId = context.SteamId,
                Trial = Trial.GameOnly, OperationId = context.OperationId ?? context.ReadyOperationId, Build = "payload-sha256:" + context.ManifestSha256,
                EvidenceDirectory = Path.GetDirectoryName(requestPath), ToolsDirectory = Path.Combine(Path.GetDirectoryName(self.Path), "RestartExperiment"),
                ResetOverlayWireVersion = child ? 2 : 0, ResetOverlayChildRole = child ? ResetOverlayRole.ResetWorker : ResetOverlayRole.None,
                ResetOverlayTrialId = child ? context.TrialId : null, ResetOverlayContextPath = child ? contextPath : null,
                ResetOverlayContextSha256 = child ? ExperimentFiles.Hash(contextPath) : null };
        }
        private void ValidateEnvironment(ExperimentRequest value)
        {
            var env = new WindowsCycleEnvironment(value, requestPath);
            env.Validate(); env.EnsureNoOtherGame();
            if (!env.OriginalSteamAlive()) throw new IOException("Original Steam client absent.");
        }
        public HandoffResult StartHelper(Action guard, Action finalGuard)
        {
            guard();
            request = MakeRequest(true);
            Directory.CreateDirectory(request.EvidenceDirectory);
            ResetOverlayWire.ReadContext(request); guard();
            ResetOverlayTrialFiles.Create(requestPath, request); guard();
            using (var process = LaunchEnvironment.Start(() =>
            {
                validateEnvironment(request); guard(); ValidateClient(); guard(); ValidateRecord(ReadRecord(), true); guard();
                OverlayObservationEvidence.RequireSame(pendingFiles, CaptureParticipantFiles()); guard();
                var start = ExperimentFiles.HostStart(request.ToolsDirectory, requestPath, false);
                Record("HelperRequestPrepared:" + request.Nonce + ":" + ExperimentFiles.Hash(requestPath)); guard();
                return start;
            }, null, start => { finalGuard(); return createProcess(start); }))
            {
                if (process == null) return HandoffResult.NotStarted;
                try
                {
                    ResetOverlayTrialFiles.Create(Path.Combine(request.EvidenceDirectory, "helper-created.json"), new ResetOverlayHelperCreation {
                        Version = 2, Nonce = request.Nonce, Origin = self, Helper = WindowsIdentityCapture.Capture(process, false) });
                    Record("HandoffAccepted");
                }
                catch (Exception e) { throw new ResetOverlayAcceptedEvidenceException(e); }
                return HandoffResult.Accepted;
            }
        }
        private ObservationSaveManifest CaptureParticipantFiles()
        {
            // The shared session lock is held exclusively, so it is verified by ownership, not by opening it again.
            // Other files, including non-JSON participant files, must remain pinned in this destructive trial.
            var root = Path.GetFullPath(paths.SaveRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var lockPath = Path.Combine(root, "exhibition-instance.lock");
            return new ObservationSaveManifest { Files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(p => !string.Equals(p, lockPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p => new ObservationSaveFile {
                    Path = p.Substring(root.Length).Replace('\\', '/'), Sha256 = ExperimentFiles.Hash(p) }).ToArray() };
        }
        private async Task CheckFilesAsync(ObservationSaveManifest expected, string stage, Action guard)
        {
            var actual = await Task.Run(() => CaptureParticipantFiles()); guard();
            OverlayObservationEvidence.RequireSame(expected, actual);
            Record("ParticipantFiles:" + stage); guard();
        }
        public async Task FinalSnapshotAsync()
        {
            if (exitFiles != null)
            {
                var actual = await Task.Run(() => CaptureParticipantFiles());
                OverlayObservationEvidence.RequireSame(exitFiles, actual);
                Record("ParticipantFiles:BeforeExit");
            }
            if (role == ResetOverlayRole.ResetWorker && !HelperCompleted()) throw new IOException("Helper terminal incomplete.");
        }
        private static Task<string> CreateAsync<T>(string path, T value) => Task.Run(() => { ResetOverlayTrialFiles.Create(path, value); return ExperimentFiles.Hash(path); });
        private ResetOverlayTrialObservation Event(string stage) => new ResetOverlayTrialObservation { Stage = stage, Utc = DateTime.UtcNow.ToString("o"),
            MonotonicSeconds = OverlayObservationEvidence.MonotonicSeconds(), TrialId = context.TrialId, OperationId = context.OperationId ?? context.ReadyOperationId,
            Role = role, Sequence = ++sequence, Process = self, AppId = context.AppId, SteamId = context.SteamId };
        public void Record(string stage) => Write(Event(stage));
        public void Failure(Exception error) { var row = Event("CoreFailure"); row.Error = error.ToString(); Write(row); }
        private void Write(ResetOverlayTrialObservation row)
        {
            try
            {
                using (var stream = new FileStream(Path.Combine(roleDirectory, "events.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(ExperimentFiles.Json(row) + Environment.NewLine);
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
            }
            catch (Exception e) { if (firstCoreError == null) firstCoreError = e.Message; throw; }
        }
        public void Quit() => quit();
        private static ProcessIdentity Capture() { using (var p = Process.GetCurrentProcess()) return WindowsIdentityCapture.Capture(p, true); }
    }
}
