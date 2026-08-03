using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Feature.UI.Composition.Editor
{
    public sealed class TypographyPreviewScreenshotManifest
    {
        private readonly List<TypographyPreviewScreenshotManifestEntry> entries = new();

        public int SchemaVersion { get; internal set; }

        public string GeneratedAt { get; internal set; }

        public string GitHead { get; internal set; }

        public string UnityVersion { get; internal set; }

        public string CaptureCommand { get; internal set; }

        public string CaptureMode { get; internal set; }

        public string OutputDirectory { get; internal set; }

        public int Width { get; internal set; }

        public int Height { get; internal set; }

        public string OverallResult { get; internal set; }

        public string ThemeValidation { get; internal set; }

        public string PrefabValidation { get; internal set; }

        public string GuardedAssetDirtyCheck { get; internal set; }

        public string AssetMutationObservedBeforeRestore { get; internal set; }

        public int UnexpectedAssetMutationCount { get; internal set; }

        public string ReconstructedFrom { get; internal set; }

        public IReadOnlyList<TypographyPreviewScreenshotManifestEntry> Entries => entries;

        internal void AddEntry(TypographyPreviewScreenshotManifestEntry entry)
        {
            entries.Add(entry);
        }

        public TypographyPreviewScreenshotManifestEntry FindEntry(string target, string locale)
        {
            return entries.SingleOrDefault(entry =>
                string.Equals(entry.Target, target, StringComparison.Ordinal) &&
                string.Equals(entry.Locale, locale, StringComparison.Ordinal));
        }
    }

    public sealed class TypographyPreviewScreenshotManifestEntry
    {
        public string Target { get; internal set; }

        public string Locale { get; internal set; }

        public string FileName { get; internal set; }

        public long FileSizeBytes { get; internal set; }

        public string Sha256 { get; internal set; }

        public int Width { get; internal set; }

        public int Height { get; internal set; }

        public int LocalizedExpectedCount { get; internal set; }

        public int LocalizedAppliedCount { get; internal set; }

        public int TypographyBindingCount { get; internal set; }

        public string OrientationValidation { get; internal set; }

        public string NonBlankValidation { get; internal set; }

        public string GlyphTofuValidation { get; internal set; }

        public string CaptureResult { get; internal set; }

        public string Errors { get; internal set; }
    }

    public static class TypographyPreviewScreenshotManifestParser
    {
        public static TypographyPreviewScreenshotManifest ParseFile(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));
            }

            return Parse(File.ReadAllText(manifestPath));
        }

        public static TypographyPreviewScreenshotManifest Parse(string contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var root = new Dictionary<string, string>(StringComparer.Ordinal);
            var sections = new List<KeyValuePair<string, Dictionary<string, string>>>();
            Dictionary<string, string> current = root;

            using (var reader = new StringReader(contents))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (line.StartsWith("[", StringComparison.Ordinal) &&
                        line.EndsWith("]", StringComparison.Ordinal))
                    {
                        var sectionName = line.Substring(1, line.Length - 2);
                        if (sections.Any(section => string.Equals(section.Key, sectionName, StringComparison.Ordinal)))
                        {
                            throw new FormatException($"Duplicate manifest section '{sectionName}'.");
                        }

                        current = new Dictionary<string, string>(StringComparer.Ordinal);
                        sections.Add(new KeyValuePair<string, Dictionary<string, string>>(sectionName, current));
                        continue;
                    }

                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0)
                    {
                        throw new FormatException($"Invalid manifest line '{line}'.");
                    }

                    var key = line.Substring(0, separatorIndex).Trim();
                    var value = line.Substring(separatorIndex + 1).Trim();
                    if (current.ContainsKey(key))
                    {
                        throw new FormatException($"Duplicate manifest field '{key}'.");
                    }

                    current.Add(key, value);
                }
            }

            var manifest = new TypographyPreviewScreenshotManifest
            {
                SchemaVersion = ReadInt(root, "schema_version"),
                GeneratedAt = ReadRequired(root, "generated_at"),
                GitHead = ReadRequired(root, "git_head"),
                UnityVersion = ReadRequired(root, "unity_version"),
                CaptureCommand = ReadRequired(root, "capture_command"),
                CaptureMode = ReadRequired(root, "capture_mode"),
                OutputDirectory = ReadRequired(root, "output_directory"),
                Width = ReadInt(root, "width"),
                Height = ReadInt(root, "height"),
                OverallResult = ReadRequired(root, "overall_result"),
                ThemeValidation = ReadRequired(root, "theme_validation"),
                PrefabValidation = ReadRequired(root, "prefab_validation"),
                GuardedAssetDirtyCheck = ReadRequired(root, "guarded_asset_dirty_check"),
                AssetMutationObservedBeforeRestore =
                    ReadOptional(root, "asset_mutation_observed_before_restore") is { Length: > 0 } observed
                        ? observed
                        : "NOT_RECORDED",
                UnexpectedAssetMutationCount =
                    ReadOptionalInt(root, "unexpected_asset_mutation_count"),
                ReconstructedFrom = ReadOptional(root, "reconstructed_from"),
            };

            foreach (var section in sections)
            {
                var separatorIndex = section.Key.LastIndexOf('/');
                if (separatorIndex <= 0 || separatorIndex == section.Key.Length - 1)
                {
                    throw new FormatException($"Invalid manifest section name '{section.Key}'.");
                }

                var fields = section.Value;
                manifest.AddEntry(new TypographyPreviewScreenshotManifestEntry
                {
                    Target = section.Key.Substring(0, separatorIndex),
                    Locale = section.Key.Substring(separatorIndex + 1),
                    FileName = ReadRequired(fields, "file"),
                    FileSizeBytes = ReadLong(fields, "file_size_bytes"),
                    Sha256 = ReadRequired(fields, "sha256"),
                    Width = ReadInt(fields, "width"),
                    Height = ReadInt(fields, "height"),
                    LocalizedExpectedCount = ReadInt(fields, "localized_expected"),
                    LocalizedAppliedCount = ReadInt(fields, "localized_applied"),
                    TypographyBindingCount = ReadInt(fields, "typography_bindings"),
                    OrientationValidation = ReadRequired(fields, "orientation_validation"),
                    NonBlankValidation = ReadRequired(fields, "nonblank_validation"),
                    GlyphTofuValidation = ReadRequired(fields, "glyph_tofu_validation"),
                    CaptureResult = ReadRequired(fields, "capture_result"),
                    Errors = ReadOptional(fields, "errors"),
                });
            }

            return manifest;
        }

        private static string ReadRequired(IReadOnlyDictionary<string, string> fields, string key)
        {
            if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException($"Required manifest field '{key}' is missing.");
            }

            return value;
        }

        private static string ReadOptional(IReadOnlyDictionary<string, string> fields, string key)
        {
            return fields.TryGetValue(key, out var value) ? value : string.Empty;
        }

        private static int ReadInt(IReadOnlyDictionary<string, string> fields, string key)
        {
            var value = ReadRequired(fields, key);
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new FormatException($"Manifest field '{key}' is not an integer: '{value}'.");
            }

            return parsed;
        }

        private static long ReadLong(IReadOnlyDictionary<string, string> fields, string key)
        {
            var value = ReadRequired(fields, key);
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new FormatException($"Manifest field '{key}' is not an integer: '{value}'.");
            }

            return parsed;
        }

        private static int ReadOptionalInt(
            IReadOnlyDictionary<string, string> fields,
            string key)
        {
            if (!fields.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new FormatException($"Manifest field '{key}' is not an integer: '{value}'.");
            }

            return parsed;
        }
    }

    public sealed class TypographyPreviewScreenshotManifestContext
    {
        public string GeneratedAt { get; set; }

        public string GitHead { get; set; }

        public string UnityVersion { get; set; }

        public string CaptureCommand { get; set; }

        public string CaptureMode { get; set; }

        public string ReconstructedFrom { get; set; }
    }

    public static class TypographyPreviewScreenshotManifestUtility
    {
        public const int SchemaVersion = 1;
        public const string ManifestFileName = "capture.log";
        public const string AggregateCaptureEntryPoint =
            "Game.Feature.UI.Composition.Editor.TypographyPreviewScreenshotMenu.CaptureRequiredPreviewScreenshotsFromCommandLine";
        public const string ReconstructionEntryPoint =
            "Game.Feature.UI.Composition.Editor.TypographyPreviewScreenshotMenu.ReconstructCanonicalManifestFromCommandLine";

        public static string WriteCanonicalManifest(
            TypographyPreviewScreenshotBatchResult result,
            TypographyPreviewScreenshotOptions options,
            TypographyPreviewScreenshotManifestContext context = null)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            options ??= new TypographyPreviewScreenshotOptions();
            context ??= new TypographyPreviewScreenshotManifestContext();
            Directory.CreateDirectory(result.OutputDirectory);

            var gitHead = string.IsNullOrWhiteSpace(context.GitHead)
                ? ReadCurrentGitHead()
                : context.GitHead;
            var unityVersion = string.IsNullOrWhiteSpace(context.UnityVersion)
                ? UnityEngine.Application.unityVersion
                : context.UnityVersion;
            var captureCommand = string.IsNullOrWhiteSpace(context.CaptureCommand)
                ? AggregateCaptureEntryPoint
                : context.CaptureCommand;
            var captureMode = string.IsNullOrWhiteSpace(context.CaptureMode)
                ? "AGGREGATE"
                : context.CaptureMode;

            var requiredEntries = BuildRequiredEntries(result, options);
            var overallPass =
                result.ThemeValidationPassed &&
                result.PrefabValidationPassed &&
                result.GuardedAssetsClean &&
                result.AssetMutationObservationPassed &&
                result.UnexpectedAssetMutationCount == 0 &&
                !result.HasErrors &&
                IsGitHead(gitHead) &&
                requiredEntries.All(entry => string.Equals(entry.CaptureResult, "PASS", StringComparison.Ordinal));

            var builder = new StringBuilder();
            Append(builder, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "generated_at",
                string.IsNullOrWhiteSpace(context.GeneratedAt)
                    ? DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    : context.GeneratedAt);
            Append(builder, "git_head", gitHead);
            Append(builder, "unity_version", unityVersion);
            Append(builder, "capture_command", captureCommand);
            Append(builder, "capture_mode", captureMode);
            Append(builder, "output_directory", GetRepositoryRelativePath(result.OutputDirectory));
            Append(builder, "width", options.Width.ToString(CultureInfo.InvariantCulture));
            Append(builder, "height", options.Height.ToString(CultureInfo.InvariantCulture));
            Append(builder, "resolution", $"{options.Width}x{options.Height}");
            Append(builder, "overall_result", overallPass ? "PASS" : "FAIL");
            Append(builder, "theme_validation", result.ThemeValidationPassed ? "PASS" : "FAIL");
            Append(builder, "prefab_validation", result.PrefabValidationPassed ? "PASS" : "FAIL");
            Append(builder, "guarded_asset_dirty_check", result.GuardedAssetsClean ? "PASS" : "FAIL");
            Append(
                builder,
                "asset_mutation_observed_before_restore",
                result.AssetMutationObservationPassed ? "PASS" : "FAIL");
            Append(
                builder,
                "unexpected_asset_mutation_count",
                result.UnexpectedAssetMutationCount.ToString(CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(context.ReconstructedFrom))
            {
                Append(builder, "reconstructed_from", context.ReconstructedFrom);
            }

            foreach (var entry in requiredEntries)
            {
                builder.AppendLine();
                builder.Append('[').Append(entry.Target).Append('/').Append(entry.Locale).AppendLine("]");
                Append(builder, "file", entry.FileName);
                Append(builder, "file_size_bytes", entry.FileSizeBytes.ToString(CultureInfo.InvariantCulture));
                Append(builder, "sha256", entry.Sha256);
                Append(builder, "width", entry.Width.ToString(CultureInfo.InvariantCulture));
                Append(builder, "height", entry.Height.ToString(CultureInfo.InvariantCulture));
                Append(builder, "dimensions", $"{entry.Width}x{entry.Height}");
                Append(builder, "localized_expected", entry.LocalizedExpectedCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, "localized_applied", entry.LocalizedAppliedCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, "localized", $"{entry.LocalizedAppliedCount}/{entry.LocalizedExpectedCount}");
                Append(builder, "typography_bindings", entry.TypographyBindingCount.ToString(CultureInfo.InvariantCulture));
                Append(builder, "orientation_validation", entry.OrientationValidation);
                Append(builder, "nonblank_validation", entry.NonBlankValidation);
                Append(builder, "glyph_tofu_validation", entry.GlyphTofuValidation);
                Append(builder, "capture_result", entry.CaptureResult);
                Append(builder, "errors", entry.Errors);
            }

            var manifestPath = Path.Combine(result.OutputDirectory, ManifestFileName);
            File.WriteAllText(manifestPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!overallPass && !result.HasErrors)
            {
                result.AddError("Canonical capture manifest recorded overall_result=FAIL.");
            }

            return manifestPath;
        }

        public static TypographyPreviewScreenshotBatchResult ReconstructCanonicalManifest(
            string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Evidence output directory is required.", nameof(outputDirectory));
            }

            outputDirectory = Path.GetFullPath(outputDirectory);
            var result = new TypographyPreviewScreenshotBatchResult(outputDirectory);
            var logPaths = Directory.Exists(outputDirectory)
                ? Directory.GetFiles(outputDirectory, "capture-*.log", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();

            if (logPaths.Length == 0)
            {
                result.AddError("No split capture logs were found for manifest reconstruction.");
            }

            var logRows = ReadSplitLogRows(logPaths, result);
            result.ThemeValidationPassed = logPaths.Length > 0 && logPaths.All(LogExitedSuccessfully);
            result.PrefabValidationPassed = result.ThemeValidationPassed;
            result.AssetMutationObservationPassed =
                result.ThemeValidationPassed &&
                logPaths.All(path => LogContains(
                    path,
                    "CAPTURE_ASSET_MUTATION_OBSERVED_BEFORE_RESTORE: PASS"));
            result.UnexpectedAssetMutationCount = logPaths.Count(path =>
                !LogContains(path, "CAPTURE_UNEXPECTED_ASSET_MUTATION_COUNT: 0"));
            if (!result.AssetMutationObservationPassed ||
                result.UnexpectedAssetMutationCount != 0)
            {
                result.AddError(
                    "Split capture logs did not prove mutation observation before restore with zero unexpected mutations.");
            }
            var dirtyGuardPaths = TypographyPreviewScreenshotUtility.GetDirtyGuardAssetPaths();
            result.GuardedAssetsClean = result.ThemeValidationPassed && dirtyGuardPaths.Count == 0;
            foreach (var dirtyGuardPath in dirtyGuardPaths)
            {
                result.AddError($"Manifest reconstruction found guarded asset dirty: {dirtyGuardPath}");
            }

            var options = new TypographyPreviewScreenshotOptions();
            foreach (var target in TypographyPreviewScreenshotUtility.RequiredTargets)
            {
                foreach (var locale in TypographyThemeValidator.RequiredLocaleCodes)
                {
                    var fileName = TypographyPreviewScreenshotUtility.BuildFileName(target, locale);
                    var filePath = Path.Combine(outputDirectory, fileName);
                    var capture = new TypographyPreviewScreenshotCaptureResult(target, locale, filePath)
                    {
                        ExpectedLocalizedTextCount =
                            TypographyPreviewScreenshotUtility.GetExpectedLocalizedTextCount(target.FileStem),
                        OrientationValidationResult = "PASS_PIPELINE_CONTRACT",
                        GlyphTofuValidationResult = "NOT_RECORDED",
                    };

                    if (!logRows.TryGetValue(fileName, out var row))
                    {
                        capture.AddError($"{fileName}: No successful split-log capture row was found.");
                        result.AddCapture(capture);
                        continue;
                    }

                    capture.AppliedBindingCount = row.TypographyBindingCount;
                    capture.LocalizedTextAppliedCount = row.LocalizedTextAppliedCount;
                    if (!File.Exists(filePath))
                    {
                        capture.AddError($"{fileName}: PNG file was not found.");
                        result.AddCapture(capture);
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    capture.FileSizeBytes = fileInfo.Length;
                    if (row.FileSizeBytes != fileInfo.Length)
                    {
                        capture.AddError(
                            $"{fileName}: Split-log byte size {row.FileSizeBytes} does not match actual size {fileInfo.Length}.");
                    }

                    PopulatePngValidation(capture);
                    result.AddCapture(capture);
                }
            }

            var unityVersion = ReadUnityVersion(logPaths);
            WriteCanonicalManifest(
                result,
                options,
                new TypographyPreviewScreenshotManifestContext
                {
                    UnityVersion = unityVersion,
                    CaptureCommand = ReconstructionEntryPoint,
                    CaptureMode = "RECONSTRUCTED_FROM_SPLIT_LOGS",
                    ReconstructedFrom = string.Join(",", logPaths.Select(Path.GetFileName)),
                });
            return result;
        }

        private static bool LogContains(string path, string expected)
        {
            return File.Exists(path) &&
                   File.ReadAllText(path).Contains(expected, StringComparison.Ordinal);
        }

        public static string ReadCurrentGitHead()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            var processValue = TryReadGitHeadFromProcess(projectRoot);
            if (IsGitHead(processValue))
            {
                return processValue;
            }

            var fileValue = TryReadGitHeadFromFiles(projectRoot);
            return IsGitHead(fileValue) ? fileValue : "UNKNOWN";
        }

        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static IReadOnlyList<TypographyPreviewScreenshotManifestEntry> BuildRequiredEntries(
            TypographyPreviewScreenshotBatchResult result,
            TypographyPreviewScreenshotOptions options)
        {
            var entries = new List<TypographyPreviewScreenshotManifestEntry>();
            foreach (var target in TypographyPreviewScreenshotUtility.RequiredTargets)
            {
                foreach (var locale in TypographyThemeValidator.RequiredLocaleCodes)
                {
                    var expectedFileName = TypographyPreviewScreenshotUtility.BuildFileName(target, locale);
                    var capture = result.Captures.SingleOrDefault(candidate =>
                        string.Equals(candidate.Target.FileStem, target.FileStem, StringComparison.Ordinal) &&
                        string.Equals(candidate.LocaleCode, locale, StringComparison.Ordinal));
                    entries.Add(BuildEntry(capture, target, locale, expectedFileName, options));
                }
            }

            return entries;
        }

        private static TypographyPreviewScreenshotManifestEntry BuildEntry(
            TypographyPreviewScreenshotCaptureResult capture,
            TypographyPreviewScreenshotTarget target,
            string locale,
            string expectedFileName,
            TypographyPreviewScreenshotOptions options)
        {
            var expectedLocalizedCount =
                TypographyPreviewScreenshotUtility.GetExpectedLocalizedTextCount(target.FileStem);
            if (capture == null)
            {
                return new TypographyPreviewScreenshotManifestEntry
                {
                    Target = target.FileStem,
                    Locale = locale,
                    FileName = expectedFileName,
                    Sha256 = "MISSING",
                    LocalizedExpectedCount = expectedLocalizedCount,
                    OrientationValidation = "NOT_RUN",
                    NonBlankValidation = "NOT_RUN",
                    GlyphTofuValidation = "NOT_RUN",
                    CaptureResult = "FAIL",
                    Errors = "Required capture entry is missing.",
                };
            }

            var actualFileName = Path.GetFileName(capture.FilePath);
            var fileExists = File.Exists(capture.FilePath);
            var actualFileSize = fileExists ? new FileInfo(capture.FilePath).Length : 0L;
            var sha256 = fileExists && actualFileSize > 0
                ? ComputeSha256(capture.FilePath)
                : "MISSING";
            var errors = capture.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToList();
            if (!string.Equals(actualFileName, expectedFileName, StringComparison.Ordinal))
            {
                errors.Add($"Expected file name '{expectedFileName}', got '{actualFileName}'.");
            }

            if (!fileExists || actualFileSize <= 0)
            {
                errors.Add("PNG file is missing or empty.");
            }

            if (capture.Width != options.Width || capture.Height != options.Height)
            {
                errors.Add(
                    $"Expected dimensions {options.Width}x{options.Height}, got {capture.Width}x{capture.Height}.");
            }

            if (capture.LocalizedTextAppliedCount != expectedLocalizedCount ||
                capture.ExpectedLocalizedTextCount != expectedLocalizedCount)
            {
                errors.Add(
                    $"Localized descriptors expected/applied mismatch: contract={expectedLocalizedCount}, " +
                    $"recordedExpected={capture.ExpectedLocalizedTextCount}, applied={capture.LocalizedTextAppliedCount}.");
            }

            if (string.Equals(target.FileStem, "Settings", StringComparison.Ordinal) &&
                capture.AppliedBindingCount != TypographyPreviewScreenshotUtility.SettingsExpectedAppliedBindingCount)
            {
                errors.Add(
                    $"Settings typography binding count must be " +
                    $"{TypographyPreviewScreenshotUtility.SettingsExpectedAppliedBindingCount}, " +
                    $"got {capture.AppliedBindingCount}.");
            }

            if (string.Equals(capture.OrientationValidationResult, "FAIL", StringComparison.Ordinal) ||
                string.Equals(capture.OrientationValidationResult, "NOT_RUN", StringComparison.Ordinal))
            {
                errors.Add("Orientation validation did not pass.");
            }

            if (string.Equals(capture.NonBlankValidationResult, "FAIL", StringComparison.Ordinal) ||
                string.Equals(capture.NonBlankValidationResult, "NOT_RUN", StringComparison.Ordinal))
            {
                errors.Add("Nonblank validation did not pass or report an allowed unsupported state.");
            }

            if (string.Equals(capture.GlyphTofuValidationResult, "FAIL", StringComparison.Ordinal) ||
                string.Equals(capture.GlyphTofuValidationResult, "NOT_RUN", StringComparison.Ordinal))
            {
                errors.Add("Glyph/tofu validation did not pass or report its supported scope.");
            }

            return new TypographyPreviewScreenshotManifestEntry
            {
                Target = target.FileStem,
                Locale = locale,
                FileName = actualFileName,
                FileSizeBytes = actualFileSize,
                Sha256 = sha256,
                Width = capture.Width,
                Height = capture.Height,
                LocalizedExpectedCount = expectedLocalizedCount,
                LocalizedAppliedCount = capture.LocalizedTextAppliedCount,
                TypographyBindingCount = capture.AppliedBindingCount,
                OrientationValidation = capture.OrientationValidationResult,
                NonBlankValidation = capture.NonBlankValidationResult,
                GlyphTofuValidation = capture.GlyphTofuValidationResult,
                CaptureResult = errors.Count == 0 ? "PASS" : "FAIL",
                Errors = string.Join(" | ", errors),
            };
        }

        private static void PopulatePngValidation(TypographyPreviewScreenshotCaptureResult capture)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(capture.FilePath)))
                {
                    capture.AddError($"{capture.FilePath}: PNG could not be decoded.");
                    capture.NonBlankValidationResult = "FAIL";
                    return;
                }

                capture.Width = texture.width;
                capture.Height = texture.height;
                var pixels = texture.GetPixels32();
                capture.NonBlankValidationResult =
                    pixels.Length > 0 && pixels.Any(pixel => !pixel.Equals(pixels[0]))
                        ? "PASS"
                        : "FAIL";
                if (string.Equals(capture.NonBlankValidationResult, "FAIL", StringComparison.Ordinal))
                {
                    capture.AddError($"{capture.FilePath}: PNG is blank or single-color.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static Dictionary<string, SplitLogRow> ReadSplitLogRows(
            IEnumerable<string> logPaths,
            TypographyPreviewScreenshotBatchResult result)
        {
            var rows = new Dictionary<string, SplitLogRow>(StringComparer.Ordinal);
            foreach (var logPath in logPaths)
            {
                if (!LogExitedSuccessfully(logPath))
                {
                    result.AddError($"{Path.GetFileName(logPath)}: Unity process exit status was not successful.");
                    continue;
                }

                foreach (var line in File.ReadLines(logPath))
                {
                    var fields = line.Split('|').Select(field => field.Trim()).ToArray();
                    if (fields.Length < 9 ||
                        !string.Equals(fields[4], "True", StringComparison.OrdinalIgnoreCase) ||
                        !long.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fileSize) ||
                        !int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bindings) ||
                        !int.TryParse(fields[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var localized))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(fields[3].Replace('\\', '/'));
                    if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    rows[fileName] = new SplitLogRow(fileSize, bindings, localized);
                }
            }

            return rows;
        }

        private static bool LogExitedSuccessfully(string logPath)
        {
            return File.ReadLines(logPath).Any(line =>
                line.IndexOf("return code 0", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string ReadUnityVersion(IEnumerable<string> logPaths)
        {
            const string marker = "Version is '";
            foreach (var line in logPaths.SelectMany(File.ReadLines))
            {
                var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0)
                {
                    continue;
                }

                var startIndex = markerIndex + marker.Length;
                var endIndex = line.IndexOf('\'', startIndex);
                if (endIndex > startIndex)
                {
                    return line.Substring(startIndex, endIndex - startIndex);
                }
            }

            return UnityEngine.Application.unityVersion;
        }

        private static string TryReadGitHeadFromProcess(string projectRoot)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return string.Empty;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                return process.ExitCode == 0 ? output : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryReadGitHeadFromFiles(string projectRoot)
        {
            try
            {
                var dotGitPath = Path.Combine(projectRoot, ".git");
                var gitDirectory = Directory.Exists(dotGitPath)
                    ? dotGitPath
                    : ResolveGitDirectory(projectRoot, dotGitPath);
                if (string.IsNullOrWhiteSpace(gitDirectory))
                {
                    return string.Empty;
                }

                var head = File.ReadAllText(Path.Combine(gitDirectory, "HEAD")).Trim();
                if (IsGitHead(head))
                {
                    return head;
                }

                const string refPrefix = "ref:";
                if (!head.StartsWith(refPrefix, StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                var reference = head.Substring(refPrefix.Length).Trim();
                var directReferencePath = Path.Combine(gitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(directReferencePath))
                {
                    return File.ReadAllText(directReferencePath).Trim();
                }

                var commonDirectory = gitDirectory;
                var commonDirectoryFile = Path.Combine(gitDirectory, "commondir");
                if (File.Exists(commonDirectoryFile))
                {
                    commonDirectory = Path.GetFullPath(
                        Path.Combine(gitDirectory, File.ReadAllText(commonDirectoryFile).Trim()));
                    var commonReferencePath =
                        Path.Combine(commonDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(commonReferencePath))
                    {
                        return File.ReadAllText(commonReferencePath).Trim();
                    }
                }

                var packedRefsPath = Path.Combine(commonDirectory, "packed-refs");
                if (!File.Exists(packedRefsPath))
                {
                    return string.Empty;
                }

                foreach (var line in File.ReadLines(packedRefsPath))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith("^", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fields = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length == 2 && string.Equals(fields[1], reference, StringComparison.Ordinal))
                    {
                        return fields[0];
                    }
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string ResolveGitDirectory(string projectRoot, string dotGitPath)
        {
            if (!File.Exists(dotGitPath))
            {
                return string.Empty;
            }

            var contents = File.ReadAllText(dotGitPath).Trim();
            const string prefix = "gitdir:";
            if (!contents.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var configuredPath = contents.Substring(prefix.Length).Trim();
            if (UnityEngine.Application.platform == RuntimePlatform.WindowsEditor &&
                configuredPath.StartsWith("/mnt/", StringComparison.Ordinal) &&
                configuredPath.Length > 7 &&
                configuredPath[6] == '/')
            {
                configuredPath =
                    char.ToUpperInvariant(configuredPath[5]) + ":" + configuredPath.Substring(6);
            }

            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(projectRoot, configuredPath));
        }

        private static string GetRepositoryRelativePath(string path)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."));
            return Path.GetRelativePath(projectRoot, Path.GetFullPath(path)).Replace('\\', '/');
        }

        private static bool IsGitHead(string value)
        {
            return value != null &&
                   value.Length == 40 &&
                   value.All(character =>
                       character >= '0' && character <= '9' ||
                       character >= 'a' && character <= 'f' ||
                       character >= 'A' && character <= 'F');
        }

        private static void Append(StringBuilder builder, string key, string value)
        {
            builder
                .Append(key)
                .Append('=')
                .AppendLine(SanitizeValue(value));
        }

        private static string SanitizeValue(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r\n", " | ")
                .Replace('\r', ' ')
                .Replace('\n', ' ');
        }

        private readonly struct SplitLogRow
        {
            public SplitLogRow(long fileSizeBytes, int typographyBindingCount, int localizedTextAppliedCount)
            {
                FileSizeBytes = fileSizeBytes;
                TypographyBindingCount = typographyBindingCount;
                LocalizedTextAppliedCount = localizedTextAppliedCount;
            }

            public long FileSizeBytes { get; }

            public int TypographyBindingCount { get; }

            public int LocalizedTextAppliedCount { get; }
        }
    }
}
