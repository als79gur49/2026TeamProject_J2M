// Shared with the Windows fake host. Unity-free, C# 5 compatible. No process creation or Steam API.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Reflection;

namespace Game.Exhibition.RestartExperiment
{
    public enum ObservationLaunchOwner { DirectExe = 1, SteamDelegated = 2 }
    public sealed class ObservationV3Options
    {
        public OverlayObservationRole Role;
        public ObservationLaunchOwner Owner;
        public string Request, RequestHash, Context, ContextHash, Endpoint;
        public static bool Present(string[] args)
        { return (args ?? new string[0]).Any(a => a != null && a.StartsWith("-j2mOverlayV3", StringComparison.OrdinalIgnoreCase)); }
        public static ObservationV3Options Parse(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var keys = new[] { "-j2mOverlayV3Role", "-j2mOverlayV3Owner", "-j2mOverlayV3Request",
                "-j2mOverlayV3RequestHash", "-j2mOverlayV3Context", "-j2mOverlayV3ContextHash", "-j2mPlatformProvider" };
            args = args ?? new string[0];
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (i == 0 && a != null && !a.StartsWith("-", StringComparison.Ordinal)) continue; // executable only
                if (a == null || !keys.Contains(a) || values.ContainsKey(a) || ++i >= args.Length ||
                    string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("-", StringComparison.Ordinal))
                    throw new IOException("InvalidObservationV3Arguments");
                values.Add(a, args[i]);
            }
            Func<string, string> get = k => values.ContainsKey(k) ? values[k] : null;
            var result = new ObservationV3Options();
            if (get("-j2mPlatformProvider") != "steam") throw new IOException("ExactlyOneSteamProviderRequired");
            string role = get("-j2mOverlayV3Role"), owner = get("-j2mOverlayV3Owner");
            if (role != "OriginObserver" && role != "ReplacementObserver") throw new IOException("InvalidObservationRole");
            if (owner != "DirectExe" && owner != "SteamDelegated") throw new IOException("InvalidLaunchOwner");
            result.Role = (OverlayObservationRole)Enum.Parse(typeof(OverlayObservationRole), role);
            result.Owner = (ObservationLaunchOwner)Enum.Parse(typeof(ObservationLaunchOwner), owner);
            result.Request = get("-j2mOverlayV3Request"); result.RequestHash = get("-j2mOverlayV3RequestHash");
            result.Context = get("-j2mOverlayV3Context"); result.ContextHash = get("-j2mOverlayV3ContextHash");
            if (result.Role == OverlayObservationRole.OriginObserver)
            {
                if (values.Count != 3) throw new IOException("MixedObservationRole");
            }
            else
            {
                if (values.Count != 7) throw new IOException("PartialReplacementArguments");
                ObservationV3Wire.Absolute(result.Request); ObservationV3Wire.Absolute(result.Context);
                ObservationV3Wire.Hash(result.RequestHash); ObservationV3Wire.Hash(result.ContextHash);
            }
            return result;
        }
    }
    public sealed class ObservationV3Request
    {
        public int Version = 3;
        public string Purpose = "OverlayObservation", RunId, Nonce, Directory, ConfigurationHash, ManifestHash,
            OperationId, MappingVersion, ReadyState, ParticipantHash, OriginVisibilityHash, OriginDisplayHash, BaselineHash;
        public Trial Cycle = Trial.FullCycle;
        public ObservationLaunchOwner Owner;
        public uint AppId;
        public ulong SteamId;
        public string[] Targets;
        public ProcessIdentity Origin, OriginalSteam;
        public string Policy = "FullCycle-30-60-120-Probe10-Handoff30-v1";
    }
    public sealed class ObservationV3HelperCreation
    {
        public int Version = 3;
        public string RequestHash;
        public ProcessIdentity Origin, Helper;
    }
    public sealed class ObservationV3Client
    {
        public int Version = 3;
        public string RequestHash;
        public ProcessIdentity Original, Current, Probe;
        public ObservationV3HelperCreation Creation;
        public ProcessIdentity Helper { get { return Creation.Helper; } }
        public bool OriginalExited, ShutdownCommandExited, ProbeShutdownReturned, ProbeExited, LoggedOn;
        public uint AppId;
        public ulong SteamId;
    }
    public sealed class ObservationV3Context
    {
        public int Version = 3;
        public string RunId, Nonce, RequestHash, ClientHash, Endpoint;
        public OverlayObservationRole Role = OverlayObservationRole.ReplacementObserver;
        public ObservationLaunchOwner Owner;
    }
    public sealed class ObservationV3Hello
    {
        public int Version = 3;
        public string RunId, Nonce, RequestHash, ContextHash, EffectiveArgumentsHash;
        public ProcessIdentity Child;
    }
    public sealed class ObservationV3Receipt
    {
        public int Version = 3;
        public string RunId, Nonce, RequestHash, ContextHash, ClientHash, Challenge, EffectiveArgumentsHash;
        public ProcessIdentity Origin, Helper, Child, NewClient;
        public long AcceptedAt;
    }
    public sealed class ObservationV3Ack
    {
        public int Version = 3;
        public string ReceiptHash, Challenge;
        public ProcessIdentity Child, Helper, NewClient;
        public uint AppId;
        public ulong SteamId;
        public bool NativeReady, LoggedOn;
    }
    public sealed class ObservationV3DisplayReport
    {
        public int Version = 3;
        public string RunId, VisibilityHash, Source = "User", ReportedUtc, ObservedUtc, OriginalText, CorrectsHash;
        public OverlayObservationRole Role;
        public ProcessIdentity Process;
        public ObservationV3TargetDisplay[] Targets;
    }
    public sealed class ObservationV3Event
    {
        public string RunId, Nonce, ContextHash, Stage, Error, HelperExit, SteamTracking;
        public ProcessIdentity Child;
        public bool ChildExited;
    }
    public sealed class ObservationV3TargetDisplay { public string Id, Display; }
    public static class ObservationV3Wire
    {
        public static void Require(bool condition, string reason) { if (!condition) throw new IOException(reason); }
        public static void Hash(string value) { Require(OverlayObservationWire.Hash(value), "InvalidHash"); }
        public static void Id(string value) { Guid g; Require(Guid.TryParseExact(value, "N", out g), "InvalidRunOrNonce"); }
        public static void Absolute(string value)
        { Require(!string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) && !value.Contains(".."), "AbsoluteEvidencePathRequired"); }
        public static void Endpoint(string value)
        { Require(value != null && value.StartsWith("j2m-observation-v3-", StringComparison.Ordinal), "InvalidEndpoint"); Id(value == null ? null : value.Substring(19)); }
        public static void Identity(ProcessIdentity p)
        {
            Require(p != null && p.Pid > 0 && p.StartTicks > 0 && p.Session >= 0 && !string.IsNullOrWhiteSpace(p.UserSid) &&
                !string.IsNullOrWhiteSpace(p.Logon), "InvalidProcessIdentity");
            Absolute(p.Path); Hash(p.Sha256);
        }
        public static bool Same(ProcessIdentity a, ProcessIdentity b) { return ResetOverlayWire.Same(a, b); }
        public static void SameFileScope(ProcessIdentity a, ProcessIdentity b)
        {
            Identity(a); Identity(b);
            Require(string.Equals(a.Path, b.Path, StringComparison.OrdinalIgnoreCase) && a.Sha256 == b.Sha256 &&
                WindowsIdentityCapture.SameScope(a, b), "FileOrScopeMismatch");
        }
        public static string Serialize(object value)
        {
            if (value == null) throw new ArgumentNullException("value");
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        public static string Digest(object value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Serialize(value)))).Replace("-", "").ToLowerInvariant();
        }
        public static T Parse<T>(string json)
        {
            // DCS otherwise silently ignores unknown fields, including a mixed reset/v2 payload.
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            Require(bytes.Length <= 1048576, "DocumentTooLarge");
            var document = new XmlDocument();
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, XmlDictionaryReaderQuotas.Max)) document.Load(reader);
            ValidateFields(document.DocumentElement, typeof(T));
            return ExperimentFiles.Parse<T>(json);
        }
        private static void ValidateFields(XmlElement node, Type type)
        {
            if (node.GetAttribute("type") == "null") return;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum) return;
            if (type.IsArray)
            {
                foreach (XmlNode item in node.ChildNodes) if (item is XmlElement) ValidateFields((XmlElement)item, type.GetElementType());
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlNode item in node.ChildNodes)
            {
                var member = item as XmlElement; if (member == null) continue;
                var field = type.GetField(member.Name, BindingFlags.Instance | BindingFlags.Public);
                Require(field != null && seen.Add(member.Name), "UnknownOrDuplicateWireField");
                ValidateFields(member, field.FieldType);
            }
        }
        public static T Copy<T>(T value) { return ExperimentFiles.Parse<T>(Serialize(value)); }
        public static void Validate(ObservationV3Request r)
        {
            Require(r != null && r.Version == 3 && r.Purpose == "OverlayObservation" && r.Cycle == Trial.FullCycle &&
                Enum.IsDefined(typeof(ObservationLaunchOwner), r.Owner) && r.Policy == "FullCycle-30-60-120-Probe10-Handoff30-v1", "InvalidV3Request");
            Id(r.RunId); Id(r.Nonce); Id(r.OperationId); Absolute(r.Directory);
            Require(r.AppId == 5218360 && r.SteamId != 0 && r.ReadyState == "Ready" && !string.IsNullOrWhiteSpace(r.MappingVersion), "InvalidReadyIdentity");
            foreach (string hash in new[] { r.ConfigurationHash, r.ManifestHash, r.ParticipantHash, r.OriginVisibilityHash, r.OriginDisplayHash, r.BaselineHash }) Hash(hash);
            Require(r.Targets != null && r.Targets.Length > 0 && r.Targets.Distinct().Count() == r.Targets.Length &&
                r.Targets.All(t => ResetOverlayWire.Names().Contains(t)), "InvalidRequestTargets");
            Identity(r.Origin); Identity(r.OriginalSteam);
            Require(!Same(r.OriginalSteam, r.Origin) && WindowsIdentityCapture.SameScope(r.Origin, r.OriginalSteam), "InvalidOriginScope");
        }
        public static void ValidateClient(ObservationV3Request r, ObservationV3Client c)
        {
            Validate(r);
            Require(c != null && c.Version == 3 && c.RequestHash == Digest(r) && c.OriginalExited && c.ShutdownCommandExited &&
                c.ProbeExited && c.ProbeShutdownReturned && c.LoggedOn && c.AppId == r.AppId && c.SteamId == r.SteamId && Same(c.Original, r.OriginalSteam), "InvalidNewClientEvidence");
            Require(c.Creation != null && c.Creation.Version == 3 && c.Creation.RequestHash == Digest(r) && Same(c.Creation.Origin, r.Origin), "HelperCreationMissing");
            Identity(c.Helper);
            Require(!Same(c.Helper, r.Origin) && !Same(c.Helper, r.OriginalSteam) && WindowsIdentityCapture.SameScope(c.Helper, r.Origin), "HelperScopeMismatch");
            SameFileScope(c.Original, c.Current); Identity(c.Probe);
            Require(!Same(c.Original, c.Current) && c.Current.StartTicks > c.Original.StartTicks &&
                WindowsIdentityCapture.SameScope(c.Probe, c.Current) && !Same(c.Probe, c.Current), "NewClientRequired");
        }
        public static void ValidateContext(ObservationV3Request r, ObservationV3Client c, ObservationV3Context x)
        {
            ValidateClient(r, c);
            Require(x != null && x.Version == 3 && x.RunId == r.RunId && x.Nonce == r.Nonce && x.RequestHash == Digest(r) &&
                x.ClientHash == Digest(c) && x.Owner == r.Owner && x.Role == OverlayObservationRole.ReplacementObserver, "InvalidLaunchContext");
            Endpoint(x.Endpoint); Require(x.Endpoint == "j2m-observation-v3-" + r.Nonce, "EndpointRunMismatch");
        }
        public static void ValidateDisplay(ObservationV3DisplayReport report, ObservationV3Request request, OverlayUserObservation visibility,
            ProcessIdentity expected, string[] targets)
        {
            Validate(request);
            Require(report != null && report.Version == 3 && visibility != null && report.RunId == request.RunId && visibility.RunId == request.RunId &&
                report.Role == visibility.Role && (report.Role == OverlayObservationRole.OriginObserver || report.Role == OverlayObservationRole.ReplacementObserver) &&
                Same(report.Process, expected) && Same(visibility.Process, expected) && report.VisibilityHash == Digest(visibility) &&
                report.Source == "User" && visibility.Source == "User" && visibility.Visibility == "opened" && visibility.AttemptReported, "DisplayAttributionFailed");
            DateTimeOffset utc;
            Require(DateTimeOffset.TryParse(report.ReportedUtc, out utc) && !string.IsNullOrWhiteSpace(report.OriginalText), "UserReportRequired");
            Require(report.ObservedUtc == null || DateTimeOffset.TryParse(report.ObservedUtc, out utc), "InvalidObservationTime");
            if (report.CorrectsHash != null) Hash(report.CorrectsHash);
            Require(targets != null && targets.OrderBy(t => t).SequenceEqual(request.Targets.OrderBy(t => t)), "TargetSetChanged");
            Require(targets != null && targets.Length > 0 && targets.Distinct().Count() == targets.Length && targets.All(t => ResetOverlayWire.Names().Contains(t)), "InvalidTargetSet");
            Require(report.Targets != null && report.Targets.Length == targets.Length && report.Targets.All(t => t != null) &&
                report.Targets.Select(t => t.Id).OrderBy(t => t).SequenceEqual(targets.OrderBy(t => t)) &&
                report.Targets.All(t => t.Display == "earned" || t.Display == "unearned" || t.Display == "inconclusive"), "InvalidTargetDisplay");
        }
    }

    // Each document is create-only. Evidence is never an automatic recovery or resume input.
    public sealed class ObservationV3Store
    {
        private readonly string root;
        public ObservationV3Store(string root) { ObservationV3Wire.Absolute(root); this.root = root; }
        private string PathFor(string name)
        {
            ObservationV3Wire.Require(name != null && Path.GetFileName(name) == name && name.EndsWith(".json", StringComparison.Ordinal), "InvalidEvidenceName");
            return OverlayObservationWire.UnderRun(root, Path.Combine(root, name));
        }
        public string Create(string name, object value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ObservationV3Wire.Serialize(value));
            using (var f = new FileStream(PathFor(name), FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes, 0, bytes.Length); f.Flush(true); }
            return ObservationV3Wire.Digest(value);
        }
        public T Read<T>(string name, string hash)
        {
            ObservationV3Wire.Hash(hash);
            byte[] bytes = File.ReadAllBytes(PathFor(name));
            using (var sha = SHA256.Create()) ObservationV3Wire.Require(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() == hash, "EvidenceHashMismatch");
            return ObservationV3Wire.Parse<T>(Encoding.UTF8.GetString(bytes));
        }
    }
}
