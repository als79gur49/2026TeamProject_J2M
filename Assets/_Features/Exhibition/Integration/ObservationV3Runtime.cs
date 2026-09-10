using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Platform.Runtime;
using Game.Platform.Steam;
using UnityEngine;

namespace Game.Exhibition.Integration
{
    // OS, file, transport and quit boundaries. The Runtime retains all session ordering,
    // admission, native health and canonical lifecycle decisions when these are substituted.
    internal class ObservationV3RuntimeIO
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        public virtual long Milliseconds => clock.ElapsedMilliseconds;
        public virtual long Timestamp => ObservationV3LaunchWindow.Now;
        public virtual long TimestampFrequency => ObservationV3LaunchWindow.Frequency;
        public virtual Task<T> Worker<T>(string operation, Func<T> work, CancellationToken token) => Task.Run(work, token);
        public virtual void Quit() { Application.Quit(); }
        public virtual string SaveRoot() => Path.GetFullPath(new ApplicationPersistentDataSavePathProvider().SaveRootPath);
        public virtual void LogFailure(Exception error) { UnityEngine.Debug.LogException(error); }
        public virtual void ValidatePins(ObservationV3Bundle bundle) { bundle.ValidatePins(); }
        public virtual void ValidateObservation(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self, ProcessIdentity client)
        {
            bundle.ValidatePins(); handles.Match(self); handles.Match(client);
            ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(WindowsIdentityCapture.Steam(self), client), "CurrentSteamChanged");
        }
        public virtual void ValidateOrigin(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self)
        { bundle.ValidatePins(); handles.Match(self); }
        public virtual void ValidateHandoffInputs(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity self)
        {
            bundle.RequireLaunchAvailable();
            handles.Match(self);
        }
        public virtual ProcessStartInfo HelperStart(ObservationV3Bundle bundle, ObservationRef request)
        {
            var start = ObservationV3WindowsHost.HostStart(Path.Combine(bundle.Plan.Value.PayloadRoot, "RestartExperiment"), request.Path, bundle.Plan.Value.WorkingDirectory, false);
            LaunchEnvironment.Apply(start, LaunchRole.Helper, bundle.Config.Value.AppId); return start;
        }
        public virtual ObservationV3OriginProcess StartHelper(ProcessStartInfo start) => new ObservationV3OriginProcess(Process.Start(start));
        public virtual ObservationPinned<T> Read<T>(ObservationRef reference, string root) => ObservationV3RuntimeWire.Read<T>(reference, root);
        public virtual ObservationRef Save(string root, string name, byte[] bytes) => ObservationV3RuntimeWire.CreateBytes(root, name, bytes);
        public virtual void FilePin(ObservationRef reference, string root) { ObservationV3RuntimeWire.FilePin(reference, root); }
        public virtual void WriteFailure(ObservationV3Bundle bundle, Exception error, string role, ObservationV3Admission admission)
        { ObservationV3WindowsHost.WriteFailure(bundle, error, role, admission); }
    }
    internal class ObservationV3OriginProcess : IDisposable
    {
        private readonly Process process;
        public ObservationV3OriginProcess(Process process) { this.process = process; }
        public virtual ProcessIdentity Capture(ObservationV3ProcessHandles handles)
        {
            if (process == null) throw new IOException("HelperCreationUnknown");
            new BoundedOutput(process.StandardError); return handles.Capture(process.Id);
        }
        public virtual void Match(ObservationV3ProcessHandles handles, ProcessIdentity identity) { handles.Match(identity); }
        public virtual void WriteLine(string value) { process.StandardInput.WriteLine(value); process.StandardInput.Flush(); }
        public virtual string ReadLine() => process.StandardOutput.ReadLine();
        public virtual void CloseInput() { process.StandardInput.Close(); }
        public virtual void Dispose() { process?.Dispose(); }
    }
    /// <summary>Dispatches to the canonical lifecycle; this component never owns a second native runtime.</summary>
    public sealed class ObservationV3Runtime : MonoBehaviour, IObservationV3PlayerPorts
    {
        private readonly ConcurrentQueue<Action> main = new ConcurrentQueue<Action>();
        private ObservationV3Session session;
        private CancellationToken Token => session.Token;
        private int generation => session.Admission.Generation;
        private Task handoffTask;
        private Task<ObservationV3Lifecycle> shutdownTask;
        private readonly TaskCompletionSource<bool> failureEvidence = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private long identityCount, lastIdentity;
        private ObservationV3OriginProcess helperProcess;
        internal ObservationV3RuntimeIO IO = new ObservationV3RuntimeIO();
        private readonly ObservationV3ProcessHandles handles = new ObservationV3ProcessHandles();
        private readonly SemaphoreSlim output = new SemaphoreSlim(1, 1);
        private ObservationV3Options options;
        private string saveRootPath;
        private PlatformStartupHandle startup;
        private ObservationV3Bundle bundle;
        private ObservationPinned<ObservationV3IndependentContext> context;
        private ObservationPinned<ObservationV3ReplacementClaim> replacementClaim;
        private ObservationRef visibilityRef, displayRef;
        private ObservationV3Visibility visibility;
        private ObservationV3Display display;
        private ProcessIdentity self, client, helper;
        private ObservationV3Flow flow;
        private int sequence;
        private ObservationV3Checkpoint checkpoint => session.Checkpoint;
        private bool prepared, quitting, pinsPending;
        private long nextPins, nextIdentity;
        public string[] Targets => bundle?.Config.Value.Targets;
        public bool CanRestart => bundle?.Baseline != null && bundle.Baseline.Value.Targets.All(t => t.Achieved == false);

        private void Awake()
        {
            session = new ObservationV3Session(() => IO.Milliseconds);
            session.Failed += FailureEffects;
            session.Changed += () => main.Enqueue(() => flow?.SessionChanged(session.Stage, session.CanReport, session.FirstError));
            Application.wantsToQuit += WantsToQuit;
        }
        private bool WantsToQuit()
        {
            if (session.Stage == ObservationV3SessionStage.QuitAllowed) return true;
            _ = Close(); return false;
        }
        private void RequireHealth(bool identity)
        {
            session.CheckNative(() =>
            {
                var runtime = SteamOverlayObservationAccess.Runtime;
                if (runtime == null || SteamOverlayObservationAccess.NativeFailure != null || runtime.Diagnostics.State != SteamPlatformRuntimeState.Available)
                    throw new IOException("ObservationNativeFault:" + SteamOverlayObservationAccess.NativeFailure);
                if (identity) ReadIdentity();
            });
        }
        public static void StartObservation(ObservationV3Options options)
        {
            var startup = PlatformStartupDeferral.Request();
            try
            {
                var go = new GameObject("Overlay v3 runtime"); DontDestroyOnLoad(go);
                var runtime = go.AddComponent<ObservationV3Runtime>(); runtime.options = options; runtime.startup = startup;
                runtime.saveRootPath = runtime.IO.SaveRoot();
                runtime.flow = new ObservationV3Flow(runtime, options.Role);
                ParticipantResetMenuAccess.Register(runtime.flow);
                ObservationV3Presentation.Attach(runtime.flow);
                _ = runtime.flow.PrepareMenuAsync();
            }
            catch { startup.Cancel(); throw; }
        }
        private Task<T> OnMain<T>(Func<T> action, CancellationToken token, bool cleanup = false)
        {
            int expected = generation; var result = new TaskCompletionSource<T>();
            var registration = token.Register(() => result.TrySetCanceled());
            main.Enqueue(() =>
            {
                try
                {
                    if (!cleanup && (token.IsCancellationRequested || expected != generation)) { result.TrySetCanceled(); return; }
                    T value = action();
                    if (!cleanup && (token.IsCancellationRequested || expected != generation))
                    { startup.ShutdownOwnedRuntime(); result.TrySetCanceled(); }
                    else result.TrySetResult(value);
                }
                catch (Exception e) { result.TrySetException(e); }
                finally { registration.Dispose(); }
            });
            return result.Task;
        }
        private void Update()
        {
            CheckHealthUpdate();
            while (main.TryDequeue(out var action)) { action(); CheckHealthUpdate(); }
            if (session.NativePhase == ObservationV3NativePhase.Ready && session.FirstError == null && IO.Milliseconds >= nextIdentity)
            {
                nextIdentity = IO.Milliseconds + 1000;
                try { RequireHealth(true); } catch (Exception error) { Fail(error); return; }
            }
            if (!prepared || quitting || session.Admission.Closed || pinsPending || IO.Milliseconds < nextPins) return;
            pinsPending = true; nextPins = IO.Milliseconds + 1000;
            _ = CheckPinsAsync();
        }
        private void CheckHealthUpdate()
        {
            if (session.NativePhase != ObservationV3NativePhase.Ready || session.FirstError != null || session.Stage == ObservationV3SessionStage.ClosingCancelledBeforeCommit) return;
            try { RequireHealth(false); } catch (Exception error) { Fail(error); }
        }
        private async Task CheckPinsAsync()
        {
            try { await Bounded(() => { IO.ValidateObservation(bundle, handles, self, client); return true; }, Token, operation: "observation-pins"); }
            catch (Exception e) { Fail(e); }
            finally { pinsPending = false; }
        }
        private async Task<T> Bounded<T>(Func<T> action, CancellationToken token, bool cleanup = false, string operation = "read")
        {
            var work = IO.Worker(operation, action, token);
            if (await Task.WhenAny(work, Task.Delay(30000, token)) != work) throw new TimeoutException("ObservationReadTimeout");
            token.ThrowIfCancellationRequested(); return await work;
        }
        public async Task Prepare()
        {
            int ownerGeneration = generation;
            try
            {
                if (options.Role == OverlayObservationRole.OriginObserver)
                {
                    await Bounded(PrepareOrigin, Token, operation: "prepare-origin");
                    session.Admission.RequireCurrent(ownerGeneration, 0);
                    await InitializeNative(bundle.Preparation.Ref.Sha256, Token, ownerGeneration);
                    var before = await PinEvidence("baseline-before");
                    long started = IO.Milliseconds;
                    var values = await OnMain(() => Targets.Select(t => SteamOverlayObservationAccess.Runtime.ReadAchievement(t)).ToArray(), Token);
                    var identity = await OnMain(ReadIdentity, Token);
                    long finished = IO.Milliseconds;
                    var after = await PinEvidence("baseline-after");
                    var baseline = Doc(new ObservationV3Baseline { PreparationRef = bundle.Preparation.Ref, BeforeRef = before, AfterRef = after,
                        Origin = self, OriginalSteam = client, AppId = identity.AppId, ExpectedSteamId = bundle.Config.Value.ExpectedSteamId,
                        ObservedSteamId = identity.SteamId, LoggedOn = identity.LoggedOn, Started = started, Finished = finished,
                        Targets = values.Select(v => new ObservationV3AchievementValue { Target = v.Target, ReadSucceeded = v.ReadSucceeded, Achieved = v.Achieved }).ToArray() }, "baseline");
                    var baselineRef = await Save("baseline.json", baseline);
                    bundle.Baseline = ObservationV3RuntimeWire.Read<ObservationV3Baseline>(baselineRef, bundle.Root);
                    ObservationV3RuntimeWire.Baseline(baseline, bundle.Preparation.Ref, bundle.Preparation.Value, bundle.Config.Value);
                }
                else
                {
                    await Bounded(PrepareReplacement, Token, operation: "prepare-replacement");
                    session.Admission.RequireCurrent(ownerGeneration, 0);
                    await StartReplacement();
                }
                await OnMain(() => { RequireStartupWindow(); session.RequireLive(); session.Admission.SetDeadline(0); session.Observing(); prepared = true; return true; }, Token);
            }
            catch (Exception) when (session.WasCancelledByClose(ownerGeneration)) { await Close(); }
            catch (Exception e) { Fail(e); throw; }
        }
        private bool PrepareOrigin()
        {
            using (var p = Process.GetCurrentProcess()) self = handles.Capture(p.Id);
            var found = WindowsIdentityCapture.Steam(self); if (found == null) throw new IOException("OriginalSteamUnavailable");
            client = handles.Capture(found.Pid);
            string saveRoot = saveRootPath;
            var journal = ObservationV3JournalReader.Read(Path.Combine(saveRoot, "exhibition-reset.json"), 5218360);
            var ready = journal.Value;
            string run = Guid.NewGuid().ToString("N");
            string root = ObservationV3RuntimeWire.EvidenceRoot(Environment.OSVersion.Platform == PlatformID.Win32NT
                ? @"D:\J2M\evidence\overlay-v3-runs" : "/mnt/d/J2M/evidence/overlay-v3-runs");
            root = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + run);
            Directory.CreateDirectory(root);
            string payload = Path.GetDirectoryName(self.Path);
            string native = Path.Combine(payload, Path.GetFileNameWithoutExtension(self.Path) + "_Data", "Plugins", "x86_64", "steam_api64.dll");
            var planRef = ObservationV3RuntimeWire.Create(root, "launch-plan.json", ObservationV3RuntimeWire.Stamp(new ObservationV3LaunchPlan {
                AppId = ready.AppId, PayloadRoot = payload, WorkingDirectory = Path.GetFullPath(Environment.CurrentDirectory),
                GameExe = new ObservationRef { Path = self.Path, Sha256 = self.Sha256 },
                SteamExe = new ObservationRef { Path = client.Path, Sha256 = client.Sha256 },
                NativeDll = new ObservationRef { Path = native, Sha256 = ExperimentFiles.Hash(native) } }, "launch-plan", "OriginObserver", 1, run));
            var configRef = ObservationV3RuntimeWire.Create(root, "origin-config.json", ObservationV3RuntimeWire.Stamp(new ObservationV3OriginConfig {
                AppId = ready.AppId, ExpectedSteamId = ready.SteamId, Targets = ResetOverlayWire.Names(), EvidenceRoot = root,
                LaunchPlanRef = planRef }, "origin-config", "OriginObserver", 2, run));
            bundle = ObservationV3Bundle.LoadConfig(configRef.Path);
            var files = new[] { journal.Reference };
            var participantRef = ObservationV3RuntimeWire.Create(bundle.Root, "participant-snapshot.json", ObservationV3RuntimeWire.Stamp(new ObservationV3ParticipantSnapshot {
                Root = saveRoot, Files = files }, "participant-snapshot", "OriginObserver", 1, run));
            var readyRef = ObservationV3RuntimeWire.Create(bundle.Root, "ready-snapshot.json", ObservationV3RuntimeWire.Stamp(new ObservationV3ReadySnapshot {
                Journal = journal.Reference, State = ready.State, OperationId = ready.OperationId,
                MappingVersion = ready.MappingVersion, AppId = ready.AppId, SteamId = ready.SteamId }, "ready-snapshot", "OriginObserver", 2, run));
            var prep = ObservationV3RuntimeWire.Stamp(new ObservationV3OriginPreparation { Nonce = Guid.NewGuid().ToString("N"), ConfigRef = bundle.Config.Ref,
                LaunchPlanRef = bundle.Plan.Ref, ReadyRef = readyRef,
                ParticipantSnapshotRef = participantRef, Self = self, OriginalSteam = client, Targets = Targets }, "origin-preparation", "OriginObserver", 3, run);
            ObservationV3JournalReader.RequireUnchanged(journal, bundle.Config.Value.AppId, bundle.Config.Value.ExpectedSteamId);
            var prepRef = ObservationV3RuntimeWire.Create(bundle.Root, "origin-preparation.json", prep);
            bundle.Preparation = ObservationV3RuntimeWire.Read<ObservationV3OriginPreparation>(prepRef, bundle.Root); bundle.ValidatePins(); return true;
        }
        private bool PrepareReplacement()
        {
            bundle = ObservationV3Bundle.LoadRequest(new ObservationRef { Path = options.Request, Sha256 = options.RequestHash });
            context = ObservationV3RuntimeWire.Read<ObservationV3IndependentContext>(new ObservationRef { Path = options.Context, Sha256 = options.ContextHash }, bundle.Root);
            var x = context.Value;
            ObservationV3RuntimeWire.Require(x.RunId == bundle.Request.Value.RunId && x.Nonce == bundle.Request.Value.Nonce && x.Role == "ReplacementObserver" &&
                x.Owner == "SteamDelegated" && ObservationV3RuntimeWire.SameRef(x.RequestRef, bundle.Request.Ref) &&
                ObservationV3RuntimeWire.SameRef(x.LaunchPlanRef, bundle.Plan.Ref), "ReplacementContextMismatch");
            var c = ObservationV3RuntimeWire.Read<ObservationV3NewClient>(x.ClientRef, bundle.Root).Value;
            var creation = bundle.ValidateNewClient(c);
            client = c.Current; helper = creation.Helper; handles.Match(client);
            using (var p = Process.GetCurrentProcess()) self = handles.Capture(p.Id);
            ObservationV3Wire.SameFileScope(self, bundle.Preparation.Value.Self);
            ObservationV3RuntimeWire.Require(!ObservationV3Wire.Same(self, bundle.Preparation.Value.Self), "ReplacementMustDiffer");
            RequireStartupWindow();
            ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(WindowsIdentityCapture.Steam(self), client), "CurrentSteamChanged");
            var args = Environment.GetCommandLineArgs();
            ObservationV3RuntimeWire.Require(ObservationV3Arguments.SemanticHash(args) ==
                ObservationV3Arguments.SemanticHash(ObservationV3Arguments.Generated(options, true)) &&
                Environment.CurrentDirectory == bundle.Plan.Value.WorkingDirectory, "ReplacementArgumentsMismatch");
            var claim = Doc(new ObservationV3ReplacementClaim { ContextRef = context.Ref, Self = self, Client = client,
                Tokens = args, EffectiveArgumentsHash = ObservationV3Arguments.SemanticHash(args), WorkingDirectory = Environment.CurrentDirectory }, "replacement-claim");
            var reference = IO.Save(bundle.Root, "replacement-claim.json", ObservationV3RuntimeWire.Bytes(claim));
            replacementClaim = IO.Read<ObservationV3ReplacementClaim>(reference, bundle.Root);
            RequireStartupWindow(); return true;
        }
        private SteamObservationIdentity ReadIdentity()
        {
            var id = SteamOverlayObservationAccess.Runtime.ReadObservationIdentity();
            if (!id.Valid || !id.LoggedOn || id.AppId != bundle.Config.Value.AppId || id.SteamId != bundle.Config.Value.ExpectedSteamId)
                throw new IOException("NativeAccountMismatch");
            identityCount++; lastIdentity = IO.Milliseconds; return id;
        }
        private async Task InitializeNative(string binding, CancellationToken token, int ownerGeneration)
        {
            using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                bounded.CancelAfter(30000);
                await InitializeNativeCore(binding, bounded.Token, ownerGeneration);
            }
        }
        private async Task InitializeNativeCore(string binding, CancellationToken token, int ownerGeneration)
        {
            await Bounded(() => { IO.ValidateObservation(bundle, handles, self, client); return true; }, token, operation: "native-pins");
            session.Admission.RequireCurrent(ownerGeneration, 0);
            try
            {
                await OnMain(() =>
                {
                    RequireStartupWindow(); session.RequireLive();
                    var snapshot = new SteamObservationPermitSnapshot { RunId = bundle.Preparation.Value.RunId,
                        Role = options.Role.ToString(), BindingHash = binding, ClientIdentity = ObservationV3Wire.Serialize(client), SelfPid = self.Pid,
                        SelfStartTicks = self.StartTicks, Generation = ownerGeneration,
                        IsCurrent = () => !token.IsCancellationRequested && !session.Admission.Closed && ownerGeneration == generation };
                    var permit = SteamObservationPermitIssuer.Issue(snapshot);
                    SteamOverlayObservationAccess.Admit(permit, snapshot); startup.Release(); return true;
                }, token);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var state = await OnMain(() => startup.State, token);
                    if (state == PlatformStartupState.Running) break;
                    if (state != PlatformStartupState.ReleaseRequested && state != PlatformStartupState.Initializing) throw new IOException("CanonicalNativeUnavailable");
                    await Task.Delay(10, token);
                }
                await OnMain(() => { var identity = ReadIdentity(); session.Ready(); return identity; }, token);
            }
            catch
            {
                main.Enqueue(() => { SteamOverlayObservationAccess.RevokePermit(); startup.ShutdownOwnedRuntime(); }); throw;
            }
        }
        private void RequireStartupWindow()
        {
            if (context == null || prepared) return;
            ObservationV3LaunchWindow.RequireOpen(context.Value, IO.Timestamp, IO.TimestampFrequency);
        }
        private async Task StartReplacement()
        {
            RequireStartupWindow();
            long remaining = (long)((context.Value.DeadlineTimestamp - IO.Timestamp) * 1000.0 / IO.TimestampFrequency);
            if (remaining <= 0) throw new TimeoutException("ReplacementStartDeadlineExceeded");
            session.Admission.SetDeadline(IO.Milliseconds + remaining);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token))
            {
                deadline.CancelAfter((int)Math.Min(remaining, 30000));
                await InitializeNative(replacementClaim.Ref.Sha256, deadline.Token, generation);
                var id = await OnMain(() => { RequireStartupWindow(); RequireHealth(true); return ReadIdentity(); }, deadline.Token);
                var started = Doc(new ObservationV3ReplacementStarted { ContextRef = context.Ref, ClaimRef = replacementClaim.Ref,
                    Self = self, Client = client, NativeReady = true, AppId = id.AppId, SteamId = id.SteamId }, "replacement-started");
                await Save("replacement-started.json", started);
                RequireStartupWindow(); session.RequireLive();
            }
        }
        private T Doc<T>(T value, string kind) where T : ObservationDocument
        { return ObservationV3RuntimeWire.Stamp(value, kind, options.Role.ToString(), Interlocked.Increment(ref sequence) + 3, bundle.Preparation.Value.RunId); }
        private Task<ObservationRef> Save(string name, object value)
        {
            int expected = generation;
            return Bounded(() => { session.Admission.RequireCurrent(expected, 0); return IO.Save(bundle.Root, name, ObservationV3RuntimeWire.Bytes(value)); }, Token, operation: "save:" + name);
        }
        private async Task<ObservationRef> PinEvidence(string phase)
        {
            int expected = generation;
            await Bounded(() => { bundle.ValidatePins(); handles.Match(self); handles.Match(client); if (!ObservationV3Wire.Same(WindowsIdentityCapture.Steam(self), client)) throw new IOException("CurrentSteamChanged"); return true; }, Token, operation: "pin:" + phase);
            session.Admission.RequireCurrent(expected, 0);
            return await Save(phase + ".json", Doc(new ObservationV3PinEvidence { PreparationRef = bundle.Preparation.Ref, Self = self, Client = client,
                Monotonic = IO.Milliseconds, Phase = phase }, "pin-validation"));
        }
        private ObservationRef Binding => options.Role == OverlayObservationRole.OriginObserver ? bundle.Preparation.Ref : context.Ref;
        public async Task StoreVisibility(string value, string text, string observedUtc)
        {
            var report = Doc(new ObservationV3Visibility { BindingRef = Binding, BaselineRef = bundle.Baseline.Ref, ClaimRef = replacementClaim?.Ref, CorrectsRef = visibilityRef,
                Self = self, Role = options.Role.ToString(), Visibility = value, OriginalText = text, ObservedUtc = observedUtc }, "visibility-report");
            ObservationV3RuntimeWire.Visibility(report, Binding, bundle.Baseline.Ref, bundle.Preparation.Value.RunId, self, options.Role.ToString());
            var stored = await SubmitReport("visibility-report", report);
            await OnMain(() => { session.RequireLive(); visibility = report; visibilityRef = stored; display = null; return true; }, Token);
        }
        public async Task StoreDisplay(ObservationV3TargetDisplay[] targets, string text, string observedUtc)
        {
            var report = Doc(new ObservationV3Display { BindingRef = Binding, BaselineRef = bundle.Baseline.Ref, ClaimRef = replacementClaim?.Ref, VisibilityRef = visibilityRef,
                CorrectsRef = displayRef, Self = self, Role = options.Role.ToString(), OriginalText = text, ObservedUtc = observedUtc, Targets = targets }, "display-report");
            ObservationV3RuntimeWire.Display(report, Binding, bundle.Baseline.Ref, visibilityRef, visibility, bundle.Preparation.Value.RunId, self, options.Role.ToString(), Targets);
            var stored = await SubmitReport("display-report", report);
            await OnMain(() => { session.RequireLive(); display = report; displayRef = stored; return true; }, Token);
        }
        private async Task<ObservationRef> SubmitReport(string kind, object report)
        {
            try
            {
                // Freeze the submitted bytes before admission; no producer can change them later.
                var bytes = ObservationV3RuntimeWire.Bytes(report);
                var operation = await OnMain(() => { RequireHealth(true); return session.AdmitReport(ObservationV3RuntimeWire.Hash(bytes)); }, Token);
                _ = SendReport(operation, kind, bytes);
                return await operation.Task;
            }
            catch (Exception error) { Fail(error); throw; }
        }
        private async Task SendReport(ObservationV3PendingReport operation, string kind, byte[] bytes)
        {
            try
            {
                await output.WaitAsync(Token);
                try
                {
                    session.Admission.RequireCurrent(operation.Generation, operation.Deadline);
                    session.RequireNativeReady();
                    var reference = await Bounded(() => IO.Save(bundle.Root, options.Role + "-report-" + operation.Sequence + ".json", bytes), Token, operation: "report-save");
                    await Bounded(() => { IO.FilePin(reference, bundle.Root); IO.ValidateObservation(bundle, handles, self, client); return true; }, Token, operation: "report-file");
                    await OnMain(() => { RequireHealth(false); session.AcceptReport(operation, reference); return true; }, Token);
                }
                finally { output.Release(); }
            }
            catch (Exception error) { Fail(error); }
        }
        public Task FullCycle()
        {
            if (handoffTask != null) return handoffTask;
            RequireHealth(true);
            if (options.Role != OverlayObservationRole.OriginObserver || !CanRestart || display == null || display.Targets.Any(t => t.Display != "earned" && t.Display != "unearned")) throw new IOException("OriginConditionMissing");
            var grantClaim = session.BeginHandoff();
            handoffTask = FullCycleCore(grantClaim);
            return FinishHandoff();
        }
        private async Task FinishHandoff()
        {
            try { await handoffTask; await Close(); }
            catch (OperationCanceledException) when (session.Stage == ObservationV3SessionStage.ClosingCancelledBeforeCommit) { }
        }
        private async Task FullCycleCore(ObservationV3Invocation grantClaim)
        {
            try
            {
                await Bounded(() => { IO.ValidateHandoffInputs(bundle, handles, self); return true; }, Token, operation: "handoff-inputs");
                await OnMain(ReadIdentity, Token); await PinEvidence("seal-pins");
                session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                var p = bundle.Preparation.Value;
                var request = Doc(new ObservationV3RuntimeRequest { Nonce = p.Nonce, Directory = bundle.Root, Owner = options.Owner.ToString(), PreparationRef = bundle.Preparation.Ref,
                    ConfigRef = bundle.Config.Ref, LaunchPlanRef = p.LaunchPlanRef,
                    ReadyRef = p.ReadyRef, ParticipantSnapshotRef = p.ParticipantSnapshotRef, BaselineRef = bundle.Baseline.Ref, OriginVisibilityRef = visibilityRef, OriginDisplayRef = displayRef }, "request");
                var requestRef = await Save("request.json", request); session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline); bundle.Request = await Bounded(() => IO.Read<ObservationV3RuntimeRequest>(requestRef, bundle.Root), Token, operation: "request-read");
                await Bounded(() => { ObservationV3Bundle.LoadRequest(requestRef); return true; }, Token, operation: "request-validate");
                session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                var helperClaim = session.Admission.Prepare("origin-helper", grantClaim.Deadline);
                var attempt = await Save("helper-attempt.json", Doc(new ObservationRunDocument(), "helper-attempt"));
                ObservationV3OriginProcess process = null;
                try
                {
                    process = await AcquireHelper(() =>
                {
                    var start = IO.HelperStart(bundle, requestRef);
                    return session.Admission.Execute(helperClaim, () => IO.ValidateOrigin(bundle, handles, self), () => IO.StartHelper(start));
                    }, Token);
                    helperProcess = process;
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    helper = await Bounded(() => process.Capture(handles), Token, operation: "helper-identity");
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    var creationRef = await Save("helper-creation.json", Doc(new ObservationV3Creation {
                        RequestRef = requestRef, AttemptRef = attempt, Origin = self, Helper = helper }, "helper-creation"));
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    var grantRef = await Save("bootstrap-grant.json", Doc(new ObservationV3Grant {
                        RequestRef = requestRef, CreationRef = creationRef, Nonce = request.Nonce, Origin = self, ExpectedHelper = helper }, "bootstrap-grant"));
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    var ready = await Bounded(() =>
                    {
                        process.WriteLine(ObservationV3Wire.Serialize(grantRef));
                        var line = process.ReadLine();
                        var readyRef = ObservationV3RuntimeWire.Parse<ObservationRef>(Encoding.UTF8.GetBytes(line ?? ""));
                        return IO.Read<ObservationV3BootstrapReady>(readyRef, bundle.Root).Value;
                    }, Token, operation: "bootstrap-ready");
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    ObservationV3RuntimeWire.Require(ready.RunId == request.RunId && ready.OriginHandleRetained && ObservationV3Wire.Same(ready.Origin, self) &&
                        ObservationV3Wire.Same(ready.Helper, helper) && ObservationV3RuntimeWire.SameRef(ready.RequestRef, requestRef) &&
                        ObservationV3RuntimeWire.SameRef(ready.GrantRef, grantRef), "BootstrapReadyMismatch");
                    await Bounded(() => { process.Match(handles, helper); return true; }, Token, operation: "helper-retained");
                    session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
                    await OnMain(() => { RequireHealth(true); return true; }, Token);
                    await Bounded(() =>
                    {
                        if (!session.CommitGrant(grantClaim)) throw new OperationCanceledException("GrantCommitDenied");
                        session.Admission.InvokeCommitted(grantClaim, () => { process.WriteLine(grantRef.Sha256); process.CloseInput(); return true; });
                        session.ConfirmGrant(grantClaim); return true;
                    }, Token, operation: "grant-confirmation");
                }
                catch (Exception error)
                {
                    // Record the operation error before cleanup can fail or time out.
                    if (!session.WasCancelledByClose(grantClaim.Generation)) Fail(error);
                    throw;
                }
                finally
                {
                    helperProcess = null;
                    if (process != null) await ReleaseHelper(process, grantClaim.Deadline);
                }
                session.Admission.RequireCurrent(grantClaim.Generation, grantClaim.Deadline);
            }
            catch (Exception) when (session.WasCancelledByClose(grantClaim.Generation)) { }
            catch (Exception e) { Fail(e); throw; }
            finally { helperProcess = null; }
        }
        private async Task ReleaseHelper(ObservationV3OriginProcess process, long deadline)
        {
            // Dispatch even when the session token is cancelled: ownership must be released.
            var cleanup = IO.Worker("helper-release", () => { process.Dispose(); return true; }, CancellationToken.None);
            if (session.CloseDeadline != 0) deadline = Math.Min(deadline, session.CloseDeadline);
            long remaining = deadline - IO.Milliseconds;
            if (remaining <= 0 || await Task.WhenAny(cleanup, Task.Delay((int)Math.Min(remaining, int.MaxValue), Token)) != cleanup)
                throw new TimeoutException("HelperHandleCleanupUncertain");
            await cleanup;
        }
        private async Task<ObservationV3OriginProcess> AcquireHelper(Func<ObservationV3OriginProcess> create, CancellationToken token)
        {
            var transfer = new object();
            ObservationV3OriginProcess unclaimed = null;
            bool abandoned = false;
            try
            {
                var value = await Bounded(() =>
                {
                    var created = create(); bool discard;
                    lock (transfer) { discard = abandoned; if (!discard) unclaimed = created; }
                    if (discard) { ReleaseUnclaimedHelper(created); throw new OperationCanceledException("HelperCreationReturnedAfterFailure"); }
                    return created;
                }, token, operation: "helper-create");
                lock (transfer) { unclaimed = null; }
                return value;
            }
            catch
            {
                ObservationV3OriginProcess discard;
                lock (transfer) { abandoned = true; discard = unclaimed; unclaimed = null; }
                if (discard != null) _ = Task.Run(() => ReleaseUnclaimedHelper(discard));
                throw;
            }
        }
        private static void ReleaseUnclaimedHelper(ObservationV3OriginProcess process)
        {
            // This only closes our streams/handle. The helper's process is never killed or recreated.
            try { process?.CloseInput(); } catch { }
            finally { process?.Dispose(); }
        }
        public Task Close() => session.RequestClose(CloseCore);
        private async Task CloseCore()
        {
            quitting = true;
            try
            {
                if (session.Stage == ObservationV3SessionStage.ClosingCancelledBeforeCommit)
                {
                    var process = helperProcess;
                    if (process != null) _ = Task.Run(() => { try { process.CloseInput(); } catch { } });
                }
                else if (handoffTask != null) await UntilCloseDeadline(handoffTask);
                session.RequireLive();
                await UntilCloseDeadline(session.DrainReports());
                if (session.NativePhase == ObservationV3NativePhase.Ready)
                    await OnMain(() => { RequireHealth(true); return true; }, Token);
                var lifecycle = await UntilCloseDeadline(ShutdownOnce());
                if (bundle?.Preparation != null) Doc(lifecycle, "native-lifecycle");
                session.RequireLive();
                if (lifecycle.Failure != "None" || (lifecycle.InitializationSucceeded &&
                    (!lifecycle.ShutdownReturned || lifecycle.ShutdownCallCount != 1 || lifecycle.RuntimeState != "Shutdown")))
                    throw new IOException("NativeCleanupUnverified");
                if (lifecycle.InitializationSucceeded) session.VerifyShutdown();
                if (bundle?.Preparation != null)
                    await UntilCloseDeadline(Save(options.Role + "-native-shutdown.json", Doc(lifecycle, "native-lifecycle")));
            }
            catch (Exception error) { Fail(error); }
            finally
            {
                // Joins the canonical cleanup even after failure. A hung native call cannot
                // be completed by a worker or replaced with a successful exit frame.
                try { await ShutdownOnce(); } catch (Exception error) { Fail(error); }
                while (true)
                {
                    if (session.FirstError != null) await failureEvidence.Task;
                    bool allowed = await OnMain(() =>
                    {
                        if (!session.TryAllowQuit(failureEvidence.Task.IsCompleted)) return false;
                        IO.Quit(); return true;
                    }, CancellationToken.None, true);
                    if (allowed) break;
                }
            }
        }
        private async Task UntilCloseDeadline(Task task)
        {
            long remaining = session.CloseDeadline - IO.Milliseconds;
            if (remaining <= 0 || await Task.WhenAny(task, Task.Delay((int)Math.Min(remaining, int.MaxValue), Token)) != task) throw new TimeoutException("CloseDeadlineExceeded");
            await task; session.RequireLive();
        }
        private async Task<T> UntilCloseDeadline<T>(Task<T> task)
        { await UntilCloseDeadline((Task)task); return await task; }
        private Task<ObservationV3Lifecycle> ShutdownOnce()
        {
            lock (session.Admission.SyncRoot)
            {
                if (shutdownTask != null) return shutdownTask;
                shutdownTask = OnMain(() =>
                {
                    session.BeginShutdown(); long started = IO.Milliseconds;
                    SteamOverlayObservationAccess.RevokePermit(); startup?.ShutdownOwnedRuntime();
                    var runtime = SteamOverlayObservationAccess.Runtime;
                    var d = runtime == null ? default(SteamPlatformDiagnostics) : runtime.Diagnostics;
                    return new ObservationV3Lifecycle { RuntimePresent = runtime != null, InitializationAttempted = d.InitializationAttempted,
                        InitializationSucceeded = d.InitializationSucceeded, ShutdownCallCount = d.ShutdownCallCount,
                        ShutdownReturned = d.ShutdownReturned, RuntimeState = runtime == null ? "NotCreated" : d.State.ToString(),
                        Failure = d.LastFailureReason.ToString(), StartupState = startup == null ? "NotCreated" : startup.State.ToString(),
                        ContextRef = context?.Ref, ClaimRef = replacementClaim?.Ref, ReceiptRef = null, Self = self, ShutdownStarted = started, ShutdownFinished = IO.Milliseconds,
                        IdentityObservationCount = identityCount, LastIdentityObservation = lastIdentity, Invocations = session.Admission.Facts() };
                }, CancellationToken.None, true);
                return shutdownTask;
            }
        }
        private void Fail(Exception error) { session.TryFail(error); }
        private void FailureEffects(Exception error)
        {
            bool keepStartupError = options?.Role == OverlayObservationRole.OriginObserver &&
                handoffTask == null && session.CloseDeadline == 0;
            _ = ShutdownOnce();
            main.Enqueue(() =>
            {
                try { IO.LogFailure(error); } catch { }
                flow?.Fail(error);
                if (!keepStartupError) _ = Close();
            });
            if (bundle?.Preparation == null) { failureEvidence.TrySetResult(true); return; }
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var cleanup = new CancellationTokenSource(30000))
                        await Bounded(() =>
                        {
                            if (bundle?.Preparation != null) IO.WriteFailure(bundle, error, options.Role.ToString(), session.Admission);
                            return true;
                        }, cleanup.Token);
                }
                catch { /* FirstError remains authoritative even if cleanup evidence cannot be stored. */ }
                finally { failureEvidence.TrySetResult(true); }
            });
        }
        private void OnApplicationQuit()
        {
            if (session.Stage != ObservationV3SessionStage.QuitAllowed) Fail(new IOException("QuitWithoutClose"));
            SteamOverlayObservationAccess.RevokePermit(); startup?.ShutdownOwnedRuntime();
        }
        private void OnDestroy()
        {
            Application.wantsToQuit -= WantsToQuit;
            session.Dispose(); SteamOverlayObservationAccess.RevokePermit(); startup?.ShutdownOwnedRuntime();
            while (main.TryDequeue(out var work)) work(); handles.Dispose();
        }
    }
}
