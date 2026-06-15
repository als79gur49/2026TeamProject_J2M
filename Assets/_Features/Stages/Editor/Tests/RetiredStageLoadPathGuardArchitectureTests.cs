using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class RetiredStageLoadPathGuardArchitectureTests
    {
        private const string PreWorkbenchGuardDocPath =
            "Docs/Testing/Stage-Authoring-Pre-Workbench-Acceptance-Guard-2026-06-16.md";

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
        public void PreWorkbenchAcceptanceGuardDocument_RecordsCurrentClassificationMatrix()
        {
            var document = File.ReadAllText(PreWorkbenchGuardDocPath);

            Assert.That(document, Does.Contain("Acceptance Guard Matrix"));
            Assert.That(document, Does.Contain("StageContentEntry"));
            Assert.That(document, Does.Contain("Stage Root"));
            Assert.That(document, Does.Contain("StageDefinition"));
            Assert.That(document, Does.Contain("Gameplay Companion"));
            Assert.That(document, Does.Contain("Reward / Progression / ClearEvaluation"));
            Assert.That(document, Does.Contain("Retired Companion Guard"));
            Assert.That(document, Does.Contain("RetiredStageLoadPathGuard"));
            Assert.That(document, Does.Contain("Retired Load Guard"));
            Assert.That(document, Does.Contain("defaultStageId"));
            Assert.That(document, Does.Contain("direct `stageDefinition`"));
            Assert.That(document, Does.Contain("Retired Load Detector"));
            Assert.That(document, Does.Contain("StageEditorDirectPlayCatalog"));
            Assert.That(document, Does.Contain("StageEditorDirectPlayLauncher"));
            Assert.That(document, Does.Contain("StageEditorDirectPlayWindow"));
            Assert.That(document, Does.Contain("Editor Direct-Play Support"));
            Assert.That(document, Does.Contain("PresentationId"));
            Assert.That(document, Does.Contain("Presentation-Only Binding"));
            Assert.That(document, Does.Contain("Weak Helper / Reference"));
            Assert.That(document, Does.Contain("StageAuthoringSurfaceKind"));
            Assert.That(document, Does.Contain("StageAuthoringSurfaceClassificationLabels"));
            Assert.That(document, Does.Contain("Forbidden Vocabulary Policy"));
            Assert.That(document, Does.Contain("This is not a Workbench implementation"));
        }

        [Test]
        public void PreWorkbenchForbiddenVocabulary_DoesNotReappearAsActiveContract()
        {
            var hits = FindActiveForbiddenVocabularyHits().ToArray();

            Assert.That(hits, Is.Empty, string.Join(Environment.NewLine, hits));
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

        private static IEnumerable<string> FindActiveForbiddenVocabularyHits()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var forbiddenVocabulary = new[]
            {
                "StageDefinition root",
                "Create Stage Content Entry From Selected StageDefinition",
                "Stage Compat Audit",
                "CanonicalGameplayAssetCount",
                "GameplayAssets as root",
                "StageGameplayAssetInventoryItem",
                "Scene Mode Summary",
                "StageLoadSourceMode",
                "CatalogResolvedStageId",
                "defaultStageId fallback",
                "defaultStageId runtime fallback",
                "Direct StageDefinition option",
                "production fallback",
                "runtime recovery path",
                "RewardAuthoring",
                "ProgressionAuthoring",
                "ClearEvaluationAuthoring",
                "active missing companion",
            };

            foreach (var root in new[] { "Assets", "Docs", "Packages", "ProjectSettings" })
            {
                var absoluteRoot = Path.Combine(projectRoot, root);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
                {
                    if (ShouldSkipVocabularyScanPath(projectRoot, file))
                    {
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                    var lines = File.ReadAllLines(file);
                    for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        if (IsAllowedForbiddenVocabularyContext(relativePath, line))
                        {
                            continue;
                        }

                        foreach (var token in forbiddenVocabulary)
                        {
                            if (line.Contains(token, StringComparison.Ordinal))
                            {
                                yield return $"{relativePath}:{lineIndex + 1}: {token}";
                            }
                        }
                    }
                }
            }
        }

        private static bool ShouldSkipVocabularyScanPath(string projectRoot, string absoluteFilePath)
        {
            var relativePath = Path.GetRelativePath(projectRoot, absoluteFilePath).Replace('\\', '/');
            var fileName = Path.GetFileName(relativePath);
            if (relativePath == PreWorkbenchGuardDocPath ||
                relativePath.EndsWith("RetiredStageLoadPathGuardArchitectureTests.cs", StringComparison.Ordinal) ||
                fileName.StartsWith("InitTestScene", StringComparison.Ordinal) && fileName.EndsWith(".unity", StringComparison.Ordinal))
            {
                return true;
            }

            if (relativePath.StartsWith("Docs/Archive/", StringComparison.Ordinal))
            {
                return true;
            }

            var extension = Path.GetExtension(relativePath);
            return extension.Equals(".meta", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedForbiddenVocabularyContext(string relativePath, string line)
        {
            if (relativePath.Contains("/Tests/", StringComparison.Ordinal) &&
                (line.Contains("Does.Not.Contain", StringComparison.Ordinal) ||
                 line.Contains("AssertForbiddenVocabulary", StringComparison.Ordinal)))
            {
                return true;
            }

            if (line.Contains("Disallowed wording", StringComparison.Ordinal) ||
                line.Contains("Forbidden", StringComparison.Ordinal) ||
                line.Contains("forbidden", StringComparison.Ordinal) ||
                line.Contains("금지", StringComparison.Ordinal) ||
                line.Contains("must not", StringComparison.Ordinal) ||
                line.Contains("should not", StringComparison.Ordinal) ||
                line.Contains("retired", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("historical", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
