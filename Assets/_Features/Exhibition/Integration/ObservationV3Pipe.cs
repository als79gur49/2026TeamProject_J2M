// Windows local IPC only. Does not start Steam, a game, or a native Steam session.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ObservationV3Frame { public string Kind, Body; public string ProtocolRevision; }
    public sealed class ObservationV3Pipe : IDisposable, IObservationV3Peer
    {
        private readonly PipeStream stream;
        private readonly bool server;
        private int disposed;
        private readonly SemaphoreSlim writer = new SemaphoreSlim(1, 1);
        private int reading;
        public bool Runtime2 { get; set; }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl, uint revision, out IntPtr descriptor, IntPtr size);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafePipeHandle CreateNamedPipe(string name, uint openMode, uint pipeMode, uint instances,
            uint outputBuffer, uint inputBuffer, uint timeout, ref SecurityAttributes attributes);
        private ObservationV3Pipe(PipeStream stream, bool server) { this.stream = stream; this.server = server; }
        public static ObservationV3Pipe Listen(string endpoint, string userSid)
        {
            ObservationV3Wire.Endpoint(endpoint);
            ObservationV3Wire.Require(userSid != null && userSid.StartsWith("S-1-", StringComparison.Ordinal) &&
                userSid.Substring(2).All(c => c == '-' || (c >= '0' && c <= '9')), "InvalidPipeUserSid");
            IntPtr descriptor;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(D;;GA;;;NU)(A;;GA;;;" + userSid + ")", 1, out descriptor, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var attributes = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = descriptor };
                // DUPLEX | OVERLAPPED | FIRST_PIPE_INSTANCE; byte mode | REJECT_REMOTE_CLIENTS.
                var handle = CreateNamedPipe(@"\\.\pipe\" + endpoint, 0x40080003, 8, 1, 65536, 65536, 0, ref attributes);
                if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
                try { return new ObservationV3Pipe(new NamedPipeServerStream(PipeDirection.InOut, true, false, handle), true); }
                catch { handle.Dispose(); throw; }
            }
            finally { LocalFree(descriptor); }
        }
        public static async Task<ObservationV3Pipe> ConnectAsync(string endpoint, int timeoutMs, CancellationToken cancellation)
        {
            ObservationV3Wire.Endpoint(endpoint);
            if (timeoutMs <= 0 || timeoutMs > 30000) throw new ArgumentOutOfRangeException("timeoutMs");
            var pipe = new ObservationV3Pipe(new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous), false);
            try
            {
                using (cancellation.Register(pipe.Dispose))
                    await Task.Run(() => ((NamedPipeClientStream)pipe.stream).Connect(timeoutMs), cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested(); return pipe;
            }
            catch { pipe.Dispose(); throw; }
        }
        public async Task WaitAsync(CancellationToken cancellation)
        {
            if (!server) throw new InvalidOperationException("ServerRequired");
            using (cancellation.Register(Dispose))
                await Task.Factory.FromAsync(((NamedPipeServerStream)stream).BeginWaitForConnection,
                    ((NamedPipeServerStream)stream).EndWaitForConnection, null).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
        }
        public ProcessIdentity CapturePeer()
        {
            uint pid;
            bool ok = server ? GetNamedPipeClientProcessId(stream.SafePipeHandle, out pid) : GetNamedPipeServerProcessId(stream.SafePipeHandle, out pid);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            using (var p = Process.GetProcessById(checked((int)pid))) return WindowsIdentityCapture.Capture(p, true);
        }
        public async Task SendAsync(string kind, object value, CancellationToken cancellation)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ExperimentFiles.Json(new ObservationV3Frame { Kind = kind, Body = ObservationV3Wire.Serialize(value), ProtocolRevision = Runtime2 ? "observation-v3-runtime-2" : null }));
            ObservationV3Wire.Require(bytes.Length > 0 && bytes.Length <= 65536, "PipeFrameTooLarge");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                timeout.CancelAfter(30000);
                await writer.WaitAsync(timeout.Token).ConfigureAwait(false);
                try
                {
                    using (timeout.Token.Register(Dispose))
                    {
                        await stream.WriteAsync(BitConverter.GetBytes(bytes.Length), 0, 4, timeout.Token).ConfigureAwait(false);
                        await stream.WriteAsync(bytes, 0, bytes.Length, timeout.Token).ConfigureAwait(false);
                        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);
                    }
                }
                finally { writer.Release(); }
            }
        }
        private async Task ReadExact(byte[] bytes, CancellationToken cancellation)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes, offset, bytes.Length - offset, cancellation).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("ObservationLeaseClosed"); offset += read;
            }
        }
        public async Task<ObservationV3Frame> ReceiveFrameAsync(CancellationToken cancellation)
        {
            if (Interlocked.CompareExchange(ref reading, 1, 0) != 0) throw new IOException("ConcurrentPipeReader");
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                using (timeout.Token.Register(Dispose))
                {
                    byte[] length = new byte[4];
                    // No observation idle expiry. The partial-frame deadline begins on the first received byte.
                    int first = await stream.ReadAsync(length, 0, 1, timeout.Token).ConfigureAwait(false);
                    if (first == 0) throw new EndOfStreamException("ObservationLeaseClosed");
                    timeout.CancelAfter(30000);
                    byte[] tail = new byte[3]; await ReadExact(tail, timeout.Token).ConfigureAwait(false);
                    Array.Copy(tail, 0, length, 1, 3);
                    int count = BitConverter.ToInt32(length, 0);
                    ObservationV3Wire.Require(count > 0 && count <= 65536, "InvalidPipeFrameLength");
                    byte[] bytes = new byte[count]; await ReadExact(bytes, timeout.Token).ConfigureAwait(false);
                    var frame = Runtime2 ? ParseRuntimeFrame(bytes) : ObservationV3Wire.Parse<ObservationV3Frame>(new UTF8Encoding(false, true).GetString(bytes));
                    ObservationV3Wire.Require(frame != null && frame.Body != null &&
                        (!Runtime2 || frame.ProtocolRevision == "observation-v3-runtime-2"), "PipeRevisionMismatch");
                    return frame;
                }
            }
            finally { Volatile.Write(ref reading, 0); }
        }
        public static ObservationV3Frame ParseRuntimeFrame(byte[] bytes)
        {
            var xml = new System.Xml.XmlDocument();
            using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(bytes,
                new System.Xml.XmlDictionaryReaderQuotas { MaxDepth = 8, MaxStringContentLength = 65536, MaxArrayLength = 65536 })) xml.Load(reader);
            var root = xml.DocumentElement; var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            ObservationV3Wire.Require(root != null && root.GetAttribute("type") == "object", "InvalidPipeFrame");
            foreach (System.Xml.XmlElement item in root.ChildNodes)
                ObservationV3Wire.Require((item.Name == "Kind" || item.Name == "Body" || item.Name == "ProtocolRevision") &&
                    seen.Add(item.Name) && item.GetAttribute("type") == "string", "InvalidPipeFrameField");
            ObservationV3Wire.Require(seen.Count == 3, "MissingPipeFrameField");
            var frame = ObservationV3Wire.Parse<ObservationV3Frame>(new UTF8Encoding(false, true).GetString(bytes));
            ObservationV3Wire.Require(frame.ProtocolRevision == "observation-v3-runtime-2", "PipeRevisionMismatch"); return frame;
        }
        public async Task<T> ReceiveAsync<T>(string kind, CancellationToken cancellation)
        {
            var frame = await ReceiveFrameAsync(cancellation).ConfigureAwait(false);
            ObservationV3Wire.Require(frame.Kind == kind, "UnexpectedPipeFrame");
            return ObservationV3Wire.Parse<T>(frame.Body);
        }
        public async Task<string> ChallengeAsync(string challenge, CancellationToken cancellation)
        { await SendAsync("challenge", challenge, cancellation).ConfigureAwait(false); return await ReceiveAsync<string>("response", cancellation).ConfigureAwait(false); }
        public async Task<ObservationV3Ack> ReceiptAsync(ObservationV3Receipt receipt, CancellationToken cancellation)
        { await SendAsync("receipt", receipt, cancellation).ConfigureAwait(false); return await ReceiveAsync<ObservationV3Ack>("ack", cancellation).ConfigureAwait(false); }
        public async Task<bool> ConfirmAsync(string receiptHash, CancellationToken cancellation)
        { await SendAsync("accepted", receiptHash, cancellation).ConfigureAwait(false); return true; }
        public void WatchLease(Action lost)
        {
            if (lost == null) throw new ArgumentNullException("lost");
            // No messages are expected after acceptance; EOF, extra data and I/O failure invalidate the lease.
            var watcher = WatchLeaseAsync(lost);
        }
        private async Task WatchLeaseAsync(Action lost)
        {
            try { await stream.ReadAsync(new byte[1], 0, 1).ConfigureAwait(false); }
            catch (Exception) { }
            finally { try { lost(); } catch (Exception) { } }
        }
        public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) != 0) return; stream.Dispose(); }
    }

    // A child requires a live, externally verified helper; a stale receipt file is never sufficient.
    public static class ObservationV3Child
    {
        public static async Task<ObservationV3Receipt> HandshakeAsync(ObservationV3Pipe pipe, ObservationV3Request request,
            ObservationV3Client client, ObservationV3Context context, ProcessIdentity self, string effectiveArgumentsHash,
            Func<int, ProcessIdentity> capture, Func<ObservationV3Receipt, Task<ObservationV3Ack>> nativeAck, Action leaseLost, CancellationToken cancellation)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                deadline.CancelAfter(30000);
                cancellation = deadline.Token;
                try
                {
                    using (cancellation.Register(pipe.Dispose))
                    {
                        if (leaseLost == null) throw new ArgumentNullException("leaseLost");
                        ObservationV3Wire.ValidateContext(request, client, context); ObservationV3Wire.SameFileScope(self, request.Origin);
                        ObservationV3Wire.Hash(effectiveArgumentsHash);
                        RequirePeer(pipe, request, client, self, capture);
                        await pipe.SendAsync("hello", new ObservationV3Hello { RunId = request.RunId, Nonce = request.Nonce,
                            RequestHash = ObservationV3Wire.Digest(request), ContextHash = ObservationV3Wire.Digest(context),
                            Child = self, EffectiveArgumentsHash = effectiveArgumentsHash }, cancellation).ConfigureAwait(false);
                        string challenge = await pipe.ReceiveAsync<string>("challenge", cancellation).ConfigureAwait(false); ObservationV3Wire.Id(challenge);
                        RequirePeer(pipe, request, client, self, capture);
                        await pipe.SendAsync("response", challenge, cancellation).ConfigureAwait(false);
                        var receipt = await pipe.ReceiveAsync<ObservationV3Receipt>("receipt", cancellation).ConfigureAwait(false);
                        ValidateReceipt(receipt, request, client, context, self, effectiveArgumentsHash, challenge);
                        RequirePeer(pipe, request, client, self, capture); cancellation.ThrowIfCancellationRequested();
                        // Start the single next-frame read now so helper EOF also interrupts native preparation.
                        var acceptance = pipe.ReceiveAsync<string>("accepted", cancellation);
                        var preparation = Task.Run(() => nativeAck(receipt), cancellation);
                        var observePreparation = preparation.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        var observeAcceptance = acceptance.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                        await Task.WhenAny(preparation, acceptance).ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        if (acceptance.IsCompleted)
                        {
                            await acceptance.ConfigureAwait(false); // propagate EOF/frame errors
                            throw new IOException("PrematureHelperAcceptance");
                        }
                        var ack = await preparation.ConfigureAwait(false); // adapter marshals to its runtime owner thread

                        ObservationV3Wire.Require(ack != null && ack.Version == 3 && ack.NativeReady && ack.LoggedOn && ack.AppId == request.AppId &&
                            ack.SteamId == request.SteamId && ack.ReceiptHash == ObservationV3Wire.Digest(receipt) && ack.Challenge == challenge &&
                            ObservationV3Wire.Same(ack.Child, self) && ObservationV3Wire.Same(ack.Helper, client.Helper) &&
                            ObservationV3Wire.Same(ack.NewClient, client.Current), "InvalidNativeAck");
                        RequirePeer(pipe, request, client, self, capture);
                        await pipe.SendAsync("ack", ack, cancellation).ConfigureAwait(false);
                        string accepted = await acceptance.ConfigureAwait(false);
                        ObservationV3Wire.Require(accepted == ack.ReceiptHash, "HelperAcceptanceMissing");
                        RequirePeer(pipe, request, client, self, capture); cancellation.ThrowIfCancellationRequested();
                        pipe.WatchLease(leaseLost); return receipt;
                    }
                }
                catch
                {
                    pipe.Dispose(); // also releases the pending acceptance read after native preparation fails
                    throw;
                }
            }
        }

        private static void RequirePeer(ObservationV3Pipe pipe, ObservationV3Request r, ObservationV3Client c, ProcessIdentity self, Func<int, ProcessIdentity> capture)
        {
            ObservationV3Wire.Require(!ObservationV3Wire.Same(self, r.Origin) && ObservationV3Wire.Same(pipe.CapturePeer(), c.Helper) &&
                ObservationV3Wire.Same(capture(c.Helper.Pid), c.Helper) && ObservationV3Wire.Same(capture(c.Current.Pid), c.Current) &&
                ObservationV3Wire.Same(capture(self.Pid), self), "ChildExternalIdentityMismatch");
        }
        public static void ValidateReceipt(ObservationV3Receipt v, ObservationV3Request r, ObservationV3Client c, ObservationV3Context x,
            ProcessIdentity self, string argsHash, string challenge)
        {
            ObservationV3Wire.ValidateContext(r, c, x);
            ObservationV3Wire.Require(v != null && v.Version == 3 && v.RunId == r.RunId && v.Nonce == r.Nonce &&
                v.RequestHash == ObservationV3Wire.Digest(r) && v.ContextHash == ObservationV3Wire.Digest(x) && v.ClientHash == ObservationV3Wire.Digest(c) &&
                v.EffectiveArgumentsHash == argsHash && v.Challenge == challenge && ObservationV3Wire.Same(v.Child, self) &&
                ObservationV3Wire.Same(v.Helper, c.Helper) && ObservationV3Wire.Same(v.Origin, r.Origin) && ObservationV3Wire.Same(v.NewClient, c.Current), "ChildReceiptMismatch");
        }
    }
}
