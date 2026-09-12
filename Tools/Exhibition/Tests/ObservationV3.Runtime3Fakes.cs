// Harmless process lifetime checks through the production final invocation seam.
// These processes never call Steam/native or imply Steam request preservation.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Game.Exhibition.RestartExperiment;

public static class Runtime3Fakes
{
    static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (IOException error) { Require(error.Message == message, "unexpected rejection: " + error); return; }
        throw new Exception("missing rejection: " + message);
    }
    static void OriginExitChecks(string root, string exe)
    {
        string release = Path.Combine(root, "release-origin");
        using (var origin = Process.Start(new ProcessStartInfo { FileName = exe,
            Arguments = "origin " + ObservationV3Arguments.Quote(release), UseShellExecute = false }))
        {
            try
            {
                int captures = 0;
                using (var handles = new ObservationV3ProcessHandles(process => {
                    captures++; return WindowsIdentityCapture.Capture(process, true);
                }))
                {
                    var identity = handles.Capture(origin.Id);
                    int baseline = captures;
                    int[] snapshot = new[] { origin.Id };
                    handles.EnsureNoOtherGame(identity, snapshot);
                    Require(!handles.Exited(identity), "excluding origin must not imply parent exit");
                    Console.WriteLine("PASS retained live origin still requires parent exit");

                    // Pause between the parent's live observation/enumeration and the
                    // production candidate check, then let the real OS process exit.
                    File.WriteAllText(release, "normal fake exit");
                    Require(origin.WaitForExit(10000) && handles.Exited(identity), "retained origin actual exit");
                    handles.EnsureNoOtherGame(identity, snapshot);
                    handles.EnsureNoOtherGame(identity, snapshot);
                    handles.EnsureNoOtherGame(identity, new int[0]);
                    Require(captures == baseline, "exited origin must not be captured again");
                    Console.WriteLine("PASS origin exit after live observation and stale enumeration without recapture");

                    using (var self = Process.GetCurrentProcess())
                        Reject(() => handles.EnsureNoOtherGame(identity, new[] { origin.Id, self.Id }), "CompetingGameBeforeSubmission");
                    var wrong = ObservationV3Wire.Copy(identity); wrong.StartTicks++;
                    Reject(() => handles.EnsureNoOtherGame(wrong, snapshot), "MissingRetainedHandle");
                    using (var missing = new ObservationV3ProcessHandles())
                        Reject(() => missing.EnsureNoOtherGame(identity, snapshot), "MissingRetainedHandle");
                    Console.WriteLine("PASS competing PID, missing handle and mismatched origin rejected");

                    int calls = 0;
                    long now = 0;
                    var timeout = new ObservationV3Admission(() => now);
                    var expiring = timeout.Prepare("origin-exit-timeout", 30000);
                    int timedOut = ObservationV3WindowsLaunch.InvokeAndReturn(timeout, expiring, () => {
                        handles.EnsureNoOtherGame(identity, snapshot); now = 30000;
                    }, () => { calls++; return null; }, () => { });
                    Require(timedOut == 1 && calls == 0, "deadline equality after origin exclusion must prevent invocation");
                    Console.WriteLine("PASS timeout after origin exclusion prevents invocation");

                    var admission = new ObservationV3Admission(() => 0);
                    var claim = admission.Prepare("origin-exit-cancel", 30000);
                    int result = ObservationV3WindowsLaunch.InvokeAndReturn(admission, claim, () => {
                        handles.EnsureNoOtherGame(identity, snapshot);
                        admission.Close(); handles.Dispose();
                    }, () => { calls++; return null; }, () => { });
                    Require(result == 1 && calls == 0, "cancel after origin exclusion must prevent invocation");
                    bool disposed = false;
                    try { handles.EnsureNoOtherGame(identity, snapshot); }
                    catch (ObjectDisposedException) { disposed = true; }
                    Require(disposed, "disposed collection cannot authorize PID exclusion");
                    Console.WriteLine("PASS cancellation after exclusion prevents invocation; disposed handles rejected");
                }
            }
            finally
            {
                File.WriteAllText(release, "ensure owned fake can exit");
                Require(origin.WaitForExit(10000), "owned fake origin cleanup");
            }
        }
    }
    static void ShutdownChecks(string root, string exe)
    {
        string release = Path.Combine(root, "release-original-client");
        using (var original = Process.Start(new ProcessStartInfo { FileName = exe,
            Arguments = "origin " + ObservationV3Arguments.Quote(release), UseShellExecute = false }))
        using (var handles = new ObservationV3ProcessHandles())
        {
            try
            {
                var identity = handles.Capture(original.Id);
                ProcessIdentity owner;
                using (var self = Process.GetCurrentProcess()) owner = WindowsIdentityCapture.Capture(self, true);
                ProcessIdentity command = null;
                foreach (int exitCode in new[] { 0, 7 })
                {
                    var admission = new ObservationV3Admission(() => 0);
                    var claim = admission.Prepare("steam-shutdown", 30000);
                    var start = new ProcessStartInfo { FileName = exe, Arguments = "shutdown " + exitCode, UseShellExecute = false };
                    int calls = 0;
                    command = handles.StartShutdown(admission, claim, start, identity, owner, () => { }, info => {
                        calls++;
                        var process = Process.Start(info);
                        // Force the command to exit before the production adoption code.
                        Require(process.WaitForExit(10000), "fast shutdown fake exit");
                        return process;
                    });
                    Require(calls == 1 && handles.Exited(command), "one command with retained actual exit");
                    if (exitCode == 0) Require(!handles.ShutdownCommandAlive(command), "normal fast command exit accepted");
                    else Reject(() => handles.ShutdownCommandAlive(command), "ShutdownCommandAbnormalExit:7");
                    Console.WriteLine("PASS production shutdown creation handles exit-before-return with code " + exitCode);
                }
                Require(handles.OriginalSteamAlive(identity, command, () => new[] { identity.Pid, command.Pid }), "live original still alive");
                bool alive = handles.OriginalSteamAlive(identity, command, () => {
                    Require(!handles.Exited(identity), "original live before enumeration pause");
                    File.WriteAllText(release, "normal client exit during enumeration");
                    Require(original.WaitForExit(10000), "original OS exit during enumeration");
                    return new[] { identity.Pid, command.Pid };
                });
                Require(!alive, "normal original exit must not become client mismatch");
                Require(!handles.OriginalSteamAlive(identity, command, () => new int[0]), "already exited original and empty scan");
                Reject(() => handles.OriginalSteamAlive(identity, command, () => new[] { identity.Pid, owner.Pid }), "OriginalClientChanged");
                bool denied = false;
                try { handles.OriginalSteamAlive(identity, command, () => { throw new System.ComponentModel.Win32Exception(5); }); }
                catch (System.ComponentModel.Win32Exception error) { denied = error.NativeErrorCode == 5; }
                Require(denied, "scan access failure cannot be swallowed as normal exit");
                Console.WriteLine("PASS original exit during enumeration; competing client and access error remain rejected");

                int deniedCalls = 0;
                foreach (bool cancel in new[] { true, false })
                {
                    long now = 0;
                    var admission = new ObservationV3Admission(() => now);
                    var claim = admission.Prepare("steam-shutdown", 30000);
                    bool failed = false;
                    try { handles.StartShutdown(admission, claim,
                        new ProcessStartInfo { FileName = exe, UseShellExecute = false }, identity, owner,
                        () => { if (cancel) admission.Close(); else now = 30000; }, info => { deniedCalls++; return null; }); }
                    catch (Exception) { failed = true; }
                    Require(failed && deniedCalls == 0, "cancel/deadline must prevent shutdown creation");
                }
                Console.WriteLine("PASS production shutdown cancellation and deadline equality prevent creation");
            }
            finally
            {
                File.WriteAllText(release, "ensure owned fake client exit");
                Require(original.WaitForExit(10000), "owned fake client cleanup");
            }
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args[0] == "shutdown") return int.Parse(args[1]);
            if (args[0] == "origin")
            {
                var limit = Stopwatch.StartNew();
                while (!File.Exists(args[1]) && limit.ElapsedMilliseconds < 30000) Thread.Sleep(10);
                return 0;
            }
            if (args[0] == "command")
            {
                var window = ObservationV3RuntimeWire.ReadInitial<ObservationV3IndependentContext>(Path.Combine(args[1], "context.json"), args[1]).Value;
                ObservationV3LaunchWindow.RequireOpen(window, ObservationV3LaunchWindow.Now, ObservationV3LaunchWindow.Frequency);
                File.WriteAllText(Path.Combine(args[1], "command.tmp"), Process.GetCurrentProcess().Id.ToString());
                File.Move(Path.Combine(args[1], "command.tmp"), Path.Combine(args[1], "command.txt"));
                var limit = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(args[1], "release-command")) && limit.ElapsedMilliseconds < 30000) Thread.Sleep(10);
                File.WriteAllText(Path.Combine(args[1], "independent-start.txt"), "fake only; no helper connection");
                return 0;
            }
            if (args[0] == "helper")
            {
                var now = ObservationV3LaunchWindow.Now;
                var reference = new ObservationRef { Path = "unused-fake", Sha256 = new string('a', 64) };
                ObservationV3RuntimeWire.Create(args[1], "context.json", ObservationV3RuntimeWire.Stamp(new ObservationV3IndependentContext {
                    RequestRef = reference, ClientRef = reference, LaunchPlanRef = reference, Nonce = Guid.NewGuid().ToString("N"),
                    Role = "ReplacementObserver", Owner = "SteamDelegated", PreparedTimestamp = now,
                    DeadlineTimestamp = now + 30000, TimestampFrequency = ObservationV3LaunchWindow.Frequency
                }, "independent-context", "Helper", 1, Guid.NewGuid().ToString("N")));
                var gate = new ObservationV3Admission(() => 0);
                var claim = gate.Prepare("launch-submission", 30000);
                return ObservationV3WindowsLaunch.InvokeAndReturn(gate, claim, () => { }, () =>
                    Process.Start(new ProcessStartInfo { FileName = Process.GetCurrentProcess().MainModule.FileName,
                        Arguments = "command " + ObservationV3Arguments.Quote(args[1]), UseShellExecute = false }), () => { });
            }
            string root = args[0]; Directory.CreateDirectory(root);
            int calls = 0; var cancelled = new ObservationV3Admission(() => 0);
            var denied = cancelled.Prepare("cancel", 30000);
            Require(ObservationV3WindowsLaunch.InvokeAndReturn(cancelled, denied, () => cancelled.Close(),
                () => { calls++; return null; }, () => { }) == 1 && calls == 0, "cancelled final validation must not invoke");
            var uncertain = new ObservationV3Admission(() => 0); var committed = uncertain.Prepare("committed", 30000);
            ObservationV3WindowsLaunch.InvokeAndReturn(uncertain, committed, () => { },
                () => { calls++; uncertain.Close(); throw new IOException("ambiguous call"); }, () => { });
            Require(calls == 1 && committed.InvocationCommitted && !committed.Returned, "uncertain invocation fact");
            ObservationV3WindowsLaunch.InvokeAndReturn(uncertain, committed, () => { }, () => { calls++; return null; }, () => { });
            Require(calls == 1, "no retry of consumed claim");
            var context = new ObservationV3IndependentContext { PreparedTimestamp = 100, DeadlineTimestamp = 30100, TimestampFrequency = 1000 };
            ObservationV3LaunchWindow.RequireOpen(context, 30099, 1000);
            bool expired = false;
            try { ObservationV3LaunchWindow.RequireOpen(context, 30100, 1000); } catch (IOException) { expired = true; }
            Require(expired, "deadline equality");
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            OriginExitChecks(root, exe);
            ShutdownChecks(root, exe);
            using (var helper = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "helper " + ObservationV3Arguments.Quote(root), UseShellExecute = false }))
            {
                Require(helper.WaitForExit(10000) && helper.ExitCode == 0, "helper must exit without command/child acknowledgement");
                var clock = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(root, "command.txt")) && clock.ElapsedMilliseconds < 10000) Thread.Sleep(10);
                using (var command = Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root, "command.txt")))))
                {
                    Require(!command.HasExited && !File.Exists(Path.Combine(root, "independent-start.txt")), "command outlives helper");
                    File.WriteAllText(Path.Combine(root, "release-command"), "test release; not a resubmission");
                    Require(command.WaitForExit(10000), "independent fake completion");
                }
                Require(File.Exists(Path.Combine(root, "independent-start.txt")), "independent fake child after helper exit");
                File.WriteAllText(Path.Combine(root, "result.txt"), "PASS production invocation admission, one attempt, helper actual exit before command return, independent fake completion");
            }
            Console.WriteLine("PASS runtime-3 harmless final executor and actual process lifetimes"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
