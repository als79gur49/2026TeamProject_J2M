using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class CampaignProfileReadinessReportCommand
    {
        public const string OutputDirectoryArgument = "-saveReadinessOutputDirectory";
        public const string RunIdArgument = "-saveReadinessRunId";
        public const string CommitShaArgument = "-saveReadinessCommitSha";

        [MenuItem("Tools/Stages/Diagnostics/Write Campaign Profile Readiness Report")]
        public static void WriteDefaultReportFromMenu()
        {
            var outputPath = WriteDefaultReport(CampaignProfileReadinessReportOptions.DefaultOutputDirectory);
            Debug.Log($"Campaign profile readiness report written: {outputPath}");
        }

        public static void WriteDefaultReportFromCommandLine()
        {
            try
            {
                var outputPath = WriteDefaultReport();
                Debug.Log($"Campaign profile readiness report written: {outputPath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string WriteDefaultReport()
        {
            return WriteDefaultReport(
                ResolveOutputDirectoryFromCommandLine(Environment.GetCommandLineArgs()));
        }

        public static string WriteDefaultReport(string outputDirectory)
        {
            var report = new CampaignProfileReadinessReportBuilder().Build(
                new CampaignProfileReadinessReportOptions(Application.persistentDataPath));
            return CampaignProfileReadinessReportWriter.Write(report, outputDirectory);
        }

        public static string ResolveOutputDirectoryFromCommandLine(string[] args)
        {
            var outputDirectory = ReadArgumentValue(args, OutputDirectoryArgument);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                return outputDirectory;
            }

            var runId = ReadArgumentValue(args, RunIdArgument);
            var commitSha = ReadArgumentValue(args, CommitShaArgument);
            if (string.IsNullOrWhiteSpace(runId) && string.IsNullOrWhiteSpace(commitSha))
            {
                return CampaignProfileReadinessReportOptions.DefaultOutputDirectory;
            }

            var directory = CampaignProfileReadinessReportOptions.DefaultOutputDirectory;
            if (!string.IsNullOrWhiteSpace(runId))
            {
                directory = Path.Combine(directory, ValidatePathSegment(runId, RunIdArgument));
            }

            if (!string.IsNullOrWhiteSpace(commitSha))
            {
                directory = Path.Combine(directory, ValidatePathSegment(commitSha, CommitShaArgument));
            }

            return directory;
        }

        private static string ReadArgumentValue(string[] args, string argumentName)
        {
            if (args == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], argumentName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"Missing value for {argumentName}.",
                        nameof(args));
                }

                return args[i + 1];
            }

            return string.Empty;
        }

        private static string ValidatePathSegment(string value, string argumentName)
        {
            var segment = value.Trim();
            if (segment.Length == 0 ||
                segment == "." ||
                segment == ".." ||
                segment.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                segment.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new ArgumentException(
                    $"{argumentName} must be a single directory name.",
                    argumentName);
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            for (var i = 0; i < invalidCharacters.Length; i++)
            {
                if (segment.IndexOf(invalidCharacters[i]) >= 0)
                {
                    throw new ArgumentException(
                        $"{argumentName} contains an invalid path character.",
                        argumentName);
                }
            }

            return segment;
        }
    }
}
