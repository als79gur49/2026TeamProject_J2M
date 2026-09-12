// Real helper/launcher/child processes around the production completion-driven handoff.
// Native readiness and Steam submission are fake ports; no Steam/native code is called.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;
public static class Runtime2HandoffFakes
{
    static string run;
    static ObservationRef binding, baseline;
    static string[] targets = { ResetOverlayWire.Names().First() };
    static T Doc<T>(T d, string kind, string role) where T : ObservationDocument
    { return ObservationV3RuntimeWire.Stamp(d, kind, role, 1, run); }
    static void Setup(string root)
    {
        run = new DirectoryInfo(root).Name;
        binding = new ObservationRef { Path = Path.Combine(root, "binding.txt"), Sha256 = ExperimentFiles.Hash(Path.Combine(root, "binding.txt")) };
        baseline = binding;
    }
    static void Check(bool ok, string why) { if (!ok) throw new IOException(why); }
    public static async Task Child(string root)
    {
        Setup(root);
        using (var selfProcess = Process.GetCurrentProcess())
        using (var timeout = new CancellationTokenSource(10000))
        using (var p = await ObservationV3Pipe.ConnectAsync("j2m-observation-v3-" + run, 10000, timeout.Token))
        {
            p.Runtime2 = true; var token = timeout.Token; var self = WindowsIdentityCapture.Capture(selfProcess, true);
            await p.SendAsync("hello", Doc(new ObservationV3RuntimeHello { ContextRef = binding, Self = self, Tokens = Environment.GetCommandLineArgs(),
                EffectiveArgumentsHash = binding.Sha256, WorkingDirectory = Environment.CurrentDirectory }, "hello", "ReplacementObserver"), token);
            Check(await p.ReceiveAsync<string>("fake-native-challenge", token) == run, "fake challenge");
            await p.SendAsync("fake-native-ack", run, token); // harmless native seam
            var receipt = await p.ReceiveAsync<ObservationRef>("accepted", token);
            var visibility = Doc(new ObservationV3Visibility { BindingRef = binding, BaselineRef = baseline, CorrectsRef = null, Self = self,
                Role = "ReplacementObserver", Visibility = "opened", OriginalText = "fake user opened", ObservedUtc = null }, "visibility-report", "ReplacementObserver");
            await p.SendAsync("visibility-report", Frame(visibility, "visibility-report", self, receipt, 1), token);
            var vis = await p.ReceiveAsync<ObservationV3Stored>("report-stored", token);
            Check(vis.ReportSequence == 1 && vis.ReportRef.Sha256 == ObservationV3RuntimeWire.Hash(ObservationV3RuntimeWire.Bytes(visibility)), "stored visibility bytes");
            var display = Doc(new ObservationV3Display { BindingRef = binding, BaselineRef = baseline, VisibilityRef = vis.ReportRef, CorrectsRef = null,
                Self = self, Role = "ReplacementObserver", OriginalText = "fake user unearned", ObservedUtc = null,
                Targets = targets.Select(t => new ObservationV3TargetDisplay { Id = t, Display = "unearned" }).ToArray() }, "display-report", "ReplacementObserver");
            await p.SendAsync("display-report", Frame(display, "display-report", self, receipt, 2), token);
            var shown = await p.ReceiveAsync<ObservationV3Stored>("report-stored", token);
            Check(shown.ReportSequence == 2 && shown.ReportRef.Sha256 == ObservationV3RuntimeWire.Hash(ObservationV3RuntimeWire.Bytes(display)), "stored display bytes");
            await p.SendAsync("exit-request", Frame(new ObservationV3ExitRequest { Boundary = 2, Checkpoint = new ObservationV3Checkpoint {
                State = "Stored", Sequence = 2, ReportRef = shown.ReportRef } }, "exit-request", self, receipt, 3), token);
            var exit = await p.ReceiveAsync<ObservationV3ExitAuthorized>("exit-authorized", token);
            Check(exit.Boundary == 2, "exit report barrier");
            var lifecycle = Doc(new ObservationV3Lifecycle { ContextRef = binding, ReceiptRef = receipt, Self = self,
                RuntimePresent = true, InitializationAttempted = true, InitializationSucceeded = true, ShutdownCallCount = 1,
                ShutdownReturned = true, RuntimeState = "Shutdown", StartupState = "Shutdown", Failure = "None", ShutdownStarted = 1, ShutdownFinished = 2,
                IdentityObservationCount = 1, LastIdentityObservation = 1 }, "native-lifecycle", "ReplacementObserver");
            var lifecycleRef = ObservationV3RuntimeWire.Create(root, "fake-native-lifecycle.json", lifecycle);
            await p.SendAsync("exit-completed", Frame(new ObservationV3ExitCompleted { Boundary = 2, Checkpoint = exit.Checkpoint, LifecycleRef = lifecycleRef }, "exit-completed", self, receipt, 4), token);
        }
    }
    static ObservationV3Envelope Frame(object body, string kind, ProcessIdentity self, ObservationRef receipt, long sequence)
    {
        var f = Doc(new ObservationV3Envelope { ContextRef = binding, ReceiptRef = receipt, Role = "ReplacementObserver", Self = self,
            Body = ObservationV3Wire.Serialize(body) }, kind, "ReplacementObserver"); f.Sequence = sequence; return f;
    }
    public static void Helper(string root)
    {
        Setup(root);
        using (var queue = new ObservationV3EventQueue())
        using (var ports = new Ports(root))
        using (var h = new ObservationV3RuntimeHandoff(ports, queue, new ObservationV3ReportLedger(run, "ReplacementObserver", null, targets, binding, baseline)))
        {
            h.Start(); var watch = Stopwatch.StartNew(); long tick = 0;
            while (!h.Closed && watch.ElapsedMilliseconds < 15000)
            {
                queue.DrainOne(5);
                if (watch.ElapsedMilliseconds >= tick) { tick = watch.ElapsedMilliseconds + 10; h.Tick(); }
            }
            while (queue.DrainOne(0)) { }
            Check(h.Stage == ObservationV3RuntimeStage.Completed && h.PurposeAchieved, "production handoff fake E2E did not complete: " + ports.Error);
            Check(ports.Submits == 1, "submission count");
        }
    }
    sealed class Ports : IObservationV3RuntimeHostPorts, IDisposable
    {
        readonly string root;
        readonly ObservationV3ProcessHandles handles = new ObservationV3ProcessHandles();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly ObservationV3Pipe pipe;
        readonly ProcessIdentity helper;
        ProcessIdentity child;
        ObservationRef receipt;
        public int Submits;
        public Exception Error;
        public long Now { get { return clock.ElapsedMilliseconds; } }
        public Ports(string root)
        {
            this.root = root; using (var p = Process.GetCurrentProcess()) helper = handles.Capture(p.Id);
            pipe = ObservationV3Pipe.Listen("j2m-observation-v3-" + run, helper.UserSid); pipe.Runtime2 = true;
            File.WriteAllText(Path.Combine(root, "helper-identity.json"), ObservationV3Wire.Serialize(helper));
        }
        public Task<ObservationV3Submission> Submit(CancellationToken t)
        {
            t.ThrowIfCancellationRequested(); Interlocked.Increment(ref Submits);
            using (var launcher = Process.Start(new ProcessStartInfo { FileName = helper.Path, Arguments = "role handoff-launcher", UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true }))
            {
                var identity = handles.Capture(launcher.Id); launcher.StandardOutput.ReadLine();
                using (var p = Process.Start(new ProcessStartInfo { FileName = helper.Path, Arguments = "handoff-child " + ObservationV3Arguments.Quote(root), UseShellExecute = false, CreateNoWindow = true }))
                { handles.Capture(p.Id); }
                launcher.StandardInput.WriteLine("exit"); Check(launcher.WaitForExit(10000), "launcher exit");
                return Task.FromResult(Doc(new ObservationV3Submission { ContextRef = binding, Launcher = identity, Disposition = "returned" }, "submission", "Helper"));
            }
        }
        public async Task<ObservationV3RuntimeHello> Hello(CancellationToken t)
        { await pipe.WaitAsync(t); var f = await pipe.ReceiveFrameAsync(t); Check(f.Kind == "hello", "hello required"); return ObservationV3RuntimeWire.Parse<ObservationV3RuntimeHello>(Encoding.UTF8.GetBytes(f.Body)); }
        public Task ValidateHello(ObservationV3RuntimeHello h, CancellationToken t)
        {
            child = handles.Capture(pipe.CapturePeer().Pid); handles.Match(child);
            Check(ObservationV3Wire.Same(child, h.Self) && child.Pid != helper.Pid && h.RunId == run, "external child identity");
            File.WriteAllText(Path.Combine(root, "child-identity.json"), ObservationV3Wire.Serialize(child));
            return Task.FromResult(true);
        }
        public Task<ObservationRef> Store(string name, object value, CancellationToken t)
        { t.ThrowIfCancellationRequested(); return Task.FromResult(ObservationV3RuntimeWire.Create(root, name, value)); }
        public async Task Accept(ObservationV3RuntimeHello h, CancellationToken t)
        {
            await pipe.SendAsync("fake-native-challenge", run, t);
            Check(await pipe.ReceiveAsync<string>("fake-native-ack", t) == run, "fake native ack");
            receipt = binding; await pipe.SendAsync("accepted", receipt, t);
        }
        public async Task<ObservationV3Envelope> Read(CancellationToken t)
        {
            try
            {
                var f = await pipe.ReceiveFrameAsync(t); var e = ObservationV3RuntimeWire.Parse<ObservationV3Envelope>(Encoding.UTF8.GetBytes(f.Body));
                Check(e.Kind == f.Kind && e.RunId == run && e.Role == "ReplacementObserver" && ObservationV3Wire.Same(e.Self, child) &&
                    ObservationV3RuntimeWire.SameRef(e.ContextRef, binding) && ObservationV3RuntimeWire.SameRef(e.ReceiptRef, receipt), "post envelope"); return e;
            }
            catch (EndOfStreamException)
            {
                return null;
            }

        }
        public async Task Send(string kind, object body, CancellationToken t)
        { await pipe.SendAsync(kind, body, t); }
        public void BindAdmission(ObservationV3Admission a, Action<Exception> fail) { }
        public Task ValidateExitCompleted(ObservationV3Envelope f, CancellationToken t)
        {
            var done = ObservationV3RuntimeWire.Parse<ObservationV3ExitCompleted>(Encoding.UTF8.GetBytes(f.Body));
            ObservationV3RuntimeWire.ValidateLifecycle(ObservationV3RuntimeWire.Read<ObservationV3Lifecycle>(done.LifecycleRef, root).Value, f);
            return Task.FromResult(true);
        }
        public Task ValidatePins(CancellationToken t) { t.ThrowIfCancellationRequested(); handles.Match(helper); ObservationV3RuntimeWire.FilePin(binding, root); return Task.FromResult(true); }
        public bool ChildExited() { return child != null && handles.Exited(child); }
        public void Close() { pipe.Dispose(); }
        public void TerminalFailure(Exception e) { Error = e; File.WriteAllText(Path.Combine(root, "failure.txt"), e.ToString()); }
        public void Dispose() { Close(); handles.Dispose(); }
    }
}
