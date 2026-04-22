using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class StageLoadSourceModeArchitectureTests
    {
        [Test]
        public void RuntimeAssembly_DoesNotReferenceRemovedCompatLoadPaths()
        {
            var featuresRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features"));
            var matches = Directory
                .GetFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("StageLoadSourceModeArchitectureTests.cs", StringComparison.Ordinal))
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
                .Where(path => !path.EndsWith("StageLoadSourceModeArchitectureTests.cs", StringComparison.Ordinal))
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
