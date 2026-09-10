// Runtime-1 shared source: no Unity or native API. C# 5 compatible.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Game.Exhibition.RestartExperiment
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class ObservationNullableAttribute : Attribute { }
    public sealed class ObservationRef { public string Path, Sha256; }
    public class ObservationDocument
    {
        public int Version;
        public string ProtocolRevision, Kind, Author, WrittenUtc;
        public long Sequence;
    }
    public class ObservationRunDocument : ObservationDocument { public string RunId; }
    public sealed class ObservationV3OriginConfig : ObservationDocument
    {
        public uint AppId;
        public ulong ExpectedSteamId;
        public string[] Targets;
        public string EvidenceRoot;
        public ObservationRef LaunchPlanRef;
    }
    public sealed class ObservationV3LaunchPlan : ObservationDocument
    {
        public uint AppId;
        public string PayloadRoot, WorkingDirectory;
        public ObservationRef SteamExe, GameExe, NativeDll;
    }
    public sealed class ObservationV3ParticipantSnapshot : ObservationRunDocument
    {
        public string Root;
        public ObservationRef[] Files;
    }
    public sealed class ObservationV3ReadySnapshot : ObservationRunDocument
    {
        public ObservationRef Journal;
        public string OperationId, MappingVersion, State;
        public uint AppId;
        public ulong SteamId;
    }
    public sealed class ObservationV3OriginPreparation : ObservationRunDocument
    {
        public string Nonce;
        public ObservationRef ConfigRef, LaunchPlanRef, ReadyRef, ParticipantSnapshotRef;
        public ProcessIdentity Self, OriginalSteam;
        public string[] Targets;
    }
    public sealed class ObservationV3PinEvidence : ObservationRunDocument
    {
        public ObservationRef PreparationRef;
        public ProcessIdentity Self, Client;
        public long Monotonic;
        public string Phase;
    }
    public sealed class ObservationV3AchievementValue
    {
        public string Target;
        public bool ReadSucceeded;
        [ObservationNullable] public bool? Achieved;
    }
    public sealed class ObservationV3Baseline : ObservationRunDocument
    {
        public ObservationRef PreparationRef, BeforeRef, AfterRef;
        public ProcessIdentity Origin, OriginalSteam;
        public uint AppId;
        public ulong ExpectedSteamId, ObservedSteamId;
        public bool LoggedOn;
        public long Started, Finished;
        public ObservationV3AchievementValue[] Targets;
    }
    public sealed class ObservationV3Visibility : ObservationRunDocument
    {
        public ObservationRef BindingRef, BaselineRef;
        [ObservationNullable] public ObservationRef ClaimRef;
        [ObservationNullable] public ObservationRef CorrectsRef;
        public ProcessIdentity Self;
        public string Role, Visibility, OriginalText;
        [ObservationNullable] public string ObservedUtc;
    }
    public sealed class ObservationV3Display : ObservationRunDocument
    {
        public ObservationRef BindingRef, BaselineRef, VisibilityRef;
        [ObservationNullable] public ObservationRef ClaimRef;
        [ObservationNullable] public ObservationRef CorrectsRef;
        public ProcessIdentity Self;
        public string Role, OriginalText;
        [ObservationNullable] public string ObservedUtc;
        public ObservationV3TargetDisplay[] Targets;
    }
    public sealed class ObservationV3RuntimeRequest : ObservationRunDocument
    {
        public string Nonce, Directory, Owner;
        public ObservationRef PreparationRef, ConfigRef, LaunchPlanRef, ReadyRef,
            ParticipantSnapshotRef, BaselineRef, OriginVisibilityRef, OriginDisplayRef;
    }
    public sealed class ObservationV3Creation : ObservationRunDocument
    {
        public ObservationRef RequestRef, AttemptRef;
        public ProcessIdentity Origin, Helper;
    }
    public sealed class ObservationV3Grant : ObservationRunDocument
    {
        public ObservationRef RequestRef, CreationRef;
        public string Nonce;
        public ProcessIdentity Origin, ExpectedHelper;
    }
    public sealed class ObservationV3BootstrapReady : ObservationRunDocument
    {
        public ObservationRef RequestRef, GrantRef;
        public ProcessIdentity Origin, Helper;
        public bool OriginHandleRetained;
    }
    public sealed class ObservationV3ProbeInput : ObservationRunDocument
    {
        public ObservationRef RequestRef, CreationRef, GrantRef, NativeDll;
        public ProcessIdentity Client, Helper;
        public uint AppId;
        public ulong SteamId;
        public string Nonce, WorkingDirectory;
    }
    public sealed class ObservationV3ProbeResult : ObservationRunDocument
    {
        public ObservationRef InputRef;
        public ProcessIdentity Self, Client;
        public ProbeObservation Observation;
    }
    public sealed class ObservationV3NewClient : ObservationRunDocument
    {
        public ObservationRef RequestRef, CreationRef, GrantRef, ProbeInputRef, ProbeResultRef;
        public ProcessIdentity Original, Current, Probe, ShutdownCommand;
        public bool OriginalExited, ShutdownCommandExited, ProbeExited;
    }
    public sealed class ObservationV3RuntimeContext : ObservationRunDocument
    {
        public ObservationRef RequestRef, ClientRef, LaunchPlanRef;
        public string Nonce, Role, Owner, Endpoint;
    }
    public sealed class ObservationV3IndependentContext : ObservationRunDocument
    {
        public ObservationRef RequestRef, ClientRef, LaunchPlanRef;
        public string Nonce, Role, Owner;
        public long PreparedTimestamp, DeadlineTimestamp, TimestampFrequency;
    }
    public sealed class ObservationV3ReplacementClaim : ObservationRunDocument
    {
        public ObservationRef ContextRef;
        public ProcessIdentity Self, Client;
        public string EffectiveArgumentsHash, WorkingDirectory;
        public string[] Tokens;
    }
    public sealed class ObservationV3ReplacementStarted : ObservationRunDocument
    {
        public ObservationRef ContextRef, ClaimRef;
        public ProcessIdentity Self, Client;
        public bool NativeReady;
        public uint AppId;
        public ulong SteamId;
    }
    public sealed class ObservationV3SubmissionIntent : ObservationRunDocument
    {
        public ObservationRef ContextRef;
        public ProcessIdentity Helper;
        public string[] Tokens;
    }
    // Both Unity/Mono and the PowerShell CLR read the same Windows uptime API.
    // Do not compare per-runtime Stopwatch timestamp units across processes.
    // The pinned Steam process identity excludes a context from an earlier boot.
    public static class ObservationV3LaunchWindow
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern ulong GetTickCount64();
        public static long Now { get { return checked((long)GetTickCount64()); } }
        public static long Frequency { get { return 1000; } }
        public static void RequireOpen(ObservationV3IndependentContext context, long now, long frequency)
        {
            ObservationV3RuntimeWire.Require(context.TimestampFrequency == frequency && frequency > 0 &&
                context.PreparedTimestamp >= 0 && context.DeadlineTimestamp > context.PreparedTimestamp &&
                context.DeadlineTimestamp - context.PreparedTimestamp == checked(frequency * 30) &&
                now >= context.PreparedTimestamp && now < context.DeadlineTimestamp, "ReplacementStartDeadlineExceeded");
        }
    }
    public sealed class ObservationV3RuntimeHello : ObservationRunDocument
    {
        public ObservationRef ContextRef;
        public ProcessIdentity Self;
        public string EffectiveArgumentsHash, WorkingDirectory;
        public string[] Tokens;
    }
    public sealed class ObservationV3RuntimeReceipt : ObservationRunDocument
    {
        public ObservationRef ContextRef;
        public ProcessIdentity Child, Helper, Client;
        public string Challenge, EffectiveArgumentsHash;
    }
    public sealed class ObservationV3RuntimeAck : ObservationRunDocument
    {
        public ObservationRef ReceiptRef;
        public ProcessIdentity Child, Helper, Client;
        public string Challenge;
        public uint AppId;
        public ulong SteamId;
        public bool LoggedOn, NativeReady;
    }
    public sealed class ObservationV3Checkpoint
    {
        public string State;
        public long Sequence;
        [ObservationNullable] public ObservationRef ReportRef;
    }
    public sealed class ObservationV3ExitRequest { public long Boundary; public ObservationV3Checkpoint Checkpoint; }
    public sealed class ObservationV3ExitAuthorized { public long Boundary; public ObservationV3Checkpoint Checkpoint; }
    public sealed class ObservationV3ExitCompleted { public long Boundary; public ObservationV3Checkpoint Checkpoint; public ObservationRef LifecycleRef; }
    public sealed class ObservationV3Stored { public long ReportSequence; public ObservationRef ReportRef; }
    public sealed class ObservationV3Envelope : ObservationRunDocument
    {
        public ObservationRef ContextRef, ReceiptRef;
        public string Role, Body;
        public ProcessIdentity Self;
    }
    public sealed class ObservationV3ArgumentEvidence : ObservationRunDocument
    { public ProcessIdentity Self; public string SemanticHash; public string[] ChildTokens; }
    public sealed class ObservationV3Submission : ObservationRunDocument
    {
        public ObservationRef ContextRef;
        public ProcessIdentity Launcher;
        public string Disposition;
    }
    public sealed class ObservationV3Lifecycle : ObservationRunDocument
    {
        public bool RuntimePresent, InitializationAttempted, InitializationSucceeded;
        public int ShutdownCallCount;
        public string RuntimeState, StartupState, Failure;
        [ObservationNullable] public ObservationV3InvocationFact[] Invocations;
        [ObservationNullable] public ObservationRef ContextRef, ReceiptRef;
        [ObservationNullable] public ObservationRef ClaimRef;
        public ProcessIdentity Self;
        public bool ShutdownReturned;
        public long ShutdownStarted, ShutdownFinished, IdentityObservationCount, LastIdentityObservation;
    }
    public sealed class ObservationV3Terminal : ObservationRunDocument
    {
        public string Phase, Outcome, FirstError, ChildExit, HelperExit, Tracking;
        [ObservationNullable] public ObservationV3InvocationFact[] Invocations;
        public bool PurposeAchieved;
        public ObservationRef BindingRef;
    }
    // Immutable bytes/hash carrier. Typed values are fresh parses, never a replacement for the bytes digest.
    public sealed class ObservationPinned<T>
    {
        private readonly byte[] bytes;
        private readonly string path, sha256;
        public ObservationRef Ref { get { return new ObservationRef { Path = path, Sha256 = sha256 }; } }
        internal ObservationPinned(byte[] bytes, ObservationRef reference) { this.bytes = (byte[])bytes.Clone(); path = reference.Path; sha256 = reference.Sha256; }
        public T Value { get { return ObservationV3RuntimeWire.Parse<T>(bytes); } }
    }
    public static class ObservationV3RuntimeWire
    {
        private static readonly Dictionary<Type, string> DocumentKinds = new Dictionary<Type, string> {
            { typeof(ObservationV3OriginConfig), "origin-config" },
            { typeof(ObservationV3LaunchPlan), "launch-plan" },
            { typeof(ObservationV3ParticipantSnapshot), "participant-snapshot" },
            { typeof(ObservationV3ReadySnapshot), "ready-snapshot" },
            { typeof(ObservationV3OriginPreparation), "origin-preparation" },
            { typeof(ObservationV3PinEvidence), "pin-validation" },
            { typeof(ObservationV3Baseline), "baseline" },
            { typeof(ObservationV3Visibility), "visibility-report" },
            { typeof(ObservationV3Display), "display-report" },
            { typeof(ObservationV3RuntimeRequest), "request" },
            { typeof(ObservationV3Creation), "helper-creation" },
            { typeof(ObservationV3Grant), "bootstrap-grant" },
            { typeof(ObservationV3BootstrapReady), "bootstrap-ready" },
            { typeof(ObservationV3ProbeInput), "probe-input" },
            { typeof(ObservationV3ProbeResult), "probe-result" },
            { typeof(ObservationV3NewClient), "new-client" },
            { typeof(ObservationV3RuntimeContext), "context" },
            { typeof(ObservationV3IndependentContext), "independent-context" },
            { typeof(ObservationV3ReplacementClaim), "replacement-claim" },
            { typeof(ObservationV3ReplacementStarted), "replacement-started" },
            { typeof(ObservationV3SubmissionIntent), "submission-intent" },
            { typeof(ObservationV3RuntimeHello), "hello" },
            { typeof(ObservationV3RuntimeReceipt), "receipt" },
            { typeof(ObservationV3RuntimeAck), "ack" },
            { typeof(ObservationV3Lifecycle), "native-lifecycle" }
        };
        public const string Revision = "observation-v3-runtime-3";
        public const string Policy = "FullCycle-30-60-120-Probe10-Handoff30-v1";
        public static void Require(bool value, string reason) { if (!value) throw new IOException(reason); }
        public static T Stamp<T>(T doc, string kind, string author, long sequence, string run) where T : ObservationDocument
        {
            doc.Version = 3; doc.ProtocolRevision = Revision; doc.Kind = kind; doc.Author = author;
            doc.WrittenUtc = DateTime.UtcNow.ToString("o"); doc.Sequence = sequence;
            var r = doc as ObservationRunDocument; if (r != null) r.RunId = run;
            return doc;
        }
        public static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return ExperimentFiles.Hex(sha.ComputeHash(bytes)); }
        public static byte[] Bytes(object value) { return new UTF8Encoding(false, true).GetBytes(ObservationV3Wire.Serialize(value)); }
        public static ObservationV3Terminal ReadTerminal(string root, string run)
        {
            var failed = Path.Combine(root, "terminal-failure.json");
            var value = ReadInitial<ObservationV3Terminal>(File.Exists(failed) ? failed : Path.Combine(root, "terminal.json"), root).Value;
            Require(value.RunId == run, "TerminalRunMismatch");
            if (File.Exists(failed)) Require(value.Outcome == "Uncertain" && !value.PurposeAchieved, "InvalidFailureTerminal");
            return value;
        }
        public static void ValidateLifecycle(ObservationV3Lifecycle value, ObservationV3Envelope frame)
        {
            Require(value.RunId == frame.RunId && value.Author == "ReplacementObserver" && frame.Role == "ReplacementObserver" &&
                SameRef(value.ContextRef, frame.ContextRef) && SameRef(value.ReceiptRef, frame.ReceiptRef) && ObservationV3Wire.Same(value.Self, frame.Self), "LifecycleOwnerMismatch");
            Require(value.RuntimePresent && value.InitializationAttempted && value.InitializationSucceeded && value.ShutdownCallCount == 1 &&
                value.ShutdownReturned && value.RuntimeState == "Shutdown" && value.Failure == "None" &&
                value.ShutdownFinished >= value.ShutdownStarted && value.IdentityObservationCount > 0 && value.LastIdentityObservation <= value.ShutdownStarted,
                "LifecycleCleanupUnverified");
        }
        public static T Parse<T>(byte[] bytes)
        {
            Require(bytes != null && bytes.Length > 0 && bytes.Length <= 1048576, "InvalidDocumentSize");
            var xml = new XmlDocument();
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, new XmlDictionaryReaderQuotas { MaxDepth = 32, MaxStringContentLength = 1048576, MaxArrayLength = 1048576 }))
            { xml.Load(reader); }
            ValidateNode(xml.DocumentElement, typeof(T), false);
            T value;
            using (var stream = new MemoryStream(bytes)) value = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
            var doc = value as ObservationDocument;
            if (doc != null)
            {
                Require(doc.Version == 3 && doc.ProtocolRevision == Revision && doc.Sequence > 0 &&
                    !string.IsNullOrWhiteSpace(doc.Kind) && !string.IsNullOrWhiteSpace(doc.Author), "RuntimeRevisionOrHeaderMismatch");
                string expected;
                if (DocumentKinds.TryGetValue(typeof(T), out expected)) Require(doc.Kind == expected, "DocumentKindMismatch");
                DateTimeOffset date; Require(DateTimeOffset.TryParse(doc.WrittenUtc, out date), "InvalidDocumentTime");
                var run = doc as ObservationRunDocument; if (run != null) ObservationV3Wire.Id(run.RunId);
            }
            return value;
        }
        private static void ValidateNode(XmlElement node, Type type, bool nullable)
        {
            Require(node != null, "MissingJsonValue");
            if (node.GetAttribute("type") == "null") { Require(nullable, "NullRequiredField"); return; }
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string)) { Require(node.GetAttribute("type") == "string", "ExpectedString"); return; }
            if (type.IsPrimitive || type.IsEnum)
            { Require(node.GetAttribute("type") == (type == typeof(bool) ? "boolean" : "number"), "ExpectedPrimitive"); return; }
            if (type.IsArray)
            {
                Require(node.GetAttribute("type") == "array", "ExpectedArray");
                foreach (XmlElement item in node.ChildNodes) ValidateNode(item, type.GetElementType(), false);
                return;
            }
            Require(node.GetAttribute("type") == "object", "ExpectedObject");
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlElement item in node.ChildNodes)
            {
                var field = fields.FirstOrDefault(f => f.Name == item.Name);
                Require(field != null && seen.Add(item.Name), "UnknownOrDuplicateField");
                // ProbeObservation is a legacy native result nested in a strict runtime envelope;
                // optional diagnostics stay optional, but every field must still be explicitly present.
                bool optional = field.IsDefined(typeof(ObservationNullableAttribute), false) ||
                    (type == typeof(ProbeObservation) && field.FieldType == typeof(string));
                ValidateNode(item, field.FieldType, optional);
            }
            Require(fields.All(f => seen.Contains(f.Name)), "MissingRequiredField");
        }
        public static string Canonical(string path, string root)
        {
            Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path), "AbsolutePathRequired");
            string full = Path.GetFullPath(path);
            Require(string.Equals(full, path, StringComparison.OrdinalIgnoreCase), "CanonicalPathRequired");
            if (root != null)
            {
                string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Require(full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "PathOutsideAllowedRoot");
            }
            for (string p = full; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
                if (File.Exists(p) || Directory.Exists(p)) Require((File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0, "ReparsePathRejected");
            return full;
        }
        public static string EvidenceRoot(string root)
        {
            Canonical(root, null);
            string allowed = Environment.OSVersion.Platform == PlatformID.Win32NT ? @"D:\J2M\evidence" : "/mnt/d/J2M/evidence";
            Require(string.Equals(root, allowed, StringComparison.OrdinalIgnoreCase) || root.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "DEvidenceRequired");
            return root;
        }
        public static bool SameRef(ObservationRef a, ObservationRef b)
        { return a != null && b != null && a.Sha256 == b.Sha256 && string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase); }
        public static ObservationPinned<T> Read<T>(ObservationRef reference, string root)
        {
            Require(reference != null, "ReferenceRequired"); ObservationV3Wire.Hash(reference.Sha256);
            byte[] bytes = File.ReadAllBytes(Canonical(reference.Path, root));
            Require(Hash(bytes) == reference.Sha256, "ReferencedBytesChanged");
            Parse<T>(bytes); return new ObservationPinned<T>(bytes, new ObservationRef { Path = reference.Path, Sha256 = reference.Sha256 });
        }
        public static ObservationPinned<T> ReadInitial<T>(string path, string root)
        {
            byte[] bytes = File.ReadAllBytes(Canonical(path, root)); Parse<T>(bytes);
            return new ObservationPinned<T>(bytes, new ObservationRef { Path = path, Sha256 = Hash(bytes) });
        }
        public static ObservationRef Create(string root, string name, object value)
        { return CreateBytes(root, name, Bytes(value)); }
        public static ObservationRef CreateBytes(string root, string name, byte[] bytes)
        {
            Require(Path.GetFileName(name) == name && name.EndsWith(".json", StringComparison.Ordinal), "InvalidEvidenceName");
            string path = Canonical(Path.Combine(root, name), root);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            string hash = Hash(bytes); Require(ExperimentFiles.Hash(path) == hash, "EvidenceWriteHashMismatch");
            return new ObservationRef { Path = path, Sha256 = hash };
        }
        public static void FilePin(ObservationRef reference, string root)
        {
            Require(reference != null, "FilePinRequired"); ObservationV3Wire.Hash(reference.Sha256);
            Require(ExperimentFiles.Hash(Canonical(reference.Path, root)) == reference.Sha256, "FilePinChanged");
        }
        public static void Targets(string[] actual, string[] expected)
        {
            Require(actual != null && actual.Length > 0 && actual.Distinct(StringComparer.Ordinal).Count() == actual.Length &&
                actual.All(t => ResetOverlayWire.Names().Contains(t)) &&
                (expected == null || actual.OrderBy(t => t, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(t => t, StringComparer.Ordinal))), "TargetSetMismatch");
        }
        public static void Baseline(ObservationV3Baseline b, ObservationRef preparation, ObservationV3OriginPreparation p, ObservationV3OriginConfig c)
        {
            Require(SameRef(b.PreparationRef, preparation) && b.RunId == p.RunId && b.AppId == c.AppId && b.LoggedOn &&
                b.ExpectedSteamId == c.ExpectedSteamId && b.ObservedSteamId == c.ExpectedSteamId && b.Started <= b.Finished &&
                ObservationV3Wire.Same(b.Origin, p.Self) && ObservationV3Wire.Same(b.OriginalSteam, p.OriginalSteam), "BaselineAttributionMismatch");
            Targets(b.Targets.Select(t => t.Target).ToArray(), c.Targets);
            Require(b.Targets.All(t => t.ReadSucceeded && t.Achieved.HasValue), "BaselineReadFailed");
        }
        public static void BaselinePins(ObservationV3Baseline b, ObservationV3OriginPreparation p, ObservationRef preparation,
            ObservationV3PinEvidence before, ObservationV3PinEvidence after)
        {
            foreach (var pin in new[] { before, after })
                Require(pin.RunId == p.RunId && SameRef(pin.PreparationRef, preparation) &&
                    ObservationV3Wire.Same(pin.Self, p.Self) && ObservationV3Wire.Same(pin.Client, p.OriginalSteam), "BaselinePinAttributionMismatch");
            Require(before.Phase == "baseline-before" && after.Phase == "baseline-after" && before.Monotonic >= 0 &&
                before.Monotonic <= b.Started && after.Monotonic >= b.Finished &&
                DateTimeOffset.Parse(before.WrittenUtc) <= DateTimeOffset.Parse(after.WrittenUtc), "BaselinePinOrderMismatch");
        }
        public static void ValidateOriginVisibility(ObservationV3Visibility v, ObservationRef prep, ObservationV3OriginPreparation p, ObservationRef baseline)
        { Visibility(v, prep, baseline, p.RunId, p.Self, "OriginObserver"); }
        public static void ValidateOriginDisplay(ObservationV3Display d, ObservationRef prep, ObservationV3OriginPreparation p,
            ObservationRef baseline, ObservationRef visibility, ObservationV3Visibility v)
        { Display(d, prep, baseline, visibility, v, p.RunId, p.Self, "OriginObserver", p.Targets); }
        private static void ObservationTime(string value)
        { DateTimeOffset parsed; Require(value == null || DateTimeOffset.TryParse(value, out parsed), "InvalidObservedUtc"); }
        public static void Visibility(ObservationV3Visibility v, ObservationRef binding, ObservationRef baseline, string run, ProcessIdentity self, string role)
        {
            ObservationTime(v.ObservedUtc);
            Require(v.RunId == run && v.Role == role && ObservationV3Wire.Same(v.Self, self) && SameRef(v.BindingRef, binding) &&
                SameRef(v.BaselineRef, baseline) && new[] { "opened", "not-visible", "inconclusive" }.Contains(v.Visibility) &&
                !string.IsNullOrWhiteSpace(v.OriginalText), "InvalidVisibilityAttribution");
        }
        public static void Display(ObservationV3Display d, ObservationRef binding, ObservationRef baseline, ObservationRef visibility,
            ObservationV3Visibility v, string run, ProcessIdentity self, string role, string[] targets)
        {
            ObservationTime(d.ObservedUtc);
            Require(d.RunId == run && d.Role == role && ObservationV3Wire.Same(d.Self, self) && SameRef(d.BindingRef, binding) &&
                SameRef(d.BaselineRef, baseline) && SameRef(d.VisibilityRef, visibility) && v.Visibility == "opened" && !string.IsNullOrWhiteSpace(d.OriginalText), "InvalidDisplayAttribution");
            Targets(d.Targets.Select(t => t.Id).ToArray(), targets);
            Require(d.Targets.All(t => new[] { "earned", "unearned", "inconclusive" }.Contains(t.Display)), "InvalidDisplayValue");
        }
    }
    public static class ObservationV3Arguments
    {
        public static string[] Generated(ObservationV3Options options, bool provider)
        {
            var a = new List<string> { "-j2mOverlayV3Role", options.Role.ToString(), "-j2mOverlayV3Owner", options.Owner.ToString() };
            if (options.Role == OverlayObservationRole.ReplacementObserver) a.AddRange(new[] { "-j2mOverlayV3Request", options.Request, "-j2mOverlayV3RequestHash", options.RequestHash,
                "-j2mOverlayV3Context", options.Context, "-j2mOverlayV3ContextHash", options.ContextHash });
            if (provider) a.AddRange(new[] { "-j2mPlatformProvider", "steam" });
            return a.ToArray();
        }
        public static string SemanticHash(string[] args)
        {
            ObservationV3Options.Parse(args);
            int offset = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal) ? 1 : 0;
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (int i = offset; i < args.Length; i += 2) values.Add(args[i], args[i + 1]);
            // Length prefixes make the hash independent of quoting, order and separator characters in values.
            var b = new StringBuilder();
            foreach (var pair in values) { b.Append(pair.Key.Length).Append(':').Append(pair.Key).Append(pair.Value.Length).Append(':').Append(pair.Value); }
            return ObservationV3RuntimeWire.Hash(Encoding.UTF8.GetBytes(b.ToString()));
        }
        public static string Quote(string arg)
        {
            if (arg == null || arg.IndexOf('\0') >= 0) throw new IOException("InvalidArgument");
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1).Append(c);
                else b.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
        // Strict supported Windows CRT quoting subset; rejects unbalanced quotes rather than repairing.
        public static string[] Tokenize(string line)
        {
            if (line == null || line.IndexOf('\0') >= 0) throw new IOException("CommandLineUnavailable");
            var result = new List<string>(); int i = 0;
            while (i < line.Length)
            {
                while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;
                if (i == line.Length) break;
                bool quoted = false; var b = new StringBuilder();
                while (i < line.Length && (quoted || (line[i] != ' ' && line[i] != '\t')))
                {
                    int slashes = 0; while (i < line.Length && line[i] == '\\') { slashes++; i++; }
                    if (i < line.Length && line[i] == '"')
                    {
                        b.Append('\\', slashes / 2);
                        if (slashes % 2 != 0) b.Append('"'); else quoted = !quoted;
                        i++;
                    }
                    else { b.Append('\\', slashes); if (i < line.Length) b.Append(line[i++]); }
                }
                if (quoted) throw new IOException("UnsupportedWindowsQuoting");
                result.Add(b.ToString());
            }
            return result.ToArray();
        }
    }
}
