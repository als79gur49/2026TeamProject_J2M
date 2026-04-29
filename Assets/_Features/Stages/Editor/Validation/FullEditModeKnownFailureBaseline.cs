using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace Game.Feature.Stages.Editor
{
    public static class FullEditModeKnownFailureBaseline
    {
        public const string DefaultBaselinePath =
            "Assets/_Features/Stages/Editor/Validation/Baselines/FullEditModeKnownFailures.json";
        public const string DefaultCurrentResultXmlPath =
            "Temp/StageAuthoringCleanupFullEditMode.xml";
        public const string FallbackCurrentResultXmlPath =
            "Temp/StageHardeningFullEditMode.after-provider.xml";

        private static readonly string[] StageAuthoringBlockingMarkers =
        {
            "StageAuthoringNormalizedDriftTests",
            "StageAuthoringDryRunTests",
            "StageAuthoringArchitectureBoundaryTests",
            "StageAuthoringGenerationTests",
            "StageAuthoringGovernanceTests",
            "StageAuthoringMigrationTests",
            "StageAuthoringGridPresentationDropdownTests",
            "StageCatalogCiValidationEntryPointTests",
        };

        public static FullEditModeKnownFailureBaselineDocument LoadBaselineJson(string json)
        {
            var baseline = JsonUtility.FromJson<FullEditModeKnownFailureBaselineDocument>(json);
            baseline ??= new FullEditModeKnownFailureBaselineDocument();
            baseline.summary ??= new FullEditModeSummary();
            baseline.knownFailures ??= Array.Empty<FullEditModeKnownFailure>();
            return baseline;
        }

        public static FullEditModeResult ParseNUnitXmlFile(string path)
        {
            return ParseNUnitXml(File.ReadAllText(path));
        }

        public static FullEditModeResult ParseNUnitXml(string xml)
        {
            var root = XDocument.Parse(xml).Root;
            if (root == null)
            {
                throw new InvalidDataException("NUnit XML does not contain a root element.");
            }

            var failures = new List<FullEditModeTestFailure>();
            var assemblies = new List<FullEditModeAssemblySummary>();
            foreach (var suite in root.Descendants("test-suite"))
            {
                if (!string.Equals((string)suite.Attribute("type"), "Assembly", StringComparison.Ordinal))
                {
                    continue;
                }

                assemblies.Add(new FullEditModeAssemblySummary
                {
                    assembly = ResolveAssemblyName(suite),
                    total = ParseInt(suite.Attribute("total")),
                    passed = ParseInt(suite.Attribute("passed")),
                    failed = ParseInt(suite.Attribute("failed")),
                    skipped = ParseInt(suite.Attribute("skipped")),
                    result = (string)suite.Attribute("result") ?? string.Empty,
                });
            }

            foreach (var testCase in root.Descendants("test-case"))
            {
                if (!string.Equals((string)testCase.Attribute("result"), "Failed", StringComparison.Ordinal))
                {
                    continue;
                }

                var message = testCase.Element("failure")?.Element("message")?.Value ?? string.Empty;
                failures.Add(new FullEditModeTestFailure
                {
                    testFullName = (string)testCase.Attribute("fullname") ??
                                   (string)testCase.Attribute("name") ??
                                   string.Empty,
                    assembly = ResolveAssemblyName(testCase),
                    failureMessage = message,
                    failureMessageHash = ComputeFailureMessageHash(message),
                });
            }

            return new FullEditModeResult
            {
                summary = new FullEditModeSummary
                {
                    total = ParseInt(root.Attribute("total")),
                    passed = ParseInt(root.Attribute("passed")),
                    failed = ParseInt(root.Attribute("failed")),
                    skipped = ParseInt(root.Attribute("skipped")),
                },
                assemblies = assemblies.ToArray(),
                failedTests = failures.ToArray(),
            };
        }

        public static FullEditModeBaselineComparison Compare(
            FullEditModeResult current,
            FullEditModeKnownFailureBaselineDocument baseline)
        {
            current ??= new FullEditModeResult();
            baseline ??= new FullEditModeKnownFailureBaselineDocument();
            current.failedTests ??= Array.Empty<FullEditModeTestFailure>();
            baseline.knownFailures ??= Array.Empty<FullEditModeKnownFailure>();

            var baselineByIdentity = baseline.knownFailures
                .GroupBy(KnownIdentityKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var currentIdentities = new HashSet<string>(
                current.failedTests.Select(FailureIdentityKey),
                StringComparer.Ordinal);

            var knownStillFailing = new List<FullEditModeTestFailure>();
            var newFailures = new List<FullEditModeTestFailure>();
            var hashDrift = new List<FullEditModeTestFailure>();
            var stageAuthoringFailures = new List<FullEditModeTestFailure>();
            foreach (var failure in current.failedTests)
            {
                if (IsStageAuthoringBlockingFailure(failure))
                {
                    stageAuthoringFailures.Add(failure);
                }

                if (baselineByIdentity.TryGetValue(FailureIdentityKey(failure), out var knownFailure))
                {
                    knownStillFailing.Add(failure);
                    if (!string.Equals(
                            knownFailure.failureMessageHash,
                            failure.failureMessageHash,
                            StringComparison.Ordinal))
                    {
                        hashDrift.Add(failure);
                    }
                }
                else
                {
                    newFailures.Add(failure);
                }
            }

            var resolved = baseline.knownFailures
                .Where(known => !currentIdentities.Contains(KnownIdentityKey(known)))
                .ToArray();

            var status = FullEditModeBaselineResult.Passed;
            if (stageAuthoringFailures.Count > 0)
            {
                status = FullEditModeBaselineResult.FailedWithStageAuthoringFailures;
            }
            else if (newFailures.Count > 0)
            {
                status = FullEditModeBaselineResult.FailedWithNewFailures;
            }
            else if (current.failedTests.Length > 0)
            {
                status = FullEditModeBaselineResult.PassedWithKnownFailures;
            }

            return new FullEditModeBaselineComparison
            {
                current = current,
                baseline = baseline,
                result = status.ToString(),
                knownFailuresStillFailing = knownStillFailing.ToArray(),
                newFailures = newFailures.ToArray(),
                resolvedKnownFailures = resolved,
                knownFailuresWithMessageHashDrift = hashDrift.ToArray(),
                stageAuthoringBlockingFailures = stageAuthoringFailures.ToArray(),
            };
        }

        public static bool TryBuildDefaultComparison(
            out FullEditModeBaselineComparison comparison,
            out string message)
        {
            comparison = null;
            message = string.Empty;

            if (!File.Exists(DefaultBaselinePath))
            {
                message = $"Baseline not found: {DefaultBaselinePath}";
                return false;
            }

            var xmlPath = File.Exists(DefaultCurrentResultXmlPath)
                ? DefaultCurrentResultXmlPath
                : FallbackCurrentResultXmlPath;
            if (!File.Exists(xmlPath))
            {
                message = $"Full EditMode XML not found: {DefaultCurrentResultXmlPath}";
                return false;
            }

            var baseline = LoadBaselineJson(File.ReadAllText(DefaultBaselinePath));
            var current = ParseNUnitXmlFile(xmlPath);
            comparison = Compare(current, baseline);
            message = xmlPath;
            return true;
        }

        public static string BuildMarkdownReport(FullEditModeBaselineComparison comparison)
        {
            var builder = new StringBuilder();
            builder.AppendLine("## Full EditMode Known Failure Baseline");
            if (comparison == null)
            {
                builder.AppendLine("Not evaluated.");
                builder.AppendLine();
                return builder.ToString();
            }

            var summary = comparison.current?.summary ?? new FullEditModeSummary();
            builder.AppendLine($"Result: {comparison.result}");
            builder.AppendLine($"Current: total={summary.total}, passed={summary.passed}, failed={summary.failed}, skipped={summary.skipped}");
            builder.AppendLine($"Known failures still failing: {comparison.knownFailuresStillFailing.Length}");
            builder.AppendLine($"New failures: {comparison.newFailures.Length}");
            builder.AppendLine($"Resolved known failures: {comparison.resolvedKnownFailures.Length}");
            builder.AppendLine($"Known failures with message hash drift: {comparison.knownFailuresWithMessageHashDrift.Length}");
            builder.AppendLine($"StageAuthoring blocking failures: {comparison.stageAuthoringBlockingFailures.Length}");
            builder.AppendLine();
            AppendFailures(builder, "StageAuthoring Blocking Failures", comparison.stageAuthoringBlockingFailures);
            AppendFailures(builder, "New Failures", comparison.newFailures);
            AppendFailures(builder, "Resolved Known Failures", comparison.resolvedKnownFailures);
            AppendFailures(builder, "Known Failures With Message Hash Drift", comparison.knownFailuresWithMessageHashDrift);
            return builder.ToString();
        }

        public static string ComputeFailureMessageHash(string message)
        {
            var normalized = NormalizeFailureMessage(message);
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
            var builder = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private static void AppendFailures(
            StringBuilder builder,
            string title,
            IReadOnlyList<FullEditModeTestFailure> failures)
        {
            builder.AppendLine($"### {title}");
            if (failures == null || failures.Count == 0)
            {
                builder.AppendLine("None");
                builder.AppendLine();
                return;
            }

            for (var i = 0; i < failures.Count; i++)
            {
                builder.AppendLine($"- {failures[i].assembly}: {failures[i].testFullName}");
            }

            builder.AppendLine();
        }

        private static void AppendFailures(
            StringBuilder builder,
            string title,
            IReadOnlyList<FullEditModeKnownFailure> failures)
        {
            builder.AppendLine($"### {title}");
            if (failures == null || failures.Count == 0)
            {
                builder.AppendLine("None");
                builder.AppendLine();
                return;
            }

            for (var i = 0; i < failures.Count; i++)
            {
                builder.AppendLine($"- {failures[i].assembly}: {failures[i].testFullName}");
            }

            builder.AppendLine();
        }

        private static bool IsStageAuthoringBlockingFailure(FullEditModeTestFailure failure)
        {
            if (failure == null)
            {
                return false;
            }

            for (var i = 0; i < StageAuthoringBlockingMarkers.Length; i++)
            {
                if (failure.testFullName.IndexOf(StageAuthoringBlockingMarkers[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return string.Equals(failure.assembly, "Game.Feature.Stages.Editor.Tests.dll", StringComparison.Ordinal) &&
                   failure.testFullName.IndexOf(".StageAuthoring", StringComparison.Ordinal) >= 0;
        }

        private static string ResolveAssemblyName(XElement element)
        {
            var current = element;
            while (current != null)
            {
                if (string.Equals((string)current.Attribute("type"), "Assembly", StringComparison.Ordinal))
                {
                    return ResolveAssemblyNameFromSuite(current);
                }

                current = current.Parent;
            }

            return string.Empty;
        }

        private static string ResolveAssemblyNameFromSuite(XElement suite)
        {
            var name = (string)suite.Attribute("name") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return Path.GetFileName(name);
            }

            var fullName = (string)suite.Attribute("fullname") ?? string.Empty;
            return string.IsNullOrWhiteSpace(fullName) ? string.Empty : Path.GetFileName(fullName);
        }

        private static int ParseInt(XAttribute attribute)
        {
            return int.TryParse(attribute?.Value, out var value) ? value : 0;
        }

        private static string NormalizeFailureMessage(string message)
        {
            return (message ?? string.Empty).Replace("\r\n", "\n").Trim();
        }

        private static string FailureIdentityKey(FullEditModeTestFailure failure)
        {
            return $"{failure.assembly}|{failure.testFullName}";
        }

        private static string KnownIdentityKey(FullEditModeKnownFailure failure)
        {
            return $"{failure.assembly}|{failure.testFullName}";
        }
    }

    public enum FullEditModeBaselineResult
    {
        Passed = 0,
        PassedWithKnownFailures = 1,
        FailedWithNewFailures = 2,
        FailedWithStageAuthoringFailures = 3,
    }

    [Serializable]
    public sealed class FullEditModeKnownFailureBaselineDocument
    {
        public string capturedAt = string.Empty;
        public string sourceXml = string.Empty;
        public FullEditModeSummary summary = new();
        public FullEditModeKnownFailure[] knownFailures = Array.Empty<FullEditModeKnownFailure>();
    }

    [Serializable]
    public sealed class FullEditModeSummary
    {
        public int total;
        public int passed;
        public int failed;
        public int skipped;
    }

    [Serializable]
    public sealed class FullEditModeKnownFailure
    {
        public string testFullName = string.Empty;
        public string assembly = string.Empty;
        public string domain = string.Empty;
        public bool authoringRelated;
        public string failureMessageHash = string.Empty;
        public string firstObserved = string.Empty;
        public string notes = string.Empty;
    }

    public sealed class FullEditModeResult
    {
        public FullEditModeSummary summary = new();
        public FullEditModeAssemblySummary[] assemblies = Array.Empty<FullEditModeAssemblySummary>();
        public FullEditModeTestFailure[] failedTests = Array.Empty<FullEditModeTestFailure>();
    }

    public sealed class FullEditModeAssemblySummary
    {
        public string assembly = string.Empty;
        public int total;
        public int passed;
        public int failed;
        public int skipped;
        public string result = string.Empty;
    }

    public sealed class FullEditModeTestFailure
    {
        public string testFullName = string.Empty;
        public string assembly = string.Empty;
        public string failureMessage = string.Empty;
        public string failureMessageHash = string.Empty;
    }

    public sealed class FullEditModeBaselineComparison
    {
        public FullEditModeResult current = new();
        public FullEditModeKnownFailureBaselineDocument baseline = new();
        public string result = string.Empty;
        public FullEditModeTestFailure[] knownFailuresStillFailing = Array.Empty<FullEditModeTestFailure>();
        public FullEditModeTestFailure[] newFailures = Array.Empty<FullEditModeTestFailure>();
        public FullEditModeKnownFailure[] resolvedKnownFailures = Array.Empty<FullEditModeKnownFailure>();
        public FullEditModeTestFailure[] knownFailuresWithMessageHashDrift = Array.Empty<FullEditModeTestFailure>();
        public FullEditModeTestFailure[] stageAuthoringBlockingFailures = Array.Empty<FullEditModeTestFailure>();
    }
}
