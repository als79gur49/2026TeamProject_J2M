using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PresentationLegacyDecommissionFreezeTests
    {
        private static readonly string[] ProductionRoots =
        {
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime",
            "Assets/_Features/Gameplay/Gameplay_PresentationContracts/Runtime",
            "Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime",
            "Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime",
            "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime",
            "Assets/_Features/Gameplay/Gameplay_Audio/Runtime",
            "Assets/_Features/Gameplay/Gameplay_ActionAudio/Runtime",
            "Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime",
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime",
        };

        private static readonly string[] YamlExecutionReferenceTokens =
        {
            "ExecutionMode",
            "TopologyPresentationExecutionMode",
            "DamageDeathVfxExecutionMode",
            "BoxMotionPresentationExecutionMode",
            "PlayerActionAnimationExecutionMode",
            "EnemyPresentationExecutionMode",
            "CoreGameplaySfxExecutionMode",
            "ActionAudioExecutionMode",
            "EnemyAudioExecutionMode",
        };

        private static readonly string[] ProductionConfigureFacades =
        {
            "ConfigureExecution",
            "ConfigureTopologyExecution",
            "ConfigureDamageDeathVfxExecution",
            "ConfigureBoxMotionPresentationExecution",
            "ConfigurePlayerActionAnimationExecution",
            "ConfigureEnemyPresentationExecution",
            "ConfigureCoreGameplaySfxExecution",
            "ConfigureActionAudioExecution",
            "ConfigureGameplayActionAudioExecution",
            "ConfigureEnemyAudioExecution",
        };

        [Test]
        [Category("Core")]
        public void CurrentProductionLegacyResidue_DoesNotExceedMonotonicManifest()
        {
            var result = PresentationLegacyFreezeAnalyzer.Analyze(
                ReadProductionSources(),
                LoadResidueManifest(),
                ProductionConfigureFacades);

            Assert.That(result.ActualCount, Is.GreaterThan(0));
            Assert.That(result.UnknownHits, Is.Empty, FormatViolations(result.UnknownHits));
            Assert.That(result.CountViolations, Is.Empty, FormatViolations(result.CountViolations));
            Assert.That(result.MovedFileViolations, Is.Empty, FormatViolations(result.MovedFileViolations));
            Assert.That(
                LoadResidueManifest().Select(row => row.Domain).Distinct().OrderBy(value => value).ToArray(),
                Is.EqualTo(new[]
                {
                    "Box Motion",
                    "Core Gameplay SFX",
                    "Damage/Death VFX",
                    "Enemy One-shot Audio",
                    "Enemy Presentation",
                    "Gameplay Action Audio",
                    "Player Action Animation",
                    "Retained Owner / Unrelated",
                    "Topology",
                }));
        }

        [Test]
        [Category("Core")]
        public void ConfigureExecutionFacadeProductionCallSites_DoNotExpand()
        {
            var sources = ReadProductionSources();
            var result = PresentationLegacyFreezeAnalyzer.AnalyzeConfigureFacades(
                sources,
                LoadConfigureManifest());

            Assert.That(result.UnknownHits, Is.Empty, FormatViolations(result.UnknownHits));
            Assert.That(result.CountViolations, Is.Empty, FormatViolations(result.CountViolations));
            Assert.That(result.MovedFileViolations, Is.Empty, FormatViolations(result.MovedFileViolations));
        }

        [Test]
        [Category("Core")]
        public void ExecutionModeEnums_PreserveLegacyZeroAndProductionOneUntilDeleted()
        {
            foreach (var row in LoadEnumManifest())
            {
                Assert.That(row.EnumType.IsEnum, Is.True, row.EnumType.Name);
                Assert.That(Enum.GetNames(row.EnumType), Has.Length.EqualTo(2), row.EnumType.Name);
                Assert.That(Convert.ToInt32(Enum.Parse(row.EnumType, row.LegacyMember)), Is.EqualTo(0), row.EnumType.Name);
                Assert.That(Convert.ToInt32(Enum.Parse(row.EnumType, row.ProductionMember)), Is.EqualTo(1), row.EnumType.Name);
            }
        }

        [Test]
        [Category("Core")]
        public void SerializedExecutionModeReferences_DoNotExpandInSourceOrUnityAssets()
        {
            var sourceResult = PresentationLegacyFreezeAnalyzer.AnalyzeSerializedSourceFields(
                ReadProductionSources(),
                LoadSerializedFieldManifest());
            var reflectionResult = PresentationLegacyFreezeAnalyzer.AnalyzeSerializedRuntimeFields(
                LoadSerializedReflectionTargets(),
                LoadSerializedFieldManifest());
            var yamlResult = PresentationLegacyFreezeAnalyzer.AnalyzeYamlExecutionReferences(
                ReadUnityYamlAssets(),
                YamlExecutionReferenceTokens,
                maximumAllowed: 0);

            Assert.That(sourceResult.UnknownHits, Is.Empty, FormatViolations(sourceResult.UnknownHits));
            Assert.That(sourceResult.CountViolations, Is.Empty, FormatViolations(sourceResult.CountViolations));
            Assert.That(reflectionResult.UnknownHits, Is.Empty, FormatViolations(reflectionResult.UnknownHits));
            Assert.That(reflectionResult.CountViolations, Is.Empty, FormatViolations(reflectionResult.CountViolations));
            Assert.That(yamlResult.UnknownHits, Is.Empty, FormatViolations(yamlResult.UnknownHits));
            Assert.That(yamlResult.CountViolations, Is.Empty, FormatViolations(yamlResult.CountViolations));
        }

        [Test]
        [Category("Core")]
        public void CoordinatorPresenterFactory_DoNotRegainLegacyExecutionResponsibility()
        {
            var coordinator = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs");
            var presenter = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs");
            var hostFactory = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs");
            var compositionFactory = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs");
            var pendingPlanResult = PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                new Dictionary<string, string>
                {
                    ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                        coordinator,
                    ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs"] = presenter,
                    ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs"] = hostFactory,
                    ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs"] =
                        compositionFactory,
                },
                LoadPendingPlanCallManifest());

            foreach (var forbiddenCoordinatorToken in new[]
                     {
                         "ExecutionPolicy.Normalize",
                         "UseProductionExecutor(",
                         "new GameplayPresentationPipeline",
                         "CreateDamageDeathVfxExecutionPipeline",
                         "CreateBoxMotionExecutionPipeline",
                         "CreatePlayerActionAnimationExecutionPipeline",
                         "CreateEnemyPresentationExecutionPipeline",
                         "CreateCoreGameplaySfxExecutionPipeline",
                         "CreateActionAudioExecutionPipeline",
                         "CreateEnemyAudioExecutionPipeline",
                         "TryBeginExecution(",
                         "RecordSkippedByPolicy(",
                         "LegacyFallback",
                     })
            {
                Assert.That(coordinator, Does.Not.Contain(forbiddenCoordinatorToken), forbiddenCoordinatorToken);
            }

            foreach (var forbiddenPresenterToken in new[]
                     {
                         "ExecutionPolicy.Normalize",
                         "UseProductionExecutor(",
                         "new GameplayPresentationRuntimeComposition",
                         "GameplayPresentationRuntimeCompositionFactory.Create",
                         "TryBeginExecution(",
                         "RecordSkippedByPolicy(",
                     })
            {
                Assert.That(presenter, Does.Not.Contain(forbiddenPresenterToken), forbiddenPresenterToken);
            }

            foreach (var forbiddenFactoryToken in new[]
                     {
                         "TryBeginExecution(",
                         "RecordSkippedByPolicy(",
                         "LegacyFallback",
                         "UseProductionExecutor(",
                         "SuppressLegacy",
                     })
            {
                Assert.That(hostFactory, Does.Not.Contain(forbiddenFactoryToken), forbiddenFactoryToken);
                Assert.That(compositionFactory, Does.Not.Contain(forbiddenFactoryToken), forbiddenFactoryToken);
            }

            Assert.That(pendingPlanResult.UnknownHits, Is.Empty, FormatViolations(pendingPlanResult.UnknownHits));
            Assert.That(pendingPlanResult.CountViolations, Is.Empty, FormatViolations(pendingPlanResult.CountViolations));
            Assert.That(pendingPlanResult.MovedFileViolations, Is.Empty, FormatViolations(pendingPlanResult.MovedFileViolations));
        }

        [Test]
        [Category("Core")]
        public void AnalyzerSelfTest_AcceptsMonotonicResidueRemoval()
        {
            var manifest = new[]
            {
                new ResidueBudget(
                    "Topology",
                    "EXECUTION_MODE",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs",
                    "AllowedType",
                    "AllowedMember",
                    "Legacy",
                    2,
                    "approved current residue",
                    "PR1"),
            };
            var baseline = new Dictionary<string, string>
            {
                ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] = "Legacy Legacy",
            };
            var decreased = new Dictionary<string, string>
            {
                ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] = "Legacy",
            };
            var symbolDeleted = new Dictionary<string, string>
            {
                ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] = string.Empty,
            };
            var fileDeleted = new Dictionary<string, string>();

            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(baseline, manifest, ProductionConfigureFacades).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(decreased, manifest, ProductionConfigureFacades).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(symbolDeleted, manifest, ProductionConfigureFacades).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(fileDeleted, manifest, ProductionConfigureFacades).HasViolations,
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void AnalyzerSelfTest_AcceptsRetainedPendingPlanDeletionAndSerializedProperties()
        {
            var pendingManifest = new[]
            {
                new PendingPlanCallBudget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
                    "GameplayTickPresentationCoordinator",
                    "Present",
                    "_blockAudioPresentationController",
                    "BlockAudioPresentationController",
                    "ReplacePendingPlan",
                    "RETAINED_ADJUNCT_PRESENTATION_CONTROLLER",
                    1),
            };
            var serializedManifest = new[]
            {
                new SerializedFieldBudget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs",
                    "public TopologyPresentationExecutionMode TopologyPresentationExecutionMode",
                    1),
            };

            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { _blockAudioPresentationController.ReplacePendingPlan(Array.Empty<BlockAudioRequest>()); } }",
                    },
                    pendingManifest).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { } }",
                    },
                    pendingManifest).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    new[]
                    {
                        new PendingPlanCallBudget(
                            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
                            "GameplayTickPresentationCoordinator",
                            "Present",
                            "_blockAudioPresentationController",
                            "BlockAudioPresentationController",
                            "ReplacePendingPlan",
                            "RETAINED_ADJUNCT_PRESENTATION_CONTROLLER",
                            2),
                    }).HasViolations,
                Is.False);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeSerializedSourceFields(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] =
                            "public TopologyPresentationExecutionMode TopologyPresentationExecutionMode = " +
                            "TopologyPresentationExecutionDefaults.ProductionDefault;",
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/ReadonlyProperties.cs"] =
                            "// public EnemyAudioExecutionMode CommentedMode; " +
                            "private const string Fake = \"public EnemyAudioExecutionMode StringMode;\"; " +
                            "private const char Separator = ';'; " +
                            "public BoxMotionPresentationExecutionMode ExecutionMode => _guard.Diagnostics.Mode; " +
                            "public EnemyAudioExecutionMode EnemyMode { get; } " +
                            "public CoreGameplaySfxExecutionMode SfxMode { get { return _mode; } } " +
                            "public ActionAudioExecutionMode ActionMode { get; private set; }",
                    },
                    serializedManifest).HasViolations,
                Is.False);
        }

        [Test]
        [Category("Core")]
        public void AnalyzerSelfTest_RejectsResidueGrowthMovementConfigureAndSerialization()
        {
            var manifest = new[]
            {
                new ResidueBudget(
                    "Topology",
                    "EXECUTION_MODE",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs",
                    "AllowedType",
                    "AllowedMember",
                    "Legacy",
                    1,
                    "approved current residue",
                    "PR1"),
            };
            var configureManifest = new[]
            {
                new ConfigureBudget(
                    "ConfigureDamageDeathVfxExecution",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs",
                    1,
                    "test synthetic baseline"),
            };
            var serializedManifest = new[]
            {
                new SerializedFieldBudget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs",
                    "public TopologyPresentationExecutionMode TopologyPresentationExecutionMode",
                    1),
            };
            var pendingManifest = new[]
            {
                new PendingPlanCallBudget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs",
                    "GameplayTickPresentationCoordinator",
                    "Present",
                    "_blockAudioPresentationController",
                    "BlockAudioPresentationController",
                    "ReplacePendingPlan",
                    "RETAINED_ADJUNCT_PRESENTATION_CONTROLLER",
                    1),
            };

            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] = "Legacy Legacy",
                    },
                    manifest,
                    ProductionConfigureFacades).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.Analyze(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] = string.Empty,
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Moved.cs"] = "Legacy",
                    },
                    manifest,
                    ProductionConfigureFacades).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeConfigureFacades(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] =
                            "ConfigureDamageDeathVfxExecution(",
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/NewCaller.cs"] =
                            "ConfigureDamageDeathVfxExecution(",
                    },
                    configureManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeSerializedSourceFields(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/Allowed.cs"] =
                            "public TopologyPresentationExecutionMode TopologyPresentationExecutionMode = TopologyPresentationExecutionMode.ExecutorBridge;",
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/NewSerialized.cs"] =
                            "[SerializeField] private EnemyAudioExecutionMode enemyAudioExecutionMode;",
                    },
                    serializedManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { " +
                            "_blockAudioPresentationController.ReplacePendingPlan(null); " +
                            "_blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly GameplayAudioPresentationController _legacyController; " +
                            "public void Present() { _legacyController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs"] =
                            "internal sealed class GameplayTickViewPresenter { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _unknownController; " +
                            "public void Present() { _unknownController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs"] =
                            "internal sealed class GameplayHostRuntimeFactory { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Create() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly GameplayAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/MovedCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "public void Present() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzePendingPlanCalls(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs"] =
                            "internal sealed class GameplayTickPresentationCoordinator { " +
                            "private readonly BlockAudioPresentationController _blockAudioPresentationController; " +
                            "private void Helper() { _blockAudioPresentationController.ReplacePendingPlan(null); } }",
                    },
                    pendingManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeSerializedSourceFields(
                    new Dictionary<string, string>
                    {
                        ["Assets/_Features/Gameplay/Gameplay_Host/Runtime/NewSerialized.cs"] =
                            "public EnemyAudioExecutionMode EnemyAudioExecutionMode; " +
                            "[Obsolete] [SerializeField] private TopologyPresentationExecutionMode _topologyMode = TopologyPresentationExecutionMode.ExecutorBridge; " +
                            "[SerializeField] private CoreGameplaySfxExecutionMode _coreMode; " +
                            "[SerializeReference] private ActionAudioExecutionMode _actionMode; " +
                            "[field: SerializeField] public PlayerActionAnimationExecutionMode PlayerMode { get; private set; } " +
                            "[field: SerializeReference] public EnemyPresentationExecutionMode EnemyMode { get; private set; }",
                    },
                    serializedManifest).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeYamlExecutionReferences(
                    new Dictionary<string, string>
                    {
                        ["Assets/Presentation.prefab"] = "Game.Feature.Gameplay.Host.EnemyAudioExecutionMode",
                    },
                    YamlExecutionReferenceTokens,
                    maximumAllowed: 0).HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeEnumSource(
                    "public enum SampleExecutionMode { Legacy = 1, Production = 0 }",
                    "SampleExecutionMode",
                    "Legacy",
                    "Production").HasViolations,
                Is.True);
            Assert.That(
                PresentationLegacyFreezeAnalyzer.AnalyzeEnumSource(
                    "public enum SampleExecutionMode { Legacy = 0, Production = 1, Rollback = 2 }",
                    "SampleExecutionMode",
                    "Legacy",
                    "Production").HasViolations,
                Is.True);
        }

        private static Dictionary<string, string> ReadProductionSources()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var root in ProductionRoots)
            {
                var absoluteRoot = ToAbsolutePath(root);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
                {
                    var relativePath = ToRepoRelativePath(path);
                    if (relativePath.Contains("/Editor/") ||
                        relativePath.Contains("/Gameplay_Tests/") ||
                        relativePath.StartsWith("Docs/", StringComparison.Ordinal) ||
                        relativePath.StartsWith("TestLogs/", StringComparison.Ordinal) ||
                        relativePath.StartsWith("TestResults/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result[relativePath] = File.ReadAllText(path);
                }
            }

            return result;
        }

        private static Dictionary<string, string> ReadUnityYamlAssets()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var assetsRoot = ToAbsolutePath("Assets");
            foreach (var path in Directory.EnumerateFiles(assetsRoot, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = ToRepoRelativePath(path);
                if (!(relativePath.EndsWith(".prefab", StringComparison.Ordinal) ||
                      relativePath.EndsWith(".unity", StringComparison.Ordinal) ||
                      relativePath.EndsWith(".asset", StringComparison.Ordinal)))
                {
                    continue;
                }

                if (relativePath.StartsWith("Assets/InitTestScene", StringComparison.Ordinal) ||
                    relativePath.IndexOf("/Gameplay_Tests/", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                result[relativePath] = File.ReadAllText(path);
            }

            return result;
        }

        private static ResidueBudget[] LoadResidueManifest()
        {
            return ParseResidueManifest(ResidueManifestTsv);
        }

        private static ConfigureBudget[] LoadConfigureManifest()
        {
            return new[]
            {
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs", 1, "box motion lane configuration facade"),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs", 1, "core SFX lane configuration facade"),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs", 1, "damage/death VFX lane configuration facade"),
                new ConfigureBudget("ConfigureExecution", string.Empty, 0, "Enemy one-shot audio lane configuration facade removed in PR2."),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs", 1, "enemy presentation lane configuration facade"),
                new ConfigureBudget("ConfigureExecution", string.Empty, 0, "Gameplay action audio lane configuration facade removed in PR1."),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs", 6, "production default lane configuration call sites"),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 6, "coordinator test facade forwarding calls"),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs", 1, "player action animation lane configuration facade"),
                new ConfigureBudget("ConfigureExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs", 1, "topology lane configuration facade"),
                new ConfigureBudget("ConfigureTopologyExecution", string.Empty, 0, "No production facade exists in PR0 baseline."),
                new ConfigureBudget("ConfigureDamageDeathVfxExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 1, "coordinator test facade definition"),
                new ConfigureBudget("ConfigureDamageDeathVfxExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs", 2, "presenter test facade definition and forwarding call"),
                new ConfigureBudget("ConfigureBoxMotionPresentationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 1, "coordinator test facade definition"),
                new ConfigureBudget("ConfigureBoxMotionPresentationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs", 2, "presenter test facade definition and forwarding call"),
                new ConfigureBudget("ConfigurePlayerActionAnimationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 1, "coordinator test facade definition"),
                new ConfigureBudget("ConfigurePlayerActionAnimationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs", 2, "presenter test facade definition and forwarding call"),
                new ConfigureBudget("ConfigureEnemyPresentationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 1, "coordinator test facade definition"),
                new ConfigureBudget("ConfigureEnemyPresentationExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs", 2, "presenter test facade definition and forwarding call"),
                new ConfigureBudget("ConfigureCoreGameplaySfxExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs", 1, "coordinator test facade definition"),
                new ConfigureBudget("ConfigureCoreGameplaySfxExecution", "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs", 2, "presenter test facade definition and forwarding call"),
                new ConfigureBudget("ConfigureActionAudioExecution", string.Empty, 0, "Gameplay action audio configure facade removed in PR1."),
                new ConfigureBudget("ConfigureGameplayActionAudioExecution", string.Empty, 0, "No production facade exists in PR0 baseline."),
                new ConfigureBudget("ConfigureEnemyAudioExecution", string.Empty, 0, "Enemy one-shot audio configure facade removed in PR2."),
            };
        }

        private static SerializedFieldBudget[] LoadSerializedFieldManifest()
        {
            return new[]
            {
                new SerializedFieldBudget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
                    "public TopologyPresentationExecutionMode TopologyPresentationExecutionMode",
                    1),
            };
        }

        private static SerializedReflectionTarget[] LoadSerializedReflectionTargets()
        {
            return new[]
            {
                new SerializedReflectionTarget(
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs",
                    typeof(GameplaySceneHostConfiguration)),
            };
        }

        private static PendingPlanCallBudget[] LoadPendingPlanCallManifest()
        {
            const string path = "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
            const string containingType = "GameplayTickPresentationCoordinator";
            const string containingMember = "Present";
            const string method = "ReplacePendingPlan";
            const string classification = "RETAINED_ADJUNCT_PRESENTATION_CONTROLLER";

            return new[]
            {
                new PendingPlanCallBudget(
                    path,
                    containingType,
                    containingMember,
                    "_blockAudioPresentationController",
                    "BlockAudioPresentationController",
                    method,
                    classification,
                    1),
                new PendingPlanCallBudget(
                    path,
                    containingType,
                    containingMember,
                    "_tileFeatureAudioPresentationController",
                    "TileFeatureAudioPresentationController",
                    method,
                    classification,
                    1),
                new PendingPlanCallBudget(
                    path,
                    containingType,
                    containingMember,
                    "_gravityFieldAudioPresentationController",
                    "GravityFieldAudioPresentationController",
                    method,
                    classification,
                    1),
                new PendingPlanCallBudget(
                    path,
                    containingType,
                    containingMember,
                    "_topologyAudioPresentationController",
                    "TopologyAudioPresentationController",
                    method,
                    classification,
                    1),
            };
        }

        private static EnumBudget[] LoadEnumManifest()
        {
            return new[]
            {
                new EnumBudget(typeof(TopologyPresentationExecutionMode), "LegacyCoordinator", "ExecutorBridge"),
                new EnumBudget(typeof(DamageDeathVfxExecutionMode), "LegacyExtension", "OrchestrationExecutor"),
                new EnumBudget(typeof(BoxMotionPresentationExecutionMode), "LegacyTrackPlanner", "OrchestrationMotionExecutor"),
                new EnumBudget(typeof(EnemyPresentationExecutionMode), "LegacyEnemyPresentationMapper", "OrchestrationEnemyPresentationExecutor"),
                new EnumBudget(typeof(CoreGameplaySfxExecutionMode), "LegacyGameplayAudioController", "OrchestrationSfxBridgeExecutor"),
            };
        }

        private static ResidueBudget[] ParseResidueManifest(string tsv)
        {
            return tsv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(line => line.Split('\t'))
                .Select(parts => new ResidueBudget(
                    parts[0],
                    parts[1],
                    parts[2],
                    parts[3],
                    parts[4],
                    parts[5],
                    int.Parse(parts[6]),
                    parts[7],
                    parts[8]))
                .ToArray();
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(ToAbsolutePath(relativePath));
        }

        private static string ToAbsolutePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
        }

        private static string ToRepoRelativePath(string absolutePath)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
        }

        private static string FormatViolations(IEnumerable<string> violations)
        {
            return string.Join(Environment.NewLine, violations);
        }

        private const string ResidueManifestTsv =
@"Domain	Category	RelativePath	ContainingType	ContainingMember	Symbol	MaximumAllowed	AllowedRole	PlannedRemovalPhase
Retained Owner / Unrelated	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BlockAudioPresentationController.cs	BlockAudioPresentationController	Audio plan lifecycle	PendingPlan	6	Unrelated block-audio pending plan vocabulary	Out of scope
Box Motion	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Mode and diagnostics	ExecutionMode	33	Current box motion execution-mode residue	Box Motion Legacy Decommission
Box Motion	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Rollback diagnostics	Rollback	2	Current rollback-mode diagnostics residue	Box Motion Legacy Decommission
Box Motion	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Fallback constants	Fallback	4	Current fallback compatibility residue	Box Motion Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Legacy telemetry	Legacy	57	Current legacy owner diagnostics residue	Box Motion Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Legacy telemetry	ExecutedByLegacy	2	Current legacy execution counter	Box Motion Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Legacy telemetry	SkippedLegacy	2	Current skipped legacy counter	Box Motion Legacy Decommission
Box Motion	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationExecutor.cs	BoxMotionPresentationExecutor	Suppression telemetry	Suppressed	17	Current migration suppression diagnostics	Box Motion Legacy Decommission
Box Motion	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	Execution guard	ExecutionMode	11	Current lane execution-mode facade	Box Motion Legacy Decommission
Box Motion	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	ConfigureExecution	ExecutionPolicy	1	Current lane policy normalization	Box Motion Legacy Decommission
Box Motion	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Box Motion Legacy Decommission
Box Motion	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Box Motion Legacy Decommission
Box Motion	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Box Motion Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	Legacy policy telemetry	Legacy	23	Current lane legacy telemetry	Box Motion Legacy Decommission
Box Motion	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	Legacy fallback	Fallback	1	Current fallback compatibility residue	Box Motion Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	Legacy policy telemetry	SkippedLegacy	1	Current skipped legacy counter	Box Motion Legacy Decommission
Box Motion	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/BoxMotionPresentationLaneRuntime.cs	BoxMotionPresentationLaneRuntime	Legacy suppression	Suppressed	2	Current production owner suppression	Box Motion Legacy Decommission
Core Gameplay SFX	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Execution guard	ExecutionMode	12	Current lane execution-mode facade	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	ConfigureExecution	ExecutionPolicy	2	Current lane policy normalization	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Legacy policy telemetry	Legacy	6	Current lane legacy telemetry	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Playback fallback	Fallback	17	Current SFX playback fallback diagnostics	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Legacy policy telemetry	SkippedLegacy	2	Current skipped legacy counter	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Legacy suppression	Suppressed	10	Current production owner suppression	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/CoreGameplaySfxLaneRuntime.cs	CoreGameplaySfxLaneRuntime	Legacy pending plan	PendingPlan	2	Current legacy controller pending plan route	Core Gameplay SFX Legacy Decommission
Damage/Death VFX	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs	DamageDeathVfxPresentationExecutor	Mode and diagnostics	ExecutionMode	32	Current damage/death VFX execution-mode residue	Damage/Death VFX Legacy Decommission
Damage/Death VFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs	DamageDeathVfxPresentationExecutor	Legacy telemetry	Legacy	50	Current legacy owner diagnostics residue	Damage/Death VFX Legacy Decommission
Damage/Death VFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs	DamageDeathVfxPresentationExecutor	Legacy telemetry	ExecutedByLegacy	2	Current legacy execution counter	Damage/Death VFX Legacy Decommission
Damage/Death VFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs	DamageDeathVfxPresentationExecutor	Legacy telemetry	SkippedLegacy	3	Current skipped legacy counter	Damage/Death VFX Legacy Decommission
Damage/Death VFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationExecutor.cs	DamageDeathVfxPresentationExecutor	Suppression telemetry	Suppressed	36	Current death-over-damage suppression diagnostics	Damage/Death VFX Legacy Decommission
Damage/Death VFX	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	Execution guard	ExecutionMode	10	Current lane execution-mode facade	Damage/Death VFX Legacy Decommission
Damage/Death VFX	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	ConfigureExecution	ExecutionPolicy	2	Current lane policy normalization	Damage/Death VFX Legacy Decommission
Damage/Death VFX	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Damage/Death VFX Legacy Decommission
Damage/Death VFX	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Damage/Death VFX Legacy Decommission
Damage/Death VFX	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Damage/Death VFX Legacy Decommission
Damage/Death VFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	Legacy policy telemetry	Legacy	6	Current lane legacy telemetry	Damage/Death VFX Legacy Decommission
Damage/Death VFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DamageDeathVfxPresentationLaneRuntime.cs	DamageDeathVfxPresentationLaneRuntime	Legacy suppression	Suppressed	2	Current production owner suppression	Damage/Death VFX Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs	DefaultGameplayEntityViewFactory	Inactive visual setup	Legacy	1	Enemy inactive color fallback compatibility	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/DefaultGameplayEntityViewFactory.cs	DefaultGameplayEntityViewFactory	Inactive visual setup	Fallback	1	Enemy inactive color fallback compatibility	Out of scope
Enemy One-shot Audio	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAudioPresentationController.cs	EnemyAudioPresentationController	Legacy pending plan	PendingPlan	8	Current legacy controller pending plan route	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAudioPresentationController.cs	EnemyAudioPresentationController	Legacy suppression	Legacy	1	Current summon windup legacy route	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyAudioPresentationController.cs	EnemyAudioPresentationController	Legacy suppression	Suppressed	1	Current cue suppression diagnostics	Enemy One-shot Audio Legacy Decommission
Retained Owner / Unrelated	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyDeathExitEffectPlanBuilder.cs	EnemyDeathExitEffectPlanBuilder	Presentation fallback	Fallback	8	Death-exit presentation fallback, not legacy route	Out of scope
Enemy One-shot Audio	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	Execution guard	ExecutionMode	11	Current lane execution-mode facade	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	ConfigureExecution	ExecutionPolicy	2	Current lane policy normalization	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	Legacy policy telemetry	Legacy	2	Current lane legacy telemetry	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyOneShotAudioLaneRuntime.cs	EnemyOneShotAudioLaneRuntime	Legacy pending plan	PendingPlan	2	Current legacy controller pending plan route	Enemy One-shot Audio Legacy Decommission
Enemy Presentation	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Mode and diagnostics	ExecutionMode	36	Current enemy presentation execution-mode residue	Enemy Presentation Legacy Decommission
Enemy Presentation	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Rollback diagnostics	Rollback	2	Current rollback-mode diagnostics residue	Enemy Presentation Legacy Decommission
Enemy Presentation	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Fallback constants	Fallback	2	Current fallback compatibility residue	Enemy Presentation Legacy Decommission
Enemy Presentation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Legacy telemetry	Legacy	109	Current legacy owner diagnostics residue	Enemy Presentation Legacy Decommission
Enemy Presentation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Legacy telemetry	ExecutedByLegacy	2	Current legacy execution counter	Enemy Presentation Legacy Decommission
Enemy Presentation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Legacy telemetry	SkippedLegacy	3	Current skipped legacy counter	Enemy Presentation Legacy Decommission
Enemy Presentation	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationExecutor.cs	EnemyPresentationExecutor	Suppression telemetry	Suppressed	19	Current one-shot suppression diagnostics	Enemy Presentation Legacy Decommission
Enemy Presentation	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	Execution guard	ExecutionMode	6	Current lane execution-mode facade	Enemy Presentation Legacy Decommission
Enemy Presentation	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	ConfigureExecution	ExecutionPolicy	1	Current lane policy normalization	Enemy Presentation Legacy Decommission
Enemy Presentation	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Enemy Presentation Legacy Decommission
Enemy Presentation	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Enemy Presentation Legacy Decommission
Enemy Presentation	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Enemy Presentation Legacy Decommission
Enemy Presentation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/EnemyPresentationLaneRuntime.cs	EnemyPresentationLaneRuntime	Legacy policy telemetry	Legacy	18	Current lane legacy telemetry	Enemy Presentation Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipArcSampler.cs	FlipArcSampler	Math helper	Fallback	2	Non-route sampling fallback	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipArcSampler.cs	FlipArcSampler	Math helper	Normalize(	1	Non-route vector normalization	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/FlipImpactStayMotionCommandBuilder.cs	FlipImpactStayMotionCommandBuilder	Command key fallback	Fallback	2	Non-route correlation fallback	Out of scope
Gameplay Action Audio	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs	GameplayActionAudioLaneRuntime	Execution guard	ExecutionMode	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs	GameplayActionAudioLaneRuntime	UseProductionExecutor	UseProduction	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs	GameplayActionAudioLaneRuntime	ConfigureExecution	ConfigureExecution	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs	GameplayActionAudioLaneRuntime	Legacy policy telemetry	Legacy	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioLaneRuntime.cs	GameplayActionAudioLaneRuntime	Legacy pending plan	PendingPlan	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationController.cs	GameplayActionAudioPresentationController	Legacy pending plan	PendingPlan	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Mode and diagnostics	ExecutionMode	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Rollback diagnostics	Rollback	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Fallback constants	Fallback	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Legacy telemetry	Legacy	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Legacy telemetry	ExecutedByLegacy	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Legacy telemetry	SkippedLegacy	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Gameplay Action Audio	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayActionAudioPresentationExecutor.cs	GameplayActionAudioPresentationExecutor	Suppression telemetry	Suppressed	0	Removed in Gameplay Action Audio Legacy Decommission PR1	Gameplay Action Audio Legacy Decommission
Player Action Animation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs	GameplayAnimationSyncCoordinator	Retained owner mapping	Legacy	12	Retained animation sync owner mapping	Out of scope
Player Action Animation	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs	GameplayAnimationSyncCoordinator	Death fallback	Fallback	2	Retained death-facing fallback	Out of scope
Player Action Animation	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAnimationSyncCoordinator.cs	GameplayAnimationSyncCoordinator	One-shot suppression	Suppressed	4	Retained enemy one-shot suppression forwarding	Out of scope
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayAudioPresentationController.cs	GameplayAudioPresentationController	Legacy pending plan	PendingPlan	8	Current legacy controller pending plan route	Core Gameplay SFX Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayBoardSurfaceRenderer.cs	GameplayBoardSurfaceRenderer	Visual fallback	Fallback	18	Board renderer visual fallback, not legacy route	Out of scope
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayBoardSurfaceRenderer.cs	GameplayBoardSurfaceRenderer	Visual suppression	Suppressed	7	Renderer visual suppression, not migration route	Out of scope
Enemy One-shot Audio	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Mode and diagnostics	ExecutionMode	34	Current enemy audio execution-mode residue	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Rollback diagnostics	Rollback	2	Current rollback-mode diagnostics residue	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Fallback constants	Fallback	0	Enemy one-shot fallback residue removed in PR2	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Legacy telemetry	Legacy	0	Enemy one-shot legacy diagnostics removed in PR2	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Legacy telemetry	ExecutedByLegacy	0	Enemy one-shot legacy execution counter removed in PR2	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Legacy telemetry	SkippedLegacy	0	Enemy one-shot skipped legacy counter removed in PR2	Enemy One-shot Audio Legacy Decommission
Enemy One-shot Audio	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEnemyAudioPresentationExecutor.cs	GameplayEnemyAudioPresentationExecutor	Suppression telemetry	Suppressed	0	Enemy one-shot migration suppression removed in PR2	Enemy One-shot Audio Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs	GameplayEntityPresentationApplier	Presentation fallback	Fallback	3	Retained presentation applier fallback	Out of scope
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs	GameplayEntityPresentationApplier	Presentation suppression	Suppressed	10	Retained presentation applier suppression	Out of scope
Retained Owner / Unrelated	SERIALIZED_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs	GameplayHostRuntimeFactory	Configuration handoff	ExecutionMode	1	Reads existing topology execution-mode configuration	PR0 or later serialized cleanup
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs	GameplayHostRuntimeFactory	Configuration handoff	Suppressed	1	Existing damage/death suppression configuration handoff	PR0 or later serialized cleanup
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayMotionTimingResolver.cs	GameplayMotionTimingResolver	Timing compatibility	Legacy	2	Retained timing compatibility vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPoseResolver.cs	GameplayPoseResolver	Pose fallback	Legacy	1	Retained pose fallback vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPoseResolver.cs	GameplayPoseResolver	Pose fallback	Fallback	1	Retained pose fallback vocabulary	Out of scope
Topology	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationExecutionPolicies.cs	GameplayPresentationExecutionPolicies	All policies	ExecutionPolicy	8	Current execution policy compatibility	Presentation Legacy Decommission
Topology	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationExecutionPolicies.cs	GameplayPresentationExecutionPolicies	All policies	ExecutionMode	44	Current execution policy mode references	Presentation Legacy Decommission
Topology	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationExecutionPolicies.cs	GameplayPresentationExecutionPolicies	All policies	Normalize(	8	Current invalid-mode compatibility	Presentation Legacy Decommission
Topology	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationExecutionPolicies.cs	GameplayPresentationExecutionPolicies	All policies	Fallback	22	Current fallback policy compatibility	Presentation Legacy Decommission
Topology	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationExecutionPolicies.cs	GameplayPresentationExecutionPolicies	All policies	Legacy	24	Current legacy policy compatibility	Presentation Legacy Decommission
Retained Owner / Unrelated	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs	GameplayPresentationRuntimeCompositionFactory	Production default guard setup	ExecutionPolicy	8	Production default configuration only	Out of scope
Retained Owner / Unrelated	CONFIGURE_FACADE_CALL	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs	GameplayPresentationRuntimeCompositionFactory	Production default guard setup	ConfigureExecution	8	Approved production default lane configuration call sites	Out of scope
Retained Owner / Unrelated	LEGACY_PORT_OR_ADAPTER	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationRuntimeCompositionFactory.cs	GameplayPresentationRuntimeCompositionFactory	Topology legacy port binding	Legacy	1	Current topology legacy port construction	Topology Legacy Decommission
Retained Owner / Unrelated	SERIALIZED_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs	GameplaySceneHostConfiguration	Topology execution field	ExecutionMode	2	Existing serialized topology execution-mode field	PR0 or later serialized cleanup
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySceneHostConfiguration.cs	GameplaySceneHostConfiguration	Damage/death suppression field	Suppressed	1	Existing damage/death suppression field	PR0 or later serialized cleanup
Core Gameplay SFX	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Mode and diagnostics	ExecutionMode	29	Current core SFX execution-mode residue	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Fallback diagnostics	Fallback	93	Current SFX fallback diagnostics	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Legacy telemetry	Legacy	43	Current legacy owner diagnostics residue	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Legacy telemetry	ExecutedByLegacy	2	Current legacy execution counter	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Legacy telemetry	SkippedLegacy	2	Current skipped legacy counter	Core Gameplay SFX Legacy Decommission
Core Gameplay SFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplaySfxPresentationExecutor.cs	GameplaySfxPresentationExecutor	Suppression telemetry	Suppressed	50	Current suppression diagnostics	Core Gameplay SFX Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs	GameplayShowcaseSceneInstallerBase	Showcase compatibility	Legacy	5	Showcase installer compatibility vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs	GameplayShowcaseSceneInstallerBase	Showcase fallback	Fallback	5	Showcase installer fallback vocabulary	Out of scope
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs	GameplayShowcaseSceneInstallerBase	Showcase suppression	Suppressed	4	Showcase installer suppression vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneScaffold.cs	GameplayShowcaseSceneScaffold	Showcase compatibility	Legacy	4	Showcase scaffold compatibility vocabulary	Out of scope
Retained Owner / Unrelated	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Read-only diagnostics facade	ExecutionMode	40	Retained read-only diagnostics and test facade	Presentation Legacy Decommission
Retained Owner / Unrelated	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Initialize topology mode	ExecutionPolicy	1	Existing topology initialization default	Topology Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureExecution	6	Current test-only forwarding facade to lanes	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureDamageDeathVfxExecution	1	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureBoxMotionPresentationExecution	1	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigurePlayerActionAnimationExecution	1	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureEnemyPresentationExecution	1	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureCoreGameplaySfxExecution	1	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureActionAudioExecution	0	Removed for Gameplay Action Audio Legacy Decommission PR1	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Test configuration facades	ConfigureEnemyAudioExecution	0	Enemy one-shot configure facade removed in PR2	Presentation Legacy Decommission
Retained Owner / Unrelated	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Read-only diagnostics aggregation	Legacy	11	Retained read-only diagnostics aggregation	Out of scope
Retained Owner / Unrelated	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Read-only diagnostics aggregation	Rollback	2	Retained rollback diagnostics aggregation	Presentation Legacy Decommission
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Read-only diagnostics aggregation	Suppressed	4	Retained read-only diagnostics aggregation	Out of scope
Retained Owner / Unrelated	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs	GameplayTickPresentationCoordinator	Read-only diagnostics aggregation	PendingPlan	4	Retained read-only diagnostics aggregation	Out of scope
Retained Owner / Unrelated	LEGACY_PORT_OR_ADAPTER	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationExtension.cs	GameplayTickPresentationExtension	Legacy extension surface	Legacy	4	Current extension compatibility surface	Damage/Death VFX Legacy Decommission
Retained Owner / Unrelated	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test facade diagnostics	ExecutionMode	34	Retained read-only diagnostics and test facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureDamageDeathVfxExecution	2	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureBoxMotionPresentationExecution	2	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigurePlayerActionAnimationExecution	2	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureEnemyPresentationExecution	2	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureCoreGameplaySfxExecution	2	Current test-only forwarding facade	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureActionAudioExecution	0	Removed for Gameplay Action Audio Legacy Decommission PR1	Presentation Legacy Decommission
Retained Owner / Unrelated	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickViewPresenter.cs	GameplayTickViewPresenter	Test configuration facades	ConfigureEnemyAudioExecution	0	Enemy one-shot configure facade removed in PR2	Presentation Legacy Decommission
Box Motion	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTrackPlanner.cs	GameplayTrackPlanner	Box legacy track suppression	Legacy	14	Retained planner with current box legacy suppression residue	Box Motion Legacy Decommission
Box Motion	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTrackPlanner.cs	GameplayTrackPlanner	Pose fallback	Fallback	3	Retained planner pose fallback	Out of scope
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/GravityFieldAudioPresentationController.cs	GravityFieldAudioPresentationController	Audio plan lifecycle	PendingPlan	8	Unrelated gravity-field pending plan vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/MoonBlockEmergencePresentationController.cs	MoonBlockEmergencePresentationController	Visual normalization	Normalize(	3	Non-route visual normalization	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/MotionTrack.cs	MotionTrack	Track fallback	Fallback	2	Non-route motion sampling fallback	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/MotionTrack.cs	MotionTrack	Track normalization	Normalize(	1	Non-route vector normalization	Out of scope
Player Action Animation	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	Execution guard	ExecutionMode	6	Current lane execution-mode facade	Player Action Animation Legacy Decommission
Player Action Animation	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	ConfigureExecution	ExecutionPolicy	1	Current lane policy normalization	Player Action Animation Legacy Decommission
Player Action Animation	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Player Action Animation Legacy Decommission
Player Action Animation	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	UseProductionExecutor	UseProduction	3	Current production/legacy branch	Player Action Animation Legacy Decommission
Player Action Animation	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Player Action Animation Legacy Decommission
Player Action Animation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationLaneRuntime.cs	PlayerActionAnimationLaneRuntime	Legacy policy telemetry	Legacy	7	Current lane legacy telemetry	Player Action Animation Legacy Decommission
Player Action Animation	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Mode and diagnostics	ExecutionMode	32	Current player action animation execution-mode residue	Player Action Animation Legacy Decommission
Player Action Animation	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Rollback diagnostics	Rollback	2	Current rollback-mode diagnostics residue	Player Action Animation Legacy Decommission
Player Action Animation	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Fallback constants	Fallback	6	Current fallback compatibility residue	Player Action Animation Legacy Decommission
Player Action Animation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Legacy telemetry	Legacy	71	Current legacy owner diagnostics residue	Player Action Animation Legacy Decommission
Player Action Animation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Legacy telemetry	ExecutedByLegacy	2	Current legacy execution counter	Player Action Animation Legacy Decommission
Player Action Animation	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Legacy telemetry	SkippedLegacy	3	Current skipped legacy counter	Player Action Animation Legacy Decommission
Player Action Animation	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerActionAnimationPresentationExecutor.cs	PlayerActionAnimationPresentationExecutor	Suppression telemetry	Suppressed	14	Current one-shot suppression diagnostics	Player Action Animation Legacy Decommission
Player Action Animation	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimationTimingAuthoring.cs	PlayerAnimationTimingAuthoring	Timing authoring compatibility	Legacy	7	Retained animator timing compatibility vocabulary	Out of scope
Player Action Animation	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerAnimatorDriver.cs	PlayerAnimatorDriver	Timing compatibility	Legacy	3	Retained animator driver compatibility vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerDeathDisplacementPlanner.cs	PlayerDeathDisplacementPlanner	Death displacement fallback	Fallback	11	Non-route death displacement fallback	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerDeathDisplacementPlanner.cs	PlayerDeathDisplacementPlanner	Vector normalization	Normalize(	7	Non-route vector normalization	Out of scope
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerLocomotionAudioPresentationController.cs	PlayerLocomotionAudioPresentationController	Audio plan lifecycle	PendingPlan	7	Unrelated locomotion audio pending plan vocabulary	Out of scope
Core Gameplay SFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerLocomotionAudioPresentationController.cs	PlayerLocomotionAudioPresentationController	Audio suppression	Suppressed	8	Unrelated locomotion audio suppression vocabulary	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PlayerViewPresentationMapper.cs	PlayerViewPresentationMapper	Death fallback	Fallback	9	Retained death-facing fallback	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_Host/Runtime/PresentationMotionTrack.cs	PresentationMotionTrack	Correlation fallback	Fallback	9	Non-route correlation fallback	Out of scope
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs	StageBackedGameplaySceneInstallerBase	Installer suppression	Suppressed	2	Installer suppression vocabulary	Out of scope
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TileFeatureAudioPresentationController.cs	TileFeatureAudioPresentationController	Audio plan lifecycle	PendingPlan	6	Unrelated tile-feature audio pending plan vocabulary	Out of scope
Core Gameplay SFX	PENDING_PLAN	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyAudioPresentationController.cs	TopologyAudioPresentationController	Audio plan lifecycle	PendingPlan	6	Unrelated topology audio pending plan vocabulary	Out of scope
Topology	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Mode and diagnostics	ExecutionMode	49	Current topology execution-mode residue	Topology Legacy Decommission
Topology	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Rollback diagnostics	Rollback	2	Current rollback-mode diagnostics residue	Topology Legacy Decommission
Topology	ROLLBACK_OR_FALLBACK	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Fallback constants	Fallback	2	Current fallback compatibility residue	Topology Legacy Decommission
Topology	LEGACY_PORT_OR_ADAPTER	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Legacy transition port	Legacy	55	Current topology legacy port and diagnostics residue	Topology Legacy Decommission
Topology	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Legacy telemetry	ExecutedByLegacy	3	Current legacy execution counter	Topology Legacy Decommission
Topology	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationExecutor.cs	TopologyPresentationExecutor	Legacy telemetry	SkippedLegacy	3	Current skipped legacy counter	Topology Legacy Decommission
Topology	EXECUTION_MODE	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	Execution guard	ExecutionMode	11	Current lane execution-mode facade	Topology Legacy Decommission
Topology	EXECUTION_POLICY	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	ConfigureExecution	ExecutionPolicy	1	Current lane policy normalization	Topology Legacy Decommission
Topology	NORMALIZATION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	ConfigureExecution	Normalize(	1	Current invalid-mode compatibility	Topology Legacy Decommission
Topology	ROUTE_BRANCH	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	UseProductionExecutor	UseProduction	2	Current production/legacy branch	Topology Legacy Decommission
Topology	CONFIGURE_FACADE_DEFINITION	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	ConfigureExecution	ConfigureExecution	1	Current lane configuration facade	Topology Legacy Decommission
Topology	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_Host/Runtime/TopologyPresentationLaneRuntime.cs	TopologyPresentationLaneRuntime	Legacy policy telemetry	Legacy	7	Current lane legacy telemetry	Topology Legacy Decommission
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime/PresentationPlanning.cs	PresentationPlanning	Plan status	Suppressed	11	Presentation semantic suppression state, not legacy route	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime/PresentationPlayback.cs	PresentationPlayback	Compatibility vocabulary	Legacy	6	Playback semantic compatibility vocabulary	Out of scope
Retained Owner / Unrelated	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime/PresentationPlayback.cs	PresentationPlayback	Playback status	Suppressed	3	Playback status suppression vocabulary	Out of scope
Core Gameplay SFX	LEGACY_ONLY_SEMANTIC	Assets/_Features/Gameplay/Gameplay_Audio/Runtime/GameplayAudioRequestPlanner.cs	GameplayAudioRequestPlanner	Legacy motion timing	Legacy	2	Current legacy motion duration semantic	Core Gameplay SFX Legacy Decommission
Enemy One-shot Audio	LEGACY_ONLY_SEMANTIC	Assets/_Features/Gameplay/Gameplay_EnemyAudio/Runtime/EnemyAudioRequestPlanner.cs	EnemyAudioRequestPlanner	Legacy summon windup	Legacy	2	Current legacy-only summon windup semantic	Enemy One-shot Audio Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/GameplayVfxHostAnchorResolver.cs	GameplayVfxHostAnchorResolver	Anchor fallback	Fallback	11	VFX anchor fallback, not legacy route	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/BoxSlideSolidStopVfxCommandBuilder.cs	BoxSlideSolidStopVfxCommandBuilder	Command fallback	Fallback	1	VFX command fallback, not legacy route	Out of scope
Damage/Death VFX	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs	EnemyDeathMotionVfxCommandBuilder	Legacy death motion compatibility	Legacy	2	Current death motion compatibility vocabulary	Damage/Death VFX Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyForwardCellProjectileVfxAuthoring.cs	EnemyForwardCellProjectileVfxAuthoring	Authoring normalization	Normalize(	5	Non-route authoring normalization	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayForwardCellProjectileVfxController.cs	GameplayForwardCellProjectileVfxController	Target fallback	Fallback	8	VFX target fallback, not legacy route	Out of scope
Damage/Death VFX	LEGACY_DIAGNOSTICS	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs	GameplayVfxProductionRuntime	Damage/death suppression diagnostics	Legacy	4	Current damage/death legacy cue suppression diagnostics	Damage/Death VFX Legacy Decommission
Damage/Death VFX	MIGRATION_SUPPRESSION	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs	GameplayVfxProductionRuntime	Damage/death suppression diagnostics	Suppressed	39	Current damage/death suppression diagnostics	Damage/Death VFX Legacy Decommission
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs	GameplayVfxProductionRuntime	VFX fallback	Fallback	21	VFX visibility/correlation fallback, not legacy route	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/ParameterizedMotion/ParameterizedMotionVfxCommand.cs	ParameterizedMotionVfxCommand	Command fallback	Fallback	1	VFX command fallback, not legacy route	Out of scope
Retained Owner / Unrelated	UNRELATED_LEGACY_TERM	Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/PresentationMotionFollowingVfxController.cs	PresentationMotionFollowingVfxController	Visibility fallback	Fallback	4	VFX visibility fallback, not legacy route	Out of scope";

        private readonly struct ResidueBudget
        {
            public ResidueBudget(
                string domain,
                string category,
                string relativePath,
                string containingType,
                string containingMember,
                string symbol,
                int maximumAllowed,
                string allowedRole,
                string plannedRemovalPhase)
            {
                Domain = domain;
                Category = category;
                RelativePath = relativePath;
                ContainingType = containingType;
                ContainingMember = containingMember;
                Symbol = symbol;
                MaximumAllowed = maximumAllowed;
                AllowedRole = allowedRole;
                PlannedRemovalPhase = plannedRemovalPhase;
            }

            public string Domain { get; }
            public string Category { get; }
            public string RelativePath { get; }
            public string ContainingType { get; }
            public string ContainingMember { get; }
            public string Symbol { get; }
            public int MaximumAllowed { get; }
            public string AllowedRole { get; }
            public string PlannedRemovalPhase { get; }
        }

        private readonly struct ConfigureBudget
        {
            public ConfigureBudget(string api, string relativePath, int maximumAllowed, string allowedRole)
            {
                Api = api;
                RelativePath = relativePath;
                MaximumAllowed = maximumAllowed;
                AllowedRole = allowedRole;
            }

            public string Api { get; }
            public string RelativePath { get; }
            public int MaximumAllowed { get; }
            public string AllowedRole { get; }
        }

        private readonly struct SerializedFieldBudget
        {
            public SerializedFieldBudget(string relativePath, string symbol, int maximumAllowed)
            {
                RelativePath = relativePath;
                Symbol = symbol;
                MaximumAllowed = maximumAllowed;
            }

            public string RelativePath { get; }
            public string Symbol { get; }
            public int MaximumAllowed { get; }
        }

        private readonly struct PendingPlanCallBudget
        {
            public PendingPlanCallBudget(
                string relativePath,
                string containingType,
                string containingMember,
                string receiver,
                string receiverType,
                string method,
                string classification,
                int maximumAllowed)
            {
                RelativePath = relativePath;
                ContainingType = containingType;
                ContainingMember = containingMember;
                Receiver = receiver;
                ReceiverType = receiverType;
                Method = method;
                Classification = classification;
                MaximumAllowed = maximumAllowed;
            }

            public string RelativePath { get; }
            public string ContainingType { get; }
            public string ContainingMember { get; }
            public string Receiver { get; }
            public string ReceiverType { get; }
            public string Method { get; }
            public string Classification { get; }
            public int MaximumAllowed { get; }
        }

        private readonly struct SerializedReflectionTarget
        {
            public SerializedReflectionTarget(string relativePath, Type targetType)
            {
                RelativePath = relativePath;
                TargetType = targetType;
            }

            public string RelativePath { get; }
            public Type TargetType { get; }
        }

        private readonly struct PendingPlanCallHit
        {
            public PendingPlanCallHit(
                string relativePath,
                string containingType,
                string containingMember,
                string receiver,
                string receiverType,
                string method,
                string classification)
            {
                RelativePath = relativePath;
                ContainingType = containingType;
                ContainingMember = containingMember;
                Receiver = receiver;
                ReceiverType = receiverType;
                Method = method;
                Classification = classification;
            }

            public string RelativePath { get; }
            public string ContainingType { get; }
            public string ContainingMember { get; }
            public string Receiver { get; }
            public string ReceiverType { get; }
            public string Method { get; }
            public string Classification { get; }
        }

        private readonly struct EnumBudget
        {
            public EnumBudget(Type enumType, string legacyMember, string productionMember)
            {
                EnumType = enumType;
                LegacyMember = legacyMember;
                ProductionMember = productionMember;
            }

            public Type EnumType { get; }
            public string LegacyMember { get; }
            public string ProductionMember { get; }
        }

        private sealed class FreezeAnalysisResult
        {
            public FreezeAnalysisResult(
                int actualCount,
                IReadOnlyList<string> unknownHits,
                IReadOnlyList<string> countViolations,
                IReadOnlyList<string> movedFileViolations)
            {
                ActualCount = actualCount;
                UnknownHits = unknownHits;
                CountViolations = countViolations;
                MovedFileViolations = movedFileViolations;
            }

            public int ActualCount { get; }
            public IReadOnlyList<string> UnknownHits { get; }
            public IReadOnlyList<string> CountViolations { get; }
            public IReadOnlyList<string> MovedFileViolations { get; }
            public bool HasViolations => UnknownHits.Count != 0 || CountViolations.Count != 0 || MovedFileViolations.Count != 0;
        }

        private static class PresentationLegacyFreezeAnalyzer
        {
            private static readonly Regex ConfigureFacadeRegex =
                new Regex(@"\bConfigure[A-Za-z0-9_]*Execution\b", RegexOptions.Compiled);

            private static readonly Regex PendingPlanCallRegex =
                new Regex(@"\b(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?<method>ReplacePendingPlan)\s*\(",
                    RegexOptions.Compiled);

            private static readonly Regex TypeDeclarationRegex =
                new Regex(@"\b(?:public|private|internal|protected)?\s*(?:sealed\s+|static\s+|partial\s+)*class\s+(?<type>[A-Za-z_][A-Za-z0-9_]*)",
                    RegexOptions.Compiled);

            private static readonly Regex MemberDeclarationRegex =
                new Regex(@"\b(?:public|private|internal|protected)\s+(?:static\s+|virtual\s+|override\s+|sealed\s+|async\s+)*[A-Za-z_][A-Za-z0-9_<>,\[\]\.? ]*\s+(?<member>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
                    RegexOptions.Compiled);

            private static readonly Regex FieldDeclarationRegex =
                new Regex(@"\b(?:public|private|internal|protected)\s+(?:readonly\s+|static\s+|volatile\s+)*(?<type>[A-Za-z_][A-Za-z0-9_<>,\.]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:=(?!>)|;)",
                    RegexOptions.Compiled);

            private static readonly Regex ExecutionModeDeclarationRegex =
                new Regex(@"(?<attributes>(?:\s*\[[^\]]+\]\s*)*)\b(?<visibility>public|private|internal|protected)\s+(?:readonly\s+|static\s+|volatile\s+)*(?<type>[A-Za-z_][A-Za-z0-9_\.]*ExecutionMode)\s+(?<declarators>[A-Za-z_][A-Za-z0-9_]*(?:\s*=(?!>)\s*[^,;{}]+)?(?:\s*,\s*[A-Za-z_][A-Za-z0-9_]*(?:\s*=(?!>)\s*[^,;{}]+)?)*)\s*(?<terminator>=(?!>)|;|\{)",
                    RegexOptions.Compiled | RegexOptions.Singleline);

            public static FreezeAnalysisResult Analyze(
                IReadOnlyDictionary<string, string> sources,
                IReadOnlyCollection<ResidueBudget> manifest,
                IReadOnlyCollection<string> configureFacades)
            {
                var symbols = manifest.Select(row => row.Symbol)
                    .Concat(configureFacades)
                    .Distinct(StringComparer.Ordinal)
                    .Where(symbol => !string.IsNullOrEmpty(symbol))
                    .ToArray();
                var actual = CountSymbols(sources, symbols);
                var approved = manifest
                    .GroupBy(row => (row.RelativePath, row.Symbol))
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(row => row.MaximumAllowed));
                var approvedFiles = new HashSet<string>(manifest.Select(row => row.RelativePath), StringComparer.Ordinal);
                var unknownHits = new List<string>();
                var movedFiles = new List<string>();

                foreach (var row in actual.Where(row => row.Value > 0))
                {
                    if (!approved.ContainsKey(row.Key))
                    {
                        unknownHits.Add($"UNAPPROVED_LEGACY_RESIDUE {row.Key.RelativePath} {row.Key.Symbol} actual={row.Value}");
                    }

                    if (!approvedFiles.Contains(row.Key.RelativePath))
                    {
                        movedFiles.Add($"LEGACY_FILE_EXPANSION {row.Key.RelativePath} {row.Key.Symbol}");
                    }
                }

                var countViolations = approved
                    .Select(row =>
                    {
                        actual.TryGetValue(row.Key, out var actualCount);
                        return (row.Key.RelativePath, row.Key.Symbol, Actual: actualCount, Maximum: row.Value);
                    })
                    .Where(row => row.Actual > row.Maximum)
                    .Select(row => $"LEGACY_RESIDUE_GROWTH {row.RelativePath} {row.Symbol} actual={row.Actual} maximum={row.Maximum}")
                    .ToArray();

                return new FreezeAnalysisResult(actual.Values.Sum(), unknownHits, countViolations, movedFiles);
            }

            public static FreezeAnalysisResult AnalyzeConfigureFacades(
                IReadOnlyDictionary<string, string> sources,
                IReadOnlyCollection<ConfigureBudget> manifest)
            {
                var apis = manifest.Select(row => row.Api).Distinct(StringComparer.Ordinal).ToArray();
                var actual = CountSymbols(sources, apis);
                var approved = manifest
                    .Where(row => !string.IsNullOrEmpty(row.RelativePath))
                    .GroupBy(row => (row.RelativePath, Symbol: row.Api))
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.MaximumAllowed));
                var knownApis = new HashSet<string>(apis, StringComparer.Ordinal);
                var unknownHits = new List<string>();

                foreach (var source in sources)
                {
                    foreach (Match match in ConfigureFacadeRegex.Matches(source.Value))
                    {
                        if (!knownApis.Contains(match.Value))
                        {
                            unknownHits.Add($"UNKNOWN_CONFIGURE_FACADE {source.Key} {match.Value}");
                            continue;
                        }

                        var key = (source.Key, Symbol: match.Value);
                        if (!approved.ContainsKey(key) &&
                            manifest.Any(row => row.Api == match.Value && row.MaximumAllowed == 0))
                        {
                            unknownHits.Add($"CONFIGURE_PRODUCTION_CALLER_ADDED {source.Key} {match.Value}");
                        }
                        else if (!approved.ContainsKey(key))
                        {
                            unknownHits.Add($"CONFIGURE_PRODUCTION_CALLER_ADDED {source.Key} {match.Value}");
                        }
                    }
                }

                var countViolations = approved
                    .Select(row =>
                    {
                        actual.TryGetValue(row.Key, out var actualCount);
                        return (row.Key.RelativePath, row.Key.Symbol, Actual: actualCount, Maximum: row.Value);
                    })
                    .Where(row => row.Actual > row.Maximum)
                    .Select(row => $"CONFIGURE_CALL_GROWTH {row.RelativePath} {row.Symbol} actual={row.Actual} maximum={row.Maximum}")
                    .ToArray();

                return new FreezeAnalysisResult(actual.Values.Sum(), unknownHits, countViolations, Array.Empty<string>());
            }

            public static FreezeAnalysisResult AnalyzePendingPlanCalls(
                IReadOnlyDictionary<string, string> sources,
                IReadOnlyCollection<PendingPlanCallBudget> manifest)
            {
                var approved = manifest
                    .GroupBy(row => (
                        row.RelativePath,
                        row.ContainingType,
                        row.ContainingMember,
                        row.Receiver,
                        row.ReceiverType,
                        row.Method,
                        row.Classification))
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.MaximumAllowed));
                var approvedPaths = new HashSet<string>(manifest.Select(row => row.RelativePath), StringComparer.Ordinal);
                var approvedReceiverTypes = new HashSet<string>(
                    manifest.Select(row => row.ReceiverType),
                    StringComparer.Ordinal);
                var targetDomainLegacyReceiverTypes = new HashSet<string>(
                    new[]
                    {
                        "GameplayActionAudioPresentationController",
                        "EnemyAudioPresentationController",
                        "GameplayAudioPresentationController",
                    },
                    StringComparer.Ordinal);
                var actual = new Dictionary<(
                    string RelativePath,
                    string ContainingType,
                    string ContainingMember,
                    string Receiver,
                    string ReceiverType,
                    string Method,
                    string Classification), int>();
                var unknownHits = new List<string>();
                var movedFiles = new List<string>();

                foreach (var hit in FindPendingPlanCalls(sources, approvedReceiverTypes, targetDomainLegacyReceiverTypes))
                {
                    var key = (
                        hit.RelativePath,
                        hit.ContainingType,
                        hit.ContainingMember,
                        hit.Receiver,
                        hit.ReceiverType,
                        hit.Method,
                        hit.Classification);
                    actual[key] = actual.TryGetValue(key, out var count) ? count + 1 : 1;

                    if (!approved.ContainsKey(key))
                    {
                        unknownHits.Add(
                            "PENDING_PLAN_CALL_UNAPPROVED " +
                            $"Path={hit.RelativePath} ContainingType={hit.ContainingType} " +
                            $"ContainingMember={hit.ContainingMember} Receiver={hit.Receiver} " +
                            $"ReceiverType={hit.ReceiverType} Method={hit.Method} " +
                            $"Category={hit.Classification} MaximumAllowed=0 Actual=1");
                    }

                    if (!approvedPaths.Contains(hit.RelativePath))
                    {
                        movedFiles.Add(
                            "PENDING_PLAN_CALL_RELOCATED " +
                            $"Path={hit.RelativePath} ContainingType={hit.ContainingType} " +
                            $"ContainingMember={hit.ContainingMember} Receiver={hit.Receiver} " +
                            $"ReceiverType={hit.ReceiverType} Method={hit.Method} " +
                            $"Category={hit.Classification} MaximumAllowed=0 Actual=1");
                    }
                }

                var countViolations = approved
                    .Select(row =>
                    {
                        actual.TryGetValue(row.Key, out var actualCount);
                        return (row.Key, Actual: actualCount, Maximum: row.Value);
                    })
                    .Where(row => row.Actual > row.Maximum)
                    .Select(row =>
                    {
                        var budget = manifest.First(item =>
                            item.RelativePath == row.Key.RelativePath &&
                            item.ContainingType == row.Key.ContainingType &&
                            item.ContainingMember == row.Key.ContainingMember &&
                            item.Receiver == row.Key.Receiver &&
                            item.ReceiverType == row.Key.ReceiverType &&
                            item.Method == row.Key.Method &&
                            item.Classification == row.Key.Classification);
                        return "PENDING_PLAN_CALL_GROWTH " +
                               $"Path={row.Key.RelativePath} ContainingType={row.Key.ContainingType} " +
                               $"ContainingMember={row.Key.ContainingMember} Receiver={row.Key.Receiver} " +
                               $"ReceiverType={row.Key.ReceiverType} Method={row.Key.Method} " +
                               $"Category={budget.Classification} MaximumAllowed={row.Maximum} Actual={row.Actual}";
                    })
                    .ToArray();

                return new FreezeAnalysisResult(
                    actual.Values.Sum(),
                    unknownHits,
                    countViolations,
                    movedFiles);
            }

            public static FreezeAnalysisResult AnalyzeSerializedSourceFields(
                IReadOnlyDictionary<string, string> sources,
                IReadOnlyCollection<SerializedFieldBudget> manifest)
            {
                var approved = manifest
                    .GroupBy(row => (row.RelativePath, row.Symbol))
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.MaximumAllowed));
                var actual = new Dictionary<(string RelativePath, string Symbol), int>();
                var unknownHits = new List<string>();

                foreach (var source in sources)
                {
                    foreach (var symbol in FindSerializedExecutionModeDeclarations(source.Value))
                    {
                        var matchedManifest = manifest.FirstOrDefault(row =>
                            row.RelativePath == source.Key &&
                            symbol.StartsWith(row.Symbol, StringComparison.Ordinal));
                        if (matchedManifest.Symbol == null)
                        {
                            unknownHits.Add($"SERIALIZED_EXECUTION_MODE_FIELD_ADDED {source.Key} {symbol}");
                            continue;
                        }

                        var key = (matchedManifest.RelativePath, matchedManifest.Symbol);
                        actual[key] = actual.TryGetValue(key, out var count) ? count + 1 : 1;
                    }
                }

                var countViolations = approved
                    .Select(row =>
                    {
                        actual.TryGetValue(row.Key, out var actualCount);
                        return (row.Key.RelativePath, row.Key.Symbol, Actual: actualCount, Maximum: row.Value);
                    })
                    .Where(row => row.Actual > row.Maximum)
                    .Select(row => $"SERIALIZED_EXECUTION_MODE_FIELD_GROWTH {row.RelativePath} {row.Symbol} actual={row.Actual} maximum={row.Maximum}")
                    .ToArray();

                return new FreezeAnalysisResult(actual.Values.Sum(), unknownHits, countViolations, Array.Empty<string>());
            }

            public static FreezeAnalysisResult AnalyzeSerializedRuntimeFields(
                IReadOnlyCollection<SerializedReflectionTarget> targets,
                IReadOnlyCollection<SerializedFieldBudget> manifest)
            {
                var approved = manifest
                    .GroupBy(row => (row.RelativePath, row.Symbol))
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.MaximumAllowed));
                var actual = new Dictionary<(string RelativePath, string Symbol), int>();
                var unknownHits = new List<string>();

                foreach (var target in targets)
                {
                    foreach (var symbol in FindSerializedExecutionModeRuntimeFields(target.TargetType))
                    {
                        var matchedManifest = manifest.FirstOrDefault(row =>
                            row.RelativePath == target.RelativePath &&
                            symbol.StartsWith(row.Symbol, StringComparison.Ordinal));
                        if (matchedManifest.Symbol == null)
                        {
                            unknownHits.Add($"SERIALIZED_EXECUTION_MODE_FIELD_ADDED {target.RelativePath} {symbol}");
                            continue;
                        }

                        var key = (matchedManifest.RelativePath, matchedManifest.Symbol);
                        actual[key] = actual.TryGetValue(key, out var count) ? count + 1 : 1;
                    }
                }

                var countViolations = approved
                    .Select(row =>
                    {
                        actual.TryGetValue(row.Key, out var actualCount);
                        return (row.Key.RelativePath, row.Key.Symbol, Actual: actualCount, Maximum: row.Value);
                    })
                    .Where(row => row.Actual > row.Maximum)
                    .Select(row => $"SERIALIZED_EXECUTION_MODE_FIELD_GROWTH {row.RelativePath} {row.Symbol} actual={row.Actual} maximum={row.Maximum}")
                    .ToArray();

                return new FreezeAnalysisResult(actual.Values.Sum(), unknownHits, countViolations, Array.Empty<string>());
            }

            public static FreezeAnalysisResult AnalyzeYamlExecutionReferences(
                IReadOnlyDictionary<string, string> yamlAssets,
                IReadOnlyCollection<string> tokens,
                int maximumAllowed)
            {
                var actual = CountSymbols(yamlAssets, tokens);
                var count = actual.Values.Sum();
                var violations = count > maximumAllowed
                    ? actual.Where(row => row.Value > 0)
                        .Select(row => $"YAML_EXECUTION_MODE_REFERENCE_ADDED {row.Key.RelativePath} {row.Key.Symbol} actual={row.Value}")
                        .ToArray()
                    : Array.Empty<string>();
                return new FreezeAnalysisResult(count, violations, Array.Empty<string>(), Array.Empty<string>());
            }

            public static FreezeAnalysisResult AnalyzeEnumSource(
                string source,
                string enumName,
                string legacyMember,
                string productionMember)
            {
                var match = Regex.Match(source, @"enum\s+" + Regex.Escape(enumName) + @"\s*\{(?<body>[^}]*)\}");
                if (!match.Success)
                {
                    return new FreezeAnalysisResult(0, new[] { $"ENUM_MISSING {enumName}" }, Array.Empty<string>(), Array.Empty<string>());
                }

                var values = match.Groups["body"].Value
                    .Split(',')
                    .Select(part => part.Trim())
                    .Where(part => part.Length != 0)
                    .Select(part => part.Split('='))
                    .ToDictionary(
                        parts => parts[0].Trim(),
                        parts => parts.Length > 1 ? int.Parse(parts[1].Trim()) : -1,
                        StringComparer.Ordinal);

                var violations = new List<string>();
                if (values.Count != 2)
                {
                    violations.Add($"ENUM_MEMBER_COUNT_CHANGED {enumName} actual={values.Count} maximum=2");
                }

                if (!values.TryGetValue(legacyMember, out var legacyValue) || legacyValue != 0)
                {
                    violations.Add($"ENUM_LEGACY_ZERO_DRIFT {enumName}.{legacyMember}");
                }

                if (!values.TryGetValue(productionMember, out var productionValue) || productionValue != 1)
                {
                    violations.Add($"ENUM_PRODUCTION_ONE_DRIFT {enumName}.{productionMember}");
                }

                return new FreezeAnalysisResult(values.Count, violations, Array.Empty<string>(), Array.Empty<string>());
            }

            private static IEnumerable<PendingPlanCallHit> FindPendingPlanCalls(
                IReadOnlyDictionary<string, string> sources,
                HashSet<string> approvedReceiverTypes,
                HashSet<string> targetDomainLegacyReceiverTypes)
            {
                foreach (var source in sources)
                {
                    var clean = StripCommentsAndStrings(source.Value);
                    var fieldTypes = ExtractFieldTypes(clean);

                    foreach (Match callMatch in PendingPlanCallRegex.Matches(clean))
                    {
                        var receiver = callMatch.Groups["receiver"].Value;
                        var method = callMatch.Groups["method"].Value;
                        fieldTypes.TryGetValue(receiver, out var receiverType);
                        if (string.IsNullOrEmpty(receiverType))
                        {
                            receiverType = "<unknown>";
                        }

                        var classification = ClassifyPendingPlanReceiver(
                            receiverType,
                            approvedReceiverTypes,
                            targetDomainLegacyReceiverTypes);
                        yield return new PendingPlanCallHit(
                            source.Key,
                            FindContainingDeclarationName(clean, TypeDeclarationRegex, "type", callMatch.Index),
                            FindContainingDeclarationName(clean, MemberDeclarationRegex, "member", callMatch.Index),
                            receiver,
                            receiverType,
                            method,
                            classification);
                    }
                }
            }

            private static Dictionary<string, string> ExtractFieldTypes(string source)
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (Match match in FieldDeclarationRegex.Matches(source))
                {
                    result[match.Groups["name"].Value] = match.Groups["type"].Value;
                }

                return result;
            }

            private static string ClassifyPendingPlanReceiver(
                string receiverType,
                HashSet<string> approvedReceiverTypes,
                HashSet<string> targetDomainLegacyReceiverTypes)
            {
                if (approvedReceiverTypes.Contains(receiverType))
                {
                    return "RETAINED_ADJUNCT_PRESENTATION_CONTROLLER";
                }

                if (targetDomainLegacyReceiverTypes.Contains(receiverType))
                {
                    return "TARGET_DOMAIN_LEGACY_PENDING_PLAN";
                }

                return "UNKNOWN_PENDING_PLAN_RECEIVER";
            }

            private static IEnumerable<string> FindSerializedExecutionModeDeclarations(string source)
            {
                var clean = StripCommentsAndStrings(source);
                foreach (Match match in ExecutionModeDeclarationRegex.Matches(clean))
                {
                    var attributes = match.Groups["attributes"].Value;
                    var visibility = match.Groups["visibility"].Value;
                    var type = match.Groups["type"].Value;
                    var terminator = match.Groups["terminator"].Value;
                    var isProperty = terminator == "{";
                    var isField = !isProperty;
                    var hasUnityFieldAttribute =
                        attributes.IndexOf("SerializeField", StringComparison.Ordinal) >= 0 ||
                        attributes.IndexOf("SerializeReference", StringComparison.Ordinal) >= 0;
                    var hasFieldTargetAttribute =
                        attributes.IndexOf("field:", StringComparison.Ordinal) >= 0 &&
                        hasUnityFieldAttribute;

                    if (isProperty)
                    {
                        if (!hasFieldTargetAttribute)
                        {
                            continue;
                        }

                        yield return $"{visibility} {type} {ExtractFirstDeclaratorName(match.Groups["declarators"].Value)}";
                        continue;
                    }

                    if (isField && visibility != "public" && !hasUnityFieldAttribute)
                    {
                        continue;
                    }

                    foreach (var name in ExtractDeclaratorNames(match.Groups["declarators"].Value))
                    {
                        yield return $"{visibility} {type} {name}";
                    }
                }
            }

            private static IEnumerable<string> FindSerializedExecutionModeRuntimeFields(Type type)
            {
                foreach (var field in type.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!field.FieldType.Name.EndsWith("ExecutionMode", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var isUnitySerialized =
                        field.IsPublic ||
                        field.GetCustomAttributes(typeof(SerializeField), inherit: false).Length > 0 ||
                        field.GetCustomAttributes(typeof(SerializeReference), inherit: false).Length > 0;
                    if (!isUnitySerialized)
                    {
                        continue;
                    }

                    yield return $"{(field.IsPublic ? "public" : "private")} {field.FieldType.Name} {field.Name}";
                }
            }

            private static string FindContainingDeclarationName(
                string source,
                Regex declarationRegex,
                string groupName,
                int targetIndex)
            {
                var result = "<unknown>";
                var resultOpenBrace = -1;
                foreach (Match match in declarationRegex.Matches(source))
                {
                    if (match.Index > targetIndex)
                    {
                        break;
                    }

                    var openBrace = source.IndexOf('{', match.Index + match.Length);
                    if (openBrace < 0 || openBrace > targetIndex || openBrace < resultOpenBrace)
                    {
                        continue;
                    }

                    var closeBrace = FindMatchingBrace(source, openBrace);
                    if (closeBrace >= targetIndex)
                    {
                        result = match.Groups[groupName].Value;
                        resultOpenBrace = openBrace;
                    }
                }

                return result;
            }

            private static int FindMatchingBrace(string source, int openBraceIndex)
            {
                var depth = 0;
                for (var index = openBraceIndex; index < source.Length; index++)
                {
                    if (source[index] == '{')
                    {
                        depth++;
                    }
                    else if (source[index] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return index;
                        }
                    }
                }

                return source.Length - 1;
            }

            private static IEnumerable<string> ExtractDeclaratorNames(string declarators)
            {
                foreach (var declarator in declarators.Split(','))
                {
                    var name = ExtractFirstDeclaratorName(declarator);
                    if (!string.IsNullOrEmpty(name))
                    {
                        yield return name;
                    }
                }
            }

            private static string ExtractFirstDeclaratorName(string declarator)
            {
                var namePart = declarator.Split('=')[0].Trim();
                var match = Regex.Match(namePart, @"^[A-Za-z_][A-Za-z0-9_]*$");
                return match.Success ? match.Value : string.Empty;
            }

            private static string StripCommentsAndStrings(string source)
            {
                var chars = source.ToCharArray();
                var index = 0;
                while (index < chars.Length)
                {
                    if (chars[index] == '/' && index + 1 < chars.Length && chars[index + 1] == '/')
                    {
                        chars[index++] = ' ';
                        chars[index++] = ' ';
                        while (index < chars.Length && chars[index] != '\n')
                        {
                            chars[index++] = ' ';
                        }

                        continue;
                    }

                    if (chars[index] == '/' && index + 1 < chars.Length && chars[index + 1] == '*')
                    {
                        chars[index++] = ' ';
                        chars[index++] = ' ';
                        while (index + 1 < chars.Length && !(chars[index] == '*' && chars[index + 1] == '/'))
                        {
                            if (chars[index] != '\n' && chars[index] != '\r')
                            {
                                chars[index] = ' ';
                            }

                            index++;
                        }

                        if (index + 1 < chars.Length)
                        {
                            chars[index++] = ' ';
                            chars[index++] = ' ';
                        }

                        continue;
                    }

                    if (chars[index] == '@' && index + 1 < chars.Length && chars[index + 1] == '"')
                    {
                        chars[index++] = ' ';
                        chars[index++] = ' ';
                        while (index < chars.Length)
                        {
                            if (chars[index] != '\n' && chars[index] != '\r')
                            {
                                chars[index] = ' ';
                            }

                            if (source[index] == '"' &&
                                (index + 1 >= source.Length || source[index + 1] != '"'))
                            {
                                index++;
                                break;
                            }

                            if (source[index] == '"' && index + 1 < source.Length && source[index + 1] == '"')
                            {
                                if (chars[index + 1] != '\n' && chars[index + 1] != '\r')
                                {
                                    chars[index + 1] = ' ';
                                }

                                index += 2;
                                continue;
                            }

                            index++;
                        }

                        continue;
                    }

                    if (chars[index] == '"')
                    {
                        chars[index++] = ' ';
                        var escaped = false;
                        while (index < chars.Length)
                        {
                            var current = source[index];
                            if (chars[index] != '\n' && chars[index] != '\r')
                            {
                                chars[index] = ' ';
                            }

                            index++;
                            if (current == '"' && !escaped)
                            {
                                break;
                            }

                            escaped = current == '\\' && !escaped;
                            if (current != '\\')
                            {
                                escaped = false;
                            }
                        }

                        continue;
                    }

                    if (chars[index] == '\'')
                    {
                        chars[index++] = ' ';
                        var escaped = false;
                        while (index < chars.Length)
                        {
                            var current = source[index];
                            if (chars[index] != '\n' && chars[index] != '\r')
                            {
                                chars[index] = ' ';
                            }

                            index++;
                            if (current == '\'' && !escaped)
                            {
                                break;
                            }

                            escaped = current == '\\' && !escaped;
                            if (current != '\\')
                            {
                                escaped = false;
                            }
                        }

                        continue;
                    }

                    index++;
                }

                return new string(chars);
            }

            private static Dictionary<(string RelativePath, string Symbol), int> CountSymbols(
                IReadOnlyDictionary<string, string> sources,
                IReadOnlyCollection<string> symbols)
            {
                var actual = new Dictionary<(string RelativePath, string Symbol), int>();
                foreach (var source in sources)
                {
                    foreach (var symbol in symbols)
                    {
                        var count = CountOccurrences(source.Value, symbol);
                        if (count > 0)
                        {
                            actual[(source.Key, symbol)] = count;
                        }
                    }
                }

                return actual;
            }

            private static int CountOccurrences(string source, string value)
            {
                var count = 0;
                var index = 0;
                while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
                {
                    count++;
                    index += value.Length;
                }

                return count;
            }
        }
    }
}
