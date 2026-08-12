using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageLoadArchitectureTests
    {
        [Test]
        public void RemovedStandaloneRuntimeTypes_AreAbsent()
        {
            var runtimeAssembly = typeof(StageId).Assembly;

            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageRun" + "Id"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageCompletionAttempt" + "Id"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageLoadSource" + "Mode"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.RewardGrant" + "Id"),
                Is.Null);
        }

        [Test]
        public void RemovedSessionAndClearPayloadContracts_AreAbsent()
        {
            var runtimeAssembly = typeof(StageId).Assembly;

            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageSessionMetric" + "Value"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageChallengeRuntime" + "State"),
                Is.Null);
            Assert.That(typeof(StageSessionState).GetProperty("Session" + "Metrics"), Is.Null);
            Assert.That(typeof(StageSessionState).GetProperty("ChallengeRuntime" + "States"), Is.Null);
            Assert.That(typeof(StageClearResult).GetProperty("Session" + "Metrics" + "Snapshot"), Is.Null);
            Assert.That(typeof(StageClearResult).GetProperty("ChallengeRuntime" + "States"), Is.Null);
        }

        [Test]
        public void CompletionBoundary_ExposesOnlyMinimalSemanticProperties()
        {
            var runtimeAssembly = typeof(StageId).Assembly;
            var clearResultProperties = typeof(StageClearResult)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var readModelProperties = typeof(MinimalStageCompletionReadModel)
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var clearConstructorParameters = typeof(StageClearResult)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();

            Assert.That(
                clearResultProperties,
                Is.EqualTo(new[] { "FinalTickIndex", "StageId" }));
            Assert.That(
                clearConstructorParameters,
                Is.EqualTo(new[] { typeof(StageId), typeof(int) }));
            Assert.That(
                readModelProperties,
                Is.EqualTo(new[]
                {
                    "ContinueRequest",
                    "FinalTickIndex",
                    "NextStageRequest",
                    "RetryRequest",
                    "StageId",
                }));
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.MinimalStageCompletion" + "Result"),
                Is.Null);
            Assert.That(
                runtimeAssembly.GetType("Game.Feature.Stages.StageClear" + "Source"),
                Is.Null);
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceRemovedCompatLoadPaths()
        {
            var featuresRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features"));
            var matches = Directory
                .GetFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageLoadArchitectureTests.cs", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Editor/", StringComparison.Ordinal) &&
                               !path.Contains("\\Editor\\", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("SerializedStageContentEntry") ||
                           source.Contains("LegacyStageDefinition") ||
                           source.Contains("StageLoadStrategyFactory") ||
                           source.Contains("IStageLoadStrategy");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void RuntimeFolder_DoesNotReferenceEditorOnlyLegacyPresentationBridge()
        {
            var runtimeRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Stages/Runtime"));
            var matches = Directory
                .GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("LegacyStagePresentationEditorBridge") ||
                           source.Contains("LegacyStagePresentationBridge") ||
                           source.Contains("CreateEditorDirectPlayFallback") ||
                           source.Contains("StageLoadFallbackPolicy") ||
                           source.Contains("defaultStageId");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void StageLoadRequest_ConstructorIsNotUsedOutsideItsOwnFile()
        {
            var featuresRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features"));
            var matches = Directory
                .GetFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageLoadRequest.cs", StringComparison.Ordinal))
                .Where(path => !path.EndsWith("StageLoadArchitectureTests.cs", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("new StageLoadRequest(");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void AliasTableSetEntries_IsOnlyCalledFromGovernanceUpdater()
        {
            var stagesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Stages"));
            var matches = Directory
                .GetFiles(stagesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageIdAliasTable.cs", StringComparison.Ordinal))
                .Where(path => !path.EndsWith("StageAliasGovernanceUpdater.cs", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Tests/", StringComparison.Ordinal) &&
                               !path.Contains("\\Tests\\", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("aliasTable.SetEntries(");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void GovernanceLedgerSetEntries_IsOnlyCalledFromGovernanceUpdater()
        {
            var stagesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Stages"));
            var matches = Directory
                .GetFiles(stagesRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageAliasGovernanceLedger.cs", StringComparison.Ordinal))
                .Where(path => !path.EndsWith("StageAliasGovernanceUpdater.cs", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Tests/", StringComparison.Ordinal) &&
                               !path.Contains("\\Tests\\", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("ledger.SetEntries(");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }
    }
}
