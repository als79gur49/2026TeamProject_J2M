using System;
using System.IO;
using System.Linq;
using Game.Exhibition.RestartExperiment;
using Newtonsoft.Json;

namespace Game.Exhibition.Integration
{
    public sealed class ObservationSaveFile { public string Path, Sha256; }
    public sealed class ObservationSaveManifest { public ObservationSaveFile[] Files; }

    public static class OverlayObservationEvidence
    {
        public static string Json(object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.None);
        }

        public static double MonotonicSeconds() => (double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency;
        public static string Limit(string value) => value == null || value.Length <= 2048 ? value : value.Substring(0, 2048);

        // No repository Load: those APIs can clean temporary files or restore backups.
        public static ObservationSaveManifest CaptureSave(string root)
        {
            var full = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
            var files = Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories) : Array.Empty<string>();
            return new ObservationSaveManifest { Files = files.Where(p =>
                System.IO.Path.GetFileName(p).IndexOf(".json", StringComparison.OrdinalIgnoreCase) >= 0 ||
                p.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".backup", StringComparison.OrdinalIgnoreCase) ||
                p.EndsWith(".rollback", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Select(p => new ObservationSaveFile {
                    Path = p.Substring(full.Length).Replace('\\', '/'), Sha256 = ExperimentFiles.Hash(p) }).ToArray() };
        }

        public static void RequireSame(ObservationSaveManifest expected, ObservationSaveManifest actual)
        {
            if (expected?.Files == null || actual?.Files == null ||
                !expected.Files.Select(f => f.Path + ":" + f.Sha256).SequenceEqual(actual.Files.Select(f => f.Path + ":" + f.Sha256)))
                throw new IOException("Participant file set or bytes changed. No recovery was attempted.");
        }

        public static string Under(string root, string path) => OverlayObservationWire.UnderRun(root, path);
    }
}
