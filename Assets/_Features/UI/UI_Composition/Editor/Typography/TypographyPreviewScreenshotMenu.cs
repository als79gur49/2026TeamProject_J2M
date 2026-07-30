using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public static class TypographyPreviewScreenshotMenu
    {
        [MenuItem("Game/UI/Typography/Capture Required Preview Screenshots")]
        public static void CaptureRequiredPreviewScreenshots()
        {
            var result = TypographyPreviewScreenshotUtility.CaptureRequiredScreenshots();
            LogResult(result);
            result.ThrowIfFailed();
        }

        public static void CaptureRequiredPreviewScreenshotsFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArg(args, "-typographyScreenshotOutput");
            var options = ReadOptions(args);

            ThrowIfUnityLogConflictsWithManifest(args, outputDirectory);
            var result = TypographyPreviewScreenshotUtility.CaptureRequiredScreenshots(outputDirectory, options);
            LogResult(result);
            result.ThrowIfFailed();
        }

        public static void CaptureRequiredPreviewScreenshotSliceFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArg(args, "-typographyScreenshotOutput");
            var targetName = ReadArg(args, "-typographyScreenshotTarget");
            var localeCode = ReadArg(args, "-typographyScreenshotLocale");
            var targets = TypographyPreviewScreenshotUtility.RequiredTargets;
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var target = targets.SingleOrDefault(candidate =>
                    string.Equals(candidate.FileStem, targetName, StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(target.FileStem))
                {
                    throw new InvalidOperationException(
                        $"-typographyScreenshotTarget must be one of: " +
                        $"{string.Join(", ", targets.Select(candidate => candidate.FileStem))}.");
                }

                targets = new[] { target };
            }

            if (!TypographyThemeValidator.RequiredLocaleCodes.Contains(localeCode, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"-typographyScreenshotLocale must be one of: " +
                    $"{string.Join(", ", TypographyThemeValidator.RequiredLocaleCodes)}.");
            }

            var result = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                targets,
                new[] { localeCode },
                outputDirectory,
                ReadOptions(args));
            LogResult(result);
            result.ThrowIfFailed();

            CaptureClimateDiagnosticsForSlice(targetName, localeCode, outputDirectory, ReadOptions(args));
        }

        public static void ReconstructCanonicalManifestFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArg(args, "-typographyScreenshotOutput");
            var result = TypographyPreviewScreenshotManifestUtility.ReconstructCanonicalManifest(outputDirectory);
            LogResult(result);
            result.ThrowIfFailed();
        }

        public static void CaptureM2bPreviewScreenshotSliceFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var outputDirectory = ReadArg(args, "-typographyScreenshotOutput");
            var targetName = ReadArg(args, "-typographyScreenshotTarget");
            var localeCode = ReadArg(args, "-typographyScreenshotLocale");
            var target = TypographyPreviewScreenshotUtility.M2bDiagnosticTargets.SingleOrDefault(
                candidate => string.Equals(candidate.FileStem, targetName, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(target.FileStem))
            {
                throw new InvalidOperationException(
                    $"-typographyScreenshotTarget must be one of: " +
                    $"{string.Join(", ", TypographyPreviewScreenshotUtility.M2bDiagnosticTargets.Select(candidate => candidate.FileStem))}.");
            }

            if (!TypographyThemeValidator.RequiredLocaleCodes.Contains(localeCode, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"-typographyScreenshotLocale must be one of: " +
                    $"{string.Join(", ", TypographyThemeValidator.RequiredLocaleCodes)}.");
            }

            var result = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                new[] { target },
                new[] { localeCode },
                System.IO.Path.Combine(outputDirectory, "Diagnostics"),
                ReadOptions(args));
            LogResult(result);
            result.ThrowIfFailed();
        }

        private static void CaptureClimateDiagnosticsForSlice(
            string targetName,
            string localeCode,
            string outputDirectory,
            TypographyPreviewScreenshotOptions options)
        {
            if ((string.Equals(localeCode, "en-US", StringComparison.Ordinal) &&
                 string.IsNullOrWhiteSpace(targetName)) ||
                (string.Equals(localeCode, "ko-KR", StringComparison.Ordinal) &&
                 string.Equals(targetName, "MainMenu", StringComparison.Ordinal)))
            {
                var saveSlotDiagnosticResult = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                    TypographyPreviewScreenshotUtility.M1bDiagnosticTargets
                        .Concat(TypographyPreviewScreenshotUtility.M2aDiagnosticTargets),
                    new[] { localeCode },
                    System.IO.Path.Combine(outputDirectory, "Diagnostics"),
                    options);
                LogResult(saveSlotDiagnosticResult);
                saveSlotDiagnosticResult.ThrowIfFailed();
            }

            if (!string.Equals(localeCode, "ko-KR", StringComparison.Ordinal))
            {
                return;
            }

            TypographyPreviewScreenshotTarget[] targets;
            if (string.Equals(targetName, "Settings", StringComparison.Ordinal))
            {
                targets = TypographyPreviewScreenshotUtility.ClimateDiagnosticTargets
                    .Where(target =>
                        string.Equals(target.FileStem, "SettingsAudioMuted", StringComparison.Ordinal) ||
                        string.Equals(target.FileStem, "SettingsDisplayStatus", StringComparison.Ordinal))
                    .ToArray();
            }
            else if (string.Equals(targetName, "Pause", StringComparison.Ordinal))
            {
                targets = TypographyPreviewScreenshotUtility.ClimateDiagnosticTargets
                    .Where(target => string.Equals(target.FileStem, "ConfirmPopup", StringComparison.Ordinal))
                    .ToArray();
            }
            else
            {
                return;
            }

            var result = TypographyPreviewScreenshotUtility.CaptureScreenshots(
                targets,
                new[] { localeCode },
                System.IO.Path.Combine(outputDirectory, "Diagnostics"),
                options);
            LogResult(result);
            result.ThrowIfFailed();
        }

        private static void ThrowIfUnityLogConflictsWithManifest(string[] args, string outputDirectory)
        {
            var unityLogPath = ReadArg(args, "-logFile");
            if (string.IsNullOrWhiteSpace(unityLogPath) || string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }

            var manifestPath = System.IO.Path.Combine(
                outputDirectory,
                TypographyPreviewScreenshotManifestUtility.ManifestFileName);
            if (string.Equals(
                    System.IO.Path.GetFullPath(unityLogPath),
                    System.IO.Path.GetFullPath(manifestPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Unity stdout -logFile must not use the canonical manifest path. " +
                    "Use a split/raw name such as capture-unity.log; capture.log is written by the screenshot utility.");
            }
        }

        private static TypographyPreviewScreenshotOptions ReadOptions(string[] args)
        {
            var options = new TypographyPreviewScreenshotOptions();
            if (int.TryParse(ReadArg(args, "-typographyScreenshotWidth"), out var width))
            {
                options.Width = width;
            }

            if (int.TryParse(ReadArg(args, "-typographyScreenshotHeight"), out var height))
            {
                options.Height = height;
            }

            return options;
        }

        private static void LogResult(TypographyPreviewScreenshotBatchResult result)
        {
            var lines = new[]
                {
                    "Typography preview screenshot capture completed.",
                    $"Output: {result.OutputDirectory}",
                    $"Manifest: {System.IO.Path.Combine(result.OutputDirectory, TypographyPreviewScreenshotManifestUtility.ManifestFileName)}",
                    "| Target | Locale | File | Exists | Size | Typography Bindings | Localized Texts |",
                    "|---|---|---|---|---|---|---|",
                }
                .Concat(result.Captures.Select(capture =>
                    $"| {capture.Target.Name} | {capture.LocaleCode} | {capture.FilePath} | {capture.Exists} | {capture.FileSizeBytes} | {capture.AppliedBindingCount} | {capture.LocalizedTextAppliedCount} | " +
                    $"PixelProof={capture.M1bPixelProofPassCount}/{capture.M1bPixelProofCount} |"))
                .Concat(result.Errors.Select(error => $"ERROR: {error}"))
                .Concat(result.Captures.SelectMany(capture => capture.Errors.Select(error => $"ERROR: {error}")));

            var message = string.Join(Environment.NewLine, lines);
            if (result.HasErrors)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private static string ReadArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
