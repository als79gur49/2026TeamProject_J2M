// Explicit PowerShell runtime-2 entry; no automatic registration or launch on assembly load.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition.RestartExperiment
{
    // One failure write, owned independently of the helper's OS-thread mutex/handles.
    // A blocked storage worker may remain uncertain; it must never pin the owner thread.
    public sealed class ObservationV3HostFailureEvidence
    {
        private readonly object gate = new object();
        private readonly Action<Exception> write;
        private readonly Func<long> now;
        private readonly int budget;
        private Task task;
        private long deadline;
        public ObservationV3HostFailureEvidence(Action<Exception> write, Func<long> now, int budget)
        { this.write = write; this.now = now; this.budget = budget; }
        public void Start(Exception error)
        {
            lock (gate)
            {
                if (task != null) return;
                deadline = now() + budget;
                task = Task.Run(() => { try { write(error); } catch { } });
            }
        }
        public bool Join()
        {
            Task pending; long remaining;
            lock (gate) { pending = task; remaining = deadline - now(); }
            if (pending == null) return true;
            if (pending.IsCompleted) return true;
            return remaining > 0 && pending.Wait((int)Math.Min(remaining, int.MaxValue));
        }
        public T RunOwned<T>(Func<T> body, Action release)
        {
            try { return body(); }
            catch (Exception error) { Start(error); Join(); throw; }
            finally { release(); }
        }
        public static bool ReadCompletion(Func<bool> read, int budget)
        {
            var pending = Task.Run(read);
            try { if (!pending.Wait(budget)) throw new TimeoutException("TerminalReadTimeout"); }
            catch (AggregateException) { return pending.GetAwaiter().GetResult(); }
            return pending.GetAwaiter().GetResult();
        }
    }
    public static class ObservationV3WindowsHost
    {
        public static readonly string[] SharedSources = { "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs",
            "ObservationV3Wire.cs", "ObservationV3Handoff.cs", "ObservationV3Pipe.cs", "ObservationV3RuntimeWire.cs", "ObservationV3RuntimeProtocol.cs","ObservationV3Admission.cs","ObservationV3JournalReader.cs","ObservationV3Session.cs",
            "ObservationV3WindowsEnvironment.cs", "ObservationV3WindowsHost.cs" };
        public static ProcessStartInfo HostStart(string directory, string path, string workingDirectory, bool probe)
        {
            ObservationV3RuntimeWire.Canonical(workingDirectory, null);
            return new ProcessStartInfo { FileName = ExperimentFiles.PowerShell, WorkingDirectory = workingDirectory,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + ObservationV3Arguments.Quote(Path.Combine(directory, "Restart-Experiment.ps1")) +
                    " -RequestPath " + ObservationV3Arguments.Quote(path) + (probe ? " -Probe" : ""),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        }
        public static string BootstrapLine(int milliseconds)
        {
            var read = Task.Run(() => Console.In.ReadLine());
            if (!read.Wait(milliseconds)) throw new TimeoutException("BootstrapGrantTimeout");
            if (string.IsNullOrWhiteSpace(read.Result)) throw new IOException("BootstrapGrantMissing");
            return read.Result;
        }
        public static int Run(string requestPath)
        {
            var initial = ObservationV3RuntimeWire.ReadInitial<ObservationV3RuntimeRequest>(requestPath, null);
            var bundle = ObservationV3Bundle.LoadRequest(initial.Ref); bundle.RequireLaunchAvailable();
            var request = bundle.Request.Value; var prep = bundle.Preparation.Value;
            using (var handles = new ObservationV3ProcessHandles())
            using (var queue = new ObservationV3EventQueue())
            {
                ProcessIdentity self;
                using (var p = Process.GetCurrentProcess()) self = handles.Capture(p.Id);
                var grantRef = ObservationV3RuntimeWire.Parse<ObservationRef>(Encoding.UTF8.GetBytes(BootstrapLine(30000)));
                var grant = ObservationV3RuntimeWire.Read<ObservationV3Grant>(grantRef, bundle.Root).Value;
                var creation = ObservationV3RuntimeWire.Read<ObservationV3Creation>(grant.CreationRef, bundle.Root).Value;
                ObservationV3RuntimeWire.Require(grant.RunId == request.RunId && grant.Nonce == request.Nonce && creation.RunId == request.RunId &&
                    ObservationV3RuntimeWire.SameRef(grant.RequestRef, initial.Ref) && ObservationV3RuntimeWire.SameRef(creation.RequestRef, initial.Ref) &&
                    ObservationV3Wire.Same(grant.ExpectedHelper, self) && ObservationV3Wire.Same(creation.Helper, self) &&
                    ObservationV3Wire.Same(creation.Origin, prep.Self) && ObservationV3Wire.Same(grant.Origin, prep.Self), "BootstrapOwnershipMismatch");
                ObservationV3RuntimeWire.FilePin(creation.AttemptRef, bundle.Root);
                handles.Match(prep.Self); handles.Match(prep.OriginalSteam);
                var ready = ObservationV3RuntimeWire.Stamp(new ObservationV3BootstrapReady { RequestRef = initial.Ref, GrantRef = grantRef,
                    Origin = prep.Self, Helper = self, OriginHandleRetained = true }, "bootstrap-ready", "Helper", 1, request.RunId);
                var readyRef = ObservationV3RuntimeWire.Create(bundle.Root, "bootstrap-ready.json", ready);
                Console.Out.WriteLine(ObservationV3Wire.Serialize(readyRef)); Console.Out.Flush();
                ObservationV3RuntimeWire.Require(BootstrapLine(30000) == grantRef.Sha256, "BootstrapConfirmationMissing");
                // The owning OS thread holds this mutex while all slow work is dispatched elsewhere.
                using (var mutex = new Mutex(false, WindowsIdentityCapture.LockName(prep.OriginalSteam)))
                {
                    bool owns = false;
                    ObservationV3Admission failureAdmission = null;
                    int submissionEntered = 0;
                    var cleanupClock = Stopwatch.StartNew();
                    var failureEvidence = new ObservationV3HostFailureEvidence(
                        e => { if (Volatile.Read(ref submissionEntered) == 0) WriteFailure(bundle, e, "Helper", failureAdmission); }, () => cleanupClock.ElapsedMilliseconds, 30000);
                    return failureEvidence.RunOwned(() =>
                    {
                        try { owns = mutex.WaitOne(0); }
                        catch (AbandonedMutexException) { mutex.ReleaseMutex(); throw new IOException("AbandonedCycleRequiresInspection"); }
                        if (!owns) throw new IOException("CycleLockBusy");
                        ObservationV3WindowsCycleEnvironment cycle = null;
                        ObservationPinned<ObservationV3IndependentContext> context = null;
                        Exception failure = null;
                        bool prepared = false;
                        ObservationV3Admission cycleAdmission = null;
                        using (var operations = new ObservationV3Operations(queue, () => cycle == null ? 0 : cycle.Milliseconds,
                            e => { failure = e; if (Volatile.Read(ref submissionEntered) == 0) failureEvidence.Start(e); }))
                        {
                            cycleAdmission = operations.Admission; failureAdmission = cycleAdmission;
                            cycle = new ObservationV3WindowsCycleEnvironment(bundle, handles, self, grant.CreationRef, grantRef, operations.Token,
                                absolute => operations.Deadline(absolute), operations.Admission);
                            operations.Deadline(cycle.Milliseconds + 30000);
                            operations.Start(token => Task.Run(() => { Cycle.PrepareClient(cycle, Trial.FullCycle); return true; }, token), value =>
                            { prepared = true; operations.Deadline(0); }, true);
                            while (!prepared && !operations.Closed) queue.DrainOne(20);
                            if (!prepared || operations.Closed) throw operations.FirstError ?? failure ?? new IOException("CyclePreparationClosed");
                            // Prepare every durable artifact before submitting. Nothing after
                            // the invocation waits for a command/child or performs evidence I/O.
                            bool hostPrepared = false;
                            operations.Deadline(cycle.Milliseconds + 30000);
                            operations.Start(token => Task.Run(() =>
                            {
                                var preparedAt = ObservationV3LaunchWindow.Now;
                                var value = ObservationV3RuntimeWire.Stamp(new ObservationV3IndependentContext {
                                    RequestRef = bundle.Request.Ref, ClientRef = cycle.StoreClient(), LaunchPlanRef = bundle.Plan.Ref,
                                    Nonce = request.Nonce, Role = "ReplacementObserver", Owner = request.Owner,
                                    PreparedTimestamp = preparedAt, TimestampFrequency = ObservationV3LaunchWindow.Frequency,
                                    DeadlineTimestamp = checked(preparedAt + ObservationV3LaunchWindow.Frequency * 30)
                                }, "independent-context", "Helper", 1, request.RunId);
                                var reference = ObservationV3RuntimeWire.Create(bundle.Root, "context.json", value);
                                context = ObservationV3RuntimeWire.Read<ObservationV3IndependentContext>(reference, bundle.Root);
                                var options = ReplacementOptions(bundle.Request.Ref, context.Ref);
                                ObservationV3RuntimeWire.Create(bundle.Root, "submission-intent.json",
                                    ObservationV3RuntimeWire.Stamp(new ObservationV3SubmissionIntent {
                                        ContextRef = context.Ref, Helper = self, Tokens = ObservationV3Arguments.Generated(options, true)
                                    }, "submission-intent", "Helper", 2, request.RunId));
                                return options;
                            }, token), value => { hostPrepared = true; });
                            while (!hostPrepared && !operations.Closed) queue.DrainOne(20);
                            if (!hostPrepared || operations.Closed) throw operations.FirstError ?? failure ?? new IOException("HostPreparationClosed");
                            bool returned = false;
                            int exitCode = 1;
                            operations.Start(token => Task.Run(() =>
                            {
                                var adapter = new ObservationV3WindowsLaunch(bundle, ReplacementOptions(bundle.Request.Ref, context.Ref), operations.Admission);
                                return adapter.SubmitAndReturn(context.Value, () => { cycle.EnsureNoOtherGame(); cycle.EnsureNewSteamUnchanged(); }, () => Interlocked.Exchange(ref submissionEntered, 1));
                            }, token), value => { exitCode = value; returned = true; });
                            while (!returned && !operations.Closed) queue.DrainOne(20);
                            if (returned && operations.Complete()) return exitCode;
                            return 1; // A blocked/failed call is unknown, never a retry or completion.

                        }
                    }, () => { if (owns) mutex.ReleaseMutex(); });
                }
            }
        }
        public static ObservationV3Options ReplacementOptions(ObservationRef request, ObservationRef context)
        {
            return new ObservationV3Options { Role = OverlayObservationRole.ReplacementObserver, Owner = ObservationLaunchOwner.SteamDelegated,
                Request = request.Path, RequestHash = request.Sha256, Context = context.Path, ContextHash = context.Sha256 };
        }
        public static void WriteFailure(ObservationV3Bundle bundle, Exception error, string author, ObservationV3Admission admission = null)
        {
            var b = bundle.Request != null ? bundle.Request.Ref : bundle.Preparation.Ref;
            var run = bundle.Request != null ? bundle.Request.Value.RunId : bundle.Preparation.Value.RunId;
            ObservationV3RuntimeWire.Create(bundle.Root, "terminal-failure.json", ObservationV3RuntimeWire.Stamp(new ObservationV3Terminal {
                BindingRef = b, Phase = author, Outcome = "Uncertain", FirstError = error.Message, ChildExit = "Unknown", HelperExit = "Unknown", Tracking = "Unknown",
                PurposeAchieved = false, Invocations = admission == null ? null : admission.Facts() }, "terminal-failure", author, long.MaxValue, run));
        }
        public static ObservationV3ProbeResult RunProbe(string inputPath, string inputHash)
        {
            var inputRef = new ObservationRef { Path = inputPath, Sha256 = inputHash };
            var input = ObservationV3RuntimeWire.Read<ObservationV3ProbeInput>(inputRef, null).Value;
            var bundle = ObservationV3Bundle.LoadRequest(input.RequestRef); bundle.RequireLaunchAvailable();
            bundle.ValidateProbeChain(input);
            ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(WindowsIdentityCapture.Steam(input.Helper), input.Client), "ProbeCurrentSteamMismatch");
            using (var handles = new ObservationV3ProcessHandles())
            using (var process = Process.GetCurrentProcess())
            {
                handles.Match(input.Helper); handles.Match(input.Client);
                var self = handles.Capture(process.Id);
                ObservationV3RuntimeWire.Require(WindowsIdentityCapture.SameScope(self, input.Client) &&
                    !ObservationV3Wire.Same(input.Client, bundle.Preparation.Value.OriginalSteam) && Environment.CurrentDirectory == input.WorkingDirectory, "ProbeScopeMismatch");
                var result = NativeProbe.RunValidated(input.Nonce, input.NativeDll.Path, (stage, error) => { handles.Match(input.Helper); handles.Match(input.Client); });
                return ObservationV3RuntimeWire.Stamp(new ObservationV3ProbeResult { InputRef = inputRef, Self = self, Client = input.Client, Observation = result },
                    "probe-result", "Probe", 1, input.RunId);
            }
        }
    }

    public sealed class ObservationV3WindowsCycleEnvironment : ICycleEnvironment
    {
        private readonly ObservationV3Bundle bundle;
        private readonly ObservationV3ProcessHandles handles;
        private readonly ProcessIdentity helper;
        private readonly CancellationToken cancellation;
        private readonly Action<long> setDeadline;
        private readonly ObservationV3Admission admission;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private int attempts;
        private ProcessIdentity shutdown;
        private ObservationRef probeInputRef, probeResultRef;
        private ProcessIdentity probe;
        private readonly ObservationRef creationRef, grantRef;
        public ProcessIdentity Current { get; private set; }
        public long Milliseconds { get { return clock.ElapsedMilliseconds; } }
        private ProcessIdentity Origin { get { return bundle.Preparation.Value.Self; } }
        private ProcessIdentity Original { get { return bundle.Preparation.Value.OriginalSteam; } }
        public ObservationV3WindowsCycleEnvironment(ObservationV3Bundle bundle, ObservationV3ProcessHandles handles, ProcessIdentity helper,
            ObservationRef creationRef, ObservationRef grantRef, CancellationToken token, Action<long> setDeadline, ObservationV3Admission admission)
        { this.bundle = bundle; this.handles = handles; this.helper = helper; this.creationRef = creationRef; this.grantRef = grantRef; cancellation = token; this.setDeadline = setDeadline; this.admission = admission; }
        private void Guard() { cancellation.ThrowIfCancellationRequested(); bundle.ValidatePins(); handles.Match(helper); }
        public void Validate() { Guard(); bundle.RequireLaunchAvailable(); }
        public IDisposable AcquireCycleLock() { throw new InvalidOperationException("Windows host owns the scope mutex."); }
        public bool ParentAlive() { Guard(); return !handles.Exited(Origin); }
        public void EnsureNoOtherGame()
        {
            Guard();
            var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Origin.Path));
            try { handles.EnsureNoOtherGame(Origin, candidates.Select(p => p.Id).ToArray()); }
            finally { foreach (var process in candidates) process.Dispose(); }
        }
        private ProcessIdentity Steam(ProcessIdentity command)
        {
            Guard(); var id = WindowsIdentityCapture.Steam(helper, command);
            return id == null ? null : handles.Capture(id.Pid);
        }
        public bool OriginalSteamAlive()
        {
            Guard();
            return handles.OriginalSteamAlive(Original, shutdown, () => {
                var candidates = Process.GetProcessesByName("steam");
                try { return candidates.Select(p => p.Id).ToArray(); }
                finally { foreach (var process in candidates) process.Dispose(); }
            });
        }
        private ProcessStartInfo SteamStart(string args)
        {
            var start = new ProcessStartInfo { FileName = Original.Path, WorkingDirectory = Path.GetDirectoryName(Original.Path), Arguments = args, UseShellExecute = false };
            LaunchEnvironment.Apply(start, LaunchRole.Steam, bundle.Config.Value.AppId); return start;
        }
        public void RequestSteamExit(Deadline deadline)
        {
            setDeadline(deadline.Expires);
            var claim = admission.Prepare("steam-shutdown", deadline.Expires);
            var start = SteamStart("-shutdown");
            if (shutdown != null) throw new IOException("ShutdownAlreadyAttempted");
            shutdown = handles.StartShutdown(admission, claim, start, Original, helper,
                () => { Guard(); handles.Match(Original); deadline.Remaining(); }, Process.Start);
            admission.RequireCurrent(claim.Generation, claim.Deadline);
        }
        public bool ShutdownCommandAlive() { Guard(); return handles.ShutdownCommandAlive(shutdown); }
        public void EnsureSteamExited() { ObservationV3RuntimeWire.Require(!OriginalSteamAlive(), "SteamExitUnconfirmed"); }
        public void StartSteam()
        {
            var claim = admission.Prepare("steam-start", admission.Deadline);
            var start = SteamStart(""); long invoked = 0;
            using (var p = admission.Execute(claim, () => { Guard(); EnsureSteamExited(); }, () => { invoked = Milliseconds; return Process.Start(start); }))
            { if (p == null) throw new IOException("ClientCreationUnknown"); Current = handles.Capture(p.Id); }
            ObservationV3Wire.SameFileScope(Current, Original);
            ObservationV3RuntimeWire.Require(!ObservationV3Wire.Same(Current, Original), "NewClientRequired");
            admission.RequireCurrent(claim.Generation, claim.Deadline);
            setDeadline(invoked + 120000);
        }
        public void EnsureNewSteamUnchanged() { ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(Steam(null), Current), "NewClientChanged"); }
        private sealed class RetainedProbeOperations : ProbeOperations
        {
            internal ObservationV3ProcessHandles Handles; internal ProcessIdentity Identity;
            internal ObservationV3Admission Admission; internal ObservationV3Invocation Claim; internal Action Prepare;
            public override Process Start(ProcessStartInfo start)
            { return Admission.Execute(Claim, Prepare, () => base.Start(start)); }
            public override void Owned(Process process, Deadline deadline)
            {
                int pid = process.Id;
                var capture = Task.Run(() => Handles.Capture(pid));
                if (!capture.Wait((int)Math.Min(int.MaxValue, deadline.Remaining()))) throw new TimeoutException("OwnedProbeIdentityTimeout");
                Identity = capture.GetAwaiter().GetResult(); deadline.Remaining();
            }
        }
        public bool ProbeReady(Deadline deadline)
        {
            Guard(); EnsureNewSteamUnchanged();
            var request = bundle.Request.Value; var config = bundle.Config.Value;
            var input = ObservationV3RuntimeWire.Stamp(new ObservationV3ProbeInput { RequestRef = bundle.Request.Ref, CreationRef = creationRef, GrantRef = grantRef,
                NativeDll = bundle.Plan.Value.NativeDll, Client = Current, Helper = helper, AppId = config.AppId, SteamId = config.ExpectedSteamId,
                Nonce = Guid.NewGuid().ToString("N"), WorkingDirectory = bundle.Plan.Value.WorkingDirectory }, "probe-input", "Helper", ++attempts, request.RunId);
            probeInputRef = ObservationV3RuntimeWire.Create(bundle.Root, "probe-input-" + attempts + ".json", input);
            ObservationV3ProbeResult result = null;
            var operations = new RetainedProbeOperations { Handles = handles, Admission = admission,
                Claim = admission.Prepare("probe-" + attempts, deadline.Expires), Prepare = () => { Guard(); EnsureNewSteamUnchanged(); deadline.Remaining(); } };
            var expected = new ProbeExpectation { Nonce = input.Nonce, EvidenceDirectory = bundle.Root, AppId = config.AppId, SteamId = config.ExpectedSteamId };
            bool ready = WindowsCycleEnvironment.RunOwnedProbeCore(() =>
            {
                Guard(); EnsureNewSteamUnchanged();
                var start = ObservationV3WindowsHost.HostStart(Path.Combine(bundle.Plan.Value.PayloadRoot, "RestartExperiment"), probeInputRef.Path, input.WorkingDirectory, true);
                LaunchEnvironment.Apply(start, LaunchRole.Probe, config.AppId); return start;
            }, deadline, () => Milliseconds, expected, new ProbeAttempt { Attempt = attempts, Nonce = input.Nonce, Utc = DateTime.UtcNow.ToString("o") }, null,
                attempt => ObservationV3RuntimeWire.Create(bundle.Root, "probe-attempt-" + attempts + ".json", attempt), operations,
                json =>
                {
                    result = ObservationV3RuntimeWire.Parse<ObservationV3ProbeResult>(Encoding.UTF8.GetBytes(json));
                    ObservationV3RuntimeWire.Require(result.RunId == request.RunId && ObservationV3RuntimeWire.SameRef(result.InputRef, probeInputRef) &&
                        ObservationV3Wire.Same(result.Self, operations.Identity) && ObservationV3Wire.Same(result.Client, Current), "ProbeResultAttributionMismatch");
                    return result.Observation;
                }, probeInputRef.Sha256);
            probe = operations.Identity;
            ObservationV3RuntimeWire.Require(handles.Exited(probe), "ProbeExitUnknown");
            if (result != null) probeResultRef = ObservationV3RuntimeWire.Create(bundle.Root, "probe-result-" + attempts + ".json", result);
            if (!ready && result != null && result.Observation.InitDisposition == ProbeInitDisposition.Succeeded) throw new IOException("ProbeNativeIdentityMismatch");
            return ready;
        }
        public ObservationRef StoreClient()
        {
            Guard(); EnsureNewSteamUnchanged();
            return ObservationV3RuntimeWire.Create(bundle.Root, "new-client.json", ObservationV3RuntimeWire.Stamp(new ObservationV3NewClient {
                RequestRef = bundle.Request.Ref, CreationRef = creationRef, GrantRef = grantRef, ProbeInputRef = probeInputRef, ProbeResultRef = probeResultRef,
                Original = Original, Current = Current, Probe = probe, ShutdownCommand = shutdown,
                OriginalExited = handles.Exited(Original), ShutdownCommandExited = !handles.ShutdownCommandAlive(shutdown), ProbeExited = handles.Exited(probe) },
                "new-client", "Helper", 1, bundle.Request.Value.RunId));
        }
        public void StartGame(Deadline deadline) { throw new InvalidOperationException("Only the runtime handoff may submit."); }
        public void Delay(int ms) { if (cancellation.WaitHandle.WaitOne(ms)) cancellation.ThrowIfCancellationRequested(); }
        public void Record(string stage) { Guard(); }
        public void Cleanup() { }
        public void RecordTerminal(Exception error) { throw new InvalidOperationException("Only the runtime host owns terminal evidence."); }
    }
}
