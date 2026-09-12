using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;

namespace RestartExperimentFakes
{
    public static class ResetWireRoundTrip
    {
        private static void Reject(Action action)
        { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } if (!rejected) throw new Exception("Invalid reset wire accepted."); }
        public static void Verify(string root)
        {
            Directory.CreateDirectory(root);
            var exe = Path.Combine(root, "fake.exe"); File.WriteAllText(exe, "not executable");
            var origin = new ProcessIdentity { Pid = 100, StartTicks = 100, Session = 1, UserSid = "fake", Logon = "fake", Path = exe, Sha256 = ExperimentFiles.Hash(exe) };
            var steam = new ProcessIdentity { Pid = 101, StartTicks = 101, Session = 1, UserSid = "fake", Logon = "fake", Path = exe, Sha256 = origin.Sha256 };
            var c = new ResetOverlayTrialContext { Version = 2, TrialId = Guid.NewGuid().ToString("N"), HandoffNonce = Guid.NewGuid().ToString("N"),
                ReadyOperationId = Guid.NewGuid().ToString("N"), OperationId = Guid.NewGuid().ToString("N"), MappingVersion = "level-clear-v1", Directory = root,
                AppId = 5218360, SteamId = 1, Origin = origin, Steam = steam, ScopeConfirmedUtc = DateTime.UtcNow.ToString("o"), ManifestSha256 = new string('a', 64) };
            c.ConfigurationPath = Path.Combine(root, "config.json"); File.WriteAllText(c.ConfigurationPath, "{}"); c.ConfigurationSha256 = ExperimentFiles.Hash(c.ConfigurationPath);
            var b = new ResetOverlaySdkBaseline { Version = 2, TrialId = c.TrialId, ReadyOperationId = c.ReadyOperationId, MappingVersion = c.MappingVersion,
                AppId = c.AppId, SteamId = c.SteamId, Process = origin, Role = ResetOverlayRole.Initiator, Source = "SDK", Utc = c.ScopeConfirmedUtc,
                Names = ResetOverlayWire.Names(), Achieved = new[] { true, false, false, false, false }, QuerySucceeded = new[] { true, true, true, true, true } };
            c.BaselinePath = Path.Combine(root, "baseline.json"); File.WriteAllText(c.BaselinePath, ExperimentFiles.Json(b)); c.BaselineSha256 = ExperimentFiles.Hash(c.BaselinePath);
            var report = new ResetOverlayUserReport { Version = 2, TrialId = c.TrialId, Role = ResetOverlayRole.Initiator, Source = "User", Process = origin,
                Utc = c.ScopeConfirmedUtc, BaselineSha256 = c.BaselineSha256, AttemptReported = true, Visibility = "opened", Names = new[] { b.Names[0] }, Judgments = new[] { "earned" } };
            c.OriginReportPath = Path.Combine(root, "report.json"); File.WriteAllText(c.OriginReportPath, ExperimentFiles.Json(report)); c.OriginReportSha256 = ExperimentFiles.Hash(c.OriginReportPath);
            c.InitialFilesPath = Path.Combine(root, "initial.json"); c.PendingFilesPath = Path.Combine(root, "pending.json");
            File.WriteAllText(c.InitialFilesPath, "{}"); File.WriteAllText(c.PendingFilesPath, "{}");
            c.InitialFilesSha256 = ExperimentFiles.Hash(c.InitialFilesPath); c.PendingFilesSha256 = ExperimentFiles.Hash(c.PendingFilesPath);
            var contextPath = Path.Combine(root, "context.json"); File.WriteAllText(contextPath, ExperimentFiles.Json(c));
            var r = new ExperimentRequest { Nonce = c.HandoffNonce, Trial = Trial.GameOnly, AppId = c.AppId, SteamId = c.SteamId, OperationId = c.OperationId,
                Parent = origin, Steam = steam, ResetOverlayWireVersion = 2, ResetOverlayTrialId = c.TrialId, ResetOverlayChildRole = ResetOverlayRole.ResetWorker,
                ResetOverlayContextPath = contextPath, ResetOverlayContextSha256 = ExperimentFiles.Hash(contextPath),
                ToolsDirectory = Path.Combine(root, "RestartExperiment"), EvidenceDirectory = Path.Combine(root, "handoff-GameOnly") };
            Directory.CreateDirectory(r.EvidenceDirectory); var requestPath = Path.Combine(r.EvidenceDirectory, "request.json"); File.WriteAllText(requestPath, ExperimentFiles.Json(r));
            ResetOverlayWire.ReadContext(r);
            ResetOverlayWire.ClaimChildCreation(r); Reject(() => ResetOverlayWire.ClaimChildCreation(r));
            var child = ExperimentFiles.Parse<ProcessIdentity>(ExperimentFiles.Json(origin)); child.Pid = 102; child.StartTicks = 102;
            ResetOverlayReceipt.Write(r, child, requestPath);
            var receipt = ExperimentFiles.Read<ResetOverlayChildReceipt>(ResetOverlayReceipt.PathFor(r));
            if (!ResetOverlayReceipt.Matches(receipt, r, child, requestPath)) throw new Exception("Reset receipt round trip failed.");
            Reject(() => ResetOverlayReceipt.Write(r, child, requestPath));
            foreach (var field in new[] { "version", "partial", "mixed", "nonce", "fullcycle", "final", "tools", "evidence" })
            {
                var changed = ExperimentFiles.Parse<ExperimentRequest>(ExperimentFiles.Json(r));
                if (field == "version") changed.ResetOverlayWireVersion = 1;
                if (field == "partial") changed.ResetOverlayContextSha256 = null;
                if (field == "mixed") changed.OverlayObservationWireVersion = 2;
                if (field == "nonce") changed.Nonce = Guid.NewGuid().ToString("N");
                if (field == "fullcycle") changed.Trial = Trial.FullCycle;
                if (field == "final") changed.ResetOverlayChildRole = ResetOverlayRole.FinalObserver;
                if (field == "tools") changed.ToolsDirectory = root;
                if (field == "evidence") changed.EvidenceDirectory = root;
                Reject(() => ResetOverlayWire.ReadContext(changed));
            }
            File.AppendAllText(requestPath, " ");
            if (ResetOverlayReceipt.Matches(receipt, r, child, requestPath)) throw new Exception("Changed request bytes accepted.");
            LaunchEnvironment.ValidateChildRole(new ExperimentRequest { Trial = Trial.FullCycle });
        }
    }
    public static class ObservationReceiptRoundTrip
    {
        public static void Verify(string fixture, string directory)
        {
            Directory.CreateDirectory(directory);
            var start = new ProcessStartInfo(fixture) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
            using (var process = Process.Start(start))
            {
                try
                {
                    ProcessIdentity origin;
                    using (var current = Process.GetCurrentProcess()) origin = WindowsIdentityCapture.Capture(current, true);
                    var context = new OverlayObservationContext { Version = 2, RunId = Guid.NewGuid().ToString("N"),
                        Role = OverlayObservationRole.ReplacementObserver, OperationId = Guid.NewGuid().ToString("N"),
                        ReadyState = "Ready", MappingVersion = "fake", Directory = directory, ConfigurationPath = Path.Combine(directory, "config.json"),
                        ConfigurationSha256 = new string('a', 64), ManifestSha256 = new string('b', 64), SaveManifestSha256 = new string('c', 64),
                        AppId = 5218360, SteamId = 1, Origin = origin };
                    context.OriginObservationPath = Path.Combine(directory, "user-observation.json");
                    File.WriteAllText(context.OriginObservationPath, ExperimentFiles.Json(new OverlayUserObservation {
                        RunId = context.RunId, Role = OverlayObservationRole.OriginObserver, Process = origin,
                        Source = "User", AttemptReported = true, Visibility = "opened", Utc = DateTime.UtcNow.ToString("o"), MonotonicSeconds = 3600 }));
                    context.OriginObservationSha256 = ExperimentFiles.Hash(context.OriginObservationPath);
                    var contextPath = Path.Combine(directory, "context.json"); File.WriteAllText(contextPath, ExperimentFiles.Json(context));
                    var request = new ExperimentRequest { Trial = Trial.GameOnly, Nonce = Guid.NewGuid().ToString("N"), AppId = context.AppId,
                        SteamId = context.SteamId, OperationId = context.OperationId, Parent = origin, EvidenceDirectory = directory,
                        OverlayObservationWireVersion = 2, OverlayObservationRunId = context.RunId,
                        OverlayObservationChildRole = context.Role, OverlayObservationContextPath = contextPath,
                        OverlayObservationContextSha256 = ExperimentFiles.Hash(contextPath) };
                    // Same production producer used immediately after shared StartGame's Process.Start.
                    var produced = OverlayObservationWire.CaptureChildAndWriteReceipt(request, process);
                    var row = ExperimentFiles.Read<OverlayObservationReceipt>(OverlayObservationWire.ReceiptPath(request));
                    var consumer = WindowsIdentityCapture.Capture(process, true);
                    if (produced.Sha256 == null || !OverlayObservationWire.Matches(row, request, context, consumer))
                        throw new IOException("Real observation receipt round trip failed.");
                    row.Child.Sha256 = null;
                    if (OverlayObservationWire.Matches(row, request, context, consumer)) throw new IOException("Unhashed receipt accepted.");
                    process.StandardInput.WriteLine("exit");
                    if (!process.WaitForExit(5000) || process.ExitCode != 0) throw new IOException("Harmless receipt fixture did not exit.");
                }
                finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); } }
            }
        }
    }
    public sealed class FailureOperations : ProbeOperations
    {
        public string Mode;
        public int OutputWaits, TerminationWaits;
        private Process retained;
        private readonly TaskCompletionSource<bool> eofRelease = new TaskCompletionSource<bool>();
        public override BoundedOutput ReadOutput(TextReader reader)
        {
            if (Mode == "reader-failure") return new BoundedOutput(new BrokenReader());
            if (Mode == "held-pipes") return new BoundedOutput(new HeldEndReader(reader, eofRelease.Task));
            return base.ReadOutput(reader);
        }
        public override Process Start(ProcessStartInfo start)
        {
            if (Mode == "create") throw new IOException("creation uncertain");
            if (Mode == "create-null") return null;
            var process = base.Start(start);
            // Independent handle only to verify cleanup of this harmless test child.
            retained = Process.GetProcessById(process.Id);
            IntPtr handle = retained.Handle;
            return process;
        }
        public override void Kill(Process process)
        {
            if (Mode == "kill") throw new IOException("kill failure");
            base.Kill(process);
        }
        public override bool WaitForExit(Process process, int milliseconds)
        {
            if (milliseconds > 50) TerminationWaits++;
            return base.WaitForExit(process, milliseconds);
        }
        public override bool WaitOutput(Task[] readers, int milliseconds)
        {
            OutputWaits++;
            if (milliseconds > 1000) throw new IOException("output budget exceeded");
            if (Mode == "output-throw") throw new IOException("collector wait failure");
            if (Mode == "output-incomplete") return false;
            return base.WaitOutput(readers, milliseconds);
        }
        public override void CloseJob(IDisposable job)
        {
            base.CloseJob(job);
            if (Mode == "job") throw new IOException("job close failure");
        }
        public void DisposeRetainedProcess()
        {
            eofRelease.TrySetResult(true);
            if (retained == null) return;
            try { if (!retained.WaitForExit(2000)) throw new IOException("Harmless child survived cleanup"); }
            finally { retained.Dispose(); retained = null; }
        }
    }
    public sealed class HeldEndReader : TextReader
    {
        private readonly TextReader reader;
        private readonly Task end;
        public HeldEndReader(TextReader reader, Task end) { this.reader = reader; this.end = end; }
        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            int read = await reader.ReadAsync(buffer, index, count).ConfigureAwait(false);
            if (read == 0) await end.ConfigureAwait(false);
            return read;
        }
    }
    public sealed class BrokenReader : StringReader
    {
        public BrokenReader() : base("") { }
        public override Task<int> ReadAsync(char[] buffer, int index, int count) { throw new IOException("reader failure"); }
    }
}
