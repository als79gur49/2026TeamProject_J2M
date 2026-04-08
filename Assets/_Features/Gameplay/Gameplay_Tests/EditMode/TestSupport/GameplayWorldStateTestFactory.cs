using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                BoardTraversalRules.Empty,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            BoardTraversalRules traversalRules)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                new CubeTopologyState(FaceId.Floor),
                traversalRules,
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
                BoardTraversalRules.Empty,
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
                BoardTraversalRules.Empty,
                GameplayTimingProfile.CreateDefault());
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology,
            GameplayTimingProfile timingProfile)
        {
            return CreateBounded(
                initialEntities,
                boardBounds,
                terrainData,
                topology,
                BoardTraversalRules.Empty,
                timingProfile);
        }

        public static WorldState CreateBounded(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology,
            BoardTraversalRules traversalRules,
            GameplayTimingProfile timingProfile)
        {
            var normalizedInitialEntities = SessionStartEntityNormalizer.Normalize(
                initialEntities,
                timingProfile ?? GameplayTimingProfile.CreateDefault());

            return GameplayCompositionRoot.CreateWorldState(
                normalizedInitialEntities,
                boardBounds,
                terrainData ?? GameplayTerrainData.Empty,
                topology,
                traversalRules ?? BoardTraversalRules.Empty);
        }
    }

    internal static class GameplayCliTestRunner
    {
        private const string TestNamesArg = "-codexTestNames";
        private const string ReportPathArg = "-codexTestReportPath";

        public static void RunEditModeTests()
        {
            try
            {
                var testNames = GetDelimitedArgumentValues(TestNamesArg);
                if (testNames.Length == 0)
                {
                    throw new InvalidOperationException($"Missing required command line argument: {TestNamesArg}");
                }

                var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
                var callbacks = new CommandLineCallbacks();
                runner.RegisterCallbacks(callbacks);

                var executionSettings = new ExecutionSettings(
                    new Filter
                    {
                        testMode = TestMode.EditMode,
                        testNames = testNames,
                    })
                {
                    runSynchronously = true,
                };

                runner.Execute(executionSettings);

                if (!callbacks.HasCompleted)
                {
                    throw new InvalidOperationException("Command line test runner did not report completion.");
                }

                var summary = callbacks.BuildSummary();
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
                EditorApplication.Exit(callbacks.ExitCode);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                EditorApplication.Exit(2);
            }
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

            public int ExitCode
            {
                get
                {
                    if (_rootResult == null)
                    {
                        return 2;
                    }

                    return _rootResult.FailCount > 0 ? 1 : 0;
                }
            }

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

            public string BuildSummary()
            {
                if (_rootResult == null)
                {
                    return "Result=Unknown";
                }

                var builder = new StringBuilder();
                builder.AppendLine($"Result={_rootResult.TestStatus}");
                builder.AppendLine($"Passed={_rootResult.PassCount}");
                builder.AppendLine($"Failed={_rootResult.FailCount}");
                builder.AppendLine($"Skipped={_rootResult.SkipCount}");
                builder.AppendLine($"Inconclusive={_rootResult.InconclusiveCount}");
                builder.AppendLine($"DurationSeconds={_rootResult.Duration:0.###}");

                var failedLeaves = new List<ITestResultAdaptor>();
                CollectFailedLeafResults(_rootResult, failedLeaves);
                if (failedLeaves.Count == 0)
                {
                    builder.AppendLine("FailedTests=None");
                    return builder.ToString();
                }

                builder.AppendLine("FailedTests:");
                for (var i = 0; i < failedLeaves.Count; i++)
                {
                    var failedResult = failedLeaves[i];
                    builder.AppendLine(failedResult.FullName);

                    if (!string.IsNullOrWhiteSpace(failedResult.Message))
                    {
                        builder.AppendLine(failedResult.Message.Trim());
                    }
                }

                return builder.ToString();
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
    }
}
