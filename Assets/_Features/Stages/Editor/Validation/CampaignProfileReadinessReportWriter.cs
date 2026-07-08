using System;
using System.IO;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class CampaignProfileReadinessReportWriter
    {
        public const string DefaultFileName = "CampaignProfileReadiness.md";

        public static string Write(
            CampaignProfileReadinessReport report,
            string outputDirectory = CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
            string fileName = DefaultFileName)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var outputPath = ResolveOutputPath(outputDirectory, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            using var writer = new StreamWriter(outputPath, append: false);
            writer.Write(report.ToMarkdown());
            return outputPath;
        }

        public static string ResolveOutputPath(
            string outputDirectory = CampaignProfileReadinessReportOptions.DefaultOutputDirectory,
            string fileName = DefaultFileName)
        {
            var projectRoot = GetProjectRoot();
            var safeRoot = Path.GetFullPath(
                Path.Combine(projectRoot, CampaignProfileReadinessReportOptions.DefaultOutputDirectory));
            var requestedDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? CampaignProfileReadinessReportOptions.DefaultOutputDirectory
                : outputDirectory;
            var requestedFileName = string.IsNullOrWhiteSpace(fileName)
                ? DefaultFileName
                : fileName;
            var outputPath = Path.IsPathRooted(requestedDirectory)
                ? Path.Combine(requestedDirectory, requestedFileName)
                : Path.Combine(projectRoot, requestedDirectory, requestedFileName);
            var fullOutputPath = Path.GetFullPath(outputPath);

            if (!IsUnderDirectory(fullOutputPath, safeRoot))
            {
                throw new InvalidOperationException(
                    $"Campaign profile readiness reports may only be written under {CampaignProfileReadinessReportOptions.DefaultOutputDirectory}.");
            }

            return fullOutputPath;
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
        }

        private static bool IsUnderDirectory(string path, string directory)
        {
            var normalizedPath = EnsureTrailingSeparator(path);
            var normalizedDirectory = EnsureTrailingSeparator(directory);
            return normalizedPath.StartsWith(normalizedDirectory, StringComparison.Ordinal);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
