using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Game.Exhibition.RestartExperiment;

namespace RestartExperimentFakes
{
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
