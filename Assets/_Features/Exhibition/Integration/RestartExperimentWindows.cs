// Shared verbatim with the diagnostic PowerShell host. No Unity dependency, C# 5 syntax.
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

    public enum ResetOverlayRole { None, Initiator, ResetWorker, FinalObserver }

    public sealed class ExperimentRequest
    {
        public string Nonce, EvidenceDirectory, PrerequisitesPath, ToolsDirectory, DllPath;
        public string OperationId, Build;
        public int OverlayObservationWireVersion;
        public string OverlayObservationContextPath, OverlayObservationRunId, OverlayObservationContextSha256;
        public OverlayObservationRole OverlayObservationChildRole;
        public int ResetOverlayWireVersion;
        public string ResetOverlayTrialId, ResetOverlayContextSha256;
        public string ResetOverlayContextPath;
        public ResetOverlayRole ResetOverlayChildRole;
        public uint AppId;
        public ulong SteamId;
        public Trial Trial;
        public ProcessIdentity Parent, Steam;
        // Product participant-reset handoff. This is deliberately separate from
        // the diagnostic reset/observation wires above.
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
            if (!HasRequest(request)) return;
            Guid operation;
            if (request.Trial != Trial.FullCycle || OverlayObservationWire.HasObservation(request) || ResetOverlayWire.HasReset(request) ||
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

    // Reset v2 is independent of the read-only observation wire and the product journal.
    public sealed class ResetOverlayTrialContext
    {
        public int Version;
        public string TrialId, HandoffNonce, ReadyOperationId, OperationId, MappingVersion, Directory, ConfigurationPath;
        public string ConfigurationSha256, ManifestSha256, BaselinePath, BaselineSha256, OriginReportPath, OriginReportSha256;
        public string InitialFilesPath, InitialFilesSha256, PendingFilesPath, PendingFilesSha256, ScopeConfirmedUtc;
        public uint AppId;
        public ulong SteamId;
        public ProcessIdentity Origin, Steam;
    }
    public sealed class ResetOverlaySdkBaseline
    {
        public int Version;
        public string TrialId, ReadyOperationId, MappingVersion, Source, Utc;
        public ResetOverlayRole Role;
        public ProcessIdentity Process;
        public uint AppId;
        public ulong SteamId;
        public double MonotonicSeconds;
        public string[] Names;
        public bool[] QuerySucceeded, Achieved;
    }
    public sealed class ResetOverlayUserReport
    {
        public int Version;
        public string TrialId, Source, Utc, Visibility, BaselineSha256, ResetResultSha256;
        public ResetOverlayRole Role;
        public ProcessIdentity Process;
        public double MonotonicSeconds;
        public bool AttemptReported;
        public string[] Names, Judgments;
    }
    public sealed class ResetOverlayHelperCreation
    {
        public int Version;
        public string Nonce;
        public ProcessIdentity Origin, Helper;
    }
    public sealed class ResetOverlayChildReceipt
    {
        public int Version;
        public string Nonce, TrialId, OperationId, ContextPath, ContextSha256, RequestSha256, MappingVersion;
        public uint AppId;
        public ulong SteamId;
        public ResetOverlayRole Role;
        public ProcessIdentity Origin, Child;
    }
    public static class ResetOverlayWire
    {
        public static string[] Names()
        { return Enumerable.Range(0, 5).Select(i => "VQ_LEVEL_" + i + "_CLEAR").ToArray(); }
        public static bool HasReset(ExperimentRequest r)
        { return r.ResetOverlayWireVersion != 0 || r.ResetOverlayTrialId != null || r.ResetOverlayContextSha256 != null || r.ResetOverlayContextPath != null || r.ResetOverlayChildRole != ResetOverlayRole.None; }
        public static bool Same(ProcessIdentity a, ProcessIdentity b)
        { return a != null && b != null && WindowsIdentityCapture.SameProcess(a, b) && WindowsIdentityCapture.SameScope(a, b) &&
            string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) && a.Sha256 == b.Sha256; }
        private static bool Identity(ProcessIdentity p)
        { return p != null && p.Pid > 0 && p.StartTicks > 0 && p.Session >= 0 && !string.IsNullOrWhiteSpace(p.UserSid) && !string.IsNullOrWhiteSpace(p.Logon) && !string.IsNullOrWhiteSpace(p.Path) && OverlayObservationWire.Hash(p.Sha256); }
        private static bool Id(string id) { Guid g; return Guid.TryParseExact(id, "N", out g); }
        private static bool Timestamp(string utc, double mono)
        { DateTimeOffset date; return DateTimeOffset.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out date) && !double.IsNaN(mono) && !double.IsInfinity(mono) && mono >= 0; }
        public static void ValidateRequest(ExperimentRequest r)
        {
            if (!HasReset(r)) return;
            if (r.ResetOverlayWireVersion != 2 || !Id(r.ResetOverlayTrialId) || !Id(r.Nonce) || !Id(r.OperationId) ||
                r.Trial != Trial.GameOnly || r.ResetOverlayChildRole != ResetOverlayRole.ResetWorker ||
                string.IsNullOrWhiteSpace(r.ResetOverlayContextPath) || !OverlayObservationWire.Hash(r.ResetOverlayContextSha256) ||
                OverlayObservationWire.HasObservation(r)) throw new IOException("Invalid reset overlay child role or old/partial/mixed reset wire.");
        }
        public static void ValidateBaseline(ResetOverlaySdkBaseline b)
        {
            if (b == null || b.Version != 2 || !Id(b.TrialId) || !Id(b.ReadyOperationId) || b.Role != ResetOverlayRole.Initiator ||
                b.Source != "SDK" || b.AppId != 5218360 || b.SteamId == 0 || b.MappingVersion != "level-clear-v1" || !Identity(b.Process) ||
                !Timestamp(b.Utc, b.MonotonicSeconds) || b.Names == null || !b.Names.SequenceEqual(Names()) ||
                b.QuerySucceeded == null || b.QuerySucceeded.Length != 5 || b.QuerySucceeded.Any(v => !v) ||
                b.Achieved == null || b.Achieved.Length != 5 || !b.Achieved.Any(v => v)) throw new IOException("A complete SDK baseline with an earned comparison target is required.");
        }
        public static void ValidateReport(ResetOverlayUserReport r, ResetOverlaySdkBaseline b, ResetOverlayRole role, ProcessIdentity process)
        {
            ValidateBaseline(b);
            var targets = b.Names.Where((name, i) => b.Achieved[i]).ToArray();
            if (r == null || r.Version != 2 || r.TrialId != b.TrialId || r.Role != role ||
                (role != ResetOverlayRole.Initiator && role != ResetOverlayRole.ResetWorker) || r.Source != "User" || !Same(r.Process, process) ||
                !Timestamp(r.Utc, r.MonotonicSeconds) || !OverlayObservationWire.Hash(r.BaselineSha256) ||
                r.Names == null || !r.Names.SequenceEqual(targets) || r.Judgments == null || r.Judgments.Length != targets.Length ||
                r.Judgments.Any(v => v != "earned" && v != "unearned" && v != "inconclusive") ||
                (r.Visibility != "opened" && r.Visibility != "not-visible" && r.Visibility != "inconclusive") ||
                r.AttemptReported != (r.Visibility != "inconclusive") ||
                (r.Visibility != "opened" && r.Judgments.Any(v => v != "inconclusive")) ||
                (role == ResetOverlayRole.Initiator ? r.ResetResultSha256 != null : !OverlayObservationWire.Hash(r.ResetResultSha256)))
                throw new IOException("Invalid user achievement report.");
        }
        public static bool BaselineAgrees(ResetOverlayUserReport report)
        { return report.Visibility == "opened" && report.AttemptReported && report.Judgments.All(v => v == "earned"); }
        private static T Pinned<T>(string root, string path, string hash)
        {
            OverlayObservationWire.UnderRun(root, path);
            if (!OverlayObservationWire.Hash(hash) || ExperimentFiles.Hash(path) != hash) throw new IOException("Reset evidence bytes changed.");
            return ExperimentFiles.Read<T>(path);
        }
        public static ResetOverlayTrialContext ReadContext(ExperimentRequest r)
        {
            ValidateRequest(r);
            if (!HasReset(r)) throw new IOException("Missing reset request.");
            if (ExperimentFiles.Hash(r.ResetOverlayContextPath) != r.ResetOverlayContextSha256) throw new IOException("Reset context bytes changed.");
            var c = ExperimentFiles.Read<ResetOverlayTrialContext>(r.ResetOverlayContextPath);
            if (c == null || c.Version != 2 || c.TrialId != r.ResetOverlayTrialId || c.OperationId != r.OperationId || c.HandoffNonce != r.Nonce ||
                !Id(c.ReadyOperationId) || c.ReadyOperationId == c.OperationId || c.MappingVersion != "level-clear-v1" ||
                c.AppId != 5218360 || c.AppId != r.AppId || c.SteamId == 0 || c.SteamId != r.SteamId ||
                !Identity(c.Origin) || !Identity(c.Steam) || !Same(c.Origin, r.Parent) || !Same(c.Steam, r.Steam) ||
                !Timestamp(c.ScopeConfirmedUtc, 0) || !OverlayObservationWire.Hash(c.ConfigurationSha256) || !OverlayObservationWire.Hash(c.ManifestSha256))
                throw new IOException("Invalid reset context identity/pins.");
            if (!string.Equals(Path.GetFullPath(r.ToolsDirectory), Path.GetFullPath(Path.Combine(Path.GetDirectoryName(c.Origin.Path), "RestartExperiment")), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFullPath(r.EvidenceDirectory), Path.GetFullPath(Path.Combine(c.Directory, "handoff-GameOnly")), StringComparison.OrdinalIgnoreCase)) throw new IOException("Reset request paths changed.");
            OverlayObservationWire.UnderRun(c.Directory, r.ResetOverlayContextPath);
            OverlayObservationWire.UnderRun(c.Directory, Path.Combine(r.EvidenceDirectory, "request.json"));
            if (ExperimentFiles.Hash(c.ConfigurationPath) != c.ConfigurationSha256) throw new IOException("Reset config changed.");
            var b = Pinned<ResetOverlaySdkBaseline>(c.Directory, c.BaselinePath, c.BaselineSha256);
            ValidateBaseline(b);
            if (b.TrialId != c.TrialId || b.ReadyOperationId != c.ReadyOperationId || b.MappingVersion != c.MappingVersion ||
                b.AppId != c.AppId || b.SteamId != c.SteamId || !Same(b.Process, c.Origin)) throw new IOException("Baseline/context mismatch.");
            var report = Pinned<ResetOverlayUserReport>(c.Directory, c.OriginReportPath, c.OriginReportSha256);
            ValidateReport(report, b, ResetOverlayRole.Initiator, c.Origin);
            if (report.BaselineSha256 != c.BaselineSha256 || !BaselineAgrees(report)) throw new IOException("Baseline screen report does not agree.");
            foreach (var pair in new[] { new[] { c.InitialFilesPath, c.InitialFilesSha256 }, new[] { c.PendingFilesPath, c.PendingFilesSha256 } })
            {
                OverlayObservationWire.UnderRun(c.Directory, pair[0]);
                if (!OverlayObservationWire.Hash(pair[1]) || ExperimentFiles.Hash(pair[0]) != pair[1]) throw new IOException("Participant manifest pin changed.");
            }
            return c;
        }
        public static void ClaimChildCreation(ExperimentRequest r)
        {
            ReadContext(r);
            using (var file = new FileStream(Path.Combine(r.EvidenceDirectory, "child-creation.claim"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { var bytes = Encoding.UTF8.GetBytes(r.Nonce); file.Write(bytes, 0, bytes.Length); file.Flush(true); }
        }
        public static ResetOverlayHelperCreation ReadCreation(ExperimentRequest r)
        {
            var row = ExperimentFiles.Read<ResetOverlayHelperCreation>(Path.Combine(r.EvidenceDirectory, "helper-created.json"));
            if (row == null || row.Version != 2 || row.Nonce != r.Nonce || !Same(row.Origin, r.Parent) ||
                row.Helper == null || row.Helper.Pid <= 0 || row.Helper.StartTicks <= 0 || string.IsNullOrWhiteSpace(row.Helper.Path) ||
                !WindowsIdentityCapture.SameScope(row.Helper, r.Parent)) throw new IOException("Owned helper creation record missing or invalid.");
            return row;
        }
    }
    public static class ResetOverlayReceipt
    {
        public static string PathFor(ExperimentRequest request) { return Path.Combine(request.EvidenceDirectory, "reset-overlay-child.json"); }
        public static void Write(ExperimentRequest request, ProcessIdentity child, string requestPath)
        {
            var c = ResetOverlayWire.ReadContext(request);
            var row = new ResetOverlayChildReceipt { Version = 2, Nonce = request.Nonce, TrialId = c.TrialId, OperationId = c.OperationId,
                ContextPath = request.ResetOverlayContextPath, ContextSha256 = request.ResetOverlayContextSha256, RequestSha256 = ExperimentFiles.Hash(requestPath),
                Role = request.ResetOverlayChildRole, AppId = c.AppId, SteamId = c.SteamId, MappingVersion = c.MappingVersion, Origin = c.Origin, Child = child };
            string path = PathFor(request), temporary = path + ".pending";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(ExperimentFiles.Json(row));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            File.Move(temporary, path);
        }
        public static bool Matches(ResetOverlayChildReceipt row, ExperimentRequest request, ProcessIdentity child, string requestPath)
        {
            var c = ResetOverlayWire.ReadContext(request);
            return row != null && row.Version == 2 && row.Nonce == request.Nonce && row.TrialId == c.TrialId && row.OperationId == c.OperationId &&
                row.ContextPath == request.ResetOverlayContextPath && row.ContextSha256 == request.ResetOverlayContextSha256 &&
                row.RequestSha256 == ExperimentFiles.Hash(requestPath) && row.Role == ResetOverlayRole.ResetWorker &&
                row.AppId == c.AppId && row.SteamId == c.SteamId && row.MappingVersion == c.MappingVersion &&
                ResetOverlayWire.Same(row.Origin, c.Origin) && ResetOverlayWire.Same(row.Child, child);
        }
    }

    public enum OverlayObservationRole { None, OriginObserver, ReplacementObserver }

    // Shared observation wire. Missing version remains zero and is rejected; no reset journal migration.
    public sealed class OverlayObservationContext
    {
        public int Version;
        public string RunId, Directory, ConfigurationPath, ConfigurationSha256, ManifestSha256, SaveManifestSha256;
        public string OperationId, ReadyState, MappingVersion;
        public string OriginObservationPath, OriginObservationSha256;
        public uint AppId;
        public ulong SteamId;
        public OverlayObservationRole Role;
        public ProcessIdentity Origin;
    }

    public sealed class OverlayUserObservation
    {
        public string RunId, Source, Visibility, Utc;
        public OverlayObservationRole Role;
        public ProcessIdentity Process;
        public bool AttemptReported;
        public double MonotonicSeconds;
    }

    public sealed class OverlayObservationReceipt
    {
        public int Version;
        public string Nonce, RunId, ContextPath, ContextSha256, ConfigurationSha256, ManifestSha256, OperationId, MappingVersion, ReadyState;
        public OverlayObservationRole Role;
        public uint AppId;
        public ulong SteamId;
        public ProcessIdentity Origin, Child;
    }

    public static class OverlayObservationWire
    {
        public static string ReceiptPath(ExperimentRequest request)
        { return System.IO.Path.Combine(request.EvidenceDirectory, "overlay-observation-child.json"); }
        public static bool HasObservation(ExperimentRequest request)
        {
            return request.OverlayObservationWireVersion != 0 || request.OverlayObservationChildRole != OverlayObservationRole.None ||
                request.OverlayObservationContextPath != null || request.OverlayObservationRunId != null || request.OverlayObservationContextSha256 != null;
        }
        public static void ValidateRequest(ExperimentRequest request)
        {
            if (!HasObservation(request)) return;
            Guid run, nonce;
            if (!Guid.TryParseExact(request.Nonce, "N", out nonce) || request.OverlayObservationWireVersion != 2 || request.Trial != Trial.GameOnly ||
                request.OverlayObservationChildRole != OverlayObservationRole.ReplacementObserver ||
                string.IsNullOrWhiteSpace(request.OverlayObservationContextPath) || !Hash(request.OverlayObservationContextSha256) ||
                !Guid.TryParseExact(request.OverlayObservationRunId, "N", out run) ||
                ResetOverlayWire.HasReset(request))
                throw new IOException("Invalid, partial or mixed observation request.");
        }
        public static bool Hash(string value)
        { return value != null && value.Length == 64 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')); }
        public static void ValidateContext(OverlayObservationContext context, OverlayObservationRole role)
        {
            Guid run, op;
            if (context == null || context.Version != 2 || context.Role != role ||
                (role != OverlayObservationRole.OriginObserver && role != OverlayObservationRole.ReplacementObserver) ||
                !Guid.TryParseExact(context.RunId, "N", out run) || !Guid.TryParseExact(context.OperationId, "N", out op) ||
                context.ReadyState != "Ready" || string.IsNullOrWhiteSpace(context.MappingVersion) ||
                string.IsNullOrWhiteSpace(context.Directory) || string.IsNullOrWhiteSpace(context.ConfigurationPath) ||
                !Hash(context.ConfigurationSha256) || !Hash(context.ManifestSha256) || !Hash(context.SaveManifestSha256) ||
                context.AppId != 5218360 || context.SteamId == 0 || context.Origin == null ||
                context.Origin.Pid <= 0 || context.Origin.StartTicks <= 0 || !Hash(context.Origin.Sha256) || string.IsNullOrEmpty(context.Origin.Path))
                throw new IOException("Invalid observation context.");
            if (role == OverlayObservationRole.OriginObserver)
            {
                if (context.OriginObservationPath != null || context.OriginObservationSha256 != null)
                    throw new IOException("Origin preparation cannot contain a future report reference.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(context.OriginObservationPath) || !Hash(context.OriginObservationSha256))
                    throw new IOException("Origin user report reference is required.");
                UnderRun(context.Directory, context.OriginObservationPath);
            }
        }
        public static string UnderRun(string root, string path)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(root) || !Path.IsPathRooted(path))
                throw new IOException("Absolute run/report paths are required.");
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Evidence is outside the run directory.");
            for (var dir = new DirectoryInfo(Path.GetDirectoryName(full)); dir != null && dir.FullName.Length >= prefix.TrimEnd(Path.DirectorySeparatorChar).Length; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point evidence path.");
            if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point evidence file.");
            return full;
        }
        public static void ValidateOriginReport(OverlayObservationContext context)
        {
            ValidateContext(context, OverlayObservationRole.ReplacementObserver);
            string path = UnderRun(context.Directory, context.OriginObservationPath);
            if (ExperimentFiles.Hash(path) != context.OriginObservationSha256) throw new IOException("Origin report bytes changed.");
            var row = ExperimentFiles.Read<OverlayUserObservation>(path);
            DateTimeOffset utc;
            if (row == null || row.RunId != context.RunId || row.Role != OverlayObservationRole.OriginObserver ||
                !Same(row.Process, context.Origin) || row.Source != "User" || !row.AttemptReported || row.Visibility != "opened" ||
                !DateTimeOffset.TryParse(row.Utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc) ||
                double.IsNaN(row.MonotonicSeconds) || double.IsInfinity(row.MonotonicSeconds) || row.MonotonicSeconds < 0)
                throw new IOException("Origin opened user report identity mismatch.");
        }
        public static OverlayObservationContext ReadContext(ExperimentRequest request)
        {
            ValidateRequest(request);
            if (ExperimentFiles.Hash(request.OverlayObservationContextPath) != request.OverlayObservationContextSha256)
                throw new IOException("Observation context bytes changed.");
            var context = ExperimentFiles.Read<OverlayObservationContext>(request.OverlayObservationContextPath);
            ValidateContext(context, OverlayObservationRole.ReplacementObserver);
            UnderRun(context.Directory, request.OverlayObservationContextPath);
            ValidateOriginReport(context);
            if (context.RunId != request.OverlayObservationRunId || context.AppId != request.AppId || context.SteamId != request.SteamId ||
                context.OperationId != request.OperationId || !Same(context.Origin, request.Parent))
                throw new IOException("Observation request/context identity mismatch.");
            return context;
        }
        private static bool Same(ProcessIdentity a, ProcessIdentity b)
        {
            return a != null && b != null && WindowsIdentityCapture.SameProcess(a, b) &&
                string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) && a.Sha256 == b.Sha256 && WindowsIdentityCapture.SameScope(a, b);
        }
        public static void WriteReceipt(ExperimentRequest request, ProcessIdentity child)
        {
            var c = ReadContext(request);
            var row = new OverlayObservationReceipt { Version = 2, Nonce = request.Nonce, RunId = c.RunId,
                ContextPath = request.OverlayObservationContextPath, ContextSha256 = request.OverlayObservationContextSha256, Role = OverlayObservationRole.ReplacementObserver,
                AppId = c.AppId, SteamId = c.SteamId, ConfigurationSha256 = c.ConfigurationSha256, ManifestSha256 = c.ManifestSha256,
                OperationId = c.OperationId, MappingVersion = c.MappingVersion, ReadyState = c.ReadyState, Origin = c.Origin, Child = child };
            var path = ReceiptPath(request);
            using (var stream = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(ExperimentFiles.Json(row));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            File.Move(path + ".pending", path);
        }
        public static ProcessIdentity CaptureChildAndWriteReceipt(ExperimentRequest request, Process process)
        {
            var child = WindowsIdentityCapture.Capture(process, true);
            WriteReceipt(request, child);
            return child;
        }
        public static bool Matches(OverlayObservationReceipt row, ExperimentRequest request, OverlayObservationContext c, ProcessIdentity child)
        {
            return row != null && row.Version == 2 && request.OverlayObservationWireVersion == 2 && c.Version == 2 && row.Nonce == request.Nonce && row.RunId == c.RunId &&
                row.ContextPath == request.OverlayObservationContextPath && row.ContextSha256 == request.OverlayObservationContextSha256 && row.Role == OverlayObservationRole.ReplacementObserver &&
                row.AppId == c.AppId && row.SteamId == c.SteamId && row.ConfigurationSha256 == c.ConfigurationSha256 &&
                row.ManifestSha256 == c.ManifestSha256 && row.OperationId == c.OperationId && row.MappingVersion == c.MappingVersion &&
                row.ReadyState == c.ReadyState && Same(row.Origin, c.Origin) && Same(row.Child, child);
        }
    }

    public sealed class ExperimentPrerequisites
    {
        public string SteamExeSha256, DllSha256, GateAEvidence, GateBEvidence;
        public bool ShutdownCommandVerified, ProbeContractReviewed, FailedInitExitReviewed, CallbackPumpReviewed;
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
        [DataMember(IsRequired = true)] public string RecordError;
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
        public string Nonce, Utc, Creation = "NotRequested", FailureStage, Error, CleanupError, CollectionError, RecordError, ResultError;
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
                ". See probe-attempts.jsonl for the SDK result and process diagnostics.",
                new IOException(ExperimentFiles.Json(attempt)))
        { }
    }

    public enum LaunchRole { Helper, Steam, Probe, FullCycleGame, GameOnlyGame }

    public static class LaunchEnvironment
    {
        public const int PolicyVersion = 2;
        public static ProcessStartInfo PrepareProbe(ExperimentRequest request, Func<ProcessStartInfo> host, Action checkClient, Action<string> record)
        {
            checkClient();
            var start = host();
            start.RedirectStandardInput = true; start.RedirectStandardOutput = true; start.RedirectStandardError = true;
            record(Apply(start, LaunchRole.Probe, request.AppId));
            return start;
        }
        public static ProcessStartInfo PrepareGame(ExperimentRequest request, string requestPath, Action checkGame, Action verifyFile,
            Action checkClient, Action<string> record)
        {
            ValidateChildRole(request);
            checkGame(); verifyFile(); checkClient();
            var start = new ProcessStartInfo { FileName = request.Parent.Path,
                Arguments = "-j2mPlatformProvider steam -j2mRestartObservation " + ExperimentFiles.Quote(requestPath),
                WorkingDirectory = System.IO.Path.GetDirectoryName(request.Parent.Path), UseShellExecute = false, CreateNoWindow = true };
            if (OverlayObservationWire.HasObservation(request))
            {
                start.Arguments = "-j2mPlatformProvider steam -j2mOverlayHandoffContext " + ExperimentFiles.Quote(request.OverlayObservationContextPath) +
                    " -j2mOverlayHandoffRequest " + ExperimentFiles.Quote(requestPath);
            }
            if (request.ResetOverlayChildRole != ResetOverlayRole.None)
            {
                start.Arguments += " -j2mResetOverlayContext " + ExperimentFiles.Quote(request.ResetOverlayContextPath) +
                    " -j2mResetOverlayPhase " + request.ResetOverlayChildRole.ToString();
            }

            record(Apply(start, request.Trial == Trial.GameOnly ? LaunchRole.GameOnlyGame : LaunchRole.FullCycleGame, request.AppId));
            return start;
        }

        public static ProcessStartInfo PrepareCompletedResetSubmission(ExperimentRequest request, string requestPath,
            Action checkGame, Action verifyFile, Action checkClient, Action<string> record)
        {
            CompletedResetProductWire.ValidateRequestPath(request, requestPath); checkGame(); verifyFile(); checkClient();
            var start = new ProcessStartInfo {
                FileName = request.Steam.Path,
                Arguments = "-applaunch " + request.AppId.ToString(CultureInfo.InvariantCulture) +
                    " -- -j2mCompletedParticipantReset " + ExperimentFiles.Quote(requestPath) +
                    " -j2mCompletedParticipantResetHash " + ExperimentFiles.Hash(requestPath),
                WorkingDirectory = Path.GetDirectoryName(request.Steam.Path), UseShellExecute = false, CreateNoWindow = true
            };
            record(Apply(start, LaunchRole.FullCycleGame, request.AppId));
            return start;
        }
        public static void ValidateChildRole(ExperimentRequest request)
        {
            OverlayObservationWire.ValidateRequest(request);
            ResetOverlayWire.ValidateRequest(request);
        }
        public static string Apply(ProcessStartInfo start, LaunchRole role, uint appId)
        {
            if (!Enum.IsDefined(typeof(LaunchRole), role)) throw new ArgumentOutOfRangeException("role");
            bool clean = role == LaunchRole.Steam || role == LaunchRole.Probe || role == LaunchRole.FullCycleGame;
            bool setId = role == LaunchRole.Probe || role == LaunchRole.FullCycleGame || role == LaunchRole.GameOnlyGame;
            var names = start.EnvironmentVariables.Keys.Cast<string>().Where(k => k.StartsWith("Steam", StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).ToArray();
            if (setId && appId == 0) throw new ArgumentOutOfRangeException("appId");
            if (clean) foreach (string key in names) start.EnvironmentVariables.Remove(key);
            string id = appId.ToString(CultureInfo.InvariantCulture);
            if (setId) { start.EnvironmentVariables["SteamAppId"] = id; start.EnvironmentVariables["SteamGameId"] = id; }
            if (clean && start.EnvironmentVariables.Keys.Cast<string>().Any(k => k.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) &&
                !(setId && (k == "SteamAppId" || k == "SteamGameId")))) throw new IOException("Steam environment cleanup failed.");
            bool match = !setId || (start.EnvironmentVariables["SteamAppId"] == id && start.EnvironmentVariables["SteamGameId"] == id);
            if (!match) throw new IOException("Steam AppID environment mismatch.");
            return "EnvironmentPolicy=" + PolicyVersion + ";Role=" + role + ";PresentNames=" + string.Join(",", names) + ";AppIdMatches=" + match;
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

    public sealed class ExperimentObservation
    {
        public string Utc, Nonce, Stage, Error, OperationId, Build;
        public long ElapsedMilliseconds;
        public ProcessIdentity Observer, Parent, OriginalSteam, NewSteam, Child;
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
        private ProcessIdentity currentSteam, child, shutdownCommand;
        private OwnedShutdownCommand ownedShutdownCommand;
        private int attempts;
        private bool completedResetSubmitted;
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
            if (!request.CompletedResetProduct && !System.IO.Path.GetFullPath(request.EvidenceDirectory).StartsWith(@"D:\J2M\evidence\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Evidence must be under D:\\J2M\\evidence.");
            VerifyFile(request.Parent); VerifyFile(request.Steam);
            if (!File.Exists(ExperimentFiles.PowerShell)) throw new FileNotFoundException("Windows PowerShell missing.");
            foreach (var name in new[] { "Restart-Experiment.ps1", "RestartExperiment.cs", "RestartExperimentWindows.cs", "RestartExperimentNativeProbe.cs" })
                if (!File.Exists(System.IO.Path.Combine(request.ToolsDirectory, name))) throw new FileNotFoundException(name);
            if (OverlayObservationWire.HasObservation(request)) OverlayObservationWire.ReadContext(request);
            if (ResetOverlayWire.HasReset(request)) ResetOverlayWire.ReadContext(request);
            if (request.CompletedResetProduct)
            {
                CompletedResetProductWire.ValidateRequestPath(request, requestPath);
                if (request.Trial != Trial.FullCycle || ExperimentFiles.Hash(request.DllPath) != ExperimentFiles.DllHash)
                    throw new InvalidOperationException("Completed-reset Steam probe payload mismatch.");
                return;
            }
            if (request.Trial == Trial.GameOnly) return;
            var prerequisites = ExperimentFiles.Read<ExperimentPrerequisites>(request.PrerequisitesPath);
            if (!prerequisites.ShutdownCommandVerified || prerequisites.SteamExeSha256 != request.Steam.Sha256)
                throw new InvalidOperationException("First verify this client's steam.exe -shutdown normally exits; record prerequisites.");
            if (request.Trial == Trial.Survival) return;
            if (!HasEvidence(prerequisites.GateAEvidence) || !prerequisites.ProbeContractReviewed ||
                !prerequisites.FailedInitExitReviewed || !prerequisites.CallbackPumpReviewed ||
                prerequisites.DllSha256 != ExperimentFiles.DllHash || ExperimentFiles.Hash(request.DllPath) != ExperimentFiles.DllHash)
                throw new InvalidOperationException("Gate A / SDK165 Init, failed-Init process exit, callbacks review missing.");
            if (request.Trial == Trial.FullCycle && !HasEvidence(prerequisites.GateBEvidence))
                throw new InvalidOperationException("Gate B native lifecycle/tracking evidence missing.");
        }

        private static bool HasEvidence(string path)
        {
            return !string.IsNullOrEmpty(path) && System.IO.Path.GetFullPath(path).StartsWith(@"D:\J2M\evidence\", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(path) && new FileInfo(path).Length > 0;
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
            return ownedShutdownCommand.Alive(code => Record("ShutdownCommandExited:ExitCode=" + code));
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
            Record(LaunchEnvironment.Apply(start, LaunchRole.Steam, request.AppId));
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
                    EnsureNewSteamUnchanged, Record);
            }, deadline, () => Milliseconds, request, attempt,
                row => Append("probes.jsonl", row), row => Append("probe-attempts.jsonl", row));
        }
        private void Append<T>(string name, T row)
        { File.AppendAllText(System.IO.Path.Combine(request.EvidenceDirectory, name), ExperimentFiles.Json(row) + Environment.NewLine); }

        // Convenience entry for harmless Windows child tests. Production supplies its absolute deadline.
        public static bool RunOwnedProbe(ProcessStartInfo start, int budgetMilliseconds, ExperimentRequest expected, Action<ProbeObservation> observe)
        {
            if (budgetMilliseconds <= 0 || budgetMilliseconds > 10000) throw new ArgumentOutOfRangeException("budgetMilliseconds");
            var clock = Stopwatch.StartNew();
            return RunOwnedProbe(() => start, new Deadline(() => clock.ElapsedMilliseconds, budgetMilliseconds, "Probe timed out; no further launch."),
                () => clock.ElapsedMilliseconds, expected, new ProbeAttempt { Attempt = 1, Nonce = expected.Nonce, Utc = DateTime.UtcNow.ToString("o") }, observe, null);
        }
        public static bool RunOwnedProbe(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ExperimentRequest expected, ProbeAttempt attempt, Action<ProbeObservation> observe, Action<ProbeAttempt> recordAttempt)
        { return RunOwnedProbe(prepare, deadline, clock, expected, attempt, observe, recordAttempt, new ProbeOperations()); }

        public static bool RunOwnedProbe(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ExperimentRequest expected, ProbeAttempt attempt, Action<ProbeObservation> observe, Action<ProbeAttempt> recordAttempt, ProbeOperations operations)
        { return RunOwnedProbeCore(prepare, deadline, clock, ProbeExpectation.FromLegacy(expected), attempt, observe, recordAttempt, operations, ParseProbe, expected.Nonce); }

        public static bool RunOwnedProbeCore(Func<ProcessStartInfo> prepare, Deadline deadline, Func<long> clock,
            ProbeExpectation expected, ProbeAttempt attempt, Action<ProbeObservation> observe, Action<ProbeAttempt> recordAttempt,
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
                    if (observe != null)
                    {
                        try { observe(row); }
                        catch (Exception e) { attempt.RecordError = e.ToString(); throw; }
                    }
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
            try { if (recordAttempt != null) recordAttempt(attempt); }
            catch (Exception e)
            {
                attempt.RecordError = JoinError(attempt.RecordError, e); attempt.Fail("AttemptRecord", e); attempt.ReadyObserved = false;
                // An independent file/host path retains the primary and recording errors if possible.
                try
                {
                    if (string.IsNullOrEmpty(expected.EvidenceDirectory)) throw new IOException("No fallback evidence directory.");
                    File.AppendAllText(System.IO.Path.Combine(expected.EvidenceDirectory, "probe-record-failures.jsonl"), ExperimentFiles.Json(attempt) + Environment.NewLine);
                }
                catch (Exception fallback) { attempt.RecordError = JoinError(attempt.RecordError, fallback); }
            }
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
                !string.IsNullOrEmpty(row.QueryError) || !string.IsNullOrEmpty(row.ShutdownError) || !string.IsNullOrEmpty(row.CleanupError) || !string.IsNullOrEmpty(row.RecordError))
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
            if (request.CompletedResetProduct)
            {
                using (var command = LaunchEnvironment.Start(() => LaunchEnvironment.PrepareCompletedResetSubmission(request, requestPath,
                    EnsureNoOtherGame, () => VerifyFile(request.Parent), EnsureNewSteamUnchanged, Record), deadline, Process.Start))
                {
                    if (command == null) throw new IOException("Completed-reset Steam submission failed.");
                    completedResetSubmitted = true;
                }
                return;
            }
            using (var process = LaunchEnvironment.Start(() =>
            {
                if (ResetOverlayWire.HasReset(request))
                {
                    ResetOverlayWire.ReadContext(request);
                    var creation = ResetOverlayWire.ReadCreation(request);
                    using (var self = Process.GetCurrentProcess())
                        if (!ResetOverlayWire.Same(creation.Helper, WindowsIdentityCapture.Capture(self, false))) throw new IOException("Helper creation owner mismatch.");
                }
                return LaunchEnvironment.PrepareGame(request, requestPath, EnsureNoOtherGame, () => VerifyFile(request.Parent), () =>
                {
                    if (request.Trial == Trial.GameOnly)
                    { if (!OriginalSteamAlive()) throw new IOException("Steam exited during GameOnly handoff."); }
                    else EnsureNewSteamUnchanged();
                }, Record);
            }, deadline, start => { if (ResetOverlayWire.HasReset(request)) { ResetOverlayWire.ClaimChildCreation(request); if (deadline != null) deadline.Remaining(); } return Process.Start(start); }))
            {
                if (process == null) throw new IOException("Game creation failed.");
                if (OverlayObservationWire.HasObservation(request))
                    child = OverlayObservationWire.CaptureChildAndWriteReceipt(request, process);
                else child = WindowsIdentityCapture.Capture(process, ResetOverlayWire.HasReset(request));
                if (request.ResetOverlayChildRole != ResetOverlayRole.None) ResetOverlayReceipt.Write(request, child, requestPath);
            }
        }
        public void Delay(int milliseconds)
        { if (milliseconds <= 0) throw new ArgumentOutOfRangeException("milliseconds"); Thread.Sleep(milliseconds); }
        public void Cleanup()
        {
            // Releasing this handle never terminates the Steam command.
            if (ownedShutdownCommand == null) return;
            Exception failure = null;
            try
            {
                if (!completedResetSubmitted)
                    Record("ShutdownCommandFinalObservation:Creation=" + ownedShutdownCommand.Creation +
                        ";Identity=" + (ownedShutdownCommand.Identity == null ? "Unknown" : ExperimentFiles.Json(ownedShutdownCommand.Identity)) +
                        ";ExitConfirmed=" + (ownedShutdownCommand.ExitConfirmed.HasValue ? ownedShutdownCommand.ExitConfirmed.ToString() : "Unknown") +
                        ";ExitCode=" + (ownedShutdownCommand.ExitCode.HasValue ? ownedShutdownCommand.ExitCode.ToString() : "Unknown"));
            }
            catch (Exception e) { failure = e; }
            try { ownedShutdownCommand.Dispose(); }
            catch (Exception e) { failure = failure == null ? e : new AggregateException(failure, e); }
            ownedShutdownCommand = null;
            if (failure != null) throw failure;
        }
        public void Record(string stage) { if (!completedResetSubmitted) WriteObservation(request, stage, null, Milliseconds, currentSteam, child); }
        public void RecordTerminal(Exception failure)
        {
            if (!completedResetSubmitted)
                WriteObservation(request, failure == null ? "HelperCompleted" : "HelperFailed", failure == null ? null : failure.ToString(), Milliseconds, currentSteam, child);
        }
        public static void WriteObservation(ExperimentRequest request, string stage, string error, long elapsed, ProcessIdentity steam, ProcessIdentity child)
        {
            using (var process = Process.GetCurrentProcess())
            {
                var row = new ExperimentObservation { Utc = DateTime.UtcNow.ToString("o"), Nonce = request.Nonce,
                    Stage = stage, Error = error, ElapsedMilliseconds = elapsed, OperationId = request.OperationId, Build = request.Build,
                    Observer = WindowsIdentityCapture.Capture(process, false), Parent = request.Parent, OriginalSteam = request.Steam, NewSteam = steam, Child = child };
                File.AppendAllText(System.IO.Path.Combine(request.EvidenceDirectory, "events-" + process.Id + ".jsonl"), ExperimentFiles.Json(row) + Environment.NewLine);
            }
        }
    }
}
