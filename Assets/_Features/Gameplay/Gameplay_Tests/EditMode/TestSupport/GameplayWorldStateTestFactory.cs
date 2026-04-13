using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests
{
    internal static class GameplayWorldStateTestFactory
    {
        private static readonly BoardBounds DefaultBoardBounds = new(
            new Vector2Int(-32, -32),
            new Vector2Int(32, 32));

        public static WorldState CreateBounded(IEnumerable<EntityState> initialEntities)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTerrainData.Empty, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTerrainData terrainData)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, terrainData, GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(initialEntities, DefaultBoardBounds, GameplayTerrainData.Empty, timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile)
        {
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(
                initialEntities,
                timingProfile ?? GameplayTimingProfile.CreateDefault());

            return GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                boardBounds,
                terrainData ?? GameplayTerrainData.Empty,
                topology);
        }
    }

    internal static class GameplayCliTestRunner
    {
        private const string TestNamesArg = "-codexTestNames";
        private const string CategoriesArg = "-codexCategories";
        private const string TestModesArg = "-codexTestModes";
        private const string ManifestPathArg = "-codexManifestPath";
        private const string ReportPathArg = "-codexTestReportPath";

        public static void RunEditModeTests()
        {
            try
            {
                var legacyTestNames = GetDelimitedArgumentValues(TestNamesArg);
                var hasStratificationArguments =
                    HasArgument(CategoriesArg) ||
                    HasArgument(TestModesArg) ||
                    HasArgument(ManifestPathArg) ||
                    legacyTestNames.Length == 0;

                if (hasStratificationArguments)
                {
                    RunStratifiedTests();
                    return;
                }

                RunLegacyEditModeTests(legacyTestNames);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
        }

        public static void RunStratifiedTests()
        {
            var manifestPath = GetSingleArgumentValue(ManifestPathArg);
            var manifest = GameplayTestStratificationLoader.LoadManifest(
                string.IsNullOrWhiteSpace(manifestPath)
                    ? null
                    : manifestPath);
            var selectedCategories = GetDelimitedArgumentValues(CategoriesArg);
            var selectedModes = GetDelimitedArgumentValues(TestModesArg);

            var filteredTests = manifest.tests
                .Where(test => selectedCategories.Length == 0 || selectedCategories.Contains(test.category, StringComparer.OrdinalIgnoreCase))
                .Where(test => selectedModes.Length == 0 || selectedModes.Contains(test.mode, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (filteredTests.Length == 0)
            {
                throw new InvalidOperationException("No tests matched the requested gameplay stratification selection.");
            }

            var aggregateResult = new AggregateRunResult();
            aggregateResult.Add(ExecuteTestMode(
                TestMode.EditMode,
                filteredTests
                    .Where(test => string.Equals(test.mode, "EditMode", StringComparison.Ordinal))
                    .Select(test => test.fullyQualifiedName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
            aggregateResult.Add(ExecuteTestMode(
                TestMode.PlayMode,
                filteredTests
                    .Where(test => string.Equals(test.mode, "PlayMode", StringComparison.Ordinal))
                    .Select(test => test.fullyQualifiedName)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));

            var summary = aggregateResult.BuildSummary(selectedCategories, selectedModes);
            WriteSummary(summary, aggregateResult.ExitCode);
        }

        private static void RunLegacyEditModeTests(string[] testNames)
        {
            if (testNames.Length == 0)
            {
                throw new InvalidOperationException($"Missing required command line argument: {TestNamesArg}");
            }

            var result = ExecuteTestMode(TestMode.EditMode, testNames);
            WriteSummary(result.BuildSummary(), result.ExitCode);
        }

        private static CommandLineRunResult ExecuteTestMode(TestMode testMode, string[] testNames)
        {
            if (testNames == null || testNames.Length == 0)
            {
                return CommandLineRunResult.Empty(testMode);
            }

            var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            try
            {
                var callbacks = new CommandLineCallbacks();
                runner.RegisterCallbacks(callbacks);

                var executionSettings = new ExecutionSettings(
                    new Filter
                    {
                        testMode = testMode,
                        testNames = testNames,
                    })
                {
                    runSynchronously = true,
                };

                runner.Execute(executionSettings);

                if (!callbacks.HasCompleted)
                {
                    throw new InvalidOperationException($"Command line test runner did not report completion for mode {testMode}.");
                }

                return callbacks.ToRunResult(testMode, testNames.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runner);
            }
        }

        private static void WriteSummary(string summary, int exitCode)
        {
            var reportPath = GetSingleArgumentValue(ReportPathArg);
            if (!string.IsNullOrWhiteSpace(reportPath))
            {
                var reportDirectory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrWhiteSpace(reportDirectory))
                {
                    Directory.CreateDirectory(reportDirectory);
                }

                File.WriteAllText(reportPath, summary);
            }

            UnityEngine.Debug.Log(summary);
            EditorApplication.Exit(exitCode);
        }

        private static string[] GetDelimitedArgumentValues(string argumentName)
        {
            var rawValue = GetSingleArgumentValue(argumentName);
            return string.IsNullOrWhiteSpace(rawValue)
                ? Array.Empty<string>()
                : rawValue
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
        }

        private static bool HasArgument(string argumentName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetSingleArgumentValue(string argumentName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return string.Empty;
        }

        private sealed class CommandLineCallbacks : ICallbacks
        {
            private ITestResultAdaptor _rootResult;

            public bool HasCompleted => _rootResult != null;

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _rootResult = result;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            public CommandLineRunResult ToRunResult(TestMode testMode, int requestedCount)
            {
                if (_rootResult == null)
                {
                    throw new InvalidOperationException($"No test result was captured for mode {testMode}.");
                }

                var failedLeaves = new List<ITestResultAdaptor>();
                CollectFailedLeafResults(_rootResult, failedLeaves);

                return new CommandLineRunResult(
                    testMode,
                    requestedCount,
                    _rootResult.TestStatus.ToString(),
                    _rootResult.PassCount,
                    _rootResult.FailCount,
                    _rootResult.SkipCount,
                    _rootResult.InconclusiveCount,
                    _rootResult.Duration,
                    failedLeaves
                        .Select(result => new FailedLeafResult(
                            result.FullName,
                            result.Message?.Trim() ?? string.Empty))
                        .ToArray());
            }

            private static void CollectFailedLeafResults(
                ITestResultAdaptor current,
                List<ITestResultAdaptor> buffer)
            {
                if (current == null)
                {
                    return;
                }

                if (!current.HasChildren)
                {
                    if (string.Equals(current.ResultState, "Failed", StringComparison.OrdinalIgnoreCase) ||
                        current.ResultState.StartsWith("Failed:", StringComparison.OrdinalIgnoreCase))
                    {
                        buffer.Add(current);
                    }

                    return;
                }

                foreach (var child in current.Children)
                {
                    CollectFailedLeafResults(child, buffer);
                }
            }
        }

        private sealed class AggregateRunResult
        {
            private readonly List<CommandLineRunResult> _results = new();

            public int ExitCode
            {
                get
                {
                    if (_results.Any(result => result.ExitCode == 2))
                    {
                        return 2;
                    }

                    return _results.Any(result => result.ExitCode == 1) ? 1 : 0;
                }
            }

            public void Add(CommandLineRunResult result)
            {
                if (result == null || result.RequestedCount == 0)
                {
                    return;
                }

                _results.Add(result);
            }

            public string BuildSummary(string[] selectedCategories, string[] selectedModes)
            {
                var builder = new StringBuilder();
                builder.AppendLine("Selection=GameplayTestStratification");
                builder.AppendLine($"SelectedCategories={(selectedCategories == null || selectedCategories.Length == 0 ? "All" : string.Join(",", selectedCategories))}");
                builder.AppendLine($"SelectedModes={(selectedModes == null || selectedModes.Length == 0 ? "All" : string.Join(",", selectedModes))}");
                builder.AppendLine($"Requested={_results.Sum(result => result.RequestedCount)}");
                builder.AppendLine($"Passed={_results.Sum(result => result.PassCount)}");
                builder.AppendLine($"Failed={_results.Sum(result => result.FailCount)}");
                builder.AppendLine($"Skipped={_results.Sum(result => result.SkipCount)}");
                builder.AppendLine($"Inconclusive={_results.Sum(result => result.InconclusiveCount)}");
                builder.AppendLine($"DurationSeconds={_results.Sum(result => result.DurationSeconds):0.###}");
                builder.AppendLine("ModeRuns:");

                for (var i = 0; i < _results.Count; i++)
                {
                    builder.AppendLine(_results[i].BuildModeLine());
                }

                var failedResults = _results
                    .SelectMany(result => result.FailedTests)
                    .ToArray();
                if (failedResults.Length == 0)
                {
                    builder.AppendLine("FailedTests=None");
                    return builder.ToString();
                }

                builder.AppendLine("FailedTests:");
                for (var i = 0; i < failedResults.Length; i++)
                {
                    builder.AppendLine(failedResults[i].FullName);
                    if (!string.IsNullOrWhiteSpace(failedResults[i].Message))
                    {
                        builder.AppendLine(failedResults[i].Message);
                    }
                }

                return builder.ToString();
            }
        }

        private sealed class CommandLineRunResult
        {
            public CommandLineRunResult(
                TestMode mode,
                int requestedCount,
                string resultStatus,
                int passCount,
                int failCount,
                int skipCount,
                int inconclusiveCount,
                double durationSeconds,
                FailedLeafResult[] failedTests)
            {
                Mode = mode;
                RequestedCount = requestedCount;
                ResultStatus = resultStatus ?? "Unknown";
                PassCount = passCount;
                FailCount = failCount;
                SkipCount = skipCount;
                InconclusiveCount = inconclusiveCount;
                DurationSeconds = durationSeconds;
                FailedTests = failedTests ?? Array.Empty<FailedLeafResult>();
            }

            public TestMode Mode { get; }

            public int RequestedCount { get; }

            public string ResultStatus { get; }

            public int PassCount { get; }

            public int FailCount { get; }

            public int SkipCount { get; }

            public int InconclusiveCount { get; }

            public double DurationSeconds { get; }

            public FailedLeafResult[] FailedTests { get; }

            public int ExitCode => FailCount > 0 ? 1 : 0;

            public static CommandLineRunResult Empty(TestMode mode)
            {
                return new CommandLineRunResult(
                    mode,
                    requestedCount: 0,
                    resultStatus: "Skipped",
                    passCount: 0,
                    failCount: 0,
                    skipCount: 0,
                    inconclusiveCount: 0,
                    durationSeconds: 0d,
                    failedTests: Array.Empty<FailedLeafResult>());
            }

            public string BuildSummary()
            {
                var builder = new StringBuilder();
                builder.AppendLine($"Mode={Mode}");
                builder.AppendLine($"Result={ResultStatus}");
                builder.AppendLine($"Requested={RequestedCount}");
                builder.AppendLine($"Passed={PassCount}");
                builder.AppendLine($"Failed={FailCount}");
                builder.AppendLine($"Skipped={SkipCount}");
                builder.AppendLine($"Inconclusive={InconclusiveCount}");
                builder.AppendLine($"DurationSeconds={DurationSeconds:0.###}");
                if (FailedTests.Length == 0)
                {
                    builder.AppendLine("FailedTests=None");
                    return builder.ToString();
                }

                builder.AppendLine("FailedTests:");
                for (var i = 0; i < FailedTests.Length; i++)
                {
                    builder.AppendLine(FailedTests[i].FullName);
                    if (!string.IsNullOrWhiteSpace(FailedTests[i].Message))
                    {
                        builder.AppendLine(FailedTests[i].Message);
                    }
                }

                return builder.ToString();
            }

            public string BuildModeLine()
            {
                return $"Mode={Mode}|Result={ResultStatus}|Requested={RequestedCount}|Passed={PassCount}|Failed={FailCount}|Skipped={SkipCount}|Inconclusive={InconclusiveCount}|DurationSeconds={DurationSeconds:0.###}";
            }
        }

        private sealed class FailedLeafResult
        {
            public FailedLeafResult(string fullName, string message)
            {
                FullName = fullName ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public string FullName { get; }

            public string Message { get; }
        }
    }

    [Serializable]
    internal sealed class GameplayTestStratificationOverrideFile
    {
        public int version = 1;
        public GameplayTestStratificationOverrideEntry[] overrides = Array.Empty<GameplayTestStratificationOverrideEntry>();
    }

    [Serializable]
    internal sealed class GameplayTestStratificationOverrideEntry
    {
        public string fullyQualifiedName = string.Empty;
        public string category = string.Empty;
        public string[] contracts = Array.Empty<string>();
        public string reason = string.Empty;
        public bool locked = false;
    }

    [Serializable]
    internal sealed class GameplayTestStratificationManifestFile
    {
        public int version = 1;
        public string generatedAtUtc = string.Empty;
        public GameplayTestStratificationManifestTest[] tests = Array.Empty<GameplayTestStratificationManifestTest>();
    }

    [Serializable]
    internal sealed class GameplayTestStratificationManifestTest
    {
        public string fullyQualifiedName = string.Empty;
        public string category = string.Empty;
        public string mode = string.Empty;
        public string[] contracts = Array.Empty<string>();
        public string reason = string.Empty;
        public string assertionSummary = string.Empty;
        public string[] targetSymbols = Array.Empty<string>();
        public string sourcePath = string.Empty;
        public int lineNumber = 0;
        public bool overridden = false;
        public bool locked = false;
    }

    internal sealed class GameplayDiscoveredTestSource
    {
        public string fullyQualifiedName = string.Empty;
        public string category = string.Empty;
        public string mode = string.Empty;
        public string sourcePath = string.Empty;
        public int lineNumber;
        public int primaryCategoryCount;
    }

    internal static class GameplayTestStratificationPaths
    {
        internal const string TestRootRelativePath = "Assets/_Features/Gameplay/Gameplay_Tests";
        internal const string OverrideRelativePath = "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayTestStratificationOverrides.json";
        internal const string ManifestRelativePath = "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayTestStratificationManifest.json";
        internal const string ReportRelativePath = "Docs/Architecture/Gameplay-Test-Stratification.md";

        internal static string GetProjectRootPath()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
        }

        internal static string GetAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                GetProjectRootPath(),
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }

    internal static class GameplayTestStratificationLoader
    {
        internal static GameplayTestStratificationManifestFile LoadManifest(string absolutePath = null)
        {
            absolutePath ??= GameplayTestStratificationPaths.GetAbsolutePath(GameplayTestStratificationPaths.ManifestRelativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"Gameplay test stratification manifest is missing: {absolutePath}", absolutePath);
            }

            var manifest = JsonUtility.FromJson<GameplayTestStratificationManifestFile>(File.ReadAllText(absolutePath));
            if (manifest == null)
            {
                throw new InvalidOperationException($"Failed to deserialize gameplay test stratification manifest: {absolutePath}");
            }

            manifest.tests ??= Array.Empty<GameplayTestStratificationManifestTest>();
            for (var i = 0; i < manifest.tests.Length; i++)
            {
                manifest.tests[i].contracts ??= Array.Empty<string>();
                manifest.tests[i].targetSymbols ??= Array.Empty<string>();
            }

            return manifest;
        }

        internal static GameplayTestStratificationOverrideFile LoadOverrides(string absolutePath = null)
        {
            absolutePath ??= GameplayTestStratificationPaths.GetAbsolutePath(GameplayTestStratificationPaths.OverrideRelativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"Gameplay test stratification override file is missing: {absolutePath}", absolutePath);
            }

            var overrideFile = JsonUtility.FromJson<GameplayTestStratificationOverrideFile>(File.ReadAllText(absolutePath));
            if (overrideFile == null)
            {
                throw new InvalidOperationException($"Failed to deserialize gameplay test stratification overrides: {absolutePath}");
            }

            overrideFile.overrides ??= Array.Empty<GameplayTestStratificationOverrideEntry>();
            for (var i = 0; i < overrideFile.overrides.Length; i++)
            {
                overrideFile.overrides[i].contracts ??= Array.Empty<string>();
            }

            return overrideFile;
        }
    }

    internal static class GameplayTestSourceDiscovery
    {
        private static readonly Regex NamespaceRegex = new(@"namespace\s+(?<name>[A-Za-z0-9_.]+)", RegexOptions.Compiled);
        private static readonly Regex ClassRegex = new(@"(?:public|internal)\s+(?:sealed\s+|static\s+|partial\s+)*class\s+(?<name>[A-Za-z0-9_]+)", RegexOptions.Compiled);
        private static readonly Regex TestAttributeRegex = new(@"^\s*\[(Test|UnityTest)\]\s*$", RegexOptions.Compiled);
        private static readonly Regex CategoryRegex = new(@"Category\(""(?<category>Core|Extended|Full)""\)", RegexOptions.Compiled);
        private static readonly Regex SignatureRegex = new(@"^\s*public\s+[^\(]*?\s+(?<name>[A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);

        internal static GameplayDiscoveredTestSource[] Discover()
        {
            var testRootPath = GameplayTestStratificationPaths.GetAbsolutePath(GameplayTestStratificationPaths.TestRootRelativePath);
            var projectRootPath = GameplayTestStratificationPaths.GetProjectRootPath();
            var buffer = new List<GameplayDiscoveredTestSource>();
            var sourceFilePaths = Directory.GetFiles(testRootPath, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < sourceFilePaths.Length; i++)
            {
                ParseFile(sourceFilePaths[i], projectRootPath, buffer);
            }

            return buffer
                .OrderBy(test => test.sourcePath, StringComparer.Ordinal)
                .ThenBy(test => test.lineNumber)
                .ToArray();
        }

        private static void ParseFile(
            string absolutePath,
            string projectRootPath,
            List<GameplayDiscoveredTestSource> buffer)
        {
            var content = File.ReadAllText(absolutePath);
            var lines = File.ReadAllLines(absolutePath);

            var namespaceMatch = NamespaceRegex.Match(content);
            var classMatch = ClassRegex.Match(content);
            if (!namespaceMatch.Success || !classMatch.Success)
            {
                return;
            }

            var namespaceName = namespaceMatch.Groups["name"].Value;
            var className = classMatch.Groups["name"].Value;
            var relativePath = MakeRelativePath(projectRootPath, absolutePath);
            var mode = relativePath.IndexOf("/PlayMode/", StringComparison.Ordinal) >= 0
                ? "PlayMode"
                : "EditMode";

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!TestAttributeRegex.IsMatch(lines[lineIndex]))
                {
                    continue;
                }

                var attributeStart = lineIndex;
                while (attributeStart > 0 && IsAttributeLine(lines[attributeStart - 1]))
                {
                    attributeStart--;
                }

                var signatureLine = lineIndex + 1;
                while (signatureLine < lines.Length && !SignatureRegex.IsMatch(lines[signatureLine]))
                {
                    signatureLine++;
                }

                if (signatureLine >= lines.Length)
                {
                    continue;
                }

                var signatureMatch = SignatureRegex.Match(lines[signatureLine]);
                if (!signatureMatch.Success)
                {
                    continue;
                }

                var primaryCategories = new List<string>();
                for (var attributeLine = attributeStart; attributeLine < signatureLine; attributeLine++)
                {
                    var categoryMatch = CategoryRegex.Match(lines[attributeLine]);
                    if (categoryMatch.Success)
                    {
                        primaryCategories.Add(categoryMatch.Groups["category"].Value);
                    }
                }

                buffer.Add(new GameplayDiscoveredTestSource
                {
                    fullyQualifiedName = $"{namespaceName}.{className}.{signatureMatch.Groups["name"].Value}",
                    category = primaryCategories.Count == 1 ? primaryCategories[0] : string.Empty,
                    mode = mode,
                    sourcePath = relativePath,
                    lineNumber = signatureLine + 1,
                    primaryCategoryCount = primaryCategories.Count,
                });
            }
        }

        private static bool IsAttributeLine(string line)
        {
            return line.TrimStart().StartsWith("[", StringComparison.Ordinal);
        }

        private static string MakeRelativePath(string projectRootPath, string absolutePath)
        {
            var normalizedProjectRoot = Path.GetFullPath(projectRootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var normalizedAbsolutePath = Path.GetFullPath(absolutePath);
            if (normalizedAbsolutePath.StartsWith(normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolutePath.Substring(normalizedProjectRoot.Length).Replace('\\', '/');
            }

            return normalizedAbsolutePath.Replace('\\', '/');
        }
    }
}
