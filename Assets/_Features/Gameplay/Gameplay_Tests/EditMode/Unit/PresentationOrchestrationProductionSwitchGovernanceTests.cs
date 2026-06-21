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
                typeof(DamageDeathVfxExecutionMode),
                "LegacyExtension",
                "OrchestrationExecutor",
                "OrchestrationExecutor",
                "OrchestrationExecutor",
                false,
                true,
                "DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback",
                "DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative",
                "DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics",
                "VfxPlanningBoundary_StaysPresentationOnly",
                "Low",
                "Low",
                "Set DamageDeathVfxExecutionMode.LegacyExtension.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered),
            new(
                "Box motion",
                typeof(BoxMotionPresentationExecutionMode),
                "LegacyTrackPlanner",
                "OrchestrationMotionExecutor",
                "OrchestrationMotionExecutor",
                "OrchestrationMotionExecutor",
                false,
                true,
                "BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced",
                "BoxMotionProductionDefault_PlayMode_IsNonAuthoritative",
                "BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose",
                "BoxMotionExecutionSwitch_DoesNotLeakIntoInputOrVfxContracts",
                "Medium: motion can affect perceived input timing.",
                "Low",
                "Set BoxMotionPresentationExecutionMode.LegacyTrackPlanner.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Player action animation",
                typeof(PlayerActionAnimationExecutionMode),
                "LegacyAnimationSync",
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
                "Set PlayerActionAnimationExecutionMode.LegacyAnimationSync.",
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
            new(
                "Action audio",
                typeof(ActionAudioExecutionMode),
                "LegacyActionAudioController",
                "OrchestrationActionAudioBridge",
                "OrchestrationActionAudioBridge",
                "OrchestrationActionAudioBridge",
                false,
                true,
                "ActionAudio_ProductionTelemetry_CoversOwnerSemanticAndRollbackValues",
                "ActionAudio_OrchestrationRoute_IsNonBlockingAndDoesNotMutateAuthoritativeTickResult",
                "ActionAudio_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState",
                "ActionAudioPlanningBoundary_StaysActionAudioOwned",
                "Low",
                "Medium: profile/authoring edge cases remain.",
                "Set ActionAudioExecutionMode.LegacyActionAudioController.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
            new(
                "Enemy audio",
                typeof(EnemyAudioExecutionMode),
                "LegacyEnemyAudioController",
                "OrchestrationEnemyAudioBridge",
                "OrchestrationEnemyAudioBridge",
                "OrchestrationEnemyAudioBridge",
                false,
                true,
                "EnemyAudio_ProductionTelemetry_CoversOneShotOwnerSemanticLoopAndRollbackValues",
                "EnemyAudio_OrchestrationBridgeMode_DoesNotMutateTickResultOrBlockingState",
                "EnemyAudio_OrchestrationBridgeMode_LifecycleClearsDiagnosticsPortAndGuard",
                "EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership",
                "Low",
                "Medium: latest audio integration.",
                "Set EnemyAudioExecutionMode.LegacyEnemyAudioController.",
                ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened),
        };

        [Test]
        [Category("Core")]
        public void ProductionSwitchReadinessMatrix_IncludesAllKnownExecutionModes()
        {
            var knownTypes = new[]
            {
                typeof(TopologyPresentationExecutionMode),
                typeof(DamageDeathVfxExecutionMode),
                typeof(BoxMotionPresentationExecutionMode),
                typeof(PlayerActionAnimationExecutionMode),
                typeof(EnemyPresentationExecutionMode),
                typeof(CoreGameplaySfxExecutionMode),
                typeof(ActionAudioExecutionMode),
                typeof(EnemyAudioExecutionMode),
            };
            var matrixTypes = ReadinessMatrix.Select(row => row.ExecutionModeType).ToArray();

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
                Assert.That(coordinator.DamageDeathVfxExecutionMode, Is.EqualTo(DamageDeathVfxExecutionMode.OrchestrationExecutor));
                Assert.That(coordinator.BoxMotionPresentationExecutionMode, Is.EqualTo(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(coordinator.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(coordinator.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(coordinator.ActionAudioExecutionMode, Is.EqualTo(ActionAudioExecutionMode.OrchestrationActionAudioBridge));
                Assert.That(coordinator.EnemyAudioExecutionMode, Is.EqualTo(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));

                Assert.That(presenter.TopologyPresentationExecutionMode, Is.EqualTo(TopologyPresentationExecutionMode.ExecutorBridge));
                Assert.That(presenter.DamageDeathVfxExecutionMode, Is.EqualTo(DamageDeathVfxExecutionMode.OrchestrationExecutor));
                Assert.That(presenter.BoxMotionPresentationExecutionMode, Is.EqualTo(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
                Assert.That(presenter.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(presenter.EnemyPresentationExecutionMode, Is.EqualTo(EnemyPresentationExecutionMode.OrchestrationEnemyPresentationExecutor));
                Assert.That(presenter.CoreGameplaySfxExecutionMode, Is.EqualTo(CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor));
                Assert.That(presenter.ActionAudioExecutionMode, Is.EqualTo(ActionAudioExecutionMode.OrchestrationActionAudioBridge));
                Assert.That(presenter.EnemyAudioExecutionMode, Is.EqualTo(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));

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
                DamageDeathVfxExecutionPolicy.Normalize((DamageDeathVfxExecutionMode)999),
                Is.EqualTo(DamageDeathVfxExecutionMode.LegacyExtension));
            Assert.That(
                BoxMotionExecutionPolicy.Normalize((BoxMotionPresentationExecutionMode)999),
                Is.EqualTo(BoxMotionPresentationExecutionMode.LegacyTrackPlanner));
            Assert.That(
                PlayerActionAnimationExecutionPolicy.Normalize((PlayerActionAnimationExecutionMode)999),
                Is.EqualTo(PlayerActionAnimationExecutionMode.LegacyAnimationSync));
            Assert.That(
                EnemyPresentationExecutionPolicy.Normalize((EnemyPresentationExecutionMode)999),
                Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(
                CoreGameplaySfxExecutionPolicy.Normalize((CoreGameplaySfxExecutionMode)999),
                Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
            Assert.That(
                ActionAudioExecutionPolicy.Normalize((ActionAudioExecutionMode)999),
                Is.EqualTo(ActionAudioExecutionMode.LegacyActionAudioController));
            Assert.That(
                EnemyAudioExecutionPolicy.Normalize((EnemyAudioExecutionMode)999),
                Is.EqualTo(EnemyAudioExecutionMode.LegacyEnemyAudioController));

            Assert.That(default(TopologyPresentationExecutionMode), Is.EqualTo(TopologyPresentationExecutionMode.LegacyCoordinator));
            Assert.That(default(DamageDeathVfxExecutionMode), Is.EqualTo(DamageDeathVfxExecutionMode.LegacyExtension));
            Assert.That(default(BoxMotionPresentationExecutionMode), Is.EqualTo(BoxMotionPresentationExecutionMode.LegacyTrackPlanner));
            Assert.That(default(PlayerActionAnimationExecutionMode), Is.EqualTo(PlayerActionAnimationExecutionMode.LegacyAnimationSync));
            Assert.That(default(EnemyPresentationExecutionMode), Is.EqualTo(EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper));
            Assert.That(default(CoreGameplaySfxExecutionMode), Is.EqualTo(CoreGameplaySfxExecutionMode.LegacyGameplayAudioController));
            Assert.That(default(ActionAudioExecutionMode), Is.EqualTo(ActionAudioExecutionMode.LegacyActionAudioController));
            Assert.That(default(EnemyAudioExecutionMode), Is.EqualTo(EnemyAudioExecutionMode.LegacyEnemyAudioController));

            foreach (var row in ReadinessMatrix)
            {
                Assert.That(row.InvalidModeNormalizesToLegacy, Is.True, row.Domain);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionConfig_DoesNotSerializeRollbackOwnersForPhase9ProductionDomains()
        {
            var productionPaths = EnumerateProductionConfigFiles().ToArray();
            var allowedTokens = ReadinessMatrix
                .Select(row => row.OrchestrationOwner)
                .Distinct()
                .ToArray();
            var forbiddenTokens = ReadinessMatrix
                .Select(row => row.LegacyOwner)
                .Distinct()
                .ToArray();

            Assert.That(productionPaths, Is.Not.Empty);
            foreach (var allowedToken in allowedTokens)
            {
                Assert.That(forbiddenTokens, Does.Not.Contain(allowedToken));
            }

            foreach (var path in productionPaths)
            {
                var source = ReadRepoFile(path);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), $"{path} must not serialize rollback owner {token}.");
                }
            }
        }

        [Test]
        [Category("Core")]
        public void RollbackSwitches_RemainAvailable()
        {
            foreach (var row in ReadinessMatrix)
            {
                Assert.That(row.RollbackPath, Is.Not.Empty, row.Domain);
                Assert.That(row.RollbackPath, Does.Contain(row.LegacyOwner), row.Domain);
                Assert.That(row.DuplicateGuardEvidence, Is.Not.Empty, row.Domain);
                Assert.That(row.LifecycleCleanupEvidence, Is.Not.Empty, row.Domain);
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
                "DamageDeathVfxSuppressionReason",
                "DamageHitSuppressedByEnemyDeathCount",
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
            var damageDeathVfx = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(DamageDeathVfxExecutionMode));
            var boxMotion = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(BoxMotionPresentationExecutionMode));
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
            Assert.That(damageDeathVfx.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(damageDeathVfx.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered));
            Assert.That(readinessDocument, Does.Contain("Phase 9F"));
            Assert.That(readinessDocument, Does.Contain("ProductionDefaultOnTelemetryHardened"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfxProductionDefault_PlayMode_UsesOrchestrationOwner"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfxProductionDefault_PlayMode_SameTickDeathSuppressesDamage"));
            Assert.That(readinessDocument, Does.Contain("DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics"));

            Assert.That(boxMotion.CurrentDefault, Is.EqualTo(boxMotion.OrchestrationOwner));
            Assert.That(boxMotion.DefaultIsLegacy, Is.False);
            Assert.That(boxMotion.InvalidModeNormalizesToLegacy, Is.True);
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

            var actionAudio = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(ActionAudioExecutionMode));
            Assert.That(actionAudio.CurrentDefault, Is.EqualTo(actionAudio.OrchestrationOwner));
            Assert.That(actionAudio.DefaultIsLegacy, Is.False);
            Assert.That(actionAudio.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(actionAudio.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));

            var enemyAudio = ReadinessMatrix.Single(row => row.ExecutionModeType == typeof(EnemyAudioExecutionMode));
            Assert.That(enemyAudio.CurrentDefault, Is.EqualTo(enemyAudio.OrchestrationOwner));
            Assert.That(enemyAudio.DefaultIsLegacy, Is.False);
            Assert.That(enemyAudio.InvalidModeNormalizesToLegacy, Is.True);
            Assert.That(enemyAudio.RecommendedStatus, Is.EqualTo(ProductionSwitchRecommendedStatus.ProductionDefaultOnTelemetryHardened));

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
                Assert.That(coordinator.DamageDeathVfxExecutionMode, Is.EqualTo(DamageDeathVfxExecutionMode.OrchestrationExecutor));
                Assert.That(presenter.DamageDeathVfxExecutionMode, Is.EqualTo(DamageDeathVfxExecutionMode.OrchestrationExecutor));
                Assert.That(coordinator.BoxMotionPresentationExecutionMode, Is.EqualTo(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
                Assert.That(presenter.BoxMotionPresentationExecutionMode, Is.EqualTo(BoxMotionPresentationExecutionMode.OrchestrationMotionExecutor));
                Assert.That(coordinator.ActionAudioExecutionMode, Is.EqualTo(ActionAudioExecutionMode.OrchestrationActionAudioBridge));
                Assert.That(coordinator.EnemyAudioExecutionMode, Is.EqualTo(EnemyAudioExecutionMode.OrchestrationEnemyAudioBridge));
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
                Assert.That(authoritativeSource, Does.Not.Contain(row.ExecutionModeType.Name), row.ExecutionModeType.Name);
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
