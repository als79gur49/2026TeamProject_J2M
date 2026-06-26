using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class PresentationOrchestrationProductionSwitchGovernanceTests
    {
        private const string ReadinessDocumentPath =
            "Docs/Architecture/Presentation-Orchestration-Production-Switch-Readiness.md";
        private const string ThisTestPath =
            "Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/PresentationOrchestrationProductionSwitchGovernanceTests.cs";
        private const string HostRuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime";
        private const string ContractsDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationContracts/Runtime";
        private const string PlanningDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlanning/Runtime";
        private const string PlaybackDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationPlayback/Runtime";
        private const string RuntimeDirectory =
            "Assets/_Features/Gameplay/Gameplay_PresentationRuntime/Runtime";

        private static readonly ProductionSwitchReadinessRow[] ReadinessMatrix =
        {
            new(
                "Topology transition",
                typeof(TopologyPresentationExecutionMode),
                "LegacyCoordinator",
                "ExecutorBridge",
                "ExecutorBridge",
                "ExecutorBridge",
                false,
                true,
                "TopologyExecution_ProductionTelemetry_CoversRetainedLegacyOwnerSemanticAndRollbackValues",
                "TopologyExecution_LegacyAndExecutorBridgePresenters_PreserveAuthoritativeTickResultOutputs",
                "TopologyExecution_ExecutorBridgeMode_CleanupResetsPortAndDiagnostics",
                "TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator",
                "High: input lock observes coordinator presentation phase.",
                "Low",
                "Set TopologyPresentationExecutionMode.LegacyCoordinator.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Damage/death VFX",
                null,
                "Removed",
                "OrchestrationExecutor",
                "OrchestrationExecutor",
                "OrchestrationExecutor",
                false,
                false,
                "DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback",
                "DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative",
                "DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics",
                "VfxPlanningBoundary_StaysPresentationOnly",
                "Low",
                "Low",
                "Removed; current-only route has no rollback mode.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered),
            new(
                "Box motion",
                null,
                "Removed",
                "GameplayMotionPresentationExecutor",
                "GameplayMotionPresentationExecutor",
                "GameplayMotionPresentationExecutor",
                false,
                false,
                "BoxMotion_DuplicateRequest_DedupesOrLayersByContract",
                "BoxMotion_SameTickPushAndSlide_FollowsPolicy",
                "BoxMotion_HiddenOrRemovedEntity_NoLegacyFallback",
                "BoxMotionExecutionSwitch_DoesNotLeakIntoInputOrVfxContracts",
                "Medium: motion can affect perceived input timing.",
                "Low",
                "Removed; Box Motion is current-only and has no rollback mode.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Player action animation",
                typeof(PlayerActionAnimationExecutionMode),
                "Removed",
                "OrchestrationAnimationExecutor",
                "OrchestrationAnimationExecutor",
                "OrchestrationAnimationExecutor",
                false,
                true,
                "PlayerActionAnimationReadiness_PlayMode_DuplicateGuardNormalAndForced",
                "PlayerActionAnimationReadiness_PlayMode_IsNonAuthoritative",
                "PlayerActionAnimationReadiness_PlayMode_LifecycleCleanupClearsAnimatorState",
                "ArchitectureBoundary_AfterPlayerAnimationSwitch_RemainsSeparated",
                "Medium: action holds can affect input feel.",
                "Low",
                "Invalid PlayerActionAnimationExecutionMode values normalize to OrchestrationAnimationExecutor.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Enemy presentation",
                typeof(EnemyPresentationExecutionMode),
                "LegacyEnemyPresentationMapper",
                "OrchestrationEnemyPresentationExecutor",
                "OrchestrationEnemyPresentationExecutor",
                "OrchestrationEnemyPresentationExecutor",
                false,
                true,
                "EnemyPresentation_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues",
                "EnemyPresentation_OrchestrationRoute_DoesNotMutateAuthoritativeResultOrBlockingState",
                "EnemyPresentation_LifecycleCleanup_ClearsGuardDiagnosticsPortAndStaleDriverState",
                "EnemyPresentationPlanningBoundary_StaysPresentationOnly",
                "Medium",
                "Low",
                "Set EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Core gameplay SFX",
                typeof(CoreGameplaySfxExecutionMode),
                "LegacyGameplayAudioController",
                "OrchestrationSfxBridgeExecutor",
                "OrchestrationSfxBridgeExecutor",
                "OrchestrationSfxBridgeExecutor",
                false,
                true,
                "CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback",
                "CoreSfx_PlayModeSmoke_IsNonAuthoritative",
                "CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState",
                "AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated",
                "Low: non-blocking one-shot.",
                "Medium: audio ownership must stay separated.",
                "Set CoreGameplaySfxExecutionMode.LegacyGameplayAudioController.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered),
        };

        private static readonly PresentationDomainLifecycleRow[] LifecycleMatrix =
        {
            new(
                "Topology transition",
                PresentationDomainLifecycleState.SerializedCompatibility,
                true,
                false,
                true,
                nameof(GameplaySceneHostConfiguration.TopologyPresentationExecutionMode),
                "ExecutorBridge",
                "LegacyCoordinator",
                "Topology keeps the serialized execution-mode field for compatibility while production defaults to the executor bridge."),
            new(
                "Damage/death VFX",
                PresentationDomainLifecycleState.CurrentOnlyDecommissioned,
                false,
                true,
                false,
                null,
                "OrchestrationExecutor",
                "Removed",
                "Damage/death VFX is current-only; rollback mode and serialized rollback owner are removed."),
            new(
                "Box motion",
                PresentationDomainLifecycleState.CurrentOnlyDecommissioned,
                false,
                true,
                false,
                null,
                "GameplayMotionPresentationExecutor",
                "Removed",
                "Box Motion is current-only; rollback mode and serialized rollback owner are removed."),
            new(
                "Player action animation",
                PresentationDomainLifecycleState.CurrentOnlyDecommissioned,
                false,
                true,
                false,
                "PlayerActionAnimationExecutionMode",
                "OrchestrationAnimationExecutor",
                "Removed",
                "PR1 removed the legacy player action animation owner; invalid values normalize to the production executor."),
            new(
                "Enemy presentation",
                PresentationDomainLifecycleState.NotYetDecommissioned,
                true,
                true,
                false,
                "EnemyPresentationExecutionMode",
                "OrchestrationEnemyPresentationExecutor",
                "LegacyEnemyPresentationMapper",
                "Enemy presentation still exposes a legacy/current route."),
            new(
                "Core gameplay SFX",
                PresentationDomainLifecycleState.NotYetDecommissioned,
                true,
                true,
                false,
                "CoreGameplaySfxExecutionMode",
                "OrchestrationSfxBridgeExecutor",
                "LegacyGameplayAudioController",
                "Core SFX still exposes a legacy/current route while retained audio adjuncts remain separate."),
            new(
                "Enemy One-shot Audio",
                PresentationDomainLifecycleState.CurrentOnlyDecommissioned,
                false,
                true,
                false,
                "EnemyAudioExecutionMode",
                "EnemyAudioSemanticProjector",
                "Removed",
                "PR2 removed enemy one-shot legacy execution ownership on this branch."),
            new(
                "Gameplay Action Audio",
                PresentationDomainLifecycleState.CurrentOnlyDecommissioned,
                false,
                true,
                false,
                "ActionAudioExecutionMode",
                "GameplayActionAudioPresentationExecutor",
                "Removed",
                "Current branch state has already removed gameplay action audio legacy execution residue; report this as a PR2 scope broadening."),
            new(
                "Block audio",
                PresentationDomainLifecycleState.RetainedAdjunct,
                false,
                true,
                false,
                null,
                "BlockAudioPresentationController",
                "Removed",
                "Retained audio adjunct controller; not a target legacy rollback route."),
            new(
                "Tile feature audio",
                PresentationDomainLifecycleState.RetainedAdjunct,
                false,
                true,
                false,
                null,
                "TileFeatureAudioPresentationController",
                "Removed",
                "Retained audio adjunct controller; not a target legacy rollback route."),
            new(
                "Gravity field audio",
                PresentationDomainLifecycleState.RetainedAdjunct,
                false,
                true,
                false,
                null,
                "GravityFieldAudioPresentationController",
                "Removed",
                "Retained audio adjunct controller; not a target legacy rollback route."),
            new(
                "Topology audio",
                PresentationDomainLifecycleState.RetainedAdjunct,
                false,
                true,
                false,
                null,
                "TopologyAudioPresentationController",
                "Removed",
                "Retained audio adjunct controller; not a target legacy rollback route."),
        };

        [Test]
        [Category("Core")]
        public void ProductionSwitchReadinessMatrix_IncludesAllKnownExecutionModes()
        {
            var knownTypes = new[]
            {
                typeof(TopologyPresentationExecutionMode),
                typeof(PlayerActionAnimationExecutionMode),
                typeof(EnemyPresentationExecutionMode),
                typeof(CoreGameplaySfxExecutionMode),
            };
            var matrixTypes = ReadinessMatrix
                .Where(row => row.ExecutionModeType != null)
                .Select(row => row.ExecutionModeType)
                .ToArray();

            foreach (var knownType in knownTypes)
            {
                Assert.That(matrixTypes.Count(type => type == knownType), Is.EqualTo(1), knownType.Name);
            }

            Assert.That(ReadinessMatrix.Select(row => row.Domain).Distinct().Count(), Is.EqualTo(ReadinessMatrix.Length));
        }

        [Test]
        [Category("Core")]
        public void PresentationExecutionDefaults_AllPhase9DomainsUseProductionOrchestrationOwner()
        {
            var config = new GameplaySceneHostConfiguration();
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
            var rootObject = new GameObject(nameof(PresentationExecutionDefaults_AllPhase9DomainsUseProductionOrchestrationOwner));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.That(config.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(coordinator.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(coordinator.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));

                Assert.That(presenter.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
                Assert.That(presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(presenter.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));

                foreach (var row in ReadinessMatrix)
                {
                    Assert.That(row.DefaultIsLegacy, Is.False, row.Domain);
                    Assert.That(row.CurrentDefault, Is.EqualTo(row.OrchestrationOwner), row.Domain);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void InvalidPresentationExecutionModes_NormalizeToLegacy()
        {
            Assert.That(
                TopologyPresentationExecutionPolicy.Normalize((TopologyPresentationExecutionMode)999),
                Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(
                PlayerActionAnimationExecutionPolicy.Normalize((PlayerActionAnimationExecutionMode)999),
                Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
            Assert.That(
                EnemyPresentationExecutionPolicy.Normalize((EnemyPresentationExecutionMode)999),
                Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(
                CoreGameplaySfxExecutionPolicy.Normalize((CoreGameplaySfxExecutionMode)999),
                Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
            Assert.That(default(TopologyPresentationExecutionMode), Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(Enum.IsDefined(typeof(PlayerActionAnimationExecutionMode), default(PlayerActionAnimationExecutionMode)), Is.False);
            Assert.That(default(EnemyPresentationExecutionMode), Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(default(CoreGameplaySfxExecutionMode), Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
            foreach (var row in ReadinessMatrix)
            {
                Assert.That(row.InvalidModeNormalizesToLegacy, Is.EqualTo(row.ExecutionModeType != null), row.Domain);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionConfig_DoesNotSerializeRollbackOwnersForPhase9ProductionDomains()
        {
            var productionPaths = EnumerateProductionConfigFiles().ToArray();

            Assert.That(productionPaths, Is.Not.Empty);
            AssertFieldAwareProductionConfigScannerContract();

            foreach (var path in productionPaths)
            {
                var source = ReadRepoFile(path);
                var violations = FindSerializedRollbackOwnerViolations(source, LifecycleMatrix);
                Assert.That(violations, Is.Empty, $"{path} must not serialize rollback owners:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
            }
        }

        [Test]
        [Category("Core")]
        public void RollbackSwitches_RemainAvailable()
        {
            var readinessByDomain = ReadinessMatrix.ToDictionary(row => row.Domain, StringComparer.Ordinal);

            foreach (var lifecycle in LifecycleMatrix)
            {
                if (!readinessByDomain.TryGetValue(lifecycle.Domain, out var readiness))
                {
                    Assert.That(
                        lifecycle.State,
                        Is.EqualTo(PresentationDomainLifecycleState.CurrentOnlyDecommissioned)
                            .Or.EqualTo(PresentationDomainLifecycleState.RetainedAdjunct),
                        lifecycle.Domain);
                    continue;
                }

                Assert.That(readiness.DuplicateGuardEvidence, Is.Not.Empty, readiness.Domain);
                Assert.That(readiness.LifecycleCleanupEvidence, Is.Not.Empty, readiness.Domain);

                switch (lifecycle.State)
                {
                    case PresentationDomainLifecycleState.NotYetDecommissioned:
                    case PresentationDomainLifecycleState.SerializedCompatibility:
                        Assert.That(lifecycle.RequiresRollbackSwitch, Is.True, lifecycle.Domain);
                        Assert.That(readiness.RollbackPath, Is.Not.Empty, readiness.Domain);
                        Assert.That(readiness.RollbackPath, Does.Contain(lifecycle.LegacyOwner), readiness.Domain);
                        Assert.That(readiness.CurrentDefault, Is.EqualTo(lifecycle.CurrentProductionOwner), readiness.Domain);
                        break;
                    case PresentationDomainLifecycleState.CurrentOnlyDecommissioned:
                        Assert.That(lifecycle.RequiresRollbackSwitch, Is.False, lifecycle.Domain);
                        if (!string.Equals(lifecycle.LegacyOwner, "Removed", StringComparison.Ordinal))
                        {
                            Assert.That(readiness.RollbackPath, Does.Not.Contain(lifecycle.LegacyOwner), readiness.Domain);
                        }

                        Assert.That(readiness.CurrentDefault, Is.EqualTo(lifecycle.CurrentProductionOwner), readiness.Domain);
                        Assert.That(readiness.LegacyOwner, Is.EqualTo("Removed"), readiness.Domain);
                        break;
                    case PresentationDomainLifecycleState.RetainedAdjunct:
                        Assert.That(lifecycle.RequiresRollbackSwitch, Is.False, lifecycle.Domain);
                        break;
                    case PresentationDomainLifecycleState.TestGovernanceOnly:
                        Assert.That(lifecycle.RequiresRollbackSwitch, Is.False, lifecycle.Domain);
                        break;
                    default:
                        Assert.Fail($"Unhandled lifecycle state {lifecycle.State} for {lifecycle.Domain}.");
                        break;
                }
            }

            var guard = new CoreGameplaySfxExecutionGuard(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController);
            var key = new CoreGameplaySfxPlaybackKey(
                tickIndex: 1,
                PresentationSemanticSource.PlayerDamage,
                sourceEntityId: 10,
                targetEntityId: 10,
                PresentationSfxCueKey.PlayerDamage);

            Assert.That(
                guard.TryBeginExecution(CoreGameplaySfxExecutionOwner.OrchestrationSfxBridgeExecutor, key),
                Is.False);
            Assert.That(guard.Diagnostics.ExecutedByExecutorCount, Is.Zero);
            Assert.That(guard.Diagnostics.SkippedExecutorBecauseLegacyOwnerCount, Is.EqualTo(1));
            Assert.That(guard.Diagnostics.Mode, Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));

            Assert.That(Enum.GetNames(typeof(PlayerActionAnimationExecutionMode)), Is.EqualTo(new[] { "OrchestrationAnimationExecutor" }));
            Assert.That(
                PlayerActionAnimationExecutionPolicy.Normalize((PlayerActionAnimationExecutionMode)999),
                Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
        }

        [Test]
        [Category("Core")]
        public void AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated()
        {
            var coreSfxExecutor = ReadRepoFile($"{HostRuntimeDirectory}/GameplaySfxPresentationExecutor.cs");
            var actionAudioExecutor = ReadRepoFile($"{HostRuntimeDirectory}/GameplayActionAudioPresentationExecutor.cs");
            var enemyAudioExecutor = ReadRepoFile($"{HostRuntimeDirectory}/GameplayEnemyAudioPresentationExecutor.cs");
            var contractsPlanningPlaybackSource = ReadDirectorySource(ContractsDirectory) + "\n" +
                                                  ReadDirectorySource(PlanningDirectory) + "\n" +
                                                  ReadDirectorySource(PlaybackDirectory);
            var bgmSource = ReadDirectorySource("Assets/_Features/Flow/Flow_Audio/Runtime");
            var uiSource = ReadDirectorySource("Assets/_Features/UI");

            Assert.That(coreSfxExecutor, Does.Not.Contain("ActionAudioExecutionMode"));
            Assert.That(coreSfxExecutor, Does.Not.Contain("EnemyAudioExecutionMode"));
            Assert.That(coreSfxExecutor, Does.Not.Contain("IGameplayActionAudioPlaybackPort"));
            Assert.That(coreSfxExecutor, Does.Not.Contain("IGameplayEnemyAudioPlaybackPort"));

            Assert.That(actionAudioExecutor, Does.Not.Contain("CoreGameplaySfxExecutionMode"));
            Assert.That(actionAudioExecutor, Does.Not.Contain("EnemyAudioExecutionMode"));
            Assert.That(actionAudioExecutor, Does.Not.Contain("PresentationSfxCueKey"));
            Assert.That(actionAudioExecutor, Does.Not.Contain("IGameplaySfxPlaybackPort"));

            Assert.That(enemyAudioExecutor, Does.Not.Contain("CoreGameplaySfxExecutionMode"));
            Assert.That(enemyAudioExecutor, Does.Not.Contain("ActionAudioExecutionMode"));
            Assert.That(enemyAudioExecutor, Does.Not.Contain("PresentationSfxCueKey"));
            Assert.That(enemyAudioExecutor, Does.Not.Contain("PresentationActionAudioCueKey"));

            Assert.That(contractsPlanningPlaybackSource, Does.Not.Contain("AudioManager"));
            Assert.That(coreSfxExecutor, Does.Not.Contain("AudioManager"));
            Assert.That(actionAudioExecutor, Does.Not.Contain("AudioManager"));
            Assert.That(enemyAudioExecutor, Does.Not.Contain("AudioManager"));
            Assert.That(bgmSource, Does.Not.Contain("GameplaySfxPresentationExecutor"));
            Assert.That(bgmSource, Does.Not.Contain("GameplayActionAudioPresentationExecutor"));
            Assert.That(bgmSource, Does.Not.Contain("GameplayEnemyAudioPresentationExecutor"));
            Assert.That(uiSource, Does.Not.Contain("GameplaySfxPresentationExecutor"));
            Assert.That(uiSource, Does.Not.Contain("GameplayActionAudioPresentationExecutor"));
            Assert.That(uiSource, Does.Not.Contain("GameplayEnemyAudioPresentationExecutor"));
        }

        [Test]
        [Category("Core")]
        public void UiBoundaryGovernance_DoesNotConsumeRawPlan()
        {
            var uiSource = ReadDirectorySource("Assets/_Features/UI");
            var forbiddenTokens = new[]
            {
                "PresentationCueFrame",
                "PresentationPlaybackPlan",
                "PresentationPlaybackScheduler",
                "TopologyPresentationOwnershipDiagnostics",
                "GameplayMotionExecutorDiagnostics",
                "GameplayAnimationExecutorDiagnostics",
                "PlayerActionAnimationProductionTelemetrySnapshot",
                "GameplayEnemyPresentationExecutorDiagnostics",
                "EnemyPresentationProductionTelemetrySnapshot",
                "DamageDeathVfxExecutorDiagnostics",
                "DamageDeathVfxSemanticDiagnostics",
                "DamageDeathVfxOmissionReason",
                "DamageHitOmittedByEnemyDeathCount",
                "GameplaySfxExecutorDiagnostics",
                "GameplaySfxSemanticDiagnostics",
                "GameplaySfxPlaybackAdapterDiagnostics",
                "GameplaySfxFallbackReason",
                "GameplayActionAudioExecutorDiagnostics",
                "ActionAudioProductionTelemetrySnapshot",
                "GameplayEnemyAudioExecutorDiagnostics",
                "EnemyAudioProductionTelemetrySnapshot",
                "TopologyPresentationExecutionMode",
                "DamageDeathVfxExecutionMode",
                "BoxMotionPresentationExecutionMode",
                "PlayerActionAnimationExecutionMode",
                "EnemyPresentationExecutionMode",
                "CoreGameplaySfxExecutionMode",
                "ActionAudioExecutionMode",
                "EnemyAudioExecutionMode",
            };

            Assert.That(uiSource, Does.Contain("GameplayUiPresentationSource"));
            Assert.That(uiSource, Does.Contain("UIPresentationSnapshot"));
            foreach (var token in forbiddenTokens)
            {
                Assert.That(uiSource, Does.Not.Contain(token), token);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionSwitchReadiness_ReflectsPlayerActionAnimationSwitch()
        {
            var coreSfx = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(CoreGameplaySfxExecutionMode));
            var damageDeathVfx = ReadinessMatrix.Single(row => row.Domain == "Damage/death VFX");
            var boxMotion = ReadinessMatrix.Single(row => row.Domain == "Box motion");
            var playerActionAnimation = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(PlayerActionAnimationExecutionMode));
            var readinessDocument = ReadRepoFile(ReadinessDocumentPath);

            Assert.That(coreSfx.CurrentDefault, Is.EqualTo(coreSfx.OrchestrationOwner));
            Assert.That(coreSfx.DefaultIsLegacy, Is.False);
            Assert.That(coreSfx.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(coreSfx.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered));
            Assert.That(readinessDocument, Does.Contain("Phase 9D"));
            Assert.That(readinessDocument, Does.Contain("ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered"));
            Assert.That(readinessDocument, Does.Contain("CoreSfxProductionDefault_PlayMode_UsesOrchestrationOwner"));
            Assert.That(readinessDocument, Does.Contain("CoreSfxProductionDefault_PlayMode_TopologyLockDefersAndDrains"));

            Assert.That(damageDeathVfx.CurrentDefault, Is.EqualTo(damageDeathVfx.OrchestrationOwner));
            Assert.That(damageDeathVfx.DefaultIsLegacy, Is.False);
            Assert.That(damageDeathVfx.InvalidModeNormalizesToLegacy, Is.False);
            Assert.That(damageDeathVfx.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered));
            Assert.That(readinessDocument, Does.Contain("Phase 9F"));
            Assert.That(readinessDocument, Does.Contain("ProductionDefaultOnTelemetryHardened"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfxProductionDefault_PlayMode_UsesOrchestrationOwner"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfxProductionDefault_PlayMode_SameTickDeathSuppressesDamage"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics"));

            Assert.That(boxMotion.CurrentDefault, Is.EqualTo(boxMotion.OrchestrationOwner));
            Assert.That(boxMotion.DefaultIsLegacy, Is.False);
            Assert.That(boxMotion.InvalidModeNormalizesToLegacy, Is.False);
            Assert.That(boxMotion.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));
            Assert.That(readinessDocument, Does.Contain("Phase 9H"));
            Assert.That(readinessDocument, Does.Contain("Phase 9J"));
            Assert.That(readinessDocument, Does.Contain("ProductionDefaultOnTelemetryHardened"));
            Assert.That(readinessDocument, Does.Contain("BoxMotionProductionDefault_PlayMode_UsesOrchestrationOwner"));
            Assert.That(readinessDocument, Does.Contain("BoxMotionProductionDefault_PlayMode_ConcreteAdapterStartsAndCompletesTracks"));
            Assert.That(readinessDocument, Does.Contain("BoxMotionProductionDefault_PlayMode_FlipPoseAndVisualRootReset"));
            Assert.That(readinessDocument, Does.Contain("BoxMotion_DefaultOrchestration_TelemetryCoversSlideFlipImpact"));
            Assert.That(readinessDocument, Does.Contain("GameplayInputHost_BoxSlidePresentation_DoesNotBlockSimulationTicks"));
            Assert.That(readinessDocument, Does.Contain("GameplayInputHost_FlipPresentation_DoesNotBlockSubsequentTicks"));

            Assert.That(playerActionAnimation.CurrentDefault, Is.EqualTo(playerActionAnimation.OrchestrationOwner));
            Assert.That(playerActionAnimation.DefaultIsLegacy, Is.False);
            Assert.That(playerActionAnimation.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(playerActionAnimation.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));
            Assert.That(readinessDocument, Does.Contain("Phase 9K"));
            Assert.That(readinessDocument, Does.Contain("Phase 9L"));
            Assert.That(readinessDocument, Does.Contain("Phase 9M"));
            Assert.That(readinessDocument, Does.Contain("Phase 9N"));
            Assert.That(readinessDocument, Does.Contain("AcceptedTemporaryAdapterContract"));
            Assert.That(readinessDocument, Does.Contain("ProductionDefaultOnTelemetryHardened"));
            Assert.That(readinessDocument, Does.Contain("PlayerActionAnimation_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues"));
            Assert.That(readinessDocument, Does.Contain("PlayerPushExecute -> PlayerPresentationPhase.PushRecovery"));
            Assert.That(readinessDocument, Does.Contain("PlayerFlipExecute -> PlayerPresentationPhase.FlipRecovery"));

            var topology = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(TopologyPresentationExecutionMode));
            Assert.That(topology.CurrentDefault, Is.EqualTo(topology.OrchestrationOwner));
            Assert.That(topology.DefaultIsLegacy, Is.False);
            Assert.That(topology.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(topology.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));

            var enemyPresentation = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(EnemyPresentationExecutionMode));
            Assert.That(enemyPresentation.CurrentDefault, Is.EqualTo(enemyPresentation.OrchestrationOwner));
            Assert.That(enemyPresentation.DefaultIsLegacy, Is.False);
            Assert.That(enemyPresentation.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(enemyPresentation.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));

            foreach (var row in ReadinessMatrix)
            {
                Assert.That(row.CurrentDefault, Is.EqualTo(row.OrchestrationOwner), row.Domain);
                Assert.That(row.DefaultIsLegacy, Is.False, row.Domain);
                Assert.That(row.RecommendedStatus, Is.Not.EqualTo(ProductionSwitchRecommendedStatus.KeepLegacy), row.Domain);
                Assert.That(row.RecommendedStatus, Is.Not.EqualTo(ProductionSwitchRecommendedStatus.LegacyRetainedByDesign), row.Domain);
            }
        }

        [Test]
        [Category("Core")]
        public void CoreSfx_ProductionDefault_RemainsStableAfterVfxTelemetry()
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
            var rootObject = new GameObject(nameof(CoreSfx_ProductionDefault_RemainsStableAfterVfxTelemetry));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.That(coordinator.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(
                    CoreGameplaySfxExecutionPolicy.Normalize((CoreGameplaySfxExecutionMode)999),
                    Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ExistingProductionDefaultsRemainStableAfterPhase9ProductionSwitch()
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
            var rootObject = new GameObject(nameof(ExistingProductionDefaultsRemainStableAfterPhase9ProductionSwitch));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.That(coordinator.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(presenter.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
                Assert.That(presenter.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.False);
                Assert.That(coordinator.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionSwitchCandidate_IsEmptyAfterPlayerAnimationSwitch()
        {
            var candidates = ReadinessMatrix
                .Where(row => row.RecommendedStatus == ProductionSwitchRecommendedStatus.CandidateForNextPR)
                .ToArray();

            Assert.That(candidates, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GovernanceData_IsNonAuthoritative()
        {
            var hostRuntimeSource = ReadDirectorySource(HostRuntimeDirectory);
            var authoritativeSource = ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Model/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Loop/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime") + "\n" +
                                      ReadDirectorySource("Assets/_Features/Gameplay/Gameplay_Entities/Runtime");
            var governanceSource = ReadRepoFile(ReadinessDocumentPath) + "\n" + ReadRepoFile(ThisTestPath);

            Assert.That(File.Exists(ToAbsolutePath(ReadinessDocumentPath)), Is.True);
            Assert.That(governanceSource, Does.Contain("WorldState"));
            Assert.That(governanceSource, Does.Contain("TickPipeline"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("ProductionSwitchReadiness"));
            Assert.That(hostRuntimeSource, Does.Not.Contain("CandidateForNextPR"));

            foreach (var row in ReadinessMatrix)
            {
                if (row.ExecutionModeType != null)
                {
                    Assert.That(authoritativeSource, Does.Not.Contain(row.ExecutionModeType.Name), row.ExecutionModeType.Name);
                }

                Assert.That(authoritativeSource, Does.Not.Contain(row.OrchestrationOwner), row.Domain);
            }
        }

        private static IEnumerable<string> EnumerateProductionConfigFiles()
        {
            var roots = new[]
            {
                "Assets/Scenes",
                "Assets/_Features/Stages/Content",
                "Assets/_Features/Gameplay/Gameplay_Host/Authoring",
            };
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".asset",
                ".prefab",
                ".unity",
            };

            foreach (var root in roots)
            {
                var absoluteRoot = ToAbsolutePath(root);
                if (!Directory.Exists(absoluteRoot))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!extensions.Contains(Path.GetExtension(file)))
                    {
                        continue;
                    }

                    var relative = ToRepoRelativePath(file);
                    if (relative.StartsWith("Assets/InitTestScene", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return relative;
                }
            }
        }

        private static void AssertFieldAwareProductionConfigScannerContract()
        {
            var playerActionField = LifecycleMatrix.Single(row => row.Domain == "Player action animation").SerializedOwnerFieldName;
            var topologyField = LifecycleMatrix.Single(row => row.Domain == "Topology transition").SerializedOwnerFieldName;

            Assert.That(
                FindSerializedRollbackOwnerViolations("m_RemovedComponents: []", LifecycleMatrix),
                Is.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations("m_RemovedGameObjects: []", LifecycleMatrix),
                Is.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations("someComment: \"Removed\"", LifecycleMatrix),
                Is.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations("unrelatedField: Removed", LifecycleMatrix),
                Is.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations($"{playerActionField}: Removed", LifecycleMatrix),
                Is.Not.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations($"{topologyField}: LegacyCoordinator", LifecycleMatrix),
                Is.Empty);
            Assert.That(
                FindSerializedRollbackOwnerViolations($"{topologyField}: LegacyEnemyPresentationMapper", LifecycleMatrix),
                Is.Not.Empty);
        }

        private static IReadOnlyList<string> FindSerializedRollbackOwnerViolations(
            string source,
            IEnumerable<PresentationDomainLifecycleRow> lifecycleRows)
        {
            var violations = new List<string>();
            foreach (var row in lifecycleRows.Where(row => !string.IsNullOrEmpty(row.SerializedOwnerFieldName)))
            {
                var values = FindYamlFieldValues(source, row.SerializedOwnerFieldName).ToArray();
                foreach (var value in values)
                {
                    if (row.AllowsSerializedRollbackOwner)
                    {
                        if (!SerializedValueEquals(value, row.CurrentProductionOwner) &&
                            !SerializedValueEquals(value, row.LegacyOwner))
                        {
                            violations.Add(
                                $"{row.Domain}: {row.SerializedOwnerFieldName} serialized unsupported owner '{value}' ({row.Reason})");
                        }

                        continue;
                    }

                    if (row.ForbidsSerializedRollbackOwner &&
                        SerializedValueEquals(value, row.LegacyOwner))
                    {
                        violations.Add(
                            $"{row.Domain}: {row.SerializedOwnerFieldName} serialized forbidden rollback owner '{value}' ({row.Reason})");
                    }
                }
            }

            return violations;
        }

        private static IEnumerable<string> FindYamlFieldValues(string source, string fieldName)
        {
            var prefix = fieldName + ":";
            var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = trimmed.Substring(prefix.Length).Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                yield return TrimSerializedScalar(value);
            }
        }

        private static bool SerializedValueEquals(string value, string expected)
        {
            return string.Equals(TrimSerializedScalar(value), expected, StringComparison.Ordinal);
        }

        private static string TrimSerializedScalar(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length >= 2 &&
                ((trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"') ||
                 (trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'')))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static string ReadDirectorySource(string relativeDirectory)
        {
            var absoluteDirectory = ToAbsolutePath(relativeDirectory);
            if (!Directory.Exists(absoluteDirectory))
            {
                return string.Empty;
            }

            return string.Join(
                "\n",
                Directory.EnumerateFiles(absoluteDirectory, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));
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

        private enum ProductionSwitchRecommendedStatus
        {
            KeepLegacy = 0,
            CandidateForNextPR = 1,
            NeedsMoreCoverage = 2,
            DoNotSwitchYet = 3,
            ReadinessHardened = 4,
            ProductionDefaultOn = 5,
            LegacyRetainedByDesign = 6,
            ProductionDefaultOnTelemetryHardened = 7,
            ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered = 8,
            NeedsExecuteDriverSurface = 9,
            NeedsMorePlayModeEvidence = 10,
            CandidateForPlayModeSmoke = 11,
        }

        private enum PresentationDomainLifecycleState
        {
            NotYetDecommissioned,
            CurrentOnlyDecommissioned,
            RetainedAdjunct,
            SerializedCompatibility,
            TestGovernanceOnly,
        }

        private sealed class PresentationDomainLifecycleRow
        {
            public PresentationDomainLifecycleRow(
                string domain,
                PresentationDomainLifecycleState state,
                bool requiresRollbackSwitch,
                bool forbidsSerializedRollbackOwner,
                bool allowsSerializedRollbackOwner,
                string serializedOwnerFieldName,
                string currentProductionOwner,
                string legacyOwner,
                string reason)
            {
                Domain = domain;
                State = state;
                RequiresRollbackSwitch = requiresRollbackSwitch;
                ForbidsSerializedRollbackOwner = forbidsSerializedRollbackOwner;
                AllowsSerializedRollbackOwner = allowsSerializedRollbackOwner;
                SerializedOwnerFieldName = serializedOwnerFieldName;
                CurrentProductionOwner = currentProductionOwner;
                LegacyOwner = legacyOwner;
                Reason = reason;
            }

            public string Domain { get; }
            public PresentationDomainLifecycleState State { get; }
            public bool RequiresRollbackSwitch { get; }
            public bool ForbidsSerializedRollbackOwner { get; }
            public bool AllowsSerializedRollbackOwner { get; }
            public string SerializedOwnerFieldName { get; }
            public string CurrentProductionOwner { get; }
            public string LegacyOwner { get; }
            public string Reason { get; }
        }

        private sealed class ProductionSwitchReadinessRow
        {
            public ProductionSwitchReadinessRow(
                string domain,
                Type executionModeType,
                string legacyOwner,
                string orchestrationOwner,
                string currentDefault,
                string controlledMode,
                bool defaultIsLegacy,
                bool invalidModeNormalizesToLegacy,
                string duplicateGuardEvidence,
                string determinismEvidence,
                string lifecycleCleanupEvidence,
                string boundaryEvidence,
                string inputLockRisk,
                string audioUiRisk,
                string rollbackPath,
                ProductionSwitchRecommendedStatus recommendedStatus)
            {
                Domain = domain;
                ExecutionModeType = executionModeType;
                LegacyOwner = legacyOwner;
                OrchestrationOwner = orchestrationOwner;
                CurrentDefault = currentDefault;
                ControlledMode = controlledMode;
                DefaultIsLegacy = defaultIsLegacy;
                InvalidModeNormalizesToLegacy = invalidModeNormalizesToLegacy;
                DuplicateGuardEvidence = duplicateGuardEvidence;
                DeterminismEvidence = determinismEvidence;
                LifecycleCleanupEvidence = lifecycleCleanupEvidence;
                BoundaryEvidence = boundaryEvidence;
                InputLockRisk = inputLockRisk;
                AudioUiRisk = audioUiRisk;
                RollbackPath = rollbackPath;
                RecommendedStatus = recommendedStatus;
            }

            public string Domain { get; }
            public Type ExecutionModeType { get; }
            public string LegacyOwner { get; }
            public string OrchestrationOwner { get; }
            public string CurrentDefault { get; }
            public string ControlledMode { get; }
            public bool DefaultIsLegacy { get; }
            public bool InvalidModeNormalizesToLegacy { get; }
            public string DuplicateGuardEvidence { get; }
            public string DeterminismEvidence { get; }
            public string LifecycleCleanupEvidence { get; }
            public string BoundaryEvidence { get; }
            public string InputLockRisk { get; }
            public string AudioUiRisk { get; }
            public string RollbackPath { get; }
            public ProductionSwitchRecommendedStatus RecommendedStatus { get; }
        }
    }
}
