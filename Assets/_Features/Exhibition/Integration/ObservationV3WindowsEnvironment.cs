// Actual Windows adapters; only the explicit runtime-1 host/player composition instantiates them.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationV3Bundle
    {
        public ObservationPinned<ObservationV3OriginConfig> Config;
        public ObservationPinned<ObservationV3LaunchPlan> Plan;
        public ObservationPinned<ObservationV3OriginPreparation> Preparation;
        public ObservationPinned<ObservationV3RuntimeRequest> Request;
        public ObservationPinned<ObservationV3Baseline> Baseline;
        public string Root;
        public static ObservationV3Bundle LoadConfig(string path)
        {
            var b = new ObservationV3Bundle();
            b.Config = ObservationV3RuntimeWire.ReadInitial<ObservationV3OriginConfig>(path, ObservationV3RuntimeWire.EvidenceRoot(Path.GetDirectoryName(path)));
            var c = b.Config.Value;
            ObservationV3RuntimeWire.Require(c.Kind == "origin-config" && c.AppId == 5218360 && c.ExpectedSteamId != 0, "OriginConfigIdentity");
            ObservationV3RuntimeWire.Targets(c.Targets, null); b.Root = ObservationV3RuntimeWire.EvidenceRoot(c.EvidenceRoot);
            b.Plan = ObservationV3RuntimeWire.Read<ObservationV3LaunchPlan>(c.LaunchPlanRef, null);
            b.ValidateGlobal(); return b;
        }
        public static ObservationV3Bundle LoadRequest(ObservationRef reference)
        {
            var r = ObservationV3RuntimeWire.Read<ObservationV3RuntimeRequest>(reference, null);
            var v = r.Value; var b = LoadConfig(v.ConfigRef.Path); b.RequireLaunchAvailable();
            ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(v.ConfigRef, b.Config.Ref), "ConfigBytesChanged");
            b.Request = r; b.Root = ObservationV3RuntimeWire.EvidenceRoot(v.Directory);
            b.Preparation = ObservationV3RuntimeWire.Read<ObservationV3OriginPreparation>(v.PreparationRef, b.Root);
            var p = b.Preparation.Value;
            ObservationV3RuntimeWire.Require(v.Kind == "request" && v.Owner == "SteamDelegated" && v.RunId == p.RunId && v.Nonce == p.Nonce &&
                ObservationV3RuntimeWire.SameRef(v.ConfigRef, p.ConfigRef) &&
                ObservationV3RuntimeWire.SameRef(v.LaunchPlanRef, p.LaunchPlanRef) &&
                ObservationV3RuntimeWire.SameRef(v.ReadyRef, p.ReadyRef) && ObservationV3RuntimeWire.SameRef(v.ParticipantSnapshotRef, p.ParticipantSnapshotRef), "RequestChainMismatch");
            b.Baseline = ObservationV3RuntimeWire.Read<ObservationV3Baseline>(v.BaselineRef, b.Root);
            ObservationV3RuntimeWire.Baseline(b.Baseline.Value, b.Preparation.Ref, p, b.Config.Value);
            ObservationV3RuntimeWire.Require(b.Baseline.Value.Targets.All(t => t.Achieved == false), "BaselineNotUnearned");
            var vis = ObservationV3RuntimeWire.Read<ObservationV3Visibility>(v.OriginVisibilityRef, b.Root);
            var display = ObservationV3RuntimeWire.Read<ObservationV3Display>(v.OriginDisplayRef, b.Root);
            ObservationV3RuntimeWire.ValidateOriginVisibility(vis.Value, b.Preparation.Ref, p, b.Baseline.Ref);
            ObservationV3RuntimeWire.ValidateOriginDisplay(display.Value, b.Preparation.Ref, p, b.Baseline.Ref, vis.Ref, vis.Value);
            ObservationV3RuntimeWire.Require(display.Value.Targets.All(t => t.Display == "earned" || t.Display == "unearned") &&
                DateTimeOffset.Parse(vis.Value.WrittenUtc) >= DateTimeOffset.Parse(b.Baseline.Value.WrittenUtc), "OriginConditionLost");
            b.ValidateBaselinePins(); b.ValidatePins(); return b;
        }
        public void ValidateBaselinePins()
        {
            var b = Baseline.Value; var p = Preparation.Value;
            var before = ObservationV3RuntimeWire.Read<ObservationV3PinEvidence>(b.BeforeRef, Root).Value;
            var after = ObservationV3RuntimeWire.Read<ObservationV3PinEvidence>(b.AfterRef, Root).Value;
            ObservationV3RuntimeWire.BaselinePins(b, p, Preparation.Ref, before, after);
        }
        public ObservationV3Creation ValidateProbeChain(ObservationV3ProbeInput input)
        {
            var grant = ObservationV3RuntimeWire.Read<ObservationV3Grant>(input.GrantRef, Root).Value;
            var creation = ObservationV3RuntimeWire.Read<ObservationV3Creation>(input.CreationRef, Root).Value;
            var r = Request.Value; var p = Preparation.Value;
            ObservationV3RuntimeWire.Require(input.Kind == "probe-input" && input.RunId == r.RunId && input.AppId == Config.Value.AppId &&
                input.SteamId == Config.Value.ExpectedSteamId && ObservationV3RuntimeWire.SameRef(input.RequestRef, Request.Ref) &&
                ObservationV3RuntimeWire.SameRef(input.CreationRef, grant.CreationRef) && ObservationV3RuntimeWire.SameRef(input.NativeDll, Plan.Value.NativeDll) &&
                grant.RunId == r.RunId && grant.Nonce == r.Nonce && creation.RunId == r.RunId &&
                ObservationV3RuntimeWire.SameRef(grant.RequestRef, Request.Ref) && ObservationV3RuntimeWire.SameRef(creation.RequestRef, Request.Ref) &&
                ObservationV3Wire.Same(grant.Origin, p.Self) && ObservationV3Wire.Same(creation.Origin, p.Self) &&
                ObservationV3Wire.Same(grant.ExpectedHelper, input.Helper) && ObservationV3Wire.Same(creation.Helper, input.Helper), "ProbeGrantChainMismatch");
            ObservationV3Wire.Id(input.Nonce); ObservationV3RuntimeWire.Canonical(input.WorkingDirectory, null);
            ObservationV3RuntimeWire.FilePin(creation.AttemptRef, Root);
            ObservationV3Wire.SameFileScope(input.Client, p.OriginalSteam);
            ObservationV3RuntimeWire.Require(!ObservationV3Wire.Same(input.Client, p.OriginalSteam), "NewClientRequired");
            return creation;
        }
        public ObservationV3Creation ValidateNewClient(ObservationV3NewClient c)
        {
            var input = ObservationV3RuntimeWire.Read<ObservationV3ProbeInput>(c.ProbeInputRef, Root).Value;
            var creation = ValidateProbeChain(input);
            var probe = ObservationV3RuntimeWire.Read<ObservationV3ProbeResult>(c.ProbeResultRef, Root).Value;
            ObservationV3RuntimeWire.Require(c.RunId == Request.Value.RunId && c.OriginalExited && c.ShutdownCommandExited && c.ProbeExited &&
                ObservationV3Wire.Same(c.Original, Preparation.Value.OriginalSteam) && ObservationV3Wire.Same(c.Current, input.Client) &&
                ObservationV3RuntimeWire.SameRef(c.RequestRef, Request.Ref) && ObservationV3RuntimeWire.SameRef(c.CreationRef, input.CreationRef) &&
                ObservationV3RuntimeWire.SameRef(c.GrantRef, input.GrantRef) && probe.RunId == c.RunId &&
                ObservationV3RuntimeWire.SameRef(probe.InputRef, c.ProbeInputRef) && ObservationV3Wire.Same(probe.Self, c.Probe) &&
                ObservationV3Wire.Same(probe.Client, c.Current), "NewClientChainMismatch");
            ObservationV3Wire.Identity(c.ShutdownCommand); ObservationV3Wire.Identity(c.Probe);
            ObservationV3RuntimeWire.Require(WindowsCycleEnvironment.ValidateProbeExpectation(probe.Observation,
                new ProbeExpectation { Nonce = input.Nonce, EvidenceDirectory = Root, AppId = Config.Value.AppId, SteamId = Config.Value.ExpectedSteamId },
                c.Probe.Pid, c.Probe.StartTicks), "ProbeReadinessRequired");
            return creation;
        }
        public void ValidateGlobal()
        {
            var p = Plan.Value;
            ObservationV3RuntimeWire.Require(p.Kind == "launch-plan" && p.AppId == Config.Value.AppId, "LaunchPlanContractMismatch");
            ObservationV3RuntimeWire.Canonical(p.PayloadRoot, null);
            ObservationV3RuntimeWire.Canonical(p.WorkingDirectory, null);
        }
        public void RequireLaunchAvailable()
        {
            ValidatePins();
            var p = Plan.Value;
            // Only the files about to be executed/loaded are checked at the handoff boundary.
            ObservationV3RuntimeWire.FilePin(p.SteamExe, null);
            ObservationV3RuntimeWire.FilePin(p.GameExe, p.PayloadRoot);
            ObservationV3RuntimeWire.FilePin(p.NativeDll, p.PayloadRoot);
        }
        public void ValidatePins()
        {
            ObservationV3RuntimeWire.FilePin(Config.Ref, null); ObservationV3RuntimeWire.FilePin(Plan.Ref, null);
            ValidateGlobal();
            if (Preparation == null) return;
            var p = Preparation.Value; var c = Config.Value;
            ObservationV3RuntimeWire.Require(ObservationV3RuntimeWire.SameRef(p.ConfigRef, Config.Ref) &&
                ObservationV3RuntimeWire.SameRef(p.LaunchPlanRef, Plan.Ref), "PreparationPinsChanged");
            ObservationV3RuntimeWire.Targets(p.Targets, c.Targets);
            ValidateReadyPins(p.ReadyRef, p.ParticipantSnapshotRef, Root, p.RunId, c.AppId, c.ExpectedSteamId);
        }
        // Shared production consumer for origin/native, helper cycle, replacement/native, and observation pins.
        public static void ValidateReadyPins(ObservationRef readyRef, ObservationRef participantRef, string root, string run, uint appId, ulong steamId)
        {
            var ready = ObservationV3RuntimeWire.Read<ObservationV3ReadySnapshot>(readyRef, root).Value;
            ObservationV3RuntimeWire.Require(ready.RunId == run && ready.State == "Ready" && ready.AppId == appId && ready.SteamId == steamId &&
                ready.MappingVersion == ObservationV3JournalReader.MappingVersion, "ReadySnapshotMismatch");
            ObservationV3Wire.Id(ready.OperationId);
            var snapshot = ObservationV3RuntimeWire.Read<ObservationV3ParticipantSnapshot>(participantRef, root).Value;
            ObservationV3RuntimeWire.Require(snapshot.RunId == run, "ParticipantRunMismatch");
            ObservationV3RuntimeWire.Canonical(ready.Journal.Path, snapshot.Root);
            ObservationV3JournalReader.Validate(ready, snapshot, appId, steamId);
        }
    }
    public sealed class ObservationV3ProcessHandles : IDisposable
    {
        private readonly object gate = new object();
        private readonly Dictionary<int, Process> processes = new Dictionary<int, Process>();
        private readonly Dictionary<int, ProcessIdentity> identities = new Dictionary<int, ProcessIdentity>();
        private bool disposed;
        private readonly Func<Process, ProcessIdentity> capture;
        public ObservationV3ProcessHandles() : this(process => WindowsIdentityCapture.Capture(process, true)) { }
        public ObservationV3ProcessHandles(Func<Process, ProcessIdentity> capture) { this.capture = capture; }
        public ProcessIdentity Capture(int pid)
        {
            lock (gate) { if (disposed) throw new ObjectDisposedException("ObservationV3ProcessHandles"); }
            // Slow identity/file queries own a local handle. They cannot block collection
            // disposal or retain a shared lock after their caller's deadline expires.
            Process process = Process.GetProcessById(pid); bool retained = false;
            try
            {
                IntPtr handle = process.Handle;
                var before = capture(process); var after = capture(process);
                ObservationV3RuntimeWire.Require(!process.HasExited && ObservationV3Wire.Same(before, after), "IdentityCaptureRace");
                lock (gate)
                {
                    if (disposed) throw new ObjectDisposedException("ObservationV3ProcessHandles");
                    ProcessIdentity existing;
                    if (identities.TryGetValue(pid, out existing))
                        ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(before, existing), "RetainedIdentityChanged");
                    else
                    {
                        processes.Add(pid, process); identities.Add(pid, ObservationV3Wire.Copy(before)); retained = true;
                    }
                }
                return before;
            }
            finally { if (!retained) process.Dispose(); }
        }
        private Process Retained(ProcessIdentity identity)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("ObservationV3ProcessHandles");
                ObservationV3RuntimeWire.Require(identity != null && identities.ContainsKey(identity.Pid) && ObservationV3Wire.Same(identity, identities[identity.Pid]), "MissingRetainedHandle");
                return processes[identity.Pid];
            }
        }
        public bool Exited(ProcessIdentity identity) { return Retained(identity).HasExited; }
        public ProcessIdentity StartShutdown(ObservationV3Admission admission, ObservationV3Invocation claim,
            ProcessStartInfo start, ProcessIdentity executable, ProcessIdentity owner, Action prepare,
            Func<ProcessStartInfo, Process> create)
        {
            var process = admission.Execute(claim, () => {
                ObservationV3RuntimeWire.Require(!start.UseShellExecute && string.IsNullOrEmpty(start.UserName) &&
                    string.IsNullOrEmpty(start.Verb) && string.Equals(start.FileName, executable.Path, StringComparison.OrdinalIgnoreCase) &&
                    WindowsIdentityCapture.SameScope(executable, owner), "ShutdownCreationContract");
                prepare();
            }, () => create(start));
            if (process == null) throw new IOException("ShutdownSubmissionUnknown");
            bool retained = false;
            try
            {
                // This is a creation identity, not a post-exit image/token query:
                // pinned executable + inherited scope + the returned OS handle's times.
                IntPtr handle = process.Handle;
                var identity = ObservationV3Wire.Copy(executable);
                identity.Pid = process.Id; identity.StartTicks = process.StartTime.ToUniversalTime().Ticks;
                ObservationV3RuntimeWire.Require(handle != IntPtr.Zero && identity.StartTicks > 0, "ShutdownHandleUnavailable");
                lock (gate)
                {
                    if (disposed) throw new ObjectDisposedException("ObservationV3ProcessHandles");
                    ObservationV3RuntimeWire.Require(!processes.ContainsKey(identity.Pid), "ShutdownIdentityAlreadyRetained");
                    processes.Add(identity.Pid, process); identities.Add(identity.Pid, ObservationV3Wire.Copy(identity)); retained = true;
                }
                return identity;
            }
            finally { if (!retained) process.Dispose(); }
        }
        public bool ShutdownCommandAlive(ProcessIdentity identity)
        {
            var process = Retained(identity);
            if (!process.HasExited) return true;
            ObservationV3RuntimeWire.Require(process.ExitCode == 0, "ShutdownCommandAbnormalExit:" + process.ExitCode);
            return false;
        }
        public bool OriginalSteamAlive(ProcessIdentity original, ProcessIdentity command, Func<int[]> steamPids)
        {
            // Enumerate outside the lock. Known handles keep both PIDs reserved even
            // when either process exits during enumeration. Other clients still fail.
            int[] candidates = steamPids();
            var unknown = new List<int>();
            lock (gate)
            {
                Retained(original);
                if (command != null) Retained(command);
                foreach (int pid in candidates)
                    if (pid != original.Pid && (command == null || pid != command.Pid)) unknown.Add(pid);
            }
            foreach (int pid in unknown)
                if (WindowsIdentityCapture.SessionId(pid) == original.Session) throw new IOException("OriginalClientChanged");
            return !Exited(original);
        }
        public void Match(ProcessIdentity expected)
        { ObservationV3RuntimeWire.Require(ObservationV3Wire.Same(expected, Capture(expected.Pid)), "ProcessPinChanged"); }
        public void EnsureNoOtherGame(ProcessIdentity origin, int[] candidatePids)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException("ObservationV3ProcessHandles");
                ProcessIdentity retained;
                ObservationV3RuntimeWire.Require(origin != null && identities.TryGetValue(origin.Pid, out retained) &&
                    ObservationV3Wire.Same(origin, retained) && processes.ContainsKey(origin.Pid), "MissingRetainedHandle");
                // Capture already opened and retained this process's native handle.
                // Windows cannot reuse its PID while that handle remains open, even
                // after exit. Do not demand live identity data from the dying origin.
                foreach (int pid in candidatePids)
                    if (pid != origin.Pid) throw new IOException("CompetingGameBeforeSubmission");
            }
        }
        public void Dispose()
        {
            Process[] owned;
            lock (gate) { if (disposed) return; disposed = true; owned = processes.Values.ToArray(); processes.Clear(); identities.Clear(); }
            foreach (var process in owned) process.Dispose();
        }
    }
    public sealed class ObservationV3WindowsLaunch
    {
        private readonly ObservationV3Bundle bundle;
        private readonly ObservationV3Options options;
        private int attempted;
        private readonly ObservationV3Admission admission;
        public ObservationV3WindowsLaunch(ObservationV3Bundle bundle, ObservationV3Options options, ObservationV3Admission admission)
        { this.admission = admission; this.bundle = bundle; this.options = options; }
        public void RequireAvailable(ObservationLaunchOwner owner)
        {
            ObservationV3RuntimeWire.Require(owner == ObservationLaunchOwner.SteamDelegated && Environment.OSVersion.Platform == PlatformID.Win32NT, "UnsupportedLaunchTransport");
            bundle.RequireLaunchAvailable();
        }
        // Production and harmless multiprocess tests share the final executor.
        // Disposing the returned command handle does not wait for command exit.
        public static int InvokeAndReturn(ObservationV3Admission gate, ObservationV3Invocation claim, Action prepare,
            Func<Process> start, Action entered)
        {
            try
            {
                using (var process = gate.Execute(claim, prepare, () => { entered(); return start(); }))
                    return process == null ? 1 : 0;
            }
            catch { return 1; }
        }
        public int SubmitAndReturn(ObservationV3IndependentContext context, Action finalValidation, Action entered)
        {
            var claim = admission.Prepare("launch-submission", admission.Deadline);
            if (Interlocked.CompareExchange(ref attempted, 1, 0) != 0) throw new IOException("LaunchAlreadyAttempted");
            var plan = bundle.Plan.Value;
            var tokens = new[] { "-applaunch", plan.AppId.ToString(System.Globalization.CultureInfo.InvariantCulture) }.Concat(ObservationV3Arguments.Generated(options, false));
            var start = new ProcessStartInfo { FileName = plan.SteamExe.Path, WorkingDirectory = Path.GetDirectoryName(plan.SteamExe.Path),
                Arguments = string.Join(" ", tokens.Select(ObservationV3Arguments.Quote).ToArray()), UseShellExecute = false };
            LaunchEnvironment.Apply(start, LaunchRole.Steam, plan.AppId);
            return InvokeAndReturn(admission, claim, () => {
                RequireAvailable(options.Owner); finalValidation();
                ObservationV3LaunchWindow.RequireOpen(context, ObservationV3LaunchWindow.Now, ObservationV3LaunchWindow.Frequency);
            }, () => Process.Start(start), entered);
        }
    }
}
