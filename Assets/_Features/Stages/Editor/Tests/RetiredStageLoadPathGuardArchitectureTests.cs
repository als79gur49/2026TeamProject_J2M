using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class RetiredStageLoadPathGuardArchitectureTests
    {
        [Test]
        public void RuntimeAssembly_DoesNotReferenceRemovedCompatLoadPaths()
        {
            var featuresRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features"));
            var retiredLoadModeShellToken = "Stage" + "Load" + "Source" + "Mode";
            var matches = Directory
                .GetFiles(featuresRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("RetiredStageLoadPathGuardArchitectureTests.cs", StringComparison.Ordinal))
                .Where(path => !path.Contains("/Editor/", StringComparison.Ordinal) &&
                               !path.Contains("\\Editor\\", StringComparison.Ordinal))
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("SerializedStageContentEntry") ||
                           source.Contains("LegacyStageDefinition") ||
                           source.Contains(retiredLoadModeShellToken) ||
                           source.Contains("StageLoadStrategyFactory") ||
                           source.Contains("IStageLoadStrategy");
                })
                .Select(path => path.Replace('\\', '/'))
                .ToArray();

            Assert.That(matches, Is.Empty);
        }

        [Test]
        public void RetiredGuard_IsEditorGovernanceAndDoesNotMoveIntoRuntime()
        {
            var runtimeRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Stages/Runtime"));
            var matches = Directory
                .GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("RetiredStageLoadPathGuard") ||
                           source.Contains("RetiredStageLoadPathGuardSummary") ||
                           source.Contains("RetiredStageLoadPathInstallerResidue");
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
                .Where(path => !path.EndsWith("RetiredStageLoadPathGuardArchitectureTests.cs", StringComparison.Ordinal))
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
        public void CiReportVocabulary_DoesNotExposeRetiredPathsAsLoadModes()
        {
            var ciEntryPointSource = File.ReadAllText(
                "Assets/_Features/Stages/Editor/Validation/StageCatalogCiValidationEntryPoint.cs");

            Assert.That(ciEntryPointSource, Does.Contain("Scene Bootstrap Guard Summary"));
            Assert.That(ciEntryPointSource, Does.Contain("LaunchContextCatalogResolvedInstallers"));
            Assert.That(ciEntryPointSource, Does.Contain("RetiredSerializedStageContentEntryResidue"));
            Assert.That(ciEntryPointSource, Does.Contain("RetiredLegacyStageDefinitionResidue"));
            Assert.That(ciEntryPointSource, Does.Contain("RemovedDefaultStageIdFallbackResidue"));
            Assert.That(ciEntryPointSource, Does.Contain("RemovedDirectStageDefinitionLoadResidue"));
            Assert.That(ciEntryPointSource, Does.Contain("EditorDirectPlayMappingSupport"));
            Assert.That(ciEntryPointSource, Does.Contain("Authoring Surface Classification"));
            Assert.That(ciEntryPointSource, Does.Contain("StageContentEntry"));
            Assert.That(ciEntryPointSource, Does.Contain("StageDefinition"));
            Assert.That(ciEntryPointSource, Does.Contain("Reward / Progression / ClearEvaluation"));
            Assert.That(ciEntryPointSource, Does.Contain("PresentationId"));
            Assert.That(ciEntryPointSource, Does.Contain("Stage Content Inventory / Retired Residue Audit"));
            Assert.That(ciEntryPointSource, Does.Contain("CanonicalGameplayCompanionCount"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("Scene Mode Summary"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("Load Source Mode"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("DefaultStageId fallback"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("Direct StageDefinition option"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("FallbackStage option"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("Stage Compat Audit"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("CanonicalGameplayAssetCount"));
            Assert.That(ciEntryPointSource, Does.Not.Contain("stage-compat-audit.md"));
        }

        [Test]
        public void AuthoringSurfaceClassification_IsEditorOnlyDisplayHelper()
        {
            var helperPath =
                "Assets/_Features/Stages/Editor/Validation/StageContentInventoryAndAudit.cs";
            var helperSource = File.ReadAllText(helperPath);

            Assert.That(helperSource, Does.Contain("StageAuthoringSurfaceKind"));
            Assert.That(helperSource, Does.Contain("StageRoot"));
            Assert.That(helperSource, Does.Contain("GameplayCompanion"));
            Assert.That(helperSource, Does.Contain("RetiredCompanionGuard"));
            Assert.That(helperSource, Does.Contain("RetiredLoadDetector"));
            Assert.That(helperSource, Does.Contain("EditorDirectPlaySupport"));
            Assert.That(helperSource, Does.Contain("PresentationOnlyBinding"));
            Assert.That(helperSource, Does.Contain("WeakHelperReference"));
            Assert.That(helperSource, Does.Not.Contain("ScriptableObject"));
            Assert.That(helperSource, Does.Not.Contain("SerializeField"));
            Assert.That(helperSource, Does.Not.Contain("CreateAssetMenu"));

            var runtimeRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Stages/Runtime"));
            var matches = Directory
                .GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var source = File.ReadAllText(path);
                    return source.Contains("StageAuthoringSurfaceKind") ||
                           source.Contains("StageAuthoringSurfaceClassificationLabels");
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
