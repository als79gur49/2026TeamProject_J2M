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
            var options = new TypographyPreviewScreenshotOptions();
            if (int.TryParse(ReadArg(args, "-typographyScreenshotWidth"), out var width))
            {
                options.Width = width;
            }

            if (int.TryParse(ReadArg(args, "-typographyScreenshotHeight"), out var height))
            {
                options.Height = height;
            }

            var result = TypographyPreviewScreenshotUtility.CaptureRequiredScreenshots(outputDirectory, options);
            LogResult(result);
            result.ThrowIfFailed();
        }

        private static void LogResult(TypographyPreviewScreenshotBatchResult result)
        {
            var lines = new[]
                {
                    "Typography preview screenshot capture completed.",
                    $"Output: {result.OutputDirectory}",
                    "| Target | Locale | File | Exists | Size | Typography Bindings | Localized Texts |",
                    "|---|---|---|---|---|---|---|",
                }
                .Concat(result.Captures.Select(capture =>
                    $"| {capture.Target.Name} | {capture.LocaleCode} | {capture.FilePath} | {capture.Exists} | {capture.FileSizeBytes} | {capture.AppliedBindingCount} | {capture.LocalizedTextAppliedCount} |"))
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
