using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Release.Steamworks.Editor
{
    public static class SteamworksConfigurationExpectationCli
    {
        public const string OutputDirectoryArgument =
            "-steamworksExpectationOutputDirectory";
        public const string SourceHeadArgument =
            "-steamworksExpectationSourceHead";
        public const string SourceTreeArgument =
            "-steamworksExpectationSourceTree";
        public const string ReportFileName =
            "steamworks-expected-configuration.json";
        public const string HashFileName =
            "steamworks-expected-configuration.sha256";

        public static void ExportFromCommandLine()
        {
            var exitCode = 1;
            try
            {
                Export(Environment.GetCommandLineArgs());
                exitCode = 0;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        public static string Export(string[] commandLine)
        {
            var arguments = ParseArguments(commandLine);
            var outputDirectory = ValidateOutputDirectory(
                arguments[OutputDirectoryArgument],
                Directory.GetCurrentDirectory());
            var expectation = SteamworksConfigurationExpectationBuilder.Build(
                arguments[SourceHeadArgument],
                arguments[SourceTreeArgument]);
            var json = SteamworksConfigurationExpectationSerializer.Serialize(expectation);
            var hash = SteamworksConfigurationExpectationSerializer.ComputeSha256(json);

            Directory.CreateDirectory(outputDirectory);
            var reportPath = Path.Combine(outputDirectory, ReportFileName);
            var hashPath = Path.Combine(outputDirectory, HashFileName);
            WriteUtf8WithoutBom(reportPath, json);
            WriteUtf8WithoutBom(
                hashPath,
                hash + "  " + ReportFileName + "\n");
            Debug.Log("Steamworks configuration expectation written: " + reportPath);
            Debug.Log("Steamworks configuration expectation SHA-256: " + hash);
            return reportPath;
        }

        public static string ValidateOutputDirectory(
            string outputDirectory,
            string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) ||
                !Path.IsPathRooted(outputDirectory))
            {
                throw new ArgumentException(
                    "An absolute private output directory is required.",
                    nameof(outputDirectory));
            }

            var outputFull = NormalizeDirectory(outputDirectory);
            var projectFull = NormalizeDirectory(projectRoot);
            if (IsSameOrUnder(outputFull, projectFull))
            {
                throw new ArgumentException(
                    "Steamworks expectation output must be outside the repository.",
                    nameof(outputDirectory));
            }

            return outputFull.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        private static Dictionary<string, string> ParseArguments(string[] commandLine)
        {
            var required = new[]
            {
                OutputDirectoryArgument,
                SourceHeadArgument,
                SourceTreeArgument,
            };
            var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in required)
            {
                var value = ReadArgument(commandLine, name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Missing required argument " + name + ".");
                }

                parsed.Add(name, value);
            }

            return parsed;
        }

        private static string ReadArgument(string[] commandLine, string name)
        {
            if (commandLine == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < commandLine.Length; index++)
            {
                if (!string.Equals(commandLine[index], name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (index + 1 >= commandLine.Length)
                {
                    return string.Empty;
                }

                return commandLine[index + 1];
            }

            return string.Empty;
        }

        private static void WriteUtf8WithoutBom(string path, string contents)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, contents, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temporaryPath, path);
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
        }

        private static bool IsSameOrUnder(string candidate, string parent)
        {
            return candidate.StartsWith(
                parent,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
