// Harmless process-backed IPC test executable. All Steam identities/native acknowledgements below are fakes.
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;

public sealed class ObservationPipeBundle
{
    public ObservationV3Request Request;
    public ObservationV3Client Client;
    public ObservationV3Context Context;
    public string Mode;
}
public static class ObservationV3PipeFakes
{
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static ProcessIdentity Capture(int pid) { using (var p = Process.GetProcessById(pid)) return WindowsIdentityCapture.Capture(p, true); }
    private static ProcessIdentity Fake(ProcessIdentity source, int pid, long ticks)
    { var p = ObservationV3Wire.Copy(source); p.Pid = pid; p.StartTicks = ticks; return p; }
    public static int Main(string[] args)
    {
        try { Run(args).GetAwaiter().GetResult(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static async Task Run(string[] args)
    {
        if (args.Length == 2 && args[0] == "child")
        {
            var b = ExperimentFiles.Read<ObservationPipeBundle>(args[1]);
            using (var deadline = new CancellationTokenSource(8000))
            using (var pipe = await ObservationV3Pipe.ConnectAsync(b.Context.Endpoint, 5000, deadline.Token))
            {
                if (b.Mode == "disconnect") return;
                if (b.Mode == "wrong-frame") { await pipe.SendAsync("wrong", "payload", deadline.Token); return; }
                if (b.Mode == "oversize")
                {
                    bool rejected = false;
                    try { await pipe.SendAsync("hello", new string('x', 70000), deadline.Token); } catch (IOException) { rejected = true; }
                    Check(rejected, "Oversized frame allowed"); return;
                }
                var self = Capture(Process.GetCurrentProcess().Id);
                bool nativeEntered = false, interrupted = false;
                bool blockedAck = b.Mode == "ack-cancel" || b.Mode == "ack-disconnect";
                bool failedAck = b.Mode == "ack-throw" || b.Mode == "ack-invalid";
                try {
                await ObservationV3Child.HandshakeAsync(pipe, b.Request, b.Client, b.Context, self, b.Request.ManifestHash, Capture,
                    receipt => {
                        nativeEntered = true;
                        if (b.Mode == "ack-throw") throw new IOException("FakeNativeFailure");
                        if (b.Mode == "ack-invalid") return Task.FromResult<ObservationV3Ack>(null);
                        if (blockedAck) {
                            File.WriteAllText(args[1] + ".native-entered", "fake");
                            if (b.Mode == "ack-cancel") deadline.CancelAfter(100);
                            return new TaskCompletionSource<ObservationV3Ack>().Task;
                        }
                        return Task.FromResult(new ObservationV3Ack { ReceiptHash = ObservationV3Wire.Digest(receipt), Challenge = receipt.Challenge,
                        Child = self, Helper = b.Client.Helper, NewClient = b.Client.Current, AppId = b.Request.AppId, SteamId = b.Request.SteamId,
                        NativeReady = true, LoggedOn = true }); }, () => { }, deadline.Token);
                } catch (Exception e) {
                    if ((!blockedAck && !failedAck) || !nativeEntered) throw;
                    interrupted = b.Mode == "ack-cancel" ? deadline.IsCancellationRequested : e is IOException && !deadline.IsCancellationRequested;
                    if (failedAck) {
                        bool disposed = false;
                        try { pipe.CapturePeer(); } catch (ObjectDisposedException) { disposed = true; }
                        Check(disposed, "Native failure left pipe and acceptance read open");
                    }
                }
                Check((!blockedAck && !failedAck) || interrupted, "Blocked native preparation did not stop on cancellation/EOF");
            }
            return;
        }
        string directory = args[0];
        var helper = Capture(Process.GetCurrentProcess().Id);
        foreach (string mode in new[] { "handshake", "disconnect", "wrong-frame", "oversize", "ack-cancel", "ack-disconnect", "ack-throw", "ack-invalid" })
        {
            string hash = new string('a', 64), nonce = Guid.NewGuid().ToString("N");
            var r = new ObservationV3Request { RunId = Guid.NewGuid().ToString("N"), Nonce = nonce, Directory = directory,
                Owner = ObservationLaunchOwner.SteamDelegated, OperationId = Guid.NewGuid().ToString("N"), MappingVersion = "fake", ReadyState = "Ready",
                AppId = 5218360, SteamId = 123, Targets = new[] { "VQ_LEVEL_0_CLEAR" }, ConfigurationHash = hash, ManifestHash = hash, ParticipantHash = hash, OriginVisibilityHash = hash,
                OriginDisplayHash = hash, BaselineHash = hash, Origin = Fake(helper, 999991, 1), OriginalSteam = Fake(helper, 999992, 1) };
            var c = new ObservationV3Client { Creation = new ObservationV3HelperCreation { RequestHash = ObservationV3Wire.Digest(r), Origin = r.Origin, Helper = helper }, RequestHash = ObservationV3Wire.Digest(r), Original = r.OriginalSteam, Current = helper,
                Probe = Fake(helper, 999993, 2), OriginalExited = true, ShutdownCommandExited = true, ProbeShutdownReturned = true, ProbeExited = true,
                AppId = r.AppId, SteamId = r.SteamId, LoggedOn = true };
            var x = new ObservationV3Context { RequestHash = ObservationV3Wire.Digest(r), ClientHash = ObservationV3Wire.Digest(c),
                RunId = r.RunId, Nonce = nonce, Endpoint = "j2m-observation-v3-" + nonce, Owner = r.Owner };
            string bundle = Path.Combine(directory, mode + ".json");
            File.WriteAllText(bundle, ExperimentFiles.Json(new ObservationPipeBundle { Request = r, Client = c, Context = x, Mode = mode }));
            using (var deadline = new CancellationTokenSource(10000))
            using (var pipe = ObservationV3Pipe.Listen(x.Endpoint, helper.UserSid))
            using (var child = Process.Start(new ProcessStartInfo { FileName = helper.Path, Arguments = "child " + ExperimentFiles.Quote(bundle), UseShellExecute = false, CreateNoWindow = true }))
            {
                try
                {
                    await pipe.WaitAsync(deadline.Token);
                    if (mode == "disconnect" || mode == "wrong-frame" || mode == "oversize")
                    {
                        bool closed = false;
                        try { await pipe.ReceiveAsync<ObservationV3Hello>("hello", deadline.Token); } catch (IOException) { closed = true; }
                        Check(closed, "Bad frame or disconnected child accepted");
                    }
                    else
                    {
                        var hello = await pipe.ReceiveAsync<ObservationV3Hello>("hello", deadline.Token);
                        var peer = pipe.CapturePeer(); Check(peer.Pid == child.Id && ObservationV3Wire.Same(peer, hello.Child), "Peer PID/self report mismatch");
                        string challenge = Guid.NewGuid().ToString("N"); Check(await pipe.ChallengeAsync(challenge, deadline.Token) == challenge, "Challenge mismatch");
                        var receipt = new ObservationV3Receipt { RunId = r.RunId, Nonce = nonce, RequestHash = ObservationV3Wire.Digest(r),
                            ContextHash = ObservationV3Wire.Digest(x), ClientHash = ObservationV3Wire.Digest(c), EffectiveArgumentsHash = hash,
                            Challenge = challenge, Origin = r.Origin, Helper = helper, NewClient = c.Current, Child = peer };
                        if (mode == "handshake") {
                            var ack = await pipe.ReceiptAsync(receipt, deadline.Token);
                            Check(ack.ReceiptHash == ObservationV3Wire.Digest(receipt) && ObservationV3Wire.Same(ack.Child, peer), "Ack mismatch");
                            await pipe.ConfirmAsync(ack.ReceiptHash, deadline.Token);
                        } else {
                            await pipe.SendAsync("receipt", receipt, deadline.Token);
                            if (mode == "ack-throw" || mode == "ack-invalid") {
                                bool closed = false;
                                try { await pipe.ReceiveAsync<ObservationV3Ack>("ack", deadline.Token); } catch (IOException) { closed = true; }
                                Check(closed, "Native failure left helper waiting for ack");
                            } else {
                            var wait = Stopwatch.StartNew();
                            while (!File.Exists(bundle + ".native-entered") && wait.ElapsedMilliseconds < 3000) await Task.Delay(10);
                            Check(File.Exists(bundle + ".native-entered"), "Fake native preparation never entered");
                            if (mode == "ack-disconnect") pipe.Dispose();
                            }
                        }
                    }
                    Check(child.WaitForExit(5000) && child.ExitCode == 0, "Fake child did not exit normally");
                }
                finally { if (!child.HasExited) { child.Kill(); child.WaitForExit(); } } // test-owned fake only
            }
            Console.WriteLine("PASS Windows pipe " + mode);
        }
        using (var cancellation = new CancellationTokenSource(100))
        using (var pipe = ObservationV3Pipe.Listen("j2m-observation-v3-" + Guid.NewGuid().ToString("N"), helper.UserSid))
        {
            bool closed = false;
            try { await pipe.WaitAsync(cancellation.Token); } catch (Exception) { closed = cancellation.IsCancellationRequested; }
            Check(closed, "Cancelled wait remained open"); Console.WriteLine("PASS Windows pipe bounded accept cancellation");
        }
        using (var cancellation = new CancellationTokenSource(100))
        {
            bool rejected = false;
            try { using (await ObservationV3Pipe.ConnectAsync("j2m-observation-v3-" + Guid.NewGuid().ToString("N"), 200, cancellation.Token)) { } }
            catch (Exception) { rejected = true; }
            Check(rejected, "Missing/late endpoint accepted"); Console.WriteLine("PASS Windows pipe missing endpoint rejected");
        }
        string duplicate = "j2m-observation-v3-" + Guid.NewGuid().ToString("N");
        using (var first = ObservationV3Pipe.Listen(duplicate, helper.UserSid))
        {
            bool rejected = false;
            try { using (ObservationV3Pipe.Listen(duplicate, helper.UserSid)) { } }
            catch (System.ComponentModel.Win32Exception) { rejected = true; }
            Check(rejected, "Duplicate server claimed endpoint"); Console.WriteLine("PASS Windows pipe duplicate server denied");
        }
        bool badSid = false;
        try { using (ObservationV3Pipe.Listen("j2m-observation-v3-" + Guid.NewGuid().ToString("N"), "S-1-5-18)(A;;GA;;;WD")) { } }
        catch (IOException) { badSid = true; }
        Check(badSid, "Invalid user ACL accepted"); Console.WriteLine("PASS Windows pipe invalid ACL identity rejected");
        Console.WriteLine("Steam/game launches=0; Steam native calls=0; fake peer process launches=8");
    }
}
