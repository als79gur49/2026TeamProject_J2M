// Shared verbatim with the participant restart PowerShell host. No Unity dependency, C# 5 syntax.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Exhibition.RestartExperiment
{
    public sealed class ProcessIdentity
    {
        public int Pid, Session;
        public long StartTicks;
        public string Path, Sha256, UserSid, Logon;
    }

    public sealed class ExperimentRequest
    {
        public string Nonce, EvidenceDirectory, ToolsDirectory, DllPath;
        public string OperationId, Build;
        public uint AppId;
        public ulong SteamId;
        public Trial Trial;
        public ProcessIdentity Parent, Steam;
        // Product participant-reset handoff, bound to the completed journal.
        public bool CompletedResetProduct;
        public string ReadyJournalPath, ReadyJournalSha256;
    }

    public sealed class ProductReadyJournal
    {
        public int SchemaVersion;
        public string OperationId, State, MappingVersion;
        public uint AppId;
        public ulong SteamId;
    }

    public static class CompletedResetProductWire
    {
        public static bool HasRequest(ExperimentRequest request) { return request != null && request.CompletedResetProduct; }
        public static void Validate(ExperimentRequest request)
        {
            if (!HasRequest(request)) throw new IOException("Completed-reset product request required.");
            Guid operation;
            if (request.Trial != Trial.FullCycle ||
                !Guid.TryParseExact(request.OperationId, "N", out operation) || string.IsNullOrWhiteSpace(request.ReadyJournalPath) ||
                string.IsNullOrWhiteSpace(request.ReadyJournalSha256) || request.AppId == 0 || request.SteamId == 0)
                throw new IOException("Invalid completed-reset product request.");
            string path = WindowsIdentityCapture.CanonicalPath(request.ReadyJournalPath);
            if (!string.Equals(path, request.ReadyJournalPath, StringComparison.OrdinalIgnoreCase) ||
                ExperimentFiles.Hash(path) != request.ReadyJournalSha256) throw new IOException("Completed-reset journal changed.");
            var journal = ExperimentFiles.Read<ProductReadyJournal>(path);
            if (journal == null || journal.SchemaVersion != 1 || journal.State != "Ready" || journal.OperationId != request.OperationId ||
                journal.MappingVersion != "level-clear-v1" || journal.AppId != request.AppId || journal.SteamId != request.SteamId)
                throw new IOException("Completed-reset Ready journal mismatch.");
        }
        public static void ValidateRequestPath(ExperimentRequest request, string requestPath)
        {
            Validate(request);
            string expected = WindowsIdentityCapture.CanonicalPath(Path.Combine(Path.GetDirectoryName(request.ReadyJournalPath),
                "participant-reset-handoff", request.OperationId, "request.json"));
            string actual = WindowsIdentityCapture.CanonicalPath(requestPath);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(request.EvidenceDirectory), Path.GetDirectoryName(expected), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Completed-reset request location mismatch.");
        }
    }

    public enum ProbeInitDisposition { Succeeded, NoSteamClient, GlobalUserConnectionUnavailable, Fatal }

    public static class ProbeInitPolicy
    {
        // Experimental SDK165 policy. This exact diagnostic does not establish a transient cause.
        // Production Validate pins the SDK DLL hash before any probe is launched.
        public static ProbeInitDisposition Classify(int code, string diagnostic)
        {
            if (code == 0) return ProbeInitDisposition.Succeeded;
            if (code == 2) return ProbeInitDisposition.NoSteamClient;
            if (code == 1 && string.Equals(diagnostic, "ConnectToGlobalUser failed.", StringComparison.Ordinal))
                return ProbeInitDisposition.GlobalUserConnectionUnavailable;
            return ProbeInitDisposition.Fatal;
        }
    }

    [DataContract]
    public sealed class ProbeObservation
    {
        [DataMember(IsRequired = true)] public int WireVersion = 3;
        [DataMember(IsRequired = true)] public ProbeInitDisposition InitDisposition = ProbeInitDisposition.Fatal;
        [DataMember(IsRequired = true)] public string InitDiagnostic;
        [DataMember(IsRequired = true)] public bool InitCalled;
        [DataMember(IsRequired = true)] public bool InitReturned;
        [DataMember(IsRequired = true)] public bool QueryCalled;
        [DataMember(IsRequired = true)] public bool QueryReturned;
        [DataMember(IsRequired = true)] public bool ShutdownCalled;
        [DataMember(IsRequired = true)] public string FailureStage;
        [DataMember(IsRequired = true)] public string QueryError;
        [DataMember(IsRequired = true)] public string ShutdownError;
        [DataMember(IsRequired = true)] public string CleanupError;
        [DataMember(IsRequired = true)] public string Nonce;
        [DataMember(IsRequired = true)] public string Error;
        [DataMember(IsRequired = true)] public int Pid;
        [DataMember(IsRequired = true)] public int InitResult;
        [DataMember(IsRequired = true)] public long StartTicks;
        [DataMember(IsRequired = true)] public uint AppId;
        [DataMember(IsRequired = true)] public ulong SteamId;
        [DataMember(IsRequired = true)] public bool LoggedOn;
        [DataMember(IsRequired = true)] public bool ShutdownReturned;
    }

    public sealed class ProbeAttempt
    {
        public int WireVersion = 3, Attempt;
        public string Nonce, Utc, Creation = "NotRequested", FailureStage, Error, CleanupError, CollectionError, ResultError;
        public long StartedMilliseconds, ElapsedMilliseconds;
        public int? Pid, ExitCode;
        public long? StartTicks;
        public bool ExitConfirmed, OwnedExitConfirmed, OutputComplete, ErrorOutputComplete, Parsed, IdentityValid, ReadyObserved;
        public int StderrPolicyVersion = ProbeStderrPolicy.Version;
        public string StderrDisposition = "NotEvaluated";
        public string Stdout, Stderr;
        public bool StdoutTruncated, StderrTruncated;
        public void Fail(string stage, Exception error)
        { if (Error == null) { FailureStage = stage; Error = error.ToString(); } }
    }

    public sealed class ProbeExpectation
    {
        public string Nonce, EvidenceDirectory;
        public uint AppId;
        public ulong SteamId;
        public static ProbeExpectation FromLegacy(ExperimentRequest request)
        { return request == null ? null : new ProbeExpectation { Nonce = request.Nonce, EvidenceDirectory = request.EvidenceDirectory, AppId = request.AppId, SteamId = request.SteamId }; }
    }

    public static class ProbeStderrPolicy
    {
        public const int Version = 1;
        // SDK165 hash is pinned in production Validate. Preserve the complete captured text.
        // Only the exact Windows diagnostic pair observed with a fully ready SDK result is allowed.
        public static string Validate(string stderr, bool sdkReady, ExperimentRequest expected)
        { return ValidateExpectation(stderr, sdkReady, ProbeExpectation.FromLegacy(expected)); }
        public static string ValidateExpectation(string stderr, bool sdkReady, ProbeExpectation expected)
        {
            if (stderr == "") return "Empty";
            if (sdkReady && expected != null && expected.AppId != 0 && expected.SteamId != 0)
            {
                string known = "Setting breakpad minidump AppID = " + expected.AppId.ToString(CultureInfo.InvariantCulture) + "\r\n" +
                    "SteamInternal_SetMinidumpSteamID:  Caching Steam ID:  " + expected.SteamId.ToString(CultureInfo.InvariantCulture) + " [API loaded no]\r\n";
                if (string.Equals(stderr, known, StringComparison.Ordinal)) return "Sdk165InitDiagnostics";
            }
            throw new IOException("Unrecognized or ineligible probe stderr; see captured attempt output.");
        }
    }

    public sealed class ProbeAttemptException : IOException
    {
        public ProbeAttemptException(ProbeAttempt attempt)
            : base("Probe attempt " + attempt.Attempt + " stopped at " + attempt.FailureStage +
                ". Native result and process failure are included in the inner exception.",
                new IOException(ExperimentFiles.Json(attempt)))
        { }
    }

    public enum LaunchRole { Steam = 1, Probe = 2, FullCycleGame = 3 }

    public static class LaunchEnvironment
    {
        public static ProcessStartInfo PrepareProbe(ExperimentRequest request, Func<ProcessStartInfo> host, Action checkClient, Action afterPreparation)
        {
            checkClient();
            var start = host();
            start.RedirectStandardInput = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            Apply(start, LaunchRole.Probe, request.AppId);
            afterPreparation();
            return start;
        }
        public static ProcessStartInfo PrepareCompletedResetSubmission(ExperimentRequest request, string requestPath,
            Action checkGame, Action verifyFile, Action checkClient, Action afterPreparation)
        {
            CompletedResetProductWire.ValidateRequestPath(request, requestPath); checkGame(); verifyFile(); checkClient();
            var start = new ProcessStartInfo {
                FileName = request.Steam.Path,
                Arguments = "-applaunch " + request.AppId.ToString(CultureInfo.InvariantCulture) +
                    " -- -j2mCompletedParticipantReset " + ExperimentFiles.Quote(requestPath) +
                    " -j2mCompletedParticipantResetHash " + ExperimentFiles.Hash(requestPath),
                WorkingDirectory = Path.GetDirectoryName(request.Steam.Path), UseShellExecute = false, CreateNoWindow = true
            };
            Apply(start, LaunchRole.FullCycleGame, request.AppId);
            afterPreparation();
            return start;
        }
        public static void ValidateChildRole(ExperimentRequest request)
        {
            CompletedResetProductWire.Validate(request);
        }
        public static void Apply(ProcessStartInfo start, LaunchRole role, uint appId)
        {
            if (!Enum.IsDefined(typeof(LaunchRole), role)) throw new ArgumentOutOfRangeException("role");
            bool clean = role == LaunchRole.Steam || role == LaunchRole.Probe || role == LaunchRole.FullCycleGame;
            bool setId = role == LaunchRole.Probe || role == LaunchRole.FullCycleGame;
            var names = start.EnvironmentVariables.Keys.Cast<string>().Where(k => k.StartsWith("Steam", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).ToArray();
            if (setId && appId == 0) throw new ArgumentOutOfRangeException("appId");
            if (clean) foreach (string key in names) start.EnvironmentVariables.Remove(key);
            string id = appId.ToString(CultureInfo.InvariantCulture);
            if (setId) { start.EnvironmentVariables["SteamAppId"] = id; start.EnvironmentVariables["SteamGameId"] = id; }
            if (clean && start.EnvironmentVariables.Keys.Cast<string>().Any(k => k.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) &&
                !(setId && (k == "SteamAppId" || k == "SteamGameId")))) throw new IOException("Steam environment cleanup failed.");
            bool match = !setId || (start.EnvironmentVariables["SteamAppId"] == id && start.EnvironmentVariables["SteamGameId"] == id);
            if (!match) throw new IOException("Steam AppID environment mismatch.");
        }

        // The same final boundary is used by real launches and harmless process-backed tests.
        public static Process Start(Func<ProcessStartInfo> prepare, Deadline deadline, Func<ProcessStartInfo, Process> create)
        {
            var start = prepare();
            if (deadline != null) deadline.Remaining();
            return create(start);
        }
    }

    public sealed class OutputSnapshot
    {
        public string Text, Error;
        public bool Overflow, Complete;
    }

    public sealed class BoundedOutput
    {
        private readonly object gate = new object();
        private readonly StringBuilder text = new StringBuilder();
        private bool overflow, complete;
        private string error;
        public readonly Task Completion;
        public BoundedOutput(TextReader reader) { Completion = Task.Run(() => Read(reader)); }
        private async Task Read(TextReader reader)
        {
            try
            {
                var chunk = new char[1024];
                while (true)
                {
                    int count = await reader.ReadAsync(chunk, 0, chunk.Length).ConfigureAwait(false);
                    if (count == 0) { lock (gate) complete = true; return; }
                    lock (gate)
                    {
                        int keep = Math.Min(count, 16384 - text.Length);
                        if (keep > 0) text.Append(chunk, 0, keep);
                        if (keep < count) overflow = true;
                    }
                }
            }
            catch (Exception e) { lock (gate) error = e.ToString(); }
        }
        public OutputSnapshot Snapshot()
        { lock (gate) return new OutputSnapshot { Text = text.ToString(), Overflow = overflow, Complete = complete, Error = error }; }
    }

    // One instance per attempt and allowance. Repeated recovery steps consume the same budget.
    public sealed class CleanupBudget
    {
        private readonly Func<long> clock;
        private readonly int limit;
        private long? expires;
        public CleanupBudget(Func<long> clock, int limit) { this.clock = clock; this.limit = limit; }
        public int Remaining()
        {
            long now = clock();
            if (!expires.HasValue) expires = checked(now + limit);
            return (int)Math.Max(0, expires.Value - now);
        }
    }

    // OS effects stay injectable so failure/allowance tests exercise the supervisor itself.
    public class ProbeOperations
    {
        public virtual Process Start(ProcessStartInfo start) { return Process.Start(start); }
        public virtual void Owned(Process process, Deadline deadline) { }
        public virtual void Kill(Process process) { process.Kill(); }
        public virtual bool WaitForExit(Process process, int milliseconds) { return process.WaitForExit(milliseconds); }
        public virtual BoundedOutput ReadOutput(TextReader reader) { return new BoundedOutput(reader); }
        public virtual bool WaitOutput(Task[] readers, int milliseconds) { return Task.WaitAll(readers, milliseconds); }
        public virtual void CloseJob(IDisposable job) { job.Dispose(); }
    }

    public sealed class OwnedShutdownCommand : IDisposable
    {
        private Process process;
        private bool exitRecorded;
        public string Creation { get; private set; }
        public ProcessIdentity Identity { get; private set; }
        public bool? ExitConfirmed { get; private set; }
        public int? ExitCode { get; private set; }
        public OwnedShutdownCommand() { Creation = "NotRequested"; }
        public void Start(Func<ProcessStartInfo> prepare, Deadline deadline, Func<ProcessStartInfo, Process> create,
            Func<Process, ProcessIdentity> identify)
        {
            if (Creation != "NotRequested") throw new InvalidOperationException("Shutdown command cannot be retried.");
            process = LaunchEnvironment.Start(prepare, deadline, start => { Creation = "Unknown"; return create(start); });
            if (process == null) throw new IOException("Shutdown command creation uncertain.");
            Creation = "Created";
            IntPtr handle = process.Handle;
            if (handle == IntPtr.Zero) throw new IOException("Shutdown command handle unavailable.");
            Identity = identify(process);
            if (Identity == null || Identity.Pid != process.Id || Identity.StartTicks <= 0 ||
                Identity.StartTicks != process.StartTime.ToUniversalTime().Ticks) throw new IOException("Shutdown command identity unavailable.");
        }
        public bool Alive(Action<int> recordExit)
        {
            if (process == null || Identity == null) throw new IOException("Shutdown command ownership unavailable.");
            ExitConfirmed = process.HasExited;
            if (!ExitConfirmed.Value) return true;
            ExitCode = process.ExitCode;
            if (!exitRecorded) { recordExit(ExitCode.Value); exitRecorded = true; }
            if (ExitCode.Value != 0) throw new IOException("Shutdown command abnormal exit: " + ExitCode.Value);
            return false;
        }
        public void Dispose()
        {
            // This class intentionally has no Kill method. Dispose releases only the retained handle.
            if (process != null) { process.Dispose(); process = null; }
        }
    }

    public static class ExperimentFiles
    {
        public const string DllHash = "8de54d32508e216c9135b8bf025749243d44e404c1c22a8e5fe35acecabe7a9c";
        public static T Read<T>(string path) { return Parse<T>(File.ReadAllText(path)); }
        public static T Parse<T>(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        public static string Json<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create()) return Hex(hash.ComputeHash(stream));
        }
        public static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        public static string Quote(string value)
        {
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); result.Append(c); slashes = 0;
            }
            result.Append('\\', slashes * 2); return result.Append('"').ToString();
        }
        public static string PowerShell
        {
            get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"); }
        }
        public static ProcessStartInfo HostStart(string directory, string requestPath, bool probe)
        {
            return new ProcessStartInfo {
                FileName = PowerShell,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File " + Quote(System.IO.Path.Combine(directory, "Restart-Experiment.ps1")) +
                    " -RequestPath " + Quote(requestPath) + (probe ? " -Probe" : ""),
                WorkingDirectory = System.IO.Path.GetDirectoryName(Read<ExperimentRequest>(requestPath).Parent.Path),
                UseShellExecute = false, CreateNoWindow = true
            };
        }
    }

    public static class WindowsIdentityCapture
    {
        [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] private struct Statistics
        {
            public Luid TokenId, AuthenticationId; public long Expiration;
            public int TokenType, ImpersonationLevel; public uint DynamicCharged, DynamicAvailable, GroupCount, PrivilegeCount;
            public Luid ModifiedId;
        }
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, out Statistics stats, int size, out int returned);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, IntPtr buffer, int size, out int returned);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr text);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandle(IntPtr file, StringBuilder path, uint length, uint flags);

        public static string CanonicalPath(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var result = new StringBuilder(32768);
                uint count = GetFinalPathNameByHandle(file.SafeFileHandle.DangerousGetHandle(), result, (uint)result.Capacity, 0);
                if (count == 0 || count >= result.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
                string value = result.ToString();
                if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + value.Substring(8);
                return value.StartsWith(@"\\?\", StringComparison.Ordinal) ? value.Substring(4) : value;
            }
        }

        public static int SessionId(int processId)
        {
            uint session;
            if (processId <= 0) throw new ArgumentOutOfRangeException("processId");
            if (!ProcessIdToSessionId((uint)processId, out session)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return checked((int)session);
        }

        public static ProcessIdentity Capture(Process process, bool hash)
        {
            IntPtr token;
            if (!OpenProcessToken(process.Handle, 8, out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                Statistics stats; int returned;
                if (!GetTokenInformation(token, 10, out stats, Marshal.SizeOf(typeof(Statistics)), out returned))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                // A newly created child can have an empty Mono Process.Modules collection.
                var image = new StringBuilder(32768);
                uint length = (uint)image.Capacity;
                if (!QueryFullProcessImageName(process.Handle, 0, image, ref length))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                string path = CanonicalPath(image.ToString());
                return new ProcessIdentity { Pid = process.Id, Session = SessionId(process.Id),
                    StartTicks = process.StartTime.ToUniversalTime().Ticks, Path = path,
                    Sha256 = hash ? ExperimentFiles.Hash(path) : null, UserSid = UserSid(token),
                    Logon = stats.AuthenticationId.High.ToString("x8") + stats.AuthenticationId.Low.ToString("x8") };
            }
            finally { CloseHandle(token); }
        }

        private static string UserSid(IntPtr token)
        {
            int size;
            GetTokenInformation(token, 1, IntPtr.Zero, 0, out size);
            if (size <= 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!GetTokenInformation(token, 1, buffer, size, out size)) throw new Win32Exception(Marshal.GetLastWin32Error());
                IntPtr text;
                if (!ConvertSidToStringSid(Marshal.ReadIntPtr(buffer), out text)) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { return Marshal.PtrToStringUni(text); } finally { LocalFree(text); }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        public static bool SameScope(ProcessIdentity a, ProcessIdentity b)
        {
            return a.Session == b.Session && a.UserSid == b.UserSid && a.Logon == b.Logon;
        }
        public static bool SameProcess(ProcessIdentity a, ProcessIdentity b)
        {
            return SameScope(a, b) && a.Pid == b.Pid && a.StartTicks == b.StartTicks &&
                string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        }
        public static ProcessIdentity Steam(ProcessIdentity owner, ProcessIdentity excluded = null)
        {
            ProcessIdentity found = null;
            foreach (var process in Process.GetProcessesByName("steam"))
                using (process)
                {
                    if (process.HasExited) continue;
                    if (excluded != null && process.Id == excluded.Pid && process.StartTime.ToUniversalTime().Ticks == excluded.StartTicks) continue;
                    if (SessionId(process.Id) != owner.Session) continue;
                    var candidate = Capture(process, true);
                    if (!SameScope(candidate, owner)) throw new InvalidOperationException("Steam user/logon differs.");
                    if (found != null) throw new InvalidOperationException("Multiple Steam clients; inspect manually.");
                    found = candidate;
                }
            return found;
        }
        public static string LockName(ProcessIdentity steam)
        {
            string scope = steam.UserSid + "\n" + steam.Session + "\n" + steam.Logon + "\n" + steam.Path.ToUpperInvariant();
            using (var hash = SHA256.Create()) return @"Local\J2M.RestartExperiment." + ExperimentFiles.Hex(hash.ComputeHash(Encoding.UTF8.GetBytes(scope)));
        }
    }

    public sealed class WindowsCycleEnvironment : ICycleEnvironment
    {
        private readonly ExperimentRequest request;
        private readonly string requestPath;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private ProcessIdentity currentSteam, shutdownCommand;
        private OwnedShutdownCommand ownedShutdownCommand;
        private int attempts;
        public bool CycleEntered { get; private set; }
        public long Milliseconds { get { return clock.ElapsedMilliseconds; } }

        public WindowsCycleEnvironment(ExperimentRequest request, string requestPath) { this.request = request; this.requestPath = requestPath; }

        public void Validate()
        {
            LaunchEnvironment.ValidateChildRole(request);
            Guid nonce;
            if (Environment.OSVersion.Platform != PlatformID.Win32NT || !Environment.Is64BitProcess ||
                request.Parent == null || request.Steam == null || request.AppId == 0 || request.SteamId == 0 ||
                !Guid.TryParseExact(request.Nonce, "N", out nonce) || !Enum.IsDefined(typeof(Trial), request.Trial))
                throw new InvalidOperationException("Invalid x64 experiment request.");
            using (var process = Process.GetCurrentProcess())
                if (!WindowsIdentityCapture.SameScope(request.Parent, WindowsIdentityCapture.Capture(process, false)))
                    throw new InvalidOperationException("Helper user/session/logon differs.");
            if (!WindowsIdentityCapture.SameScope(request.Parent, request.Steam)) throw new InvalidOperationException("Steam scope differs.");
            VerifyFile(request.Parent); VerifyFile(request.Steam);
            if (!File.Exists(ExperimentFiles.PowerShell)) throw new FileNotFoundException("Windows PowerShell missing.");
            foreach (var name in new[] { "Restart-Experiment.ps1", "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs" })
                if (!File.Exists(System.IO.Path.Combine(request.ToolsDirectory, name))) throw new FileNotFoundException(name);
            CompletedResetProductWire.ValidateRequestPath(request, requestPath);
            if (request.Trial != Trial.FullCycle || ExperimentFiles.Hash(request.DllPath) != ExperimentFiles.DllHash)
                throw new InvalidOperationException("Completed-reset Steam probe payload mismatch.");
        }

        private static void VerifyFile(ProcessIdentity target)
        {
            if (string.IsNullOrEmpty(target.Sha256) || !string.Equals(WindowsIdentityCapture.CanonicalPath(target.Path), target.Path, StringComparison.OrdinalIgnoreCase) ||
                ExperimentFiles.Hash(target.Path) != target.Sha256) throw new IOException("Target path/hash changed: " + target.Path);
        }
        public IDisposable AcquireCycleLock()
        {
            var mutex = new Mutex(false, WindowsIdentityCapture.LockName(request.Steam));
            try
            {
                if (!mutex.WaitOne(0)) throw new InvalidOperationException("Cycle lock busy.");
                CycleEntered = true;
                return new MutexLease(mutex);
            }
            catch (AbandonedMutexException) { mutex.ReleaseMutex(); mutex.Dispose(); throw; }
            catch { mutex.Dispose(); throw; }
        }
        private sealed class MutexLease : IDisposable
        {
            private readonly Mutex mutex;
            public MutexLease(Mutex mutex) { this.mutex = mutex; }
            public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
        }
        public bool ParentAlive()
        {
            Process process;
            try { process = Process.GetProcessById(request.Parent.Pid); } catch (ArgumentException) { return false; }
            using (process)
            {
                if (process.HasExited || process.StartTime.ToUniversalTime().Ticks != request.Parent.StartTicks) return false;
                if (!WindowsIdentityCapture.SameProcess(request.Parent, WindowsIdentityCapture.Capture(process, false)))
                    throw new InvalidOperationException("Parent identity changed.");
                return true;
            }
        }
        public void EnsureNoOtherGame()
        {
            foreach (var process in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(request.Parent.Path)))
                using (process)
                {
                    if (process.HasExited || WindowsIdentityCapture.SessionId(process.Id) != request.Parent.Session) continue;
                    var identity = WindowsIdentityCapture.Capture(process, false);
                    if (WindowsIdentityCapture.SameProcess(identity, request.Parent)) continue;
                    if (string.Equals(identity.Path, request.Parent.Path, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Another instance of the game appeared; no additional launch.");
                }
        }
        public bool OriginalSteamAlive()
        {
            var found = WindowsIdentityCapture.Steam(request.Parent, shutdownCommand);
            if (found == null) return false;
            if (!WindowsIdentityCapture.SameProcess(found, request.Steam) || found.Sha256 != request.Steam.Sha256)
                throw new InvalidOperationException("Replacement/restarted Steam detected; inspect manually.");
            return true;
        }
        public void RequestSteamExit(Deadline deadline)
        {
            ownedShutdownCommand = new OwnedShutdownCommand();
            ownedShutdownCommand.Start(() =>
            {
                VerifyFile(request.Steam);
                if (!OriginalSteamAlive()) throw new InvalidOperationException("Steam exited before shutdown request.");
                return SteamStart("-shutdown");
            }, deadline, Process.Start, process => new ProcessIdentity { Pid = process.Id, StartTicks = process.StartTime.ToUniversalTime().Ticks });
            shutdownCommand = ownedShutdownCommand.Identity;
        }
        public bool ShutdownCommandAlive()
        {
            if (ownedShutdownCommand == null || shutdownCommand == null) throw new IOException("Shutdown command ownership unavailable.");
            return ownedShutdownCommand.Alive(code => { });
        }
        public void EnsureSteamExited()
        {
            if (WindowsIdentityCapture.Steam(request.Parent, shutdownCommand) != null)
                throw new IOException("Steam client appeared after original exit; inspect manually.");
        }
        private ProcessStartInfo SteamStart(string arguments)
        {
            var start = new ProcessStartInfo { FileName = request.Steam.Path, Arguments = arguments,
                WorkingDirectory = System.IO.Path.GetDirectoryName(request.Steam.Path), UseShellExecute = false, CreateNoWindow = true };
            LaunchEnvironment.Apply(start, LaunchRole.Steam, request.AppId);
            return start;
        }
        public void StartSteam()
        {
            EnsureNoOtherGame(); VerifyFile(request.Steam);
            if (WindowsIdentityCapture.Steam(request.Parent) != null) throw new InvalidOperationException("Steam already restarted.");
            using (var process = Process.Start(SteamStart("")))
            {
                if (process == null) throw new IOException("Steam creation failed.");
                currentSteam = WindowsIdentityCapture.Capture(process, true);
                if (!WindowsIdentityCapture.SameScope(currentSteam, request.Steam) || currentSteam.Path != request.Steam.Path || currentSteam.Sha256 != request.Steam.Sha256)
                    throw new IOException("New Steam target differs.");
            }
        }
        public void EnsureNewSteamUnchanged()
        {
            var found = WindowsIdentityCapture.Steam(request.Parent);
            if (found == null || currentSteam == null || !WindowsIdentityCapture.SameProcess(found, currentSteam) || found.Sha256 != currentSteam.Sha256)
                throw new InvalidOperationException("New Steam exited or was replaced; no cycle retry.");
        }
        public bool ProbeReady(Deadline deadline)
        {
            var attempt = new ProbeAttempt { Attempt = ++attempts, Nonce = request.Nonce,
                Utc = DateTime.UtcNow.ToString("o"), StartedMilliseconds = Milliseconds };
            return RunOwnedProbe(() =>
            {
                return LaunchEnvironment.PrepareProbe(request, () => ExperimentFiles.HostStart(request.ToolsDirectory, requestPath, true),
                    EnsureNewSteamUnchanged, () => { });
            }, deadline, () => Milliseconds, request, attempt);
        }
        // Convenience entry for harmless Windows child tests. Production supplies its absolute deadline.
        public static bool RunOwnedProbe(ProcessStartInfo start, int budgetMilliseconds, ExperimentRequest expected)
        {
            if (budgetMilliseconds <= 0 || budgetMilliseconds > 10000) throw new ArgumentOutOfRangeException("budgetMilliseconds");
            var clock = Stopwatch.StartNew();
            return RunOwnedProbe(() => start, new Deadline(() => clock.ElapsedMilliseconds, budgetMilliseconds, "Probe timed out; no further launch."),
                () => clock.ElapsedMilliseconds, expected, new ProbeAttempt { Attempt = 1, Nonce = expected.Nonce, Utc = DateTime.UtcNow.ToString("o") });
        }
        public static bool RunOwnedProbe(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ExperimentRequest expected, ProbeAttempt attempt)
        { return RunOwnedProbe(prepare, deadline, clock, expected, attempt, new ProbeOperations()); }

        public static bool RunOwnedProbe(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ExperimentRequest expected, ProbeAttempt attempt, ProbeOperations operations)
        { return RunOwnedProbeCore(prepare, deadline, clock, ProbeExpectation.FromLegacy(expected), attempt, operations, ParseProbe, expected.Nonce); }

        public static bool RunOwnedProbeCore(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ProbeExpectation expected, ProbeAttempt attempt,
            ProbeOperations operations, Func<string, ProbeObservation> parse, string grant)
        {
            Process process = null; ProbeJob job = null;
            BoundedOutput stdout = null, stderr = null;
            var drainBudget = new CleanupBudget(clock, 1000);
            var killBudget = new CleanupBudget(clock, 2000);
            string stage = "Prepare";
            bool ready = false;
            try
            {
                job = new ProbeJob();
                process = LaunchEnvironment.Start(() =>
                {
                    var start = prepare();
                    if (!start.RedirectStandardInput || !start.RedirectStandardOutput || !start.RedirectStandardError || start.UseShellExecute)
                        throw new ArgumentException("Probe requires redirected pipes and direct creation.");
                    return start;
                }, deadline, start =>
                {
                    stage = "Create"; attempt.Creation = "Unknown";
                    var created = operations.Start(start);
                    if (created != null) attempt.Creation = "Created";
                    return created;
                });
                if (process == null) throw new IOException("Probe creation uncertain.");
                stage = "Ownership";
                stdout = operations.ReadOutput(process.StandardOutput); stderr = operations.ReadOutput(process.StandardError);
                IntPtr handle = process.Handle;
                attempt.Pid = process.Id; attempt.StartTicks = process.StartTime.ToUniversalTime().Ticks;
                job.Assign(handle);
                operations.Owned(process, deadline);
                deadline.Remaining();
                stage = "Grant";
                process.StandardInput.WriteLine(grant); process.StandardInput.Close();
                stage = "Execution";
                while (true)
                {
                    bool exited = process.HasExited;
                    long remaining = deadline.Remaining();
                    CheckOutput(stdout, stderr);
                    if (exited)
                    {
                        if (process.ExitCode != 0) throw new IOException("Probe abnormal exit: ExitCode=" + process.ExitCode);
                        break;
                    }
                    operations.WaitForExit(process, (int)Math.Min(50, remaining));
                }
            }
            catch (Exception e) { attempt.Fail(stage, e); }
            // Capture the primary cause first. All later failures are supplementary.
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        int remaining = killBudget.Remaining();
                        operations.Kill(process);
                        remaining = killBudget.Remaining();
                        if (!process.HasExited && (remaining <= 0 || !operations.WaitForExit(process, remaining)))
                            throw new TimeoutException("Probe termination unconfirmed; manual recovery required.");
                    }
                    attempt.ExitConfirmed = process.HasExited;
                    attempt.OwnedExitConfirmed = attempt.ExitConfirmed;
                    if (attempt.ExitConfirmed) attempt.ExitCode = process.ExitCode;
                }
                catch (Exception e) { attempt.CleanupError = e.ToString(); attempt.Fail("Cleanup", e); }
            }
            // Closing the probe-only job is cleanup too, and precedes the final pipe observation.
            try { if (job != null) operations.CloseJob(job); }
            catch (Exception e) { attempt.CleanupError = JoinError(attempt.CleanupError, e); attempt.Fail("JobCleanup", e); }
            // If Kill failed, job close may still have terminated our child. Observe that outcome
            // with the original termination allowance; never erase the failed cleanup operation.
            if (process != null && !attempt.ExitConfirmed)
            {
                try
                {
                    bool exited = process.HasExited;
                    int remaining = exited ? 0 : killBudget.Remaining();
                    if (!exited && remaining > 0) exited = operations.WaitForExit(process, remaining);
                    attempt.ExitConfirmed = exited; attempt.OwnedExitConfirmed = exited;
                    if (exited) attempt.ExitCode = process.ExitCode;
                    else throw new IOException("Owned exit remains unconfirmed after job cleanup.");
                }
                catch (Exception e) { attempt.CleanupError = JoinError(attempt.CleanupError, e); attempt.Fail("ExitObservation", e); }
            }
            if (stdout != null && stderr != null)
            {
                try
                {
                    int remaining = drainBudget.Remaining();
                    if (!operations.WaitOutput(new[] { stdout.Completion, stderr.Completion }, remaining))
                        throw new IOException("Probe output did not close within shared drain budget.");
                    CheckOutput(stdout, stderr);
                }
                catch (Exception e) { attempt.CollectionError = e.ToString(); attempt.Fail("Output", e); }
                var output = stdout.Snapshot(); var errors = stderr.Snapshot();
                attempt.Stdout = output.Text; attempt.Stderr = errors.Text;
                attempt.StdoutTruncated = output.Overflow; attempt.StderrTruncated = errors.Overflow;
                attempt.OutputComplete = output.Complete; attempt.ErrorOutputComplete = errors.Complete;
                if (output.Error != null || errors.Error != null)
                    attempt.CollectionError = (attempt.CollectionError ?? "") + "\n" + output.Error + "\n" + errors.Error;
            }
            try { if (process != null) process.Dispose(); }
            catch (Exception e) { attempt.CleanupError = JoinError(attempt.CleanupError, e); attempt.Fail("ProcessDispose", e); }
            if (!attempt.ExitConfirmed || !attempt.OwnedExitConfirmed || attempt.ExitCode != 0 ||
                !attempt.OutputComplete || !attempt.ErrorOutputComplete || attempt.StdoutTruncated || attempt.StderrTruncated)
                attempt.Fail("ExitOutput", new IOException("Probe exit/output failed: ExitCode=" +
                    (attempt.ExitCode.HasValue ? attempt.ExitCode.ToString() : "Unknown") + "; Stderr=" + attempt.Stderr));
            try
            {
                // Parse complete bounded output even for failed exits, preserving useful diagnostics.
                ProbeObservation row = null;
                if (attempt.OutputComplete && !attempt.StdoutTruncated && !string.IsNullOrWhiteSpace(attempt.Stdout))
                {
                    row = parse(attempt.Stdout); attempt.Parsed = true;
                    attempt.IdentityValid = row.Nonce == expected.Nonce && row.Pid == attempt.Pid && row.StartTicks == attempt.StartTicks;

                }
                if (attempt.Error != null || attempt.CleanupError != null || attempt.CollectionError != null)
                    throw new IOException("Probe has captured failure(s).");
                if (!attempt.ExitConfirmed || !attempt.OwnedExitConfirmed || attempt.ExitCode != 0 ||
                    !attempt.OutputComplete || !attempt.ErrorOutputComplete || attempt.StdoutTruncated || attempt.StderrTruncated) throw new IOException("Probe exit/output incomplete or failed: " + attempt.Stderr);
                bool sdkReady = ValidateProbeExpectation(row, expected, attempt.Pid.Value, attempt.StartTicks.Value);
                attempt.StderrDisposition = "Rejected";
                try { attempt.StderrDisposition = ProbeStderrPolicy.ValidateExpectation(attempt.Stderr, sdkReady, expected); }
                catch (Exception e) { attempt.Fail("StderrPolicy", e); throw; }
                ready = sdkReady;
            }
            catch (Exception e) { attempt.ResultError = e.ToString(); attempt.Fail("Result", e); }
            attempt.ReadyObserved = ready && attempt.Error == null;
            attempt.ElapsedMilliseconds = clock() - attempt.StartedMilliseconds;
            if (attempt.Error != null) throw new ProbeAttemptException(attempt);
            return attempt.ReadyObserved;
        }
        private static string JoinError(string prior, Exception error) { return (prior == null ? "" : prior + "\n") + error; }
        private static void CheckOutput(BoundedOutput stdout, BoundedOutput stderr)
        {
            var output = stdout.Snapshot(); var error = stderr.Snapshot();
            if (output.Overflow || error.Overflow) throw new IOException("Probe output exceeds result limit.");
            if (output.Error != null || error.Error != null) throw new IOException("Probe collection failed: " + output.Error + error.Error);
        }
        public static ProbeObservation ParseProbe(string json)
        {
            // DataContractJsonSerializer alone can accept a first JSON value with trailing content.
            using (var reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(
                Encoding.UTF8.GetBytes(json), System.Xml.XmlDictionaryReaderQuotas.Max))
            {
                var row = (ProbeObservation)new DataContractJsonSerializer(typeof(ProbeObservation)).ReadObject(reader);
                if (reader.Read()) throw new IOException("Trailing probe output.");
                return row;
            }
        }

        private sealed class ProbeJob : IDisposable
        {
            [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
            {
                public long ProcessTime, JobTime; public uint Flags;
                public UIntPtr MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit;
                public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
            }
            [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes; }
            [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
            {
                public BasicLimits Basic; public IoCounters Io;
                public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
            }
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref ExtendedLimits limits, int size);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
            [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
            private IntPtr handle;
            public ProbeJob()
            {
                handle = CreateJobObject(IntPtr.Zero, null);
                if (handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                var limits = new ExtendedLimits(); limits.Basic.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                if (!SetInformationJobObject(handle, 9, ref limits, Marshal.SizeOf(typeof(ExtendedLimits))))
                {
                    var error = new Win32Exception(Marshal.GetLastWin32Error());
                    try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                    throw error;
                }
            }
            public void Assign(IntPtr process)
            {
                if (!AssignProcessToJobObject(handle, process)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot supervise probe; SDK initialization denied.");
            }
            public void Dispose()
            {
                if (handle == IntPtr.Zero) return;
                if (!CloseHandle(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Probe job close failed.");
                handle = IntPtr.Zero;
            }
        }
        public static bool ValidateProbe(ProbeObservation row, ExperimentRequest expected, int pid, long ticks)
        { return ValidateProbeExpectation(row, ProbeExpectation.FromLegacy(expected), pid, ticks); }
        public static bool ValidateProbeExpectation(ProbeObservation row, ProbeExpectation expected, int pid, long ticks)
        {
            if (row == null || row.WireVersion != 3 || !row.InitCalled || !row.InitReturned || row.Nonce != expected.Nonce || row.Pid != pid || row.StartTicks != ticks)
                throw new IOException("Probe result identity mismatch (nonce/PID/start time).");
            if (!string.IsNullOrEmpty(row.Error) || !string.IsNullOrEmpty(row.FailureStage) ||
                !string.IsNullOrEmpty(row.QueryError) || !string.IsNullOrEmpty(row.ShutdownError) || !string.IsNullOrEmpty(row.CleanupError))
                throw new IOException("Probe observation failed: " + ExperimentFiles.Json(row));
            if (row.InitDiagnostic != null && row.InitDiagnostic.Length > 1023) throw new IOException("Init diagnostic exceeds limit.");
            if (row.InitDisposition != ProbeInitPolicy.Classify(row.InitResult, row.InitDiagnostic))
                throw new IOException("Probe Init disposition mismatch.");
            // RunOwnedProbe calls this only after normal exit and complete, validated output.
            // Both qualified failed-Init shapes permit observation only, never readiness.
            if (row.InitDisposition == ProbeInitDisposition.NoSteamClient ||
                row.InitDisposition == ProbeInitDisposition.GlobalUserConnectionUnavailable)
            {
                if (row.QueryCalled || row.QueryReturned || row.ShutdownCalled || row.ShutdownReturned || row.AppId != 0 || row.SteamId != 0 || row.LoggedOn)
                    throw new IOException("Inconsistent failed Init result: failed Init must not query or call Shutdown.");
                return false;
            }
            if (row.InitResult != 0)
                throw new IOException("Steam Init failed: " + (row.InitResult == 1 ? "FailedGeneric" :
                    row.InitResult == 3 ? "VersionMismatch" : "UnknownResult") + " (" + row.InitResult + ").");
            if (!row.QueryCalled || !row.QueryReturned || !row.ShutdownCalled || !row.ShutdownReturned) throw new IOException("Probe Shutdown did not return after successful Init.");
            return row.AppId == expected.AppId && row.SteamId == expected.SteamId && row.LoggedOn;
        }
        public void StartGame(Deadline deadline)
        {
            using (var command = LaunchEnvironment.Start(() => LaunchEnvironment.PrepareCompletedResetSubmission(request, requestPath,
                EnsureNoOtherGame, () => VerifyFile(request.Parent), EnsureNewSteamUnchanged, () => { }), deadline, Process.Start))
            {
                if (command == null) throw new IOException("Completed-reset Steam submission failed.");
            }
        }
        public void Delay(int milliseconds)
        { if (milliseconds <= 0) throw new ArgumentOutOfRangeException("milliseconds"); Thread.Sleep(milliseconds); }
        public void Cleanup()
        {
            // Releasing this handle never terminates the Steam command.
            if (ownedShutdownCommand == null) return;
            try { ownedShutdownCommand.Dispose(); }
            finally { ownedShutdownCommand = null; }
        }
    }
}
