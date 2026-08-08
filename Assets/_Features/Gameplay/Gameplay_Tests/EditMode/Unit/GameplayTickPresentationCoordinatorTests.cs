using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.PresentationContracts;
using Game.Feature.Gameplay.PresentationPlanning;
using Game.Feature.Gameplay.PresentationPlayback;
using Game.Feature.Gameplay.PresentationRuntime;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTickPresentationCoordinatorTests
    {
        private const string MoonGeneratorPrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Prefabs/TileFeature_MoonGenerator_Default.prefab";

        private const string MoonGeneratorDoorOpenClipPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Animations/MoonBlockGenerator_DoorOpen.anim";
        private const string MoonGeneratorProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Board/Profiles/TileFeatureVisualProfile_MoonGenerator.asset";
        private const string EnemyJumpAnimatorControllerPath =
            "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyAnimator_Jump.controller";
        private const string GravityFieldLockableShaderName = "Game/Presentation/GravityFieldLockableBoxLit";
        private const string GravityFieldLockedWeightProperty = "_GravityFieldLockedWeight";
        private const string GravityFieldLockRevealProperty = "_GravityFieldLockReveal";
        private const string GravityFieldLockNoiseMapProperty = "_GravityFieldLockNoiseMap";
        private const string GravityFieldLockEdgeWidthProperty = "_GravityFieldLockEdgeWidth";
        private const string GravityFieldLockedTintProperty = "_GravityFieldLockedTint";
        private const string GravityFieldDimFactorProperty = "_GravityFieldDimFactor";
        private const string GravityFieldTintStrengthProperty = "_GravityFieldTintStrength";
        private const string GravityFieldEmissionOmissionProperty = "_GravityFieldEmissionOmission";
        private const float GravityFieldLockRevealInSeconds = 0.234f;
        private const float GravityFieldLockRevealOutSeconds = 0.208f;
        private const string EnemyInactiveBlendProperty = "_InactiveBlend";
        private const string EnemyInactiveNoiseRevealProperty = "_InactiveNoiseReveal";
        private const string EnemyInactiveTintProperty = "_InactiveTint";
        private const string EnemyInactiveDesaturateStrengthProperty = "_DesaturateStrength";
        private const string EnemyInactiveEmissionOmissionProperty = "_EmissionOmission";
        private const float EnemyInactiveRevealInSeconds = 0.25f;
        private const float EnemyInactiveRevealOutSeconds = 0.18f;
        private const string StaticBoxShowcasePrefabPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_Showcase.prefab";
        private const string StaticBoxShowcaseMaterialPath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Profiles/M_Static_Box_Showcase.mat";
        private const string GravityFieldLockableMaterialDirectory =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Materials/GravityFieldLockable";
        private static readonly string[] GravityFieldLockableBoxPrefabPaths =
        {
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_Block_Tutorial.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_GravityBlock.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_Box_MetalBlock_.prefab",
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Presentation/Boxes/Prefabs/StaticView_MoonBox_Showcase.prefab",
        };

        [Test]
        [Category("Extended")]
        public void CurrentTilePresentationRequests_DefaultsEmpty()
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();

            Assert.That(coordinator.CurrentTilePresentationRequests, Is.Not.Null);
            Assert.That(coordinator.CurrentTilePresentationRequests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void CurrentGravityFieldPresentationRequests_DefaultsEmpty()
        {
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();

            Assert.That(coordinator.CurrentGravityFieldPresentationRequests, Is.Not.Null);
            Assert.That(coordinator.CurrentGravityFieldPresentationRequests, Is.Empty);
            Assert.That(coordinator.CurrentGravityFieldVisualStates, Is.Not.Null);
            Assert.That(coordinator.CurrentGravityFieldVisualStates, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_CurrentRoute_UsesExecutorPortOnce()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_CurrentRoute_UsesExecutorPortOnce));
            var port = new RecordingTopologyTransitionPlaybackPort();

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var coordinator = CreateInitializedTopologyCoordinator(
                    rootObject,
                    port,
                    initialTopology);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 7,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);

                var diagnostics = coordinator.TopologyPresentationOwnershipDiagnostics;
                Assert.That(port.BeginOrRefreshCallCount, Is.EqualTo(1));
                Assert.That(port.LastRequest.TickIndex, Is.EqualTo(7));
                Assert.That(port.LastRequest.SourceTopology, Is.EqualTo(initialTopology));
                Assert.That(port.LastRequest.DestinationTopology, Is.EqualTo(destinationTopology));
                Assert.That(port.LastRequest.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
                Assert.That(port.LastRequest.SourceTickIndex, Is.EqualTo(7));
                Assert.That(port.LastRequest.HasSourceMetadata, Is.False);
                Assert.That(diagnostics.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(diagnostics.LastExecutionTickIndex, Is.EqualTo(7));
                Assert.That(diagnostics.LastExecutionHasSourceMetadata, Is.False);
                Assert.That(diagnostics.LastExecutionSourceMetadataKey, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_ProductionTelemetry_CoversCurrentPlaybackPortRoute()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_ProductionTelemetry_CoversCurrentPlaybackPortRoute));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var coordinator = CreateInitializedDefaultTopologyCoordinator(
                    rootObject,
                    initialTopology,
                    CreateTimingProfile());
                var result = CreateTopologyTransitionResult(
                    tickIndex: 17,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);

                var snapshot = coordinator.TopologyProductionTelemetrySnapshot;
                Assert.That(snapshot.IsProductionDefaultOwner, Is.True);
                Assert.That(snapshot.LastTickIndex, Is.EqualTo(17));
                Assert.That(snapshot.LastSourceTopology, Is.EqualTo(initialTopology));
                Assert.That(snapshot.LastDestinationTopology, Is.EqualTo(destinationTopology));
                Assert.That(snapshot.LastRotationKind, Is.EqualTo(CubeRotationKind.Forward));
                Assert.That(snapshot.LastSourceTickIndex, Is.EqualTo(17));
                Assert.That(snapshot.ExecutorOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(snapshot.ExecutorOwnerExecutedCount, Is.EqualTo(1));
                Assert.That(snapshot.DuplicateOwnerAttemptCount, Is.Zero);
                Assert.That(snapshot.ObservedTrackCount, Is.EqualTo(1));
                Assert.That(snapshot.RouteCount, Is.EqualTo(1));
                Assert.That(snapshot.IgnoredCount, Is.Zero);
                Assert.That(snapshot.InvalidTrackCount, Is.Zero);
                Assert.That(snapshot.MissingPortCount, Is.Zero);
                Assert.That(snapshot.HasBlockingPresentation, Is.True);
                Assert.That(snapshot.IsTopologyTransitionActive, Is.True);
                Assert.That(snapshot.BlockingSnapshot.HasActiveBlockingPresentation, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_ForcedDoubleCurrentAttemptBlocksSecondExecutor()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_ForcedDoubleCurrentAttemptBlocksSecondExecutor));
            var port = new RecordingTopologyTransitionPlaybackPort();

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedTopologyCoordinator(
                    rootObject,
                    port,
                    initialTopology,
                    duplicateExecutors: true);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 8,
                    initialTopology,
                    new CubeTopologyState(FaceId.Front),
                    CubeRotationKind.Forward);

                coordinator.Present(result);

                var diagnostics = coordinator.TopologyPresentationOwnershipDiagnostics;
                Assert.That(port.BeginOrRefreshCallCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateAttemptCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_DefaultOrchestration_TelemetryReportsProductionOwner()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_DefaultOrchestration_TelemetryReportsProductionOwner));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                var telemetry = coordinator.DamageDeathVfxExecutorDiagnostics;
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.DamageCuePlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.DamagePlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(telemetry.DuplicateOmittedCount, Is.Zero);
                Assert.That(telemetry.LastTickIndex, Is.EqualTo(12));
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationVfxCueKey.DamageHit));
                Assert.That(telemetry.LastTargetEntityId, Is.EqualTo(40));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_CurrentPlaybackPortConfiguration_RoutesExecutor()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_CurrentPlaybackPortConfiguration_RoutesExecutor));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_CompositionFactoryPort_RoutesExecutorWithoutPostConfigure()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_CompositionFactoryPort_RoutesExecutorWithoutPostConfigure));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedFactoryConfiguredDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                var telemetry = coordinator.DamageDeathVfxExecutorDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(telemetry.PortMissingCount, Is.Zero);
                Assert.That(telemetry.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_CompositionFactoryNullPort_KeepsProductionMissingPortDiagnostics()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_CompositionFactoryNullPort_KeepsProductionMissingPortDiagnostics));

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedFactoryConfiguredDamageDeathVfxCoordinator(
                    rootObject,
                    playbackPort: null,
                    initialTopology: topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                var telemetry = coordinator.DamageDeathVfxExecutorDiagnostics;
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.PortMissingCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackRequestedCount, Is.Zero);
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_LegacyRoute_NotReachable()
        {
            Assert.That(
                ResolveType("Game.Feature.Gameplay.Host.DamageDeathVfxExecutionMode"),
                Is.Null);
            Assert.That(
                typeof(GameplayTickPresentationCoordinator).GetMethod(
                    "ConfigureDamageDeathVfxExecution",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(GameplayTickViewPresenter).GetMethod(
                    "ConfigureDamageDeathVfxExecution",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_CurrentRoute_IsProductionDefault()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_CurrentRoute_IsProductionDefault));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(rootObject, port, topology);

                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40));

                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.IsProductionDefaultOwner, Is.True);
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_DamageCue_UsesCurrentExecutor()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_DamageCue_UsesCurrentExecutor));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var deathCell = new SurfaceCell(FaceId.Floor, 2, 1);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);

                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 12,
                    topology,
                    enemyDamageEntityId: 40));
                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 13,
                    topology,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9141));

                Assert.That(port.Requests, Has.Count.EqualTo(2));
                AssertDamageVfxRequest(port.Requests[0], tickIndex: 12, entityId: 40);
                AssertDeathVfxRequest(port.Requests[1], tickIndex: 13, entityId: 41, deathCell, presentationSeed: 9141);
                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(2));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                var telemetry = coordinator.DamageDeathVfxExecutorDiagnostics;
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.DamageCuePlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.DamagePlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.DeathPlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackRequestedCount, Is.EqualTo(2));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(2));
                Assert.That(telemetry.DuplicateOmittedCount, Is.Zero);
                Assert.That(telemetry.SemanticDiagnostics, Has.Count.EqualTo(2));
                AssertSemanticTelemetry(
                    telemetry,
                    PresentationVfxCueKey.DamageHit,
                    planned: 1,
                    requested: 1,
                    succeeded: 1,
                    entityId: 40,
                    anchorKind: PresentationAnchorKind.EntityCenter);
                AssertSemanticTelemetry(
                    telemetry,
                    PresentationVfxCueKey.EnemyDeath,
                    planned: 1,
                    requested: 1,
                    succeeded: 1,
                    entityId: 41,
                    anchorKind: PresentationAnchorKind.SurfaceCellCenter);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_DeathCue_UsesCurrentExecutor()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_DeathCue_UsesCurrentExecutor));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var deathCell = new SurfaceCell(FaceId.Floor, 2, 1);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);

                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 13,
                    topology,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9141));

                Assert.That(port.Requests, Has.Count.EqualTo(1));
                AssertDeathVfxRequest(port.Requests[0], tickIndex: 13, entityId: 41, deathCell, presentationSeed: 9141);
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.DeathPlaybackRequestedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_SameTickDamageAndDeath_FollowsPolicy()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_SameTickDamageAndDeath_FollowsPolicy));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var deathCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 14,
                    topology,
                    enemyDamageEntityId: 40,
                    enemyDeathEntityId: 40,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9040);

                coordinator.Present(result);

                Assert.That(port.Requests, Has.Count.EqualTo(1));
                AssertDeathVfxRequest(port.Requests[0], tickIndex: 14, entityId: 40, deathCell, presentationSeed: 9040);
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                var telemetry = coordinator.DamageDeathVfxExecutorDiagnostics;
                Assert.That(telemetry.DamageCuePlannedCount, Is.Zero);
                Assert.That(telemetry.DamagePlaybackRequestedCount, Is.Zero);
                Assert.That(telemetry.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(telemetry.DeathPlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackSucceededCount, Is.EqualTo(1));
                Assert.That(telemetry.SameTickDamageHitOmittedByDeathCount, Is.EqualTo(1));
                Assert.That(telemetry.DuplicateOmittedCount, Is.Zero);
                Assert.That(telemetry.LastOmissionReason, Is.EqualTo(DamageDeathVfxOmissionReason.SameTickDamageHitOmittedByDeath));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 15,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_DuplicateDamage_DedupesOrLayersByContract()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_DuplicateDamage_DedupesOrLayersByContract));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology,
                    duplicateExecutors: true);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 15,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);

                var ownership = coordinator.DamageDeathVfxOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_MissingRequiredDependency_ProductionDiagnostic()
        {
            var missingPortRoot = new GameObject(nameof(DamageDeathVfx_MissingRequiredDependency_ProductionDiagnostic) + "_MissingPort");
            var bindingRoot = new GameObject(nameof(DamageDeathVfx_MissingRequiredDependency_ProductionDiagnostic) + "_Binding");
            var bindingPort = new RecordingDamageDeathVfxPlaybackPort(GameplayVfxPlaybackResultKind.BindingMissing);

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var missingPortCoordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    missingPortRoot,
                    playbackPort: null,
                    initialTopology: topology);
                var bindingCoordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    bindingRoot,
                    bindingPort,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 16,
                    topology,
                    enemyDamageEntityId: 40);

                missingPortCoordinator.Present(result);
                bindingCoordinator.Present(result);

                Assert.That(missingPortCoordinator.DamageDeathVfxExecutorDiagnostics.MissingPortCount, Is.EqualTo(1));
                Assert.That(missingPortCoordinator.DamageDeathVfxExecutorDiagnostics.BindingMissingCount, Is.Zero);
                Assert.That(bindingCoordinator.DamageDeathVfxExecutorDiagnostics.MissingPortCount, Is.Zero);
                Assert.That(bindingCoordinator.DamageDeathVfxExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(bindingPort.TryPlayCallCount, Is.EqualTo(1));

                var targetMissing = PlayDamageDeathVfxCueDirectly(
                    CreateDamageDeathVfxCue(
                        PresentationVfxCueKey.DamageHit,
                        PresentationTarget.Global(),
                        PresentationAnchor.ForGlobal(),
                        tickIndex: 17));
                var anchorMissing = PlayDamageDeathVfxCueDirectly(
                    CreateDamageDeathVfxCue(
                        PresentationVfxCueKey.DamageHit,
                        PresentationTarget.Entity(40),
                        PresentationAnchor.ForGlobal(),
                        tickIndex: 18));
                Assert.That(targetMissing.TargetMissingCount, Is.EqualTo(1));
                Assert.That(targetMissing.AnchorMissingCount, Is.Zero);
                Assert.That(targetMissing.BindingMissingCount, Is.Zero);
                Assert.That(targetMissing.PortMissingCount, Is.Zero);
                Assert.That(targetMissing.LastOmissionReason, Is.EqualTo(DamageDeathVfxOmissionReason.TargetMissing));
                Assert.That(anchorMissing.TargetMissingCount, Is.Zero);
                Assert.That(anchorMissing.AnchorMissingCount, Is.EqualTo(1));
                Assert.That(anchorMissing.BindingMissingCount, Is.Zero);
                Assert.That(anchorMissing.PortMissingCount, Is.Zero);
                Assert.That(anchorMissing.LastOmissionReason, Is.EqualTo(DamageDeathVfxOmissionReason.AnchorMissing));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(missingPortRoot);
                UnityEngine.Object.DestroyImmediate(bindingRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_MissingOptionalContent_NoLegacyFallback()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_MissingOptionalContent_NoLegacyFallback));
            var bindingPort = new RecordingDamageDeathVfxPlaybackPort(GameplayVfxPlaybackResultKind.BindingMissing);

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    bindingPort,
                    topology);

                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 16,
                    topology,
                    enemyDamageEntityId: 40));

                Assert.That(bindingPort.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_EntityExitOrHidden_NoLegacyFallback()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_EntityExitOrHidden_NoLegacyFallback));

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var deathCell = new SurfaceCell(FaceId.Floor, 1, 1);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    playbackPort: null,
                    initialTopology: topology);

                coordinator.Present(CreateDamageDeathVfxResult(
                    tickIndex: 18,
                    topology,
                    enemyDeathEntityId: 41,
                    enemyDeathCell: deathCell,
                    presentationSeed: 9141));

                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.DeathCuePlannedCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.MissingPortCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_LifecycleCleanup_TelemetryClearsState()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_LifecycleCleanup_TelemetryClearsState));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 17,
                    topology,
                    enemyDamageEntityId: 40);

                coordinator.Present(result);
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));

                coordinator.PresentInitial(Array.Empty<EntityState>(), topology);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.DamageCuePlannedCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.SemanticDiagnostics, Is.Empty);

                coordinator.Present(result);
                coordinator.HardCleanupPresentationExtensions();
                Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(coordinator.DamageDeathVfxOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(coordinator.DamageDeathVfxExecutorDiagnostics.SemanticDiagnostics, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DamageDeathVfx_ProductionTelemetry_IsNonAuthoritative()
        {
            var rootObject = new GameObject(nameof(DamageDeathVfx_ProductionTelemetry_IsNonAuthoritative));
            var port = new RecordingDamageDeathVfxPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedDefaultDamageDeathVfxCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateDamageDeathVfxResult(
                    tickIndex: 18,
                    topology,
                    enemyDamageEntityId: 40);
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;
                var determinismHash = result.DeterminismHash;

                coordinator.Present(result);

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.DamageDeathVfxExecutionPipelineBlockingSnapshot);
                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_CurrentRoute_IsProductionDefault()
        {
            var rootObject = new GameObject(nameof(BoxMotion_CurrentRoute_IsProductionDefault));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateBoxMotionResult(
                    tickIndex: 21,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide);

                coordinator.Present(result);

                var ownership = coordinator.BoxMotionOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                Assert.That(ownership.LastExecutionOwner, Is.EqualTo(BoxMotionPresentationExecutionOwner.CurrentExecutor));
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                AssertBlockingSnapshotCleared(coordinator.BoxMotionExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_LegacyRoute_NotReachable()
        {
            var rootObject = new GameObject(nameof(BoxMotion_LegacyRoute_NotReachable));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateBoxMotionResult(
                    tickIndex: 22,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide);

                coordinator.Present(result);

                Assert.That(
                    typeof(GameplayTickPresentationCoordinator).GetMethod("ConfigureBoxMotionPresentationExecution"),
                    Is.Null);
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.LastExecutionOwner, Is.EqualTo(BoxMotionPresentationExecutionOwner.CurrentExecutor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_DefaultMode_IsCurrentExecutor()
        {
            var rootObject = new GameObject(nameof(BoxMotion_DefaultMode_IsCurrentExecutor));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinatorUsingProductionDefault(
                    rootObject,
                    port,
                    topology);

                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 22,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide));

                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.LastExecutionOwner, Is.EqualTo(BoxMotionPresentationExecutionOwner.CurrentExecutor));
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_DefaultOrchestration_RoutesSlideFlipImpact()
        {
            var rootObject = new GameObject(nameof(BoxMotion_DefaultOrchestration_RoutesSlideFlipImpact));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinatorUsingProductionDefault(
                    rootObject,
                    port,
                    topology);
                var slideSource = new SurfaceCell(FaceId.Floor, 0, 0);
                var slideDestination = new SurfaceCell(FaceId.Floor, 1, 0);
                var flipSource = new SurfaceCell(FaceId.Floor, 1, 0);
                var flipDestination = new SurfaceCell(FaceId.Floor, 2, 0);
                var impactSource = new SurfaceCell(FaceId.Floor, 2, 0);
                var impactCell = new SurfaceCell(FaceId.Floor, 2, 1);

                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 23,
                    topology,
                    boxEntityId: 40,
                    slideSource,
                    slideDestination,
                    TickEntityMotionKind.BoxSlide));
                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 24,
                    topology,
                    boxEntityId: 40,
                    flipSource,
                    flipDestination,
                    TickEntityMotionKind.Flip));
                coordinator.Present(CreateBoxFlipImpactResult(
                    tickIndex: 25,
                    topology,
                    boxEntityId: 40,
                    impactTargetEntityId: 50,
                    impactSource,
                    impactCell));

                Assert.That(port.Requests, Has.Count.EqualTo(3));
                AssertBoxMotionRequest(
                    port.Requests[0],
                    PresentationMotionCueKey.BoxSlide,
                    tickIndex: 23,
                    boxEntityId: 40,
                    slideSource,
                    slideDestination,
                    topology);
                AssertBoxMotionRequest(
                    port.Requests[1],
                    PresentationMotionCueKey.BoxFlip,
                    tickIndex: 24,
                    boxEntityId: 40,
                    flipSource,
                    flipDestination,
                    topology);
                AssertBoxMotionRequest(
                    port.Requests[2],
                    PresentationMotionCueKey.BoxFlipImpact,
                    tickIndex: 25,
                    boxEntityId: 40,
                    impactSource,
                    impactCell,
                    topology);
                var ownership = coordinator.BoxMotionOwnershipDiagnostics;
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(3));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(3));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.IsCurrentProductionOwner, Is.True);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.TrackStartedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_DuplicateGuardNormalAndForced()
        {
            var rootObject = new GameObject(nameof(BoxMotion_Readiness_DuplicateGuardNormalAndForced));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology,
                    duplicateExecutors: true);
                var result = CreateBoxMotionResult(
                    tickIndex: 26,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide);

                coordinator.Present(result);

                var ownership = coordinator.BoxMotionOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.LastExecutionOwner, Is.EqualTo(BoxMotionPresentationExecutionOwner.CurrentExecutor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_IsNonBlockingAndInputLockNeutral()
        {
            var rootObject = new GameObject(nameof(BoxMotion_Readiness_IsNonBlockingAndInputLockNeutral));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateBoxMotionResult(
                    tickIndex: 27,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide);
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;
                var determinismHash = result.DeterminismHash;

                coordinator.Present(result);

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.BoxMotionExecutionPipelineBlockingSnapshot);
                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                Assert.That(result.MovementPhaseResult, Is.SameAs(MovementPhaseResult.Empty));
                Assert.That(result.AttackPhaseResult, Is.SameAs(AttackPhaseResult.Empty));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_IsDeterminismNeutral()
        {
            var rootObject = new GameObject(nameof(BoxMotion_Readiness_IsDeterminismNeutral));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology);
                var result = CreateBoxMotionResult(
                    tickIndex: 28,
                    topology,
                    boxEntityId: 40,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide);
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;
                var movementPhaseResult = result.MovementPhaseResult;
                var attackPhaseResult = result.AttackPhaseResult;
                var determinismHash = result.DeterminismHash;

                coordinator.Present(result);

                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                Assert.That(result.MovementPhaseResult, Is.SameAs(movementPhaseResult));
                Assert.That(result.AttackPhaseResult, Is.SameAs(attackPhaseResult));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_MissingDiagnosticsSeparated()
        {
            var driverRoot = new GameObject(nameof(BoxMotion_Readiness_MissingDiagnosticsSeparated) + "_Driver");
            var bindingRoot = new GameObject(nameof(BoxMotion_Readiness_MissingDiagnosticsSeparated) + "_Binding");
            var portRoot = new GameObject(nameof(BoxMotion_Readiness_MissingDiagnosticsSeparated) + "_Port");

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var driverMissingCoordinator = CreateInitializedDefaultBoxMotionCoordinator(
                    driverRoot,
                    topology,
                    new MotionOverrideViewFactory(driverRoot.transform));
                var bindingMissingCoordinator = CreateInitializedDefaultBoxMotionCoordinator(
                    bindingRoot,
                    topology,
                    new MotionOverrideViewFactory(bindingRoot.transform));
                var missingPortCoordinator = CreateInitializedBoxMotionCoordinatorWithNullExecutorPort(
                    portRoot,
                    topology);

                driverMissingCoordinator.Present(CreateBoxFlipImpactResult(
                    tickIndex: 28,
                    topology,
                    boxEntityId: 40,
                    impactTargetEntityId: 50,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    impactCell: new SurfaceCell(FaceId.Floor, 1, 0)));
                bindingMissingCoordinator.Present(CreateBoxMotionResult(
                    tickIndex: 29,
                    topology,
                    boxEntityId: 41,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide,
                    includeFinalBox: false));
                missingPortCoordinator.Present(CreateBoxMotionResult(
                    tickIndex: 30,
                    topology,
                    boxEntityId: 42,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    destinationCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    TickEntityMotionKind.BoxSlide));

                Assert.That(driverMissingCoordinator.BoxMotionExecutorDiagnostics.DriverMissingCount, Is.EqualTo(1));
                Assert.That(bindingMissingCoordinator.BoxMotionExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(missingPortCoordinator.BoxMotionExecutorDiagnostics.MissingPortCount, Is.EqualTo(1));
                Assert.That(driverMissingCoordinator.BoxMotionExecutorDiagnostics.TargetMissingCount, Is.Zero);
                Assert.That(driverMissingCoordinator.BoxMotionExecutorDiagnostics.AnchorMissingCount, Is.Zero);
                Assert.That(driverMissingCoordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(bindingMissingCoordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.EqualTo(1));
                Assert.That(missingPortCoordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(driverMissingCoordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(bindingMissingCoordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(missingPortCoordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(driverRoot);
                UnityEngine.Object.DestroyImmediate(bindingRoot);
                UnityEngine.Object.DestroyImmediate(portRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_InvalidMode_NormalizesToExecutorAndCallsPlaybackPort()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_InvalidMode_NormalizesToExecutorAndCallsPlaybackPort));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    (PlayerActionAnimationExecutionMode)999,
                    port,
                    topology);
                var result = CreatePlayerActionAnimationResult(tickIndex: 31, topology);
                var expectedCueCount = CountPlayerActionAnimationCues(result);

                coordinator.Present(result);

                var ownership = coordinator.PlayerActionAnimationOwnershipDiagnostics;
                Assert.That(coordinator.PlayerActionAnimationExecutionMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(expectedCueCount));
                Assert.That(ownership.Mode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(ownership.PlannedCueCount, Is.EqualTo(expectedCueCount));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(expectedCueCount));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(expectedCueCount));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                AssertBlockingSnapshotCleared(coordinator.PlayerActionAnimationExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_OrchestrationExecutorMode_RoutesPushFlipPhaseRequests()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_OrchestrationExecutorMode_RoutesPushFlipPhaseRequests));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology);
                var result = CreatePlayerActionAnimationResult(tickIndex: 32, topology);

                coordinator.Present(result);

                Assert.That(port.Requests, Has.Count.EqualTo(9));
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerPushWindup,
                    tickIndex: 32,
                    sequenceId: 101,
                    actionKind: PresentationAnimationActionKind.Push,
                    phaseKind: PresentationAnimationPhaseKind.Windup,
                    outcomeKind: PresentationAnimationOutcomeKind.Started);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    tickIndex: 32,
                    sequenceId: 102,
                    actionKind: PresentationAnimationActionKind.Push,
                    phaseKind: PresentationAnimationPhaseKind.Execute,
                    outcomeKind: PresentationAnimationOutcomeKind.Executed);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerPushRecovery,
                    tickIndex: 32,
                    sequenceId: 103,
                    actionKind: PresentationAnimationActionKind.Push,
                    phaseKind: PresentationAnimationPhaseKind.Recovery,
                    outcomeKind: PresentationAnimationOutcomeKind.Recovery);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerPushBlocked,
                    tickIndex: 32,
                    sequenceId: 104,
                    actionKind: PresentationAnimationActionKind.Push,
                    phaseKind: PresentationAnimationPhaseKind.Execute,
                    outcomeKind: PresentationAnimationOutcomeKind.Blocked);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerFlipWindup,
                    tickIndex: 32,
                    sequenceId: 201,
                    actionKind: PresentationAnimationActionKind.Flip,
                    phaseKind: PresentationAnimationPhaseKind.Windup,
                    outcomeKind: PresentationAnimationOutcomeKind.Started);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerFlipExecute,
                    tickIndex: 32,
                    sequenceId: 202,
                    actionKind: PresentationAnimationActionKind.Flip,
                    phaseKind: PresentationAnimationPhaseKind.Execute,
                    outcomeKind: PresentationAnimationOutcomeKind.Executed);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerFlipRecovery,
                    tickIndex: 32,
                    sequenceId: 203,
                    actionKind: PresentationAnimationActionKind.Flip,
                    phaseKind: PresentationAnimationPhaseKind.Recovery,
                    outcomeKind: PresentationAnimationOutcomeKind.Recovery);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerFlipImpactContact,
                    tickIndex: 32,
                    sequenceId: 204,
                    actionKind: PresentationAnimationActionKind.Flip,
                    phaseKind: PresentationAnimationPhaseKind.Execute,
                    outcomeKind: PresentationAnimationOutcomeKind.Impact);
                AssertPlayerActionAnimationRequest(
                    port.Requests,
                    PresentationAnimationCueKey.PlayerFlipFailed,
                    tickIndex: 32,
                    sequenceId: 1,
                    actionKind: PresentationAnimationActionKind.Flip,
                    phaseKind: PresentationAnimationPhaseKind.Failed,
                    outcomeKind: PresentationAnimationOutcomeKind.Failed,
                    expectedSemanticSource: PresentationSemanticSource.PlayerActionAttempt,
                    expectedActionPlanId: 0);

                var ownership = coordinator.PlayerActionAnimationOwnershipDiagnostics;
                Assert.That(ownership.Mode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(ownership.PlannedCueCount, Is.EqualTo(9));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(9));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(9));
                Assert.That(ownership.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationExecutorDiagnostics.CommandRequestedCount, Is.EqualTo(9));
                Assert.That(coordinator.PlayerActionAnimationExecutorDiagnostics.CommandAppliedCount, Is.EqualTo(9));
                Assert.That(coordinator.PlayerActionAnimationExecutorDiagnostics.OwnerPolicyIgnoredCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_ProductionTelemetry_CoversOwnerAndSemanticValues()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_ProductionTelemetry_CoversOwnerAndSemanticValues));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionDefaults.ProductionDefault,
                    port,
                    topology);
                var result = CreatePlayerActionAnimationResult(tickIndex: 132, topology);

                coordinator.Present(result);

                var telemetry = coordinator.PlayerActionAnimationProductionTelemetrySnapshot;
                Assert.That(telemetry.CurrentMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(telemetry.IsProductionDefaultOwner, Is.True);
                Assert.That(telemetry.ProductionDefaultMode, Is.EqualTo(PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor));
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(9));
                Assert.That(telemetry.PlaybackCommandRequestedCount, Is.EqualTo(9));
                Assert.That(telemetry.PlaybackCommandAppliedCount, Is.EqualTo(9));
                Assert.That(telemetry.PlannedCueCount, Is.EqualTo(9));
                Assert.That(telemetry.ExecutorOwnerAttemptCount, Is.EqualTo(9));
                Assert.That(telemetry.ExecutorOwnerExecutedCount, Is.EqualTo(9));
                Assert.That(telemetry.DuplicateOwnerAttemptCount, Is.Zero);
                Assert.That(telemetry.DuplicateSuppressedCount, Is.Zero);
                Assert.That(telemetry.LastTickIndex, Is.EqualTo(132));
                Assert.That(telemetry.LastCueKey, Is.EqualTo(PresentationAnimationCueKey.PlayerFlipFailed));
                Assert.That(telemetry.LastPlayerEntityId, Is.EqualTo(10));
                Assert.That(telemetry.LastActionKind, Is.EqualTo(PresentationAnimationActionKind.Flip));
                Assert.That(telemetry.LastPhaseKind, Is.EqualTo(PresentationAnimationPhaseKind.Failed));
                Assert.That(telemetry.LastOutcomeKind, Is.EqualTo(PresentationAnimationOutcomeKind.Failed));
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.None));
                Assert.That(telemetry.LastCleanupReason, Is.EqualTo(PlayerActionAnimationTelemetryCleanupReason.None));
                Assert.That(telemetry.SemanticDiagnostics.Count, Is.EqualTo(12));
                AssertPlayerActionAnimationSemanticTelemetry(
                    telemetry,
                    PresentationAnimationCueKey.PlayerPushWindup,
                    planned: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticTelemetry(
                    telemetry,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticTelemetry(
                    telemetry,
                    PresentationAnimationCueKey.PlayerFlipFailed,
                    planned: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticTelemetry(
                    telemetry,
                    PresentationAnimationCueKey.PlayerFlipImpactContact,
                    planned: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_SemanticTelemetry_CoversAllPushFlipSemanticsWithPlannerOwnedPlannedCount()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_SemanticTelemetry_CoversAllPushFlipSemanticsWithPlannerOwnedPlannedCount));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology);
                var result = CreateAllPlayerActionAnimationSemanticResult(tickIndex: 133, topology);

                coordinator.Present(result);

                var telemetry = coordinator.PlayerActionAnimationProductionTelemetrySnapshot;
                Assert.That(telemetry.SemanticDiagnostics.Count, Is.EqualTo(12));
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(12));
                Assert.That(telemetry.PlaybackCommandRequestedCount, Is.EqualTo(12));
                Assert.That(telemetry.PlaybackCommandAppliedCount, Is.EqualTo(12));

                foreach (var cueKey in PlayerActionAnimationSemanticCueKeys())
                {
                    AssertPlayerActionAnimationSemanticTelemetry(
                        telemetry,
                        cueKey,
                        planned: 1,
                        requested: 1,
                        applied: 1,
                        ignored: 0,
                        observed: 1);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_SemanticTelemetry_RequestedResultDoesNotCountAsApplied()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_SemanticTelemetry_RequestedResultDoesNotCountAsApplied));
            var port = new RecordingGameplayAnimationPlaybackPort(
                GameplayAnimationPlaybackResultKind.Requested);

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology);
                var result = CreateSinglePlayerActionAnimationResult(
                    tickIndex: 134,
                    topology,
                    PlayerActionKind.Push,
                    sequenceId: 134,
                    executedThisTick: true);

                coordinator.Present(result);

                var telemetry = coordinator.PlayerActionAnimationProductionTelemetrySnapshot;
                AssertPlayerActionAnimationSemanticTelemetry(
                    telemetry,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 1,
                    requested: 1,
                    applied: 0,
                    ignored: 0,
                    observed: 1);
                Assert.That(telemetry.PlaybackCommandRequestedCount, Is.EqualTo(1));
                Assert.That(telemetry.PlaybackCommandAppliedCount, Is.Zero);
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_SemanticTelemetry_IgnoredTerminalResultDoesNotCountAsAppliedAcrossPushFlip()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_SemanticTelemetry_IgnoredTerminalResultDoesNotCountAsAppliedAcrossPushFlip));
            var port = new RecordingGameplayAnimationPlaybackPort(
                GameplayAnimationPlaybackResultKind.IgnoredByPolicy);

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology);
                var result = CreateAllPlayerActionAnimationSemanticResult(tickIndex: 135, topology);

                coordinator.Present(result);

                var telemetry = coordinator.PlayerActionAnimationProductionTelemetrySnapshot;
                Assert.That(telemetry.ObservedCueCount, Is.EqualTo(12));
                Assert.That(telemetry.PlaybackCommandRequestedCount, Is.EqualTo(12));
                Assert.That(telemetry.PlaybackCommandAppliedCount, Is.Zero);
                Assert.That(telemetry.PlaybackCommandIgnoredByPolicyCount, Is.EqualTo(12));
                Assert.That(telemetry.LastFailureReason, Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.IgnoredByPolicy));

                foreach (var cueKey in PlayerActionAnimationSemanticCueKeys())
                {
                    AssertPlayerActionAnimationSemanticTelemetry(
                        telemetry,
                        cueKey,
                        planned: 1,
                        requested: 1,
                        applied: 0,
                        ignored: 1,
                        observed: 1);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_HostPath_PreservesSemanticRequests()
        {
            var executorRoot = new GameObject(nameof(PlayerActionAnimation_HostPath_PreservesSemanticRequests));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var executorCoordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    executorRoot,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology,
                    viewFactory: new DefaultGameplayEntityViewFactory(executorRoot.transform, 1f, playerEntityId: 10));
                var cases = new[]
                {
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Push,
                        PresentationAnimationCueKey.PlayerPushWindup,
                        PresentationAnimationPhaseKind.Windup,
                        PresentationAnimationOutcomeKind.Started,
                        sequenceId: 901,
                        startedThisTick: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Push,
                        PresentationAnimationCueKey.PlayerPushExecute,
                        PresentationAnimationPhaseKind.Execute,
                        PresentationAnimationOutcomeKind.Executed,
                        sequenceId: 902,
                        executedThisTick: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Push,
                        PresentationAnimationCueKey.PlayerPushRecovery,
                        PresentationAnimationPhaseKind.Recovery,
                        PresentationAnimationOutcomeKind.Recovery,
                        sequenceId: 903,
                        recoveryPhase: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Push,
                        PresentationAnimationCueKey.PlayerPushBlocked,
                        PresentationAnimationPhaseKind.Execute,
                        PresentationAnimationOutcomeKind.Blocked,
                        sequenceId: 904,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Flip,
                        PresentationAnimationCueKey.PlayerFlipWindup,
                        PresentationAnimationPhaseKind.Windup,
                        PresentationAnimationOutcomeKind.Started,
                        sequenceId: 905,
                        startedThisTick: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Flip,
                        PresentationAnimationCueKey.PlayerFlipExecute,
                        PresentationAnimationPhaseKind.Execute,
                        PresentationAnimationOutcomeKind.Executed,
                        sequenceId: 906,
                        executedThisTick: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Flip,
                        PresentationAnimationCueKey.PlayerFlipRecovery,
                        PresentationAnimationPhaseKind.Recovery,
                        PresentationAnimationOutcomeKind.Recovery,
                        sequenceId: 907,
                        recoveryPhase: true),
                    new PlayerActionAnimationSemanticCase(
                        PlayerActionKind.Flip,
                        PresentationAnimationCueKey.PlayerFlipImpactContact,
                        PresentationAnimationPhaseKind.Execute,
                        PresentationAnimationOutcomeKind.Impact,
                        sequenceId: 908,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Impact),
                };

                for (var i = 0; i < cases.Length; i++)
                {
                    var semanticCase = cases[i];
                    var result = CreateSinglePlayerActionAnimationResult(
                        tickIndex: 40 + i,
                        topology,
                        semanticCase.ActionKind,
                        semanticCase.SequenceId,
                        semanticCase.StartedThisTick,
                        semanticCase.ExecutedThisTick,
                        semanticCase.RecoveryPhase,
                        semanticCase.ResolutionKind);

                    executorCoordinator.Present(result);

                    var request = port.Requests.Last();
                    Assert.That(request.PlayerEntityId, Is.EqualTo(10));
                    Assert.That(request.AnimationPayload.ActionKind, Is.EqualTo(semanticCase.ActionKind));
                    Assert.That(request.AnimationPayload.SourceSequenceId, Is.EqualTo(semanticCase.SequenceId));
                    Assert.That(request.CueKey, Is.EqualTo(semanticCase.CueKey));
                    Assert.That(request.AnimationPayload.PhaseKind, Is.EqualTo(semanticCase.PhaseKind));
                    Assert.That(request.AnimationPayload.OutcomeKind, Is.EqualTo(semanticCase.OutcomeKind));
                    Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(10)));
                    Assert.That(request.Anchor, Is.EqualTo(PresentationAnchor.ForEntityVisualRoot(10)));
                    Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(semanticCase.CueKey));
                }

                Assert.That(executorCoordinator.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(executorRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_OrchestrationExecutorMode_ForcedDuplicateAttemptBlocksSecondOwner()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_OrchestrationExecutorMode_ForcedDuplicateAttemptBlocksSecondOwner));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology,
                    duplicateExecutors: true);
                var result = CreateSinglePlayerActionAnimationResult(
                    tickIndex: 33,
                    topology,
                    PlayerActionKind.Push,
                    sequenceId: 301,
                    startedThisTick: true);

                coordinator.Present(result);

                var ownership = coordinator.PlayerActionAnimationOwnershipDiagnostics;
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(ownership.PlannedCueCount, Is.EqualTo(1));
                Assert.That(ownership.ExecutorAttemptCount, Is.EqualTo(2));
                Assert.That(ownership.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(ownership.DuplicateAttemptCount, Is.EqualTo(1));
                Assert.That(ownership.LastExecutionOwner, Is.EqualTo(PlayerActionAnimationExecutionOwner.OrchestrationAnimationExecutor));
                Assert.That(coordinator.PlayerActionAnimationExecutorDiagnostics.DuplicateSuppressedCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationProductionTelemetrySnapshot.DuplicateOwnerAttemptCount, Is.EqualTo(1));
                Assert.That(coordinator.PlayerActionAnimationProductionTelemetrySnapshot.DuplicateSuppressedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_ControlledHostExecutor_DistinguishesMissingDiagnostics()
        {
            var malformedRoot = new GameObject(nameof(PlayerActionAnimation_ControlledHostExecutor_DistinguishesMissingDiagnostics) + "_Malformed");
            var adapterRoot = new GameObject(nameof(PlayerActionAnimation_ControlledHostExecutor_DistinguishesMissingDiagnostics) + "_Adapter");

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var malformedCoordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    malformedRoot,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    new RecordingGameplayAnimationPlaybackPort(),
                    topology,
                    overrideCues: new[]
                    {
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushWindup,
                            PresentationAnimationPhaseKind.Windup,
                            PresentationAnimationOutcomeKind.Started,
                            tickIndex: 34,
                            sequenceId: 401,
                            target: PresentationTarget.None()),
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushExecute,
                            PresentationAnimationPhaseKind.Execute,
                            PresentationAnimationOutcomeKind.Executed,
                            tickIndex: 34,
                            sequenceId: 402,
                            anchor: PresentationAnchor.None()),
                    });
                var adapterPort = new RecordingGameplayAnimationPlaybackPort(request =>
                    request.CueKey == PresentationAnimationCueKey.PlayerPushWindup
                        ? GameplayAnimationPlaybackResultKind.BindingMissing
                        : request.CueKey == PresentationAnimationCueKey.PlayerPushExecute
                            ? GameplayAnimationPlaybackResultKind.DriverMissing
                            : GameplayAnimationPlaybackResultKind.AnimatorMissing);
                var adapterCoordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    adapterRoot,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    adapterPort,
                    topology,
                    overrideCues: new[]
                    {
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushWindup,
                            PresentationAnimationPhaseKind.Windup,
                            PresentationAnimationOutcomeKind.Started,
                            tickIndex: 35,
                            sequenceId: 501),
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushExecute,
                            PresentationAnimationPhaseKind.Execute,
                            PresentationAnimationOutcomeKind.Executed,
                            tickIndex: 35,
                            sequenceId: 502),
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushRecovery,
                            PresentationAnimationPhaseKind.Recovery,
                            PresentationAnimationOutcomeKind.Recovery,
                            tickIndex: 35,
                            sequenceId: 503),
                    });
                var missingPortGuard = new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                missingPortGuard.ResetSession();
                var missingPortExecutor = new GameplayAnimationPresentationExecutor(
                    playbackPort: null,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    missingPortGuard);

                malformedCoordinator.Present(CreateTickResult(34, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));
                adapterCoordinator.Present(CreateTickResult(35, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));
                missingPortExecutor.Play(CreatePlayerActionAnimationPlanFromStaticPlanner(
                    36,
                    new[]
                    {
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerFlipWindup,
                            PresentationAnimationPhaseKind.Windup,
                            PresentationAnimationOutcomeKind.Started,
                            tickIndex: 36,
                            sequenceId: 601),
                    }));

                Assert.That(malformedCoordinator.PlayerActionAnimationExecutorDiagnostics.TargetMissingCount, Is.EqualTo(1));
                Assert.That(malformedCoordinator.PlayerActionAnimationExecutorDiagnostics.AnchorMissingCount, Is.EqualTo(1));
                AssertPlayerActionAnimationSemanticTelemetry(
                    malformedCoordinator.PlayerActionAnimationProductionTelemetrySnapshot,
                    PresentationAnimationCueKey.PlayerPushWindup,
                    planned: 1,
                    requested: 0,
                    applied: 0,
                    ignored: 1,
                    observed: 1);
                AssertPlayerActionAnimationSemanticTelemetry(
                    malformedCoordinator.PlayerActionAnimationProductionTelemetrySnapshot,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 1,
                    requested: 0,
                    applied: 0,
                    ignored: 1,
                    observed: 1);
                Assert.That(
                    malformedCoordinator.PlayerActionAnimationProductionTelemetrySnapshot.LastFailureReason,
                    Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.AnchorMissing));
                Assert.That(adapterCoordinator.PlayerActionAnimationExecutorDiagnostics.BindingMissingCount, Is.EqualTo(1));
                Assert.That(adapterCoordinator.PlayerActionAnimationExecutorDiagnostics.DriverMissingCount, Is.EqualTo(1));
                Assert.That(adapterCoordinator.PlayerActionAnimationExecutorDiagnostics.AnimatorMissingCount, Is.EqualTo(1));
                AssertPlayerActionAnimationSemanticTelemetry(
                    adapterCoordinator.PlayerActionAnimationProductionTelemetrySnapshot,
                    PresentationAnimationCueKey.PlayerPushWindup,
                    planned: 1,
                    requested: 1,
                    applied: 0,
                    ignored: 1,
                    observed: 1);
                AssertPlayerActionAnimationSemanticTelemetry(
                    adapterCoordinator.PlayerActionAnimationProductionTelemetrySnapshot,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 1,
                    requested: 1,
                    applied: 0,
                    ignored: 1,
                    observed: 1);
                AssertPlayerActionAnimationSemanticTelemetry(
                    adapterCoordinator.PlayerActionAnimationProductionTelemetrySnapshot,
                    PresentationAnimationCueKey.PlayerPushRecovery,
                    planned: 1,
                    requested: 1,
                    applied: 0,
                    ignored: 1,
                    observed: 1);
                Assert.That(
                    adapterCoordinator.PlayerActionAnimationProductionTelemetrySnapshot.LastFailureReason,
                    Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.AnimatorMissing));
                Assert.That(missingPortExecutor.Diagnostics.MissingPortCount, Is.EqualTo(1));
                Assert.That(missingPortExecutor.Diagnostics.LastFailureReason, Is.EqualTo(PlayerActionAnimationTelemetryFailureReason.PortMissing));
                AssertPlayerActionAnimationSemanticDiagnostics(
                    missingPortExecutor.Diagnostics,
                    PresentationAnimationCueKey.PlayerFlipWindup,
                    planned: 1,
                    observed: 1,
                    requested: 0,
                    applied: 0,
                    ignored: 1);
                Assert.That(malformedCoordinator.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(adapterCoordinator.PlayerActionAnimationOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(missingPortGuard.Diagnostics.DuplicateAttemptCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(malformedRoot);
                UnityEngine.Object.DestroyImmediate(adapterRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_OrchestrationRoute_IsNonBlockingCleansLifecycleAndDoesNotMutateTickResult()
        {
            var rootObject = new GameObject(nameof(PlayerActionAnimation_OrchestrationRoute_IsNonBlockingCleansLifecycleAndDoesNotMutateTickResult));
            var port = new RecordingGameplayAnimationPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedPlayerActionAnimationCoordinator(
                    rootObject,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    port,
                    topology);
                var result = CreateSinglePlayerActionAnimationResult(
                    tickIndex: 37,
                    topology,
                    PlayerActionKind.Flip,
                    sequenceId: 701,
                    startedThisTick: true);
                var finalEntities = result.FinalEntities.ToArray();
                var eventLog = result.EventLog.ToArray();
                var objectiveResult = result.ObjectiveResult;
                var determinismHash = result.DeterminismHash;

                coordinator.Present(result);

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.PlayerActionAnimationExecutionPipelineBlockingSnapshot);
                Assert.That(result.DeterminismHash, Is.EqualTo(determinismHash));
                Assert.That(result.FinalEntities, Is.EqualTo(finalEntities));
                Assert.That(result.EventLog, Is.EqualTo(eventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(objectiveResult));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));

                coordinator.PresentInitial(Array.Empty<EntityState>(), topology);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationExecutorDiagnostics.CommandRequestedCount, Is.Zero);
                Assert.That(
                    coordinator.PlayerActionAnimationProductionTelemetrySnapshot.LastCleanupReason,
                    Is.EqualTo(PlayerActionAnimationTelemetryCleanupReason.ResetSession));

                coordinator.Present(result);
                coordinator.HardCleanupPresentationExtensions();
                Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(coordinator.PlayerActionAnimationOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(
                    coordinator.PlayerActionAnimationProductionTelemetrySnapshot.LastCleanupReason,
                    Is.EqualTo(PlayerActionAnimationTelemetryCleanupReason.HardCleanupPresentationExtensions));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_ExecuteCueLowering_MapsToLegacyRecoveryDriverContract()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                nameof(PlayerActionAnimation_ExecuteCueLowering_MapsToLegacyRecoveryDriverContract));

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                Assert.That(view, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                var sync = new GameplayAnimationSyncCoordinator();
                var port = new GameplayAnimationSyncPlaybackPort(
                    sync,
                    CreateStateStoreWithView(view));
                var guard = new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                var executor = new GameplayAnimationPresentationExecutor(
                    port,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    guard);
                var plan = CreatePlayerActionAnimationPlanFromStaticPlanner(
                    38,
                    new[]
                    {
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushExecute,
                            PresentationAnimationPhaseKind.Execute,
                            PresentationAnimationOutcomeKind.Executed,
                            tickIndex: 38,
                            sequenceId: 801),
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerFlipExecute,
                            PresentationAnimationPhaseKind.Execute,
                            PresentationAnimationOutcomeKind.Executed,
                            tickIndex: 38,
                            sequenceId: 802),
                    });

                sync.CacheDrivers(10, view);
                executor.Play(plan);

                Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(2));
                Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(2));
                Assert.That(executor.Diagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.EqualTo(2));
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 1,
                    observed: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerFlipExecute,
                    planned: 1,
                    observed: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerPushRecovery,
                    planned: 0,
                    observed: 0,
                    requested: 0,
                    applied: 0,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerFlipRecovery,
                    planned: 0,
                    observed: 0,
                    requested: 0,
                    applied: 0,
                    ignored: 0);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.FlipRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Flip_Recovery"));
                Assert.That(driver.ActionExecuteSignalCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PlayerActionAnimation_RecoveryCueTelemetry_DoesNotIncrementExecuteOrLoweringCounters()
        {
            var rootObject = PlayerViewPrefabTestUtility.CreatePlayerViewPrefabObject(
                nameof(PlayerActionAnimation_RecoveryCueTelemetry_DoesNotIncrementExecuteOrLoweringCounters));

            try
            {
                var view = rootObject.GetComponent<GameplayEntityView>();
                var driver = rootObject.GetComponent<PlayerAnimatorDriver>();
                var sync = new GameplayAnimationSyncCoordinator();
                var port = new GameplayAnimationSyncPlaybackPort(
                    sync,
                    CreateStateStoreWithView(view));
                var guard = new PlayerActionAnimationExecutionGuard(
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor);
                var executor = new GameplayAnimationPresentationExecutor(
                    port,
                    PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor,
                    guard);
                var plan = CreatePlayerActionAnimationPlanFromStaticPlanner(
                    39,
                    new[]
                    {
                        CreatePlayerActionAnimationCue(
                            PresentationAnimationCueKey.PlayerPushRecovery,
                            PresentationAnimationPhaseKind.Recovery,
                            PresentationAnimationOutcomeKind.Recovery,
                            tickIndex: 39,
                            sequenceId: 803),
                    });

                sync.CacheDrivers(10, view);
                executor.Play(plan);

                Assert.That(executor.Diagnostics.CommandRequestedCount, Is.EqualTo(1));
                Assert.That(executor.Diagnostics.CommandAppliedCount, Is.EqualTo(1));
                Assert.That(executor.Diagnostics.ExecuteCueMappedToRecoveryCommandCount, Is.Zero);
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerPushRecovery,
                    planned: 1,
                    observed: 1,
                    requested: 1,
                    applied: 1,
                    ignored: 0);
                AssertPlayerActionAnimationSemanticDiagnostics(
                    executor.Diagnostics,
                    PresentationAnimationCueKey.PlayerPushExecute,
                    planned: 0,
                    observed: 0,
                    requested: 0,
                    applied: 0,
                    ignored: 0);
                Assert.That(driver.CurrentPresentationPhase, Is.EqualTo(PlayerPresentationPhase.PushRecovery));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Push_Recovery"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_PoseEquivalenceAndVisualRootReset()
        {
            var referenceRoot = new GameObject(nameof(BoxMotion_Readiness_PoseEquivalenceAndVisualRootReset) + "_Reference");
            var executorRoot = new GameObject(nameof(BoxMotion_Readiness_PoseEquivalenceAndVisualRootReset) + "_Executor");

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var referencePresenter = CreateInitializedBoxMotionPresenter(
                    referenceRoot,
                    topology,
                    boardBounds,
                    timingProfile,
                    out var referenceRegistry);
                var executorPresenter = CreateInitializedBoxMotionPresenter(
                    executorRoot,
                    topology,
                    boardBounds,
                    timingProfile,
                    out var executorRegistry);
                var slideSource = new SurfaceCell(FaceId.Floor, 0, 0);
                var slideDestination = new SurfaceCell(FaceId.Floor, 1, 0);
                var flipDestination = new SurfaceCell(FaceId.Floor, 2, 0);

                referencePresenter.PresentInitial(new[] { CreateBox(40, slideSource) }, topology);
                executorPresenter.PresentInitial(new[] { CreateBox(40, slideSource) }, topology);
                Assert.That(referenceRegistry.TryGetView(40, out var referenceView), Is.True);
                Assert.That(executorRegistry.TryGetView(40, out var executorView), Is.True);
                AssertPositionApproximately(executorView.transform.localPosition, referenceView.transform.localPosition);
                AssertPositionApproximately(executorView.ModelRoot.localPosition, Vector3.zero);
                Assert.That(Quaternion.Angle(executorView.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(0.001f));

                referencePresenter.Present(CreateBoxMotionResult(
                    tickIndex: 31,
                    topology,
                    boxEntityId: 40,
                    slideSource,
                    slideDestination,
                    TickEntityMotionKind.BoxSlide));
                executorPresenter.Present(CreateBoxMotionResult(
                    tickIndex: 31,
                    topology,
                    boxEntityId: 40,
                    slideSource,
                    slideDestination,
                    TickEntityMotionKind.BoxSlide));
                referencePresenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);
                executorPresenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                AssertPositionApproximately(executorView.transform.localPosition, referenceView.transform.localPosition);
                AssertScaleApproximately(executorView.ModelRoot.localScale, referenceView.ModelRoot.localScale);

                referencePresenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds);
                executorPresenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds);
                AssertPositionApproximately(executorView.transform.localPosition, referenceView.transform.localPosition);
                AssertScaleApproximately(executorView.ModelRoot.localScale, Vector3.one);
                AssertPositionApproximately(executorView.ModelRoot.localPosition, Vector3.zero);
                Assert.That(Quaternion.Angle(executorView.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(0.001f));

                referencePresenter.Present(CreateBoxMotionResult(
                    tickIndex: 32,
                    topology,
                    boxEntityId: 40,
                    slideDestination,
                    flipDestination,
                    TickEntityMotionKind.Flip));
                executorPresenter.Present(CreateBoxMotionResult(
                    tickIndex: 32,
                    topology,
                    boxEntityId: 40,
                    slideDestination,
                    flipDestination,
                    TickEntityMotionKind.Flip));
                referencePresenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);
                executorPresenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);
                AssertPositionApproximately(executorView.transform.localPosition, referenceView.transform.localPosition);
                Assert.That(Quaternion.Angle(executorView.transform.localRotation, referenceView.transform.localRotation), Is.LessThan(0.001f));
                AssertScaleApproximately(executorView.ModelRoot.localScale, referenceView.ModelRoot.localScale);

                referencePresenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds);
                executorPresenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds);
                AssertPositionApproximately(executorView.transform.localPosition, referenceView.transform.localPosition);
                Assert.That(Quaternion.Angle(executorView.transform.localRotation, referenceView.transform.localRotation), Is.LessThan(0.001f));
                AssertScaleApproximately(executorView.ModelRoot.localScale, Vector3.one);
                AssertPositionApproximately(executorView.ModelRoot.localPosition, Vector3.zero);
                Assert.That(Quaternion.Angle(executorView.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(executorPresenter.HasBlockingPresentation, Is.False);
                Assert.That(executorPresenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(referenceRoot);
                UnityEngine.Object.DestroyImmediate(executorRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_Readiness_LifecycleCleanupClearsState()
        {
            var rootObject = new GameObject(nameof(BoxMotion_Readiness_LifecycleCleanupClearsState));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinator(
                    rootObject,
                    port,
                    topology,
                    viewFactory: new BoxFlipDriverViewFactory(rootObject.transform));
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                coordinator.PresentInitial(new[] { CreateBox(40, sourceCell) }, topology);
                Assert.That(rootObject.GetComponent<GameplayEntityViewRegistry>().TryGetView(40, out var view), Is.True);
                var driver = view.GetComponent<BoxFlipInteractionDriver>();
                Assert.That(driver, Is.Not.Null);
                driver.ApplyInteraction(new Vector3(0.25f, 0.1f, 0f), Quaternion.Euler(0f, 0f, 15f), 1f);
                Assert.That(Vector3.Distance(view.ModelRoot.localPosition, Vector3.zero), Is.GreaterThan(0.001f));

                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 33,
                    topology,
                    boxEntityId: 40,
                    sourceCell,
                    destinationCell,
                    TickEntityMotionKind.BoxSlide));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(port.Requests, Has.Count.EqualTo(1));

                coordinator.PresentInitial(new[] { CreateBox(40, sourceCell) }, topology);
                Assert.That(port.ResetSessionCallCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(port.CleanupRequestedCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(port.CleanupSucceededCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(port.Requests, Is.Empty);
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(GetPresentationTrackState(coordinator).LocalMotionTracks.ContainsKey(40), Is.False);
                Assert.That(GetPresentationTrackState(coordinator).CompletedPresentationMotionKeys.Any(key => key.EntityId == 40), Is.False);
                Assert.That(GetPresentationTrackState(coordinator).FlipInteractionTracks, Is.Empty);
                AssertPositionApproximately(view.ModelRoot.localPosition, Vector3.zero);
                Assert.That(Quaternion.Angle(view.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(0.001f));

                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 34,
                    topology,
                    boxEntityId: 40,
                    sourceCell,
                    destinationCell,
                    TickEntityMotionKind.BoxSlide));
                driver.ApplyInteraction(new Vector3(0.25f, 0.1f, 0f), Quaternion.Euler(0f, 0f, 15f), 1f);
                coordinator.HardCleanupPresentationExtensions();

                Assert.That(port.HardCleanupCallCount, Is.EqualTo(1));
                Assert.That(port.CleanupRequestedCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(port.CleanupSucceededCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(port.TryPlayCallCount, Is.Zero);
                Assert.That(port.Requests, Is.Empty);
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.ExecutorAttemptCount, Is.Zero);
                Assert.That(coordinator.BoxMotionExecutorDiagnostics.PlaybackRequestedCount, Is.Zero);
                Assert.That(GetPresentationTrackState(coordinator).LocalMotionTracks.ContainsKey(40), Is.False);
                Assert.That(GetPresentationTrackState(coordinator).CompletedPresentationMotionKeys.Any(key => key.EntityId == 40), Is.False);
                Assert.That(GetPresentationTrackState(coordinator).FlipInteractionTracks, Is.Empty);
                AssertPositionApproximately(view.ModelRoot.localPosition, Vector3.zero);
                Assert.That(Quaternion.Angle(view.ModelRoot.localRotation, Quaternion.identity), Is.LessThan(0.001f));

                coordinator.Present(CreateBoxMotionResult(
                    tickIndex: 35,
                    topology,
                    boxEntityId: 40,
                    sourceCell,
                    destinationCell,
                    TickEntityMotionKind.BoxSlide));
                Assert.That(port.TryPlayCallCount, Is.EqualTo(1));
                Assert.That(port.Requests.Single().TickIndex, Is.EqualTo(35));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void BoxMotion_DefaultOrchestration_DoesNotSuppressUnrelatedMotionTracks()
        {
            var rootObject = new GameObject(nameof(BoxMotion_DefaultOrchestration_DoesNotSuppressUnrelatedMotionTracks));
            var port = new RecordingGameplayMotionPlaybackPort();

            try
            {
                var topology = new CubeTopologyState(FaceId.Floor);
                var coordinator = CreateInitializedBoxMotionCoordinatorUsingProductionDefault(
                    rootObject,
                    port,
                    topology);
                var boxSource = new SurfaceCell(FaceId.Floor, 0, 0);
                var boxDestination = new SurfaceCell(FaceId.Floor, 1, 0);
                var enemySource = new SurfaceCell(FaceId.Floor, 0, 1);
                var enemyDestination = new SurfaceCell(FaceId.Floor, 1, 1);
                var presentationData = new TickPresentationData(new[]
                {
                    new TickEntityMotion(
                        40,
                        TickEntityMotionKind.BoxSlide,
                        boxSource,
                        boxDestination,
                        topology,
                        topology,
                        Direction.Right,
                        Direction.Right),
                    new TickEntityMotion(
                        20,
                        TickEntityMotionKind.Move,
                        enemySource,
                        enemyDestination,
                        topology,
                        topology,
                        Direction.Right,
                        Direction.Right),
                });

                coordinator.Present(CreateTickResult(
                    36,
                    new[]
                    {
                        CreateBox(40, boxDestination),
                        CreateEnemyUnit(20, enemyDestination),
                    },
                    topology,
                    presentationData));

                var trackState = GetPresentationTrackState(coordinator);
                Assert.That(port.Requests.Single().CueKey, Is.EqualTo(PresentationMotionCueKey.BoxSlide));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(coordinator.BoxMotionOwnershipDiagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(trackState.LocalMotionTracks.ContainsKey(40), Is.False);
                Assert.That(trackState.LocalMotionTracks.ContainsKey(20), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_CurrentRoute_BlockingMirrorMatchesControllerState()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_CurrentRoute_BlockingMirrorMatchesControllerState));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var coordinator = CreateInitializedDefaultTopologyCoordinator(
                    rootObject,
                    initialTopology,
                    timingProfile);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 11,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);

                var diagnostics = coordinator.TopologyPresentationOwnershipDiagnostics;
                Assert.That(diagnostics.ExecutedByExecutorCount, Is.EqualTo(1));
                Assert.That(diagnostics.DuplicateAttemptCount, Is.Zero);
                Assert.That(coordinator.HasBlockingPresentation, Is.True);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.True);
                AssertTopologyBlockingSnapshotParity(
                    coordinator.TopologyExecutionPipelineBlockingSnapshot,
                    expectedPlanned: true,
                    expectedActive: true,
                    expectedTickIndex: 11);

                coordinator.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                AssertTopologyBlockingSnapshotParity(
                    coordinator.TopologyExecutionPipelineBlockingSnapshot,
                    expectedPlanned: true,
                    expectedActive: false,
                    expectedTickIndex: 11);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyExecution_BlockingMirrorResetAndHardCleanup_ClearSnapshot()
        {
            var rootObject = new GameObject(nameof(TopologyExecution_BlockingMirrorResetAndHardCleanup_ClearSnapshot));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var coordinator = CreateInitializedDefaultTopologyCoordinator(
                    rootObject,
                    initialTopology,
                    timingProfile);
                coordinator.EnablePresentationPipelineDiagnostics();
                var result = CreateTopologyTransitionResult(
                    tickIndex: 12,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);

                Assert.That(coordinator.PresentationPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.True);
                Assert.That(coordinator.TopologyExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.True);

                coordinator.PresentInitial(Array.Empty<EntityState>(), destinationTopology);

                AssertBlockingSnapshotCleared(coordinator.PresentationPipelineBlockingSnapshot);
                AssertBlockingSnapshotCleared(coordinator.TopologyExecutionPipelineBlockingSnapshot);

                var secondResult = CreateTopologyTransitionResult(
                    tickIndex: 13,
                    destinationTopology,
                    initialTopology,
                    CubeRotationKind.Backward);
                coordinator.Present(secondResult);
                Assert.That(coordinator.PresentationPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.True);
                Assert.That(coordinator.TopologyExecutionPipelineBlockingSnapshot.HasActiveBlockingPresentation, Is.True);

                coordinator.HardCleanupPresentationExtensions();

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                Assert.That(coordinator.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.PresentationPipelineBlockingSnapshot);
                AssertBlockingSnapshotCleared(coordinator.TopologyExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void Topology_CurrentPlayback_ActiveTransition_HardCleanup_ReleasesInputLock()
        {
            var rootObject = new GameObject(nameof(Topology_CurrentPlayback_ActiveTransition_HardCleanup_ReleasesInputLock));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var coordinator = CreateInitializedDefaultTopologyCoordinator(
                    rootObject,
                    initialTopology,
                    timingProfile);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 14,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);
                Assert.That(coordinator.HasBlockingPresentation, Is.True);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.True);
                Assert.That(coordinator.CurrentTopologyTransitionVisualState.IsActive, Is.True);

                coordinator.HardCleanupPresentationExtensions();

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                Assert.That(coordinator.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.TopologyExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void Topology_Production_ActiveTransition_HardCleanup_ReleasesInputLockAndClearsBarrier()
        {
            var rootObject = new GameObject(nameof(Topology_Production_ActiveTransition_HardCleanup_ReleasesInputLockAndClearsBarrier));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var coordinator = CreateInitializedDefaultTopologyCoordinator(
                    rootObject,
                    initialTopology,
                    timingProfile);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 15,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                coordinator.Present(result);
                Assert.That(coordinator.HasBlockingPresentation, Is.True);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.True);
                AssertTopologyBlockingSnapshotParity(
                    coordinator.TopologyExecutionPipelineBlockingSnapshot,
                    expectedPlanned: true,
                    expectedActive: true,
                    expectedTickIndex: 15);

                coordinator.HardCleanupPresentationExtensions();

                Assert.That(coordinator.HasBlockingPresentation, Is.False);
                Assert.That(coordinator.IsTopologyTransitionActive, Is.False);
                Assert.That(coordinator.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                AssertBlockingSnapshotCleared(coordinator.TopologyExecutionPipelineBlockingSnapshot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_CurrentRoute_PreservesVisualStateAndInputLock()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_CurrentRoute_PreservesVisualStateAndInputLock));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var presenter = CreateInitializedTopologyPresenter(
                    rootObject,
                    initialTopology,
                    timingProfile);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 9,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);

                presenter.Present(result);

                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.IsTopologyTransitionActive, Is.True);

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.True);
                Assert.That(Quaternion.Angle(Quaternion.identity, presenter.PresentedBoardRotation), Is.GreaterThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(presenter.CurrentTopologyTransitionVisualState.IsActive, Is.False);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.CurrentTopologyTransitionVisualState.SourceTopology, Is.EqualTo(destinationTopology));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void TopologyPresentation_CurrentRoute_PreservesAuthoritativeTickResultOutputs()
        {
            var rootObject = new GameObject(nameof(TopologyPresentation_CurrentRoute_PreservesAuthoritativeTickResultOutputs));

            try
            {
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var result = CreateTopologyTransitionResult(
                    tickIndex: 10,
                    initialTopology,
                    destinationTopology,
                    CubeRotationKind.Forward);
                var initialHash = result.DeterminismHash;
                var initialEntities = result.FinalEntities.ToArray();
                var initialEventLog = result.EventLog.ToArray();
                var initialObjective = result.ObjectiveResult;
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 0.2f);
                var presenter = CreateInitializedTopologyPresenter(
                    rootObject,
                    initialTopology,
                    timingProfile);

                presenter.Present(result);
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(result.DeterminismHash, Is.EqualTo(initialHash));
                Assert.That(result.FinalEntities, Is.EqualTo(initialEntities));
                Assert.That(result.EventLog, Is.EqualTo(initialEventLog));
                Assert.That(result.ObjectiveResult, Is.SameAs(initialObjective));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_PresentInitial_CallsInitialExtensionOnceAfterViewsExist()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_CallsInitialExtensionOnceAfterViewsExist));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var extension = new RecordingInitialPresentationExtension();
                var player = CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 1));
                var signal = new EntitySpawnPresentationSignal(
                    player.entityId,
                    EntityPresentationKind.Player,
                    EntitySpawnPresentationReason.InitialStageStart,
                    player.position,
                    topology,
                    player.facing,
                    sourceTileFeature: null);
                var initialPresentationData = new InitialPresentationData(new[] { signal });

                presenter.AttachPresentationExtension(extension);
                presenter.PresentInitial(new[] { player }, topology, initialPresentationData);
                presenter.Present(CreateTickResult(1, new[] { player }, topology, TickPresentationData.Empty));

                Assert.That(extension.InitialPresentCallCount, Is.EqualTo(1));
                Assert.That(extension.TickPresentCallCount, Is.EqualTo(1));
                Assert.That(extension.CapturedInitialPresentationData, Is.SameAs(initialPresentationData));
                Assert.That(extension.HadPlayerViewDuringInitial, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_PresentInitial_StaticWallViewRemainsVisible()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentInitial_StaticWallViewRemainsVisible));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out var registry, out var topology);
                var wall = CreateWall(30, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.PresentInitial(new[] { wall }, topology);

                Assert.That(registry.TryGetView(wall.entityId, out var wallView), Is.True);
                Assert.That(wallView.gameObject.activeSelf, Is.True);
                Assert.That(wallView.gameObject.activeInHierarchy, Is.True);
                Assert.That(wallView.GetComponentInChildren<Renderer>(includeInactive: false), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_PresentationPause_FreezesExtensionProgressUntilResume()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_PresentationPause_FreezesExtensionProgressUntilResume));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var extension = new RecordingPausablePresentationExtension();
                presenter.AttachPresentationExtension(extension);
                presenter.PresentInitial(Array.Empty<EntityState>(), topology);

                presenter.SetPresentationPaused(true);
                presenter.UpdatePresentation(0.5f);

                Assert.That(presenter.IsPresentationPaused, Is.True);
                Assert.That(extension.IsPaused, Is.True);
                Assert.That(extension.PauseCallCount, Is.EqualTo(1));
                Assert.That(extension.UpdateCallCount, Is.Zero);
                Assert.That(extension.AdvancedSeconds, Is.EqualTo(0f).Within(0.0001f));

                presenter.SetPresentationPaused(false);
                presenter.UpdatePresentation(0.5f);

                Assert.That(presenter.IsPresentationPaused, Is.False);
                Assert.That(extension.IsPaused, Is.False);
                Assert.That(extension.ResumeCallCount, Is.EqualTo(1));
                Assert.That(extension.UpdateCallCount, Is.EqualTo(1));
                Assert.That(extension.AdvancedSeconds, Is.EqualTo(0.5f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void AstretonAirbornePresentation_GameplayPause_FreezesJumpTrackProgress()
        {
            var rootObject = new GameObject(nameof(AstretonAirbornePresentation_GameplayPause_FreezesJumpTrackProgress));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    startedAirborneThisTick: true,
                    landingTick: 61,
                    remainingAirborneTicks: 60));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.TryGetValue(20, out var jumpTrack), Is.True);
                var pausedElapsed = jumpTrack.ElapsedSeconds;
                var pausedProgress = jumpTrack.Progress01;
                var pausedPosition = view.transform.localPosition;

                presenter.SetPresentationPaused(true);
                presenter.UpdatePresentation(10f);

                Assert.That(jumpTrack.HasClip, Is.True);
                Assert.That(jumpTrack.ElapsedSeconds, Is.EqualTo(pausedElapsed).Within(0.0001f));
                Assert.That(jumpTrack.Progress01, Is.EqualTo(pausedProgress).Within(0.0001f));
                AssertPositionApproximately(view.transform.localPosition, pausedPosition);

                presenter.SetPresentationPaused(false);
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(jumpTrack.ElapsedSeconds, Is.GreaterThan(pausedElapsed));
                Assert.That(jumpTrack.Progress01, Is.GreaterThan(pausedProgress));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void AstretonAirbornePresentation_GameplayPause_DoesNotCompleteJumpTrack()
        {
            var rootObject = new GameObject(nameof(AstretonAirbornePresentation_GameplayPause_DoesNotCompleteJumpTrack));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    startedAirborneThisTick: true,
                    landingTick: 2,
                    remainingAirborneTicks: 1));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.TryGetValue(20, out var jumpTrack), Is.True);
                presenter.SetPresentationPaused(true);
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 10f);

                Assert.That(registry.TryGetView(20, out _), Is.True);
                Assert.That(jumpTrack.HasClip, Is.True);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);
                Assert.That(trackState.CompletedAirborneJumpTrackKeys, Is.Empty);
                Assert.That(GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.ContainsKey(20), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAirborneMotion_CompletedTrack_SustainedAirborneState_DoesNotRestart()
        {
            var rootObject = new GameObject(nameof(EnemyAirborneMotion_CompletedTrack_SustainedAirborneState_DoesNotRestart));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    startedAirborneThisTick: true,
                    landingTick: 2,
                    remainingAirborneTicks: 1));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 2f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var completedPosition = view.transform.localPosition;
                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                Assert.That(trackState.CompletedAirborneJumpTrackKeys.Count, Is.EqualTo(1));
                Assert.That(GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.ContainsKey(20), Is.True);

                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    startedAirborneThisTick: false,
                    landingTick: 2,
                    remainingAirborneTicks: 1));

                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                AssertPositionApproximately(view.transform.localPosition, completedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAirborneMotion_NewAirborneSequence_StartsNewMotion()
        {
            var rootObject = new GameObject(nameof(EnemyAirborneMotion_NewAirborneSequence_StartsNewMotion));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    sequence: 1,
                    startedAirborneThisTick: true,
                    landingTick: 2,
                    remainingAirborneTicks: 1));
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 2f);

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);

                presenter.Present(CreateEnemyAirborneTick(
                    tickIndex: 3,
                    finalTopology: topology,
                    sourceCell: sourceCell,
                    landingCell: landingCell,
                    sequence: 2,
                    startedAirborneThisTick: true,
                    landingTick: 4,
                    remainingAirborneTicks: 1));

                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);
                Assert.That(trackState.ActiveAirborneJumpTrackKeys.ContainsKey(20), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyAnimatorDriver_GameplayPause_DoesNotEnsureJumpAirborneState()
        {
            var rootObject = new GameObject(nameof(EnemyAnimatorDriver_GameplayPause_DoesNotEnsureJumpAirborneState));

            try
            {
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                driver.SetPresentationPaused(true);

                driver.Apply(CreateEnemyAirbornePresentationState(startedAirborneThisTick: true));

                Assert.That(driver.IsPresentationPaused, Is.True);
                Assert.That(driver.JumpAirborneSignalCount, Is.Zero);
                Assert.That(driver.EnsureJumpAirborneBaseAnimation(), Is.False);
                Assert.That(driver.LastCrossFadedStateName, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayPresentationPauseRegistry_LateRegisteredEnemyMotion_ReceivesCurrentPauseState()
        {
            var rootObject = new GameObject(nameof(GameplayPresentationPauseRegistry_LateRegisteredEnemyMotion_ReceivesCurrentPauseState));

            try
            {
                var registry = new GameplayPresentationPauseRegistry();
                registry.SetPresentationPaused(true);
                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var childObject = new GameObject("Animator");
                childObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var animator = childObject.AddComponent<Animator>();
                animator.speed = 2f;

                registry.RegisterRoot(rootObject);

                Assert.That(driver.IsPresentationPaused, Is.True);
                Assert.That(animator.speed, Is.EqualTo(0f).Within(0.0001f));

                registry.SetPresentationPaused(false);
                Assert.That(driver.IsPresentationPaused, Is.False);
                Assert.That(animator.speed, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void DirectParticleSystem_GameplayPause_PausesAndRestoresPreviousPlayingState()
        {
            var rootObject = new GameObject(nameof(DirectParticleSystem_GameplayPause_PausesAndRestoresPreviousPlayingState));

            try
            {
                var particles = rootObject.AddComponent<ParticleSystem>();
                particles.Play(withChildren: true);
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);

                registry.SetPresentationPaused(true);
                Assert.That(particles.isPaused, Is.True);

                registry.SetPresentationPaused(false);
                Assert.That(particles.isPlaying, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisual_GameplayPause_DoesNotAdvanceOrEmit()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisual_GameplayPause_DoesNotAdvanceOrEmit));

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();
                var particles = rootObject.AddComponent<ParticleSystem>();
                SetPrivateField(target, "activatedParticles", particles);
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);
                registry.SetPresentationPaused(true);

                target.PlayGravityFieldActivated();

                Assert.That(particles.isPaused, Is.True);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyFloatingPresentation_GameplayPause_DoesNotAdvanceOffset()
        {
            var rootObject = new GameObject(nameof(EnemyFloatingPresentation_GameplayPause_DoesNotAdvanceOffset));
            var targetObject = new GameObject("Target");

            try
            {
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var driver = rootObject.AddComponent<EnemyFloatingPresentationDriver>();
                SetPrivateField(driver, "target", targetObject.transform);
                driver.CaptureBaseLocalPosition();
                driver.SetPresentationPaused(true);

                driver.Advance(1f);

                Assert.That(driver.CurrentOffset, Is.EqualTo(Vector3.zero));
                Assert.That(targetObject.transform.localPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisual_GameplayPause_DoesNotAdvancePulse()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisual_GameplayPause_DoesNotAdvancePulse));

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.ApplyEnemyVisualSemanticState(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.SetPresentationPaused(true);

                controller.AdvanceInactiveNoiseReveal(1f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyPupilVisual_GameplayPause_DoesNotAdvanceLookMotion()
        {
            var rootObject = new GameObject(nameof(EnemyPupilVisual_GameplayPause_DoesNotAdvanceLookMotion));

            try
            {
                var controller = rootObject.AddComponent<EnemyPupilVisualController>();
                InvokePrivate(controller, "BeginWindup", 1f);
                controller.SetPresentationPaused(true);

                controller.Advance(0.5f);

                Assert.That(GetPrivateField<float>(controller, "_phaseElapsedSeconds"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void AnimatorDrivenGameplayPresentation_GameplayPause_FreezesAndRestoresSpeed()
        {
            var rootObject = new GameObject(nameof(AnimatorDrivenGameplayPresentation_GameplayPause_FreezesAndRestoresSpeed));

            try
            {
                var animator = rootObject.AddComponent<Animator>();
                animator.speed = 0.75f;
                var registry = new GameplayPresentationPauseRegistry();
                registry.RegisterRoot(rootObject);

                registry.SetPresentationPaused(true);
                Assert.That(animator.speed, Is.EqualTo(0f));

                registry.SetPresentationPaused(false);
                Assert.That(animator.speed, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_NoTileEvents_StaysEmpty()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_NoTileEvents_StaysEmpty));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(1, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Not.Null);
                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_ButtonActivated_ExposesRequest()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_ButtonActivated_ExposesRequest));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));

                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                var request = presenter.CurrentTilePresentationRequests[0];
                Assert.That(request.RequestKind, Is.EqualTo(TilePresentationRequestKind.ButtonActivated));
                Assert.That(request.TileId, Is.EqualTo(100));
                Assert.That(request.Cell, Is.EqualTo(cell));
                Assert.That(request.TileFeatureKind, Is.EqualTo(TileFeatureKind.Button));
                Assert.That(request.SourceEntityId, Is.EqualTo(101));
                Assert.That(request.OwnerEntityId, Is.EqualTo(102));
                Assert.That(request.TeamId, Is.EqualTo(103));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_PreservesOrderAndDuplicates()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_PreservesOrderAndDuplicates));
            var firstCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 1, 0);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var duplicate = CreateButtonActivatedTileEvent(30, firstCell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(
                        duplicate,
                        CreateButtonActivatedTileEvent(10, secondCell),
                        duplicate)));

                Assert.That(
                    presenter.CurrentTilePresentationRequests.Select(request => request.TileId).ToArray(),
                    Is.EqualTo(new[] { 30, 10, 30 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_ReplacesAndClearsPerTick()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_ReplacesAndClearsPerTick));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(
                        CreateButtonActivatedTileEvent(100, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateButtonActivatedTileEvent(200, new SurfaceCell(FaceId.Floor, 1, 0)))));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(2));

                presenter.Present(CreateTickResult(
                    2,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(300, new SurfaceCell(FaceId.Floor, 2, 0)))));

                Assert.That(
                    presenter.CurrentTilePresentationRequests.Select(request => request.TileId).ToArray(),
                    Is.EqualTo(new[] { 300 }));

                presenter.Present(CreateTickResult(3, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_ReplacesAndClearsPerTick()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_ReplacesAndClearsPerTick));
            var firstCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var secondCell = new SurfaceCell(FaceId.Floor, 1, 0);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, firstCell),
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Expired, 31, secondCell))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(2));
                Assert.That(
                    presenter.CurrentGravityFieldPresentationRequests.Select(request => request.EmitterEntityId).ToArray(),
                    Is.EqualTo(new[] { 30, 31 }));

                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_IsReadOnlyDefensiveCopy()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldPresentationRequests_IsReadOnlyDefensiveCopy));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Activated,
                        30,
                        new SurfaceCell(FaceId.Floor, 0, 0)))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Is.InstanceOf<ReadOnlyCollection<GravityFieldPresentationRequest>>());
                var list = (IList<GravityFieldPresentationRequest>)presenter.CurrentGravityFieldPresentationRequests;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentGravityFieldVisualStates_ReplacesClearsAndIsReadOnly()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentGravityFieldVisualStates_ReplacesClearsAndIsReadOnly));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var visualState = CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[]
                    {
                        cell,
                    },
                    slotVisibilityMask: 1 << 4);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldVisualPresentationData(visualState)));

                Assert.That(presenter.CurrentGravityFieldVisualStates, Is.InstanceOf<ReadOnlyCollection<GravityFieldVisualState>>());
                Assert.That(presenter.CurrentGravityFieldVisualStates, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].EmitterEntityId, Is.EqualTo(30));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].AreaCells.ToArray(), Is.EqualTo(new[] { cell }));
                Assert.That(presenter.CurrentGravityFieldVisualStates[0].AreaFootprint.IsSlotVisible(4), Is.True);
                var list = (IList<GravityFieldVisualState>)presenter.CurrentGravityFieldVisualStates;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));

                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(presenter.CurrentGravityFieldVisualStates, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_DoNotReplaceRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_DoNotReplaceRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(
                        new[] { CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Charging, timerTicks: 3, durationTicks: 10) },
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, cell))));

                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldVisualStates, Has.Count.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldRequests_InvokeEntityVisualTargets()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldRequests_InvokeEntityVisualTargets));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldPresentationData(
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Activated, 30, cell),
                        new GravityFieldPresentationEvent(GravityFieldPresentationEventKind.Expired, 30, cell))));

                Assert.That(target.ActivatedCount, Is.EqualTo(1));
                Assert.That(target.ExpiredCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentGravityFieldPresentationRequests, Has.Count.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_InvokeAndClearContinuousTargets()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_InvokeAndClearContinuousTargets));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5))));

                Assert.That(target.ApplyContinuousCount, Is.EqualTo(1));
                Assert.That(target.ClearContinuousCount, Is.Zero);
                Assert.That(target.LastContinuousState.EmitterEntityId, Is.EqualTo(30));
                Assert.That(target.LastContinuousState.Phase, Is.EqualTo(GravityFieldPhase.Active));

                presenter.Present(CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty));

                Assert.That(target.ClearContinuousCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_StateDisappearanceClearsAreaSlots()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_StateDisappearanceClearsAreaSlots));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);
            var slots = CreateAreaSlots(rootObject.transform, slotCount: 9);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<GravityFieldVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "areaCellSlots", slots);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            cell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            new[] { cell },
                            slotVisibilityMask: 1 << 4))));

                Assert.That(slots[4].activeSelf, Is.True);

                presenter.Present(CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty));

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugClearContinuousStateCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualTargetView_AppliesAndClearsAuthoredAreaSlots()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisualTargetView_AppliesAndClearsAuthoredAreaSlots));
            var slots = CreateAreaSlots(rootObject.transform, slotCount: 10);
            var cell = new SurfaceCell(FaceId.Floor, 0, 0);

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "areaCellSlots", slots);

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { cell },
                    slotVisibilityMask: (1 << 4) | (1 << 5) | (1 << 7) | (1 << 8)));

                Assert.That(slots[4].activeSelf, Is.True);
                Assert.That(slots[5].activeSelf, Is.True);
                Assert.That(slots[7].activeSelf, Is.True);
                Assert.That(slots[8].activeSelf, Is.True);
                Assert.That(slots[0].activeSelf, Is.False);
                Assert.That(slots[9].activeSelf, Is.False);
                Assert.That(target.DebugLastAreaCellCount, Is.EqualTo(1));
                Assert.That(target.DebugVisibleAreaSlotCount, Is.EqualTo(4));

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Charging,
                    timerTicks: 3,
                    durationTicks: 10,
                    new[] { cell },
                    slotVisibilityMask: 1 << 4));

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);

                target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    cell,
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { cell },
                    slotVisibilityMask: 1 << 4));
                target.ClearGravityFieldVisualState();

                Assert.That(slots.All(slot => !slot.activeSelf), Is.True);
                Assert.That(target.DebugLastAreaCellCount, Is.Zero);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualTargetView_MissingAreaSlotsNoOps()
        {
            var rootObject = new GameObject(nameof(GravityFieldVisualTargetView_MissingAreaSlotsNoOps));

            try
            {
                var target = rootObject.AddComponent<GravityFieldVisualTargetView>();

                Assert.DoesNotThrow(() => target.ApplyGravityFieldVisualState(CreateGravityFieldVisualState(
                    30,
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    GravityFieldPhase.Active,
                    timerTicks: 2,
                    durationTicks: 5,
                    new[] { new SurfaceCell(FaceId.Floor, 1, 1) },
                    slotVisibilityMask: 1 << 4)));
                Assert.DoesNotThrow(target.ClearGravityFieldVisualState);
                Assert.That(target.DebugVisibleAreaSlotCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_TracksSourcesAndClearsOnDisable()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_TracksSourcesAndClearsOnDisable));
            var lockedRoot = new GameObject("LockedRoot");
            lockedRoot.SetActive(false);
            lockedRoot.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "lockedVisualRoot", lockedRoot);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ApplyGravityFieldLockedTarget(31);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));

                target.ClearGravityFieldLockedTarget(999);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ClearGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.ClearGravityFieldLockedTarget(31);
                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.True);

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds + 0.01f);
                Assert.That(lockedRoot.activeSelf, Is.False);

                target.ApplyGravityFieldLockedTarget(30);
                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                typeof(GravityFieldLockedTargetVisualTargetView)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, Array.Empty<object>());
                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockWeight()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockWeight));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockEdgeWidthProperty), Is.EqualTo(0.08f).Within(0.0001f));
                AssertColorApproximately(
                    new Color(0.45f, 0.55f, 0.85f, 1f),
                    GetRendererColor(renderer, GravityFieldLockedTintProperty));
                Assert.That(GetRendererFloat(renderer, GravityFieldDimFactorProperty), Is.EqualTo(0.55f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldTintStrengthProperty), Is.EqualTo(0.15f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldEmissionOmissionProperty), Is.EqualTo(0.85f).Within(0.0001f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds * 0.5f + 0.01f);

                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockEmissionOmission()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_AppliesRendererPropertyBlockEmissionOmission));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });
                PlayerViewPrefabTestUtility.SetSerializedField(target, "gravityFieldEmissionOmission", 0.42f);

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(
                    GetRendererFloat(renderer, GravityFieldEmissionOmissionProperty),
                    Is.EqualTo(0.42f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_KeepsDimmingUntilLastEmitterClears()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_KeepsDimmingUntilLastEmitterClears));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);
                target.ApplyGravityFieldLockedTarget(2);
                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealInSeconds + 0.01f);
                target.ClearGravityFieldLockedTarget(1);

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));

                target.ClearGravityFieldLockedTarget(2);

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(1f).Within(0.0001f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                target.UpdateGravityFieldLockedTargetReveal(GravityFieldLockRevealOutSeconds * 0.5f + 0.01f);

                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_OnDisableResetsRendererWeight()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_OnDisableResetsRendererWeight));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);
                typeof(GravityFieldLockedTargetVisualTargetView)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(target, Array.Empty<object>());

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(target.DebugCurrentLockReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(target.DebugTargetLockReveal, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_NullAndEmptyRenderersNoOp()
        {
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_NullAndEmptyRenderersNoOp));

            try
            {
                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();

                Assert.DoesNotThrow(() => target.ApplyGravityFieldLockedTarget(1));
                Assert.DoesNotThrow(() => target.ClearGravityFieldLockedTarget(1));

                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", Array.Empty<Renderer>());

                Assert.DoesNotThrow(() => target.ApplyGravityFieldLockedTarget(2));
                Assert.DoesNotThrow(() => target.ClearGravityFieldLockedTarget(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetVisualTargetView_PreservesExistingPropertyBlockValues()
        {
            const string existingPropertyName = "_ExistingMpbValue";
            var rootObject = new GameObject(nameof(GravityFieldLockedTargetVisualTargetView_PreservesExistingPropertyBlockValues));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var existingBlock = new MaterialPropertyBlock();
                existingBlock.SetFloat(existingPropertyName, 0.75f);
                renderer.SetPropertyBlock(existingBlock);

                var target = rootObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "dimRenderers", new[] { renderer });

                target.ApplyGravityFieldLockedTarget(1);

                Assert.That(GetRendererFloat(renderer, existingPropertyName), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockedWeightProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, GravityFieldLockRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedTargetBoxPrefabs_AreAuthoredForLockableDimming()
        {
            var lockableShader = Shader.Find(GravityFieldLockableShaderName);

            Assert.That(lockableShader, Is.Not.Null, GravityFieldLockableShaderName);
            foreach (var prefabPath in GravityFieldLockableBoxPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                Assert.That(prefab, Is.Not.Null, prefabPath);
                var target = prefab.GetComponent<GravityFieldLockedTargetVisualTargetView>();
                Assert.That(target, Is.Not.Null, $"{prefabPath} root component");

                var dimRenderers = ReadDimRenderers(target);
                Assert.That(dimRenderers, Is.Not.Null, prefabPath);
                Assert.That(dimRenderers, Is.Not.Empty, prefabPath);
                for (var i = 0; i < dimRenderers.Length; i++)
                {
                    var dimRenderer = dimRenderers[i];

                    Assert.That(dimRenderer, Is.Not.Null, $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.transform.IsChildOf(prefab.transform), Is.True, $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.name, Does.Not.Contain("GripPoint"), $"{prefabPath} dimRenderers[{i}]");
                    Assert.That(dimRenderer.GetComponent<ParticleSystem>(), Is.Null, $"{prefabPath} dimRenderers[{i}]");
                    foreach (var material in dimRenderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, $"{prefabPath} {dimRenderer.name}");
                        Assert.That(material.shader, Is.SameAs(lockableShader), $"{prefabPath} {dimRenderer.name} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockRevealProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockNoiseMapProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldLockEdgeWidthProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.HasProperty(GravityFieldEmissionOmissionProperty), Is.True, $"{prefabPath} {material.name}");
                        Assert.That(material.GetTexture(GravityFieldLockNoiseMapProperty), Is.Not.Null, $"{prefabPath} {material.name}");
                        Assert.That(
                            material.GetFloat(GravityFieldLockEdgeWidthProperty),
                            Is.InRange(0.001f, 0.5f),
                            $"{prefabPath} {material.name}");
                    }
                }
            }

            var materialDirectory = Path.Combine(
                Application.dataPath,
                GravityFieldLockableMaterialDirectory.Substring("Assets/".Length));
            var materialPaths = Directory.GetFiles(
                    materialDirectory,
                    "*.mat",
                    SearchOption.TopDirectoryOnly)
                .Select(path => "Assets" + path.Replace("\\", "/").Substring(Application.dataPath.Length))
                .OrderBy(path => path)
                .ToArray();
            Assert.That(materialPaths, Has.Length.EqualTo(19));
            foreach (var materialPath in materialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                Assert.That(material, Is.Not.Null, materialPath);
                Assert.That(material.shader, Is.SameAs(lockableShader), materialPath);
                Assert.That(material.HasProperty(GravityFieldEmissionOmissionProperty), Is.True, materialPath);
                Assert.That(material.GetTexture(GravityFieldLockNoiseMapProperty), Is.Not.Null, materialPath);
                Assert.That(material.GetFloat(GravityFieldLockEdgeWidthProperty), Is.InRange(0.001f, 0.5f), materialPath);
            }
        }

        [Test]
        [Category("Core")]
        public void StaticViewBoxShowcase_RemainsExcludedFromGravityFieldLockableDimming()
        {
            var showcasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaticBoxShowcasePrefabPath);
            var showcaseMaterial = AssetDatabase.LoadAssetAtPath<Material>(StaticBoxShowcaseMaterialPath);

            Assert.That(showcasePrefab, Is.Not.Null, StaticBoxShowcasePrefabPath);
            Assert.That(showcasePrefab.GetComponent<GravityFieldLockedTargetVisualTargetView>(), Is.Null);
            Assert.That(showcaseMaterial, Is.Not.Null, StaticBoxShowcaseMaterialPath);
            Assert.That(showcaseMaterial.shader.name, Is.Not.EqualTo(GravityFieldLockableShaderName));
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualStates_MissingUnsupportedAndDuplicate_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualStates_MissingUnsupportedAndDuplicate_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var emitter = CreateBox(30, cell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter }, topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(999, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5),
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 2, durationTicks: 5))));

                Assert.That(diagnostics.Any(message => message.Contains("missing continuous visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported continuous visual target")), Is.True);

                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var target = view.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Active, timerTicks: 4, durationTicks: 5),
                        CreateGravityFieldVisualState(30, cell, GravityFieldPhase.Charging, timerTicks: 9, durationTicks: 10))));

                Assert.That(target.ApplyContinuousCount, Is.EqualTo(1));
                Assert.That(target.LastContinuousState.Phase, Is.EqualTo(GravityFieldPhase.Active));
                Assert.That(target.LastContinuousState.TimerTicks, Is.EqualTo(4));
                Assert.That(diagnostics.Any(message => message.Contains("duplicate continuous visual state")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_ApplyRetainLateAttachAndClear()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_ApplyRetainLateAttachAndClear));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked target visual target")), Is.True);

                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
                Assert.That(target.LastLockedTargetEmitterEntityId, Is.EqualTo(30));

                presenter.Present(CreateTickResult(
                    3,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(2));

                presenter.Present(CreateTickResult(4, new[] { emitter, targetBox }, topology, TickPresentationData.Empty));

                Assert.That(target.ClearLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.LastLockedTargetEmitterEntityId, Is.EqualTo(30));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MissingAndUnsupported_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MissingAndUnsupported_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20, 999 }))));

                Assert.That(diagnostics.Any(message => message.Contains("missing locked target visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked target visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MultipleEmittersMaintainSourceSet()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MultipleEmittersMaintainSourceSet));
            var firstEmitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var secondEmitterCell = new SurfaceCell(FaceId.Floor, 2, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var firstEmitter = CreateBox(30, firstEmitterCell);
            var secondEmitter = CreateBox(31, secondEmitterCell);
            var targetBox = CreateBox(20, targetCell);
            var lockedRoot = new GameObject("LockedRoot");
            lockedRoot.SetActive(false);
            lockedRoot.transform.SetParent(rootObject.transform, worldPositionStays: false);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { firstEmitter, secondEmitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<GravityFieldLockedTargetVisualTargetView>();
                PlayerViewPrefabTestUtility.SetSerializedField(target, "lockedVisualRoot", lockedRoot);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { firstEmitter, secondEmitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            firstEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }),
                        CreateGravityFieldVisualState(
                            31,
                            secondEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(2));
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.Present(CreateTickResult(
                    2,
                    new[] { firstEmitter, secondEmitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            31,
                            secondEmitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 1,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.DebugActiveEmitterCount, Is.EqualTo(1));
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.Present(CreateTickResult(3, new[] { firstEmitter, secondEmitter, targetBox }, topology, TickPresentationData.Empty));

                Assert.That(target.DebugActiveEmitterCount, Is.Zero);
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.UpdatePresentation(GravityFieldLockRevealOutSeconds * 0.5f);
                Assert.That(lockedRoot.activeSelf, Is.True);

                presenter.UpdatePresentation(GravityFieldLockRevealOutSeconds * 0.5f + 0.01f);
                Assert.That(lockedRoot.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_DuplicateTargetIdsApplyOnce()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_DuplicateTargetIdsApplyOnce));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20, 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_CallsTargetInterfaceAndDoesNotClearDimming()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_CallsTargetInterfaceAndDoesNotClearDimming));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);
            var payload = new GravityFieldLockedBoxPayload(30, 20, emitterCell, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(
                        new[]
                        {
                            CreateGravityFieldVisualState(
                                30,
                                emitterCell,
                                GravityFieldPhase.Active,
                                timerTicks: 2,
                                durationTicks: 5,
                                lockedTargetEntityIds: new[] { 20 }),
                        },
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.LockedBox,
                            30,
                            emitterCell,
                            targetEntityId: 20,
                            lockedBoxPayload: payload))));

                Assert.That(target.LockedBoxOneShotCount, Is.EqualTo(1));
                Assert.That(target.LastLockedBoxPayload, Is.EqualTo(payload));
                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_MissingUnsupportedAndLateAttachDoNotReplay()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedBoxOneShot_MissingUnsupportedAndLateAttachDoNotReplay));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);
            var payload = new GravityFieldLockedBoxPayload(30, 20, emitterCell, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.LockedBox,
                        30,
                        emitterCell,
                        targetEntityId: 20,
                        lockedBoxPayload: payload))));

                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();
                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                presenter.Present(CreateTickResult(
                    3,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.LockedBox,
                        30,
                        emitterCell,
                        targetEntityId: 999,
                        lockedBoxPayload: new GravityFieldLockedBoxPayload(30, 999, emitterCell, targetCell)))));

                Assert.That(diagnostics.Any(message => message.Contains("unsupported locked box one-shot visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("missing locked box one-shot visual target")), Is.True);
                Assert.That(target.LockedBoxOneShotCount, Is.Zero);
                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_DoesNotCreateSnapshots()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_DoesNotCreateSnapshots));
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    presenter.Present(CreateTickResult(
                        1,
                        new[] { emitter, targetBox },
                        topology,
                        CreateGravityFieldVisualPresentationData(
                            CreateGravityFieldVisualState(
                                30,
                                emitterCell,
                                GravityFieldPhase.Active,
                                timerTicks: 2,
                                durationTicks: 5,
                                lockedTargetEntityIds: new[] { 20 }))));

                    Assert.That(capture.Counts.WorldStateCreateSnapshotCount, Is.Zero);
                    Assert.That(capture.Counts.ProjectedWorldMaterializedSnapshotCount, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyGravityFieldAuraLockedTargetsUseSameTintAsGravityField()
        {
            var rootObject = new GameObject(nameof(EnemyGravityFieldAuraLockedTargetsUseSameTintAsGravityField));
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var source = CreateEnemyUnit(40, sourceCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { source, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                var target = targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { source, targetBox },
                    topology,
                    CreateEnemyGravityFieldAuraPresentationData(
                        new TickEnemyGravityFieldAuraVisualState(
                            40,
                            sourceCell,
                            EnemyUtilityEffectPhase.Active,
                            radius: 1,
                            timerTicks: 2,
                            durationTicks: 3,
                            progress01: 0.33f,
                            effectIndex: 0,
                            activationSequence: 1,
                            areaFootprint: GravityFieldAreaFootprint.Empty,
                            startedThisTick: false,
                            lockedTargetEntityIds: new[] { 20 }))));

                Assert.That(target.ApplyLockedTargetCount, Is.EqualTo(1));
                Assert.That(target.ClearLockedTargetCount, Is.Zero);

                presenter.Present(CreateTickResult(
                    2,
                    new[] { source, targetBox },
                    topology,
                    CreateEnemyGravityFieldAuraPresentationData(
                        new TickEnemyGravityFieldAuraVisualState(
                            40,
                            sourceCell,
                            EnemyUtilityEffectPhase.Active,
                            radius: 1,
                            timerTicks: 1,
                            durationTicks: 3,
                            progress01: 0.66f,
                            effectIndex: 0,
                            activationSequence: 1,
                            areaFootprint: GravityFieldAreaFootprint.Empty,
                            startedThisTick: false,
                            lockedTargetEntityIds: Array.Empty<int>()))));

                Assert.That(target.ClearLockedTargetCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyGravityFieldAuraPresentationDoesNotMutateWorldState()
        {
            var rootObject = new GameObject(nameof(EnemyGravityFieldAuraPresentationDoesNotMutateWorldState));
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var source = CreateEnemyUnit(40, sourceCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.PresentInitial(new[] { source, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    presenter.Present(CreateTickResult(
                        1,
                        new[] { source, targetBox },
                        topology,
                        CreateEnemyGravityFieldAuraPresentationData(
                            new TickEnemyGravityFieldAuraVisualState(
                                40,
                                sourceCell,
                                EnemyUtilityEffectPhase.Active,
                                radius: 1,
                                timerTicks: 2,
                                durationTicks: 3,
                                progress01: 0.33f,
                                effectIndex: 0,
                                activationSequence: 1,
                                areaFootprint: GravityFieldAreaFootprint.Empty,
                                startedThisTick: false,
                                lockedTargetEntityIds: new[] { 20 }))));

                    Assert.That(capture.Counts.WorldStateCreateSnapshotCount, Is.Zero);
                    Assert.That(capture.Counts.ProjectedWorldMaterializedSnapshotCount, Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldLockedTargets_MissingOnClearNoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldLockedTargets_MissingOnClearNoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitterCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var emitter = CreateBox(30, emitterCell);
            var targetBox = CreateBox(20, targetCell);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.PresentInitial(new[] { emitter, targetBox }, topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                Assert.That(registry.TryGetView(20, out var targetView), Is.True);
                targetView.gameObject.AddComponent<RecordingGravityFieldVisualTarget>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { emitter, targetBox },
                    topology,
                    CreateGravityFieldVisualPresentationData(
                        CreateGravityFieldVisualState(
                            30,
                            emitterCell,
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 5,
                            lockedTargetEntityIds: new[] { 20 }))));

                UnityEngine.Object.DestroyImmediate(targetView.gameObject);
                registry.Unregister(20);

                Assert.DoesNotThrow(() => presenter.Present(
                    CreateTickResult(2, new[] { emitter }, topology, TickPresentationData.Empty)));
                Assert.That(diagnostics.Any(message => message.Contains("missing locked target clear visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_GravityFieldVisualMissingOrUnsupported_NoOpsWithDiagnostic()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_GravityFieldVisualMissingOrUnsupported_NoOpsWithDiagnostic));
            var diagnostics = new List<string>();
            var emitter = CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Activated,
                        999,
                        new SurfaceCell(FaceId.Floor, 0, 0)))));

                presenter.PresentInitial(new[] { emitter }, topology);
                presenter.SetGravityFieldVisualDiagnosticSink(diagnostics.Add);
                presenter.Present(CreateTickResult(
                    2,
                    new[] { emitter },
                    topology,
                    CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                        GravityFieldPresentationEventKind.Expired,
                        30,
                        emitter.position))));

                Assert.That(diagnostics.Any(message => message.Contains("missing Activated visual target")), Is.True);
                Assert.That(diagnostics.Any(message => message.Contains("unsupported Expired visual target")), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_CurrentTilePresentationRequests_IsReadOnlyDefensiveCopy()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_CurrentTilePresentationRequests_IsReadOnlyDefensiveCopy));

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, new SurfaceCell(FaceId.Floor, 0, 0)))));

                Assert.That(presenter.CurrentTilePresentationRequests, Is.InstanceOf<ReadOnlyCollection<TilePresentationRequest>>());
                var list = (IList<TilePresentationRequest>)presenter.CurrentTilePresentationRequests;
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list.Add(default));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_ButtonActivatedRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_ButtonActivatedRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));

                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TileId, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_TopologyMotion_SyncsTileFeaturePoseBeforePlayingRequests()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_TopologyMotion_SyncsTileFeaturePoseBeforePlayingRequests));
            var targetObject = new GameObject("DestinationTileFeatureTarget");
            var destinationCell = new SurfaceCell(FaceId.Ceiling, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var sourceTopology);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var registry = rootObject.AddComponent<TileFeatureVisualRegistry>();
                targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var target = targetObject.AddComponent<ActiveStateRecordingTileFeatureTarget>();
                target.Configure(100, destinationCell);
                registry.ConfigureSearchRoot(rootObject.transform);
                var poseResolver = new BoardSurfaceCellPresentationPoseResolver(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    1f,
                    sourceTopology,
                    1f);
                var synchronizer = new TileFeatureVisualPoseSynchronizer(registry, poseResolver);
                synchronizer.RefreshAll(sourceTopology);
                presenter.AttachTileFeatureVisualRegistry(registry);
                presenter.AttachTileFeatureVisualPoseSynchronizer(synchronizer);

                Assert.That(targetObject.activeSelf, Is.False);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    destinationTopology,
                    CreateTilePresentationDataWithTopologyMotion(
                        new TickTopologyMotion(sourceTopology, destinationTopology, CubeRotationKind.Forward),
                        CreateButtonActivatedTileEvent(100, destinationCell))));

                Assert.That(target.PlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(target.WasActiveInHierarchyWhenButtonActivated, Is.True);
                Assert.That(targetObject.activeSelf, Is.True);
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TileId, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_DestroyTileRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_DestroyTileRequest_InvokesAttachedTileVisualTargetAndKeepsRequestCache));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateDestroyTileTriggeredTileEvent(100, cell, targetEntityId: 20))));

                Assert.That(target.DebugPlayDestroyTileTriggeredCount, Is.EqualTo(1));
                Assert.That(target.DebugPlayButtonActivatedCount, Is.Zero);
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.DestroyTileTriggered));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TargetEntityId, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockGeneratedRequest_InvokesGeneratorTileVisualTarget()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockGeneratedRequest_InvokesGeneratorTileVisualTarget));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(target.DebugMoonBlockGeneratedCount, Is.EqualTo(1));
                Assert.That(target.DebugLastMoonBlockGeneratedEntityId, Is.EqualTo(20));
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.MoonBlockGenerated));
                Assert.That(presenter.CurrentTilePresentationRequests[0].TargetEntityId, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorPrefab_UsesAnimatorDoorOpenVisualAndNoDoorDriver()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoonGeneratorPrefabPath);

            Assert.That(prefab, Is.Not.Null);

            var target = prefab.GetComponent<TileFeatureVisualTargetView>();
            var provider = prefab.GetComponent<TileFeatureVisualProfileProvider>();
            var animator = prefab.GetComponent<Animator>();
            Assert.That(target, Is.Not.Null);
            Assert.That(provider, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.name, Is.EqualTo("TileFeature_MoonGenerator_Default"));

            var monoBehaviours = prefab.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            Assert.That(
                monoBehaviours.Select(component => component != null ? component.GetType().Name : string.Empty),
                Does.Not.Contain("MoonBlockGeneratorDoorPresentationDriver"));

            var profile = AssetDatabase.LoadAssetAtPath<TileFeatureVisualProfile>(MoonGeneratorProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.FeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(provider.TryGetProfile(TileFeatureKind.MoonBlockGenerator, out var providerProfile), Is.True);
            Assert.That(providerProfile, Is.SameAs(profile));
            Assert.That(profile.TryGetCueBinding(TileFeatureVisualCueId.MoonBlockGenerated, out var generatedBinding), Is.True);
            Assert.That(generatedBinding.AnimatorBinding.ParameterOrStateName, Is.EqualTo("MoonBlockGenerated"));

            var controller = target.DebugAnimator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.parameters.Any(parameter =>
                parameter.name == "MoonBlockGenerated" &&
                parameter.type == AnimatorControllerParameterType.Trigger), Is.True);
        }

        [Test]
        [Category("Core")]
        public void MoonBlockGeneratorDoorOpenClip_TargetsLeftAndRightDoorTransforms()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MoonGeneratorDoorOpenClipPath);

            Assert.That(clip, Is.Not.Null);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.That(bindings.Any(binding =>
                binding.path == "ModelRoot/MoonSpawner/L_Door" &&
                binding.propertyName == "localEulerAnglesRaw.z"), Is.True);
            Assert.That(bindings.Any(binding =>
                binding.path == "ModelRoot/MoonSpawner/R_Door" &&
                binding.propertyName == "localEulerAnglesRaw.z"), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockGeneratedBeforeEntityBind_StartsEmergenceWhenViewRegisters()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockGeneratedBeforeEntityBind_StartsEmergenceWhenViewRegisters));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(presenter.PendingMoonBlockEmergenceRequestCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out _), Is.False);

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateBox(20, cell) },
                    topology,
                    TickPresentationData.Empty));

                Assert.That(presenter.PendingMoonBlockEmergenceRequestCount, Is.Zero);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(driver.DebugPlayCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_OnlyMovesModelRootAndNormalizes()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_OnlyMovesModelRootAndNormalizes));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                var expectedRootPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    cell,
                    EntityType.Box);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var rootPositionDuringEmergence = view.transform.localPosition;
                var rootRotationDuringEmergence = view.transform.localRotation;
                var modelRoot = view.ModelRoot;
                AssertPositionApproximately(rootPositionDuringEmergence, expectedRootPosition);
                Assert.That(modelRoot.localPosition.z, Is.LessThan(-0.001f));
                Assert.That(modelRoot.localPosition.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(modelRoot.localPosition.y, Is.EqualTo(0f).Within(0.001f));
                AssertPositionApproximately(modelRoot.localScale, Vector3.zero);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.UpdatePresentation(driver.DebugDurationSeconds + 0.01f);

                AssertPositionApproximately(view.transform.localPosition, rootPositionDuringEmergence);
                Assert.That(view.transform.localRotation, Is.EqualTo(rootRotationDuringEmergence));
                AssertPositionApproximately(modelRoot.localPosition, Vector3.zero);
                AssertPositionApproximately(modelRoot.localScale, Vector3.one);
                Assert.That(driver.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_HidesThenLaunchesAfterDoorOpenLead()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_HidesThenLaunchesAfterDoorOpenLead));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var startPosition = view.ModelRoot.localPosition;
                var startScale = view.ModelRoot.localScale;

                presenter.UpdatePresentation(0.05f);

                AssertPositionApproximately(view.ModelRoot.localPosition, startPosition);
                AssertPositionApproximately(startScale, Vector3.zero);
                AssertPositionApproximately(view.ModelRoot.localScale, Vector3.zero);

                presenter.UpdatePresentation(0.04f);

                Assert.That(view.ModelRoot.localPosition.z, Is.GreaterThan(startPosition.z));
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(startScale.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockEmergence_UsesPresentationLaunchAfterSpawnLockWindow()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_MoonBlockEmergence_UsesPresentationLaunchAfterSpawnLockWindow));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateBox(20, cell) },
                    topology,
                    CreateTilePresentationData(CreateMoonBlockGeneratedTileEvent(100, cell, moonBlockEntityId: 20, spawnTick: 1))));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var driver = view.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.IsPlaying, Is.True);

                var lockDurationSeconds =
                    MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks /
                    (float)timingProfile.SimulationTicksPerSecond;
                Assert.That(lockDurationSeconds, Is.LessThan(timingProfile.MoonBlockEmergenceDurationSeconds));
                Assert.That(driver.DebugDurationSeconds, Is.GreaterThan(lockDurationSeconds));

                presenter.UpdatePresentation(lockDurationSeconds + 0.06f);

                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(view.ModelRoot.localPosition.z, Is.GreaterThan(0f));
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));

                presenter.UpdatePresentation(driver.DebugDurationSeconds);

                Assert.That(driver.IsPlaying, Is.False);
                AssertPositionApproximately(view.ModelRoot.localPosition, Vector3.zero);
                AssertPositionApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockEmergencePresentationController_DisposeUnsubscribesAndClearsPending()
        {
            var rootObject = new GameObject(nameof(MoonBlockEmergencePresentationController_DisposeUnsubscribesAndClearsPending));

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var controller = new MoonBlockEmergencePresentationController();
                controller.Configure(registry, CreateTimingProfile());
                controller.QueueRequests(
                    new[]
                    {
                        CreateMoonBlockGeneratedRequest(moonBlockEntityId: 20, spawnTick: 1),
                    },
                    currentTickIndex: 1);
                Assert.That(controller.PendingRequestCount, Is.EqualTo(1));

                controller.Dispose();
                var viewObject = new GameObject("EntityView_20");
                viewObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(20);
                registry.Register(view);

                Assert.That(controller.PendingRequestCount, Is.Zero);
                Assert.That(view.GetComponent<MoonBlockEmergencePresentationDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void MoonBlockEmergencePresentationController_PendingRequestExpiresIfViewNeverBinds()
        {
            var rootObject = new GameObject(nameof(MoonBlockEmergencePresentationController_PendingRequestExpiresIfViewNeverBinds));

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var controller = new MoonBlockEmergencePresentationController();
                controller.Configure(registry, CreateTimingProfile());
                controller.QueueRequests(
                    new[]
                    {
                        CreateMoonBlockGeneratedRequest(moonBlockEntityId: 20, spawnTick: 1),
                    },
                    currentTickIndex: 1);

                controller.QueueRequests(Array.Empty<TilePresentationRequest>(), currentTickIndex: 8);

                Assert.That(controller.PendingRequestCount, Is.Zero);
                controller.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_ButtonActivatedRequest_NextTickWithoutRequest_DoesNotInvokeAgain()
        {
            var rootObject = new GameObject(nameof(GameplayTickViewPresenter_ButtonActivatedRequest_NextTickWithoutRequest_DoesNotInvokeAgain));
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);

            try
            {
                var presenter = CreateInitializedPresenter(rootObject, out var topology);
                var target = AttachTileVisualTarget(rootObject, presenter, 100, cell);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateTilePresentationData(CreateButtonActivatedTileEvent(100, cell))));
                presenter.Present(CreateTickResult(2, Array.Empty<EntityState>(), topology, TickPresentationData.Empty));

                Assert.That(target.DebugPlayButtonActivatedCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentTilePresentationRequests, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void MoveOwnership_GenericMovePresentation_Retained()
        {
            GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration();
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicTrack_AppliesTickOneDestinationPoseImmediately()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicTrack_AppliesTickOneDestinationPoseImmediately");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(10, sourceCell),
                    },
                    topology);

                var kinematicTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreateEnemyUnit(10, sourceCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_ComposesWithActiveMove()
        {
            var rootObject = new GameObject("GlidePresentation_ComposesWithActiveMove");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, destinationCell),
                        },
                        topology,
                        CreateGlidePresentationData(
                            new[]
                            {
                                new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, destinationCell),
                            },
                            new[]
                            {
                                CreateGlideSignal(20, destinationCell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit) -
                               GetProjectedEntityNormal(boardBounds, topology, destinationCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_ComposesWithFutureKinematicTrack()
        {
            var rootObject = new GameObject("GlidePresentation_ComposesWithFutureKinematicTrack");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                var kinematicTrack = CreateKinematicTrack(
                    20,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, sourceCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack },
                            enemyGlideSignals: new[]
                            {
                                CreateGlideSignal(20, sourceCell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0) -
                               GetProjectedEntityNormal(boardBounds, topology, sourceCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GlidePresentation_RisesAwayFromProjectedNormal_OnNonTopFaceAndClearsWhenSignalMissing()
        {
            var rootObject = new GameObject("GlidePresentation_RisesAwayFromProjectedNormal_OnNonTopFaceAndClearsWhenSignalMissing");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var cell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, cell),
                    },
                    topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[]
                        {
                            CreateEnemyUnit(20, cell),
                        },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, cell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var basePosition = GetProjectedEntityPosition(boardBounds, topology, cell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, cell, EntityType.Unit);
                Assert.That(Mathf.Abs(Vector3.Dot(normal.normalized, Vector3.up)), Is.LessThan(0.01f));
                AssertPositionApproximately(view.transform.localPosition, basePosition - (normal * 0.25f));

                presenter.Present(
                    CreateTickResult(
                        2,
                        new[]
                        {
                            CreateEnemyUnit(20, cell),
                        },
                        topology,
                        TickPresentationData.Empty));

                AssertPositionApproximately(view.transform.localPosition, basePosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Windup_RaisesInPlaceAndKeepsFacing()
        {
            var rootObject = new GameObject("GliderVisual_Windup_RaisesInPlaceAndKeepsFacing");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, cell) }, topology);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var basePosition = GetProjectedEntityPosition(boardBounds, topology, cell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, cell, EntityType.Unit);
                var initialRotation = view.transform.localRotation;

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, cell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, cell, EnemyGlidePhase.Windup, SimulationFixed.UnitsPerCell / 8),
                            })));

                AssertPositionApproximately(view.transform.localPosition, basePosition - (normal * 0.125f));
                Assert.That(Quaternion.Angle(view.transform.localRotation, initialRotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Active_OnSolidCell_UsesAirborneHeight()
        {
            var rootObject = new GameObject("GliderVisual_Active_OnSolidCell_UsesAirborneHeight");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var solidCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, solidCell) },
                        topology,
                        CreateGlidePresentationData(
                            new[]
                            {
                                new TickEntityMotion(20, TickEntityMotionKind.Move, sourceCell, solidCell),
                            },
                            new[]
                            {
                                CreateGlideSignal(20, solidCell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));
                presenter.UpdatePresentation(CreateTimingProfile().MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedEntityPosition(boardBounds, topology, solidCell, EntityType.Unit) -
                               GetProjectedEntityNormal(boardBounds, topology, solidCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Active_StepByStepMovementDoesNotSkip()
        {
            var rootObject = new GameObject("GliderVisual_Active_StepByStepMovementDoesNotSkip");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var nextCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                var kinematicTrack = CreateKinematicTrack(
                    20,
                    sourceCell,
                    sourceLocalX: 0,
                    destinationAnchorCell: sourceCell,
                    destinationLocalX: SimulationFixed.UnitsPerCell / 2,
                    topology: topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { kinematicTrack },
                            enemyGlideSignals: new[]
                            {
                                CreateGlideSignal(20, sourceCell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var expected = GetProjectedKinematicEntityPosition(
                                   boardBounds,
                                   topology,
                                   sourceCell,
                                   EntityType.Unit,
                                   SimulationFixed.UnitsPerCell / 2,
                                   0) -
                               GetProjectedEntityNormal(boardBounds, topology, sourceCell, EntityType.Unit) * 0.25f;
                var skippedPosition = GetProjectedEntityPosition(boardBounds, topology, nextCell, EntityType.Unit) -
                                      GetProjectedEntityNormal(boardBounds, topology, nextCell, EntityType.Unit) * 0.25f;
                AssertPositionApproximately(view.transform.localPosition, expected);
                Assert.That(Vector3.Distance(view.transform.localPosition, skippedPosition), Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GliderVisual_Recover_LowersInPlaceOnlyOnNonSolid()
        {
            var rootObject = new GameObject("GliderVisual_Recover_LowersInPlaceOnlyOnNonSolid");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var nonSolidCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, nonSolidCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateEnemyUnit(20, nonSolidCell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, nonSolidCell, EnemyGlidePhase.Active, SimulationFixed.UnitsPerCell / 4),
                            })));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var activePose = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        2,
                        new[] { CreateEnemyUnit(20, nonSolidCell) },
                        topology,
                        CreateGlidePresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[]
                            {
                                CreateGlideSignal(20, nonSolidCell, EnemyGlidePhase.Recovery, SimulationFixed.UnitsPerCell / 8),
                            })));

                var basePosition = GetProjectedEntityPosition(boardBounds, topology, nonSolidCell, EntityType.Unit);
                var normal = GetProjectedEntityNormal(boardBounds, topology, nonSolidCell, EntityType.Unit);
                var recoverPose = view.transform.localPosition;
                AssertPositionApproximately(recoverPose, basePosition - (normal * 0.125f));
                Assert.That(Vector3.Distance(recoverPose, basePosition), Is.LessThan(Vector3.Distance(activePose, basePosition)));

                presenter.Present(CreateTickResult(3, new[] { CreateEnemyUnit(20, nonSolidCell) }, topology, TickPresentationData.Empty));

                AssertPositionApproximately(view.transform.localPosition, basePosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicTrack_BeatsLegacyMotionOnAnchorCommit()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicTrack_BeatsLegacyMotionOnAnchorCommit");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(10, sourceCell),
                    },
                    topology);

                var legacyMotion = new TickEntityMotion(
                    10,
                    TickEntityMotionKind.Move,
                    sourceCell,
                    destinationCell,
                    topology,
                    topology,
                    Direction.Right,
                    Direction.Right);
                var kinematicTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 1024,
                    destinationCell,
                    destinationLocalX: -2048,
                    topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreateEnemyUnit(10, destinationCell),
                        },
                        topology,
                        CreateKinematicPresentationData(
                            new[] { legacyMotion },
                            new[] { kinematicTrack })));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds * 2f);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit, -2048, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_KinematicRemovedTerminal_RetainsPoseWithoutFinalEntity()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KinematicRemovedTerminal_RetainsPoseWithoutFinalEntity");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateKinematicTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    topology,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateKinematicPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, -2048, 0));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousPose_AppliesAnchorPlusLocalOffset()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousPose_AppliesAnchorPlusLocalOffset");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var continuousTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 0,
                    sourceCell,
                    destinationLocalX: 1024,
                    ContinuousLocomotionMode.Moving);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { continuousTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, 1024, 0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousIdleNonZero_DoesNotSnapToAnchor()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousIdleNonZero_DoesNotSnapToAnchor");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var expectedPosition = GetProjectedKinematicEntityPosition(
                    boardBounds,
                    topology,
                    sourceCell,
                    EntityType.Unit,
                    1024,
                    0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var idleTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: 1024,
                    sourceCell,
                    destinationLocalX: 1024,
                    ContinuousLocomotionMode.Idle);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreatePlayerUnit(10, sourceCell) },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { idleTrack })));
                presenter.UpdatePresentation(CreateTimingProfile().MoveMotionDurationSeconds);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreatePlayerUnit(10, sourceCell) },
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { idleTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
                Assert.That(
                    Vector3.Distance(
                        view.transform.localPosition,
                        GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit)),
                    Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ContinuousRemovedTerminal_RetainsPose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ContinuousRemovedTerminal_RetainsPose");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    ContinuousLocomotionMode.Idle,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedKinematicEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit, -2048, 0));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerDeathHold_RetainsContinuousRemovedTerminalPoseUntilSignalClears()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerDeathHold_RetainsContinuousRemovedTerminalPoseUntilSignalClears");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var expectedPosition = GetProjectedKinematicEntityPosition(
                    boardBounds,
                    topology,
                    sourceCell,
                    EntityType.Unit,
                    -2048,
                    0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var removedTrack = CreateContinuousTrack(
                    10,
                    sourceCell,
                    sourceLocalX: -2048,
                    sourceCell,
                    destinationLocalX: -2048,
                    ContinuousLocomotionMode.Idle,
                    TickKinematicMotionTerminalKind.Removed);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new[] { removedTrack },
                            new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 2,
                                    eligibleTick: 5,
                                    remainingTicks: 3,
                                    startedThisTick: true),
                            })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            Array.Empty<TickContinuousLocomotionTrack>(),
                            new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 2,
                                    eligibleTick: 5,
                                    remainingTicks: 2,
                                    startedThisTick: false),
                            })));

                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        Array.Empty<EntityState>(),
                        topology,
                        TickPresentationData.Empty));

                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerDeathHold_DoesNotUseEnemyKinematicPoseOverride()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerDeathHold_DoesNotUseEnemyKinematicPoseOverride");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var committedPosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var enemyKinematicPosition = GetProjectedKinematicEntityPosition(
                    boardBounds,
                    topology,
                    sourceCell,
                    EntityType.Unit,
                    -2048,
                    0);

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                var trackState = GetPresentationTrackState(presenter);
                trackState.EnemyKinematicPresentationPoseOverrides[10] = new KinematicPresentationPose(
                    new GameplayEntityPose(enemyKinematicPosition, Quaternion.identity),
                    MotionMode.Voluntary,
                    TickKinematicMotionTerminalKind.Removed);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        topology,
                        CreateContinuousPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            Array.Empty<TickContinuousLocomotionTrack>(),
                            new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 2,
                                    eligibleTick: 5,
                                    remainingTicks: 3,
                                    startedThisTick: true),
                            })));

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(view.transform.localPosition, committedPosition);
                Assert.That(
                    Vector3.Distance(view.transform.localPosition, enemyKinematicPosition),
                    Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_PlayerMoveMotionOverride_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                const float playerMoveOverrideSeconds = 0.4f;
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var motionAuthoring = playerViewPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();
                Assert.That(motionAuthoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "moveMotionDurationSeconds",
                    playerMoveOverrideSeconds);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreatePlayerUnit(10, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: playerMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float unitMoveOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        unitMoveOverridesByEntityId: new Dictionary<int, float>
                        {
                            { 20, unitMoveOverrideSeconds },
                        },
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 20, new EntityMotionPresentationSnapshot(0.8f, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: unitMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float entityMoveOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 20, new EntityMotionPresentationSnapshot(entityMoveOverrideSeconds, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: entityMoveOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float pushOverrideSeconds = 0.4f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        entityMotionOverridesByEntityId: new Dictionary<int, EntityMotionPresentationSnapshot>
                        {
                            { 30, new EntityMotionPresentationSnapshot(EntityMotionPresentationAuthoring.UseGlobalTimingSentinel, pushOverrideSeconds, EntityMotionPresentationAuthoring.UseGlobalTimingSentinel) },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Push));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Box);
                var expectedPosition = ResolveEasedLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.PushMotionDurationSeconds,
                    durationSeconds: pushOverrideSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxFlipMotion_ScalesModelRootDuringRiseImpactAndReset()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxFlipMotion_ScalesModelRootDuringRiseImpactAndReset");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Flip));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.25f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.y, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.GreaterThan(1f));

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.65f);

                Assert.That(view.ModelRoot.localScale.x, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.y, Is.GreaterThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.10f);

                AssertScaleApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxSlideMotion_StretchesAlongTravelAndResets()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxSlideMotion_StretchesAlongTravelAndResets");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateBox(30, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.BoxSlide));
                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(Mathf.Max(view.ModelRoot.localScale.x, view.ModelRoot.localScale.y), Is.GreaterThan(1f));
                Assert.That(Mathf.Min(view.ModelRoot.localScale.x, view.ModelRoot.localScale.y), Is.LessThan(1f));
                Assert.That(view.ModelRoot.localScale.z, Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                AssertScaleApproximately(view.ModelRoot.localScale, Vector3.one);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_EnemyPrefabUnitMoveOverride_PrefersUnitLocomotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyPrefabUnitMoveOverride_PrefersUnitLocomotionAuthoring");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_UnitMoveOverride",
                unitMoveDurationSeconds: 0.4f,
                entityMoveDurationSeconds: 0.8f);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                        {
                            { 20, enemyPrefab },
                        }));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: 0.4f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickViewPresenter_EnemyPrefabUnitMoveAuthoringWithoutOverride_FallsBackToEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyPrefabUnitMoveAuthoringWithoutOverride_FallsBackToEntityMotionAuthoring");
            var enemyPrefab = CreateEnemyViewPrefab(
                "EnemyPrefab_UnitMoveFallback",
                unitMoveDurationSeconds: UnitLocomotionPresentationAuthoring.UseGlobalTimingSentinel,
                entityMoveDurationSeconds: 0.4f);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                        {
                            { 20, enemyPrefab },
                        }));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateEnemyUnit(20, sourceCell),
                    },
                    topology);

                presenter.Present(CreateMotionTickResult(CreateEnemyUnit(20, destinationCell), topology, sourceCell, destinationCell, TickEntityMotionKind.Move));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: timingProfile.MoveMotionDurationSeconds,
                    durationSeconds: 0.4f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_InterpolatesAndKeepsEntityMotionPhase()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_InterpolatesAndKeepsEntityMotionPhase");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    sequence: 1,
                                    phase: EnemyJumpPhase.Airborne,
                                    startedWindupThisTick: false,
                                    startedAirborneThisTick: true,
                                    landedThisTick: false,
                                    retryThisTick: false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var landingPosition = GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit);

                Assert.That(view.transform.localPosition.x, Is.GreaterThan(sourcePosition.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(landingPosition.x - 0.001f));
                Assert.That(view.transform.localPosition, Is.Not.EqualTo(sourcePosition));
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_UsesJumpMotionPresentationEase()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedEntity_UsesJumpMotionPresentationEase));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var jumpMotionSnapshot = new EnemyJumpMotionPresentationSnapshot(
                    horizontalHoldBias: 0.12f,
                    apexHoldPower: 4f);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        jumpMotionOverridesByEntityId: new Dictionary<int, EnemyJumpMotionPresentationSnapshot>
                        {
                            { 20, jumpMotionSnapshot },
                        }));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(10, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 10, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 60));

                presenter.UpdatePresentation(0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var landingPosition = GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit);
                var defaultMidpointX = Mathf.Lerp(sourcePosition.x, landingPosition.x, 0.5f);
                var easedHangX = Mathf.Lerp(sourcePosition.x, landingPosition.x, 0.38f);

                Assert.That(view.transform.localPosition.x, Is.LessThan(defaultMidpointX - 0.001f));
                Assert.That(view.transform.localPosition.x, Is.EqualTo(easedHangX).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalBottomFaceVisible()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalBottomFaceVisible));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.JumpDetachedVisibilityStates.TryGetValue(20, out var detachedState), Is.True);
                Assert.That(detachedState.AuthoritativeCell, Is.EqualTo(sourceCell));
                Assert.That(detachedState.AuthoritativeFace, Is.EqualTo(FaceId.Floor));
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsJumpDetachedVisible, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalFrontBackFaceVisible()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_LogicalFrontBackFaceVisible));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Ceiling);
                var sourceCell = new SurfaceCell(FaceId.Back, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Back, 2, 0);

                Assert.That(topology.FrontFace, Is.EqualTo(FaceId.Back));
                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.JumpDetachedVisibilityStates.TryGetValue(20, out var detachedState), Is.True);
                Assert.That(detachedState.AuthoritativeFace, Is.EqualTo(FaceId.Back));
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsJumpDetachedVisible, Is.True);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_InactiveFaceDoesNotVisibleOr()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpAirborneDetachedVisibility_InactiveFaceDoesNotVisibleOr));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var activeTopology = new CubeTopologyState(FaceId.Floor);
                var inactiveTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                Assert.That(activeTopology.IsFaceActive(sourceCell.face), Is.True);
                Assert.That(inactiveTopology.IsFaceActive(sourceCell.face), Is.False);
                presenter.Initialize(binder, boardBounds, activeTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, activeTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: activeTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, activeTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: inactiveTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(activeTopology, inactiveTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(0f);

                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));
                Assert.That(stateStore.JumpDetachedVisibilityStates[20].AuthoritativeFace, Is.EqualTo(FaceId.Floor));
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsVisible, Is.False);
                Assert.That(facts.IsJumpDetachedVisible, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_JumpWindupDoesNotRetainDetachedVisibility()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_JumpWindupDoesNotRetainDetachedVisibility));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));
                presenter.UpdatePresentation(0f);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    tickIndex: 2,
                    new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        Array.Empty<TickPlayerActionPresentationSignal>(),
                        Array.Empty<TickEnemyActionPresentationSignal>(),
                        new[]
                        {
                            new TickEnemyJumpPresentationSignal(
                                20,
                                sequence: 1,
                                phase: EnemyJumpPhase.Windup,
                                startedWindupThisTick: true,
                                startedAirborneThisTick: false,
                                landedThisTick: false,
                                retryThisTick: false,
                                sourceCell: sourceCell,
                                lockedTargetCell: landingCell,
                                presentationTargetCell: landingCell,
                                facing: Direction.Right,
                                windupTicks: 1,
                                landingTick: 4,
                                remainingAirborneTicks: 0,
                                retryCount: 0),
                        },
                        Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologySuspendDoesNotReplaceJumpTrack()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologySuspendDoesNotReplaceJumpTrack));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 1.5f);
                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpTracks.TryGetValue(20, out var originalTrack), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(trackState.JumpTracks.TryGetValue(20, out var frozenTrack), Is.True);
                Assert.That(frozenTrack, Is.SameAs(originalTrack));
                Assert.That(frozenTrack.HasClip, Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.EqualTo(1));

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(trackState.JumpTracks.TryGetValue(20, out var resumedFrozenTrack), Is.True);
                Assert.That(resumedFrozenTrack, Is.SameAs(originalTrack));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologySuspendDoesNotAdvanceFrozenPose()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologySuspendDoesNotAdvanceFrozenPose));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(0.01f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeSuspend = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        suspendedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                AssertPositionApproximately(view.transform.localPosition, poseBeforeSuspend);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpLandingCompletionTopologySuspendDoesNotAdvanceCompletion()
        {
            var rootObject = new GameObject(nameof(EnemyJumpLandingCompletionTopologySuspendDoesNotAdvanceCompletion));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 1, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[] { new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right) },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpFreezePoseSurvivesFrontFaceInactiveVisibility()
        {
            var rootObject = new GameObject(nameof(EnemyJumpFreezePoseSurvivesFrontFaceInactiveVisibility));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(0.01f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeSuspend = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, sourceCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0.1f);

                var stateStore = GetPresentationStateStore(presenter);
                AssertPositionApproximately(view.transform.localPosition, poseBeforeSuspend);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRotationFrameFinalAnimatorStateIsAirborne),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: initialTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.25f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var airborneBeforeRotation = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(initialTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(airborneBeforeRotation));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyRotationDoesNotAdvanceAnimatorNormalizedTime),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);
                var beforeRotation = GetAnimatorNormalizedTime(animator);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                var frozen = GetAnimatorNormalizedTime(animator);
                Assert.That(frozen, Is.EqualTo(beforeRotation).Within(0.0001f));

                for (var i = 0; i < 4; i++)
                {
                    presenter.UpdatePresentation(0.03f);
                    animator.Update(0.03f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyRotationSetsAnimatorPauseOnFirstFrame),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(driver.IsPlaybackSuppressed, Is.True);
                Assert.That(animator.speed, Is.Zero);
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneTopologyTweenDoesNotReachExitTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime));
            var exitController = CreateJumpAirborneExitTimeAnimatorController(
                nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime),
                out var jumpClip,
                out var moveClip);
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneTopologyTweenDoesNotReachExitTime),
                out var jumpReferenceClip,
                exitController);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 1.25f);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);
                var frozen = GetAnimatorNormalizedTime(animator);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                for (var i = 0; i < 5; i++)
                {
                    presenter.UpdatePresentation(0.25f);
                    animator.Update(0.25f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(exitController);
                UnityEngine.Object.DestroyImmediate(jumpClip);
                UnityEngine.Object.DestroyImmediate(moveClip);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneFrontInactiveFreezesAnimatorTimeNotJustStateHash),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var frozen = GetAnimatorNormalizedTime(animator);

                for (var i = 0; i < 3; i++)
                {
                    presenter.UpdatePresentation(0.2f);
                    animator.Update(0.2f);
                    AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneResumeContinuesFromFrozenNormalizedTime),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 4));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 3));
                var frozen = GetAnimatorNormalizedTime(animator);
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                animator.Update(timingProfile.TopologyMotionDurationSeconds);
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 3,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(suspendedTopology, bottomTopology, CubeRotationKind.Backward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                animator.Update(timingProfile.TopologyMotionDurationSeconds);
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                presenter.UpdatePresentation(0f);
                Assert.That(animator.speed, Is.GreaterThan(0f));
                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);

                animator.Update(0.1f);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(GetAnimatorNormalizedTime(animator), Is.GreaterThan(frozen));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime));
            var exitController = CreateJumpAirborneExitTimeAnimatorController(
                nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime),
                out var jumpClip,
                out var moveClip);
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRestoreCannotMaskAdvancedAnimatorTime),
                out var jumpReferenceClip,
                exitController);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile(topologyMotionDurationSeconds: 1.25f);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Update(0.25f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                var frozen = GetAnimatorNormalizedTime(animator);
                animator.Update(1.1f);

                AssertJumpAirborneAnimatorStateAndNormalizedTime(animator, frozen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(exitController);
                UnityEngine.Object.DestroyImmediate(jumpClip);
                UnityEngine.Object.DestroyImmediate(moveClip);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneFrontInactivePausesAirborneNotIdle()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneFrontInactivePausesAirborneNotIdle));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneFrontInactivePausesAirborneNotIdle),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(driver.IsPlaybackSuppressed, Is.True);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneSustainedTickDoesNotFallbackToIdle),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                var signalCount = driver.JumpAirborneSignalCount;

                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                Assert.That(driver.JumpAirborneSignalCount, Is.EqualTo(signalCount));
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneResumeFinalAnimatorStateIsAirborne),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var bottomTopology = new CubeTopologyState(FaceId.Floor);
                var suspendedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, bottomTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, bottomTopology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, bottomTopology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 3));

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: suspendedTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(bottomTopology, suspendedTopology, CubeRotationKind.Forward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 3,
                    finalTopology: bottomTopology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: new TickTopologyMotion(suspendedTopology, bottomTopology, CubeRotationKind.Backward),
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                presenter.UpdatePresentation(0f);

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneRestoreNotOverwrittenByIdleInSameFrame),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneSnapshotCapturedBeforeInactiveFallback),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                animator.Rebind();
                animator.Update(0f);

                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 2,
                    finalTopology: topology,
                    finalEntity: CreateEnemyUnit(20, sourceCell),
                    topologyMotion: null,
                    visibilityChanges: Array.Empty<TickVisibilityChange>(),
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: false,
                    remainingAirborneTicks: 1));

                Assert.That(driver.HasJumpAirborneTopologySuspendSnapshot, Is.True);
                AssertJumpAirborneAnimatorState(animator);
                Assert.That(animator.speed, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState()
        {
            var rootObject = new GameObject(nameof(EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState));
            var enemyPrefab = CreateEnemyJumpAnimatorViewPrefab(
                nameof(EnemyJumpAirborneHiddenRebindRestoresFromJumpDetachedState),
                out var jumpReferenceClip);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 2, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.Present(CreateJumpAirborneTick(
                    tickIndex: 1,
                    finalTopology: topology,
                    finalEntity: WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached),
                    topologyMotion: null,
                    visibilityChanges: new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                    },
                    sourceCell,
                    landingCell,
                    startedAirborneThisTick: true,
                    remainingAirborneTicks: 2));

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var animator = GetRequiredAnimator(view);
                view.gameObject.SetActive(false);
                animator.Rebind();
                animator.Update(0f);

                presenter.UpdatePresentation(0f);

                Assert.That(view.gameObject.activeSelf, Is.True);
                Assert.That(GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.ContainsKey(20), Is.True);
                AssertJumpAirborneAnimatorState(animator);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(jumpReferenceClip);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, landingCell, EntityType.Unit));
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpFallbackLanding_InterpolatesAndBlocksUntilCompletion()
        {
            var rootObject = new GameObject(
                "GameplayTickPresentationCoordinator_JumpFallbackLanding_InterpolatesAndBlocksUntilCompletion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var lockedTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
                var fallbackCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: lockedTargetCell,
                                    presentationTargetCell: lockedTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 4,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeLanding = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, fallbackCell) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: lockedTargetCell,
                                    presentationTargetCell: fallbackCell,
                                    facing: Direction.Right,
                                    landingTick: 5,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                var fallbackPosition = GetProjectedEntityPosition(boardBounds, topology, fallbackCell, EntityType.Unit);
                Assert.That(view.transform.localPosition.x, Is.GreaterThan(poseBeforeLanding.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(fallbackPosition.x - 0.001f));

                var stateStore = GetPresentationStateStore(presenter);
                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsJumpLandingCompletionHeld, Is.True);
                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                AssertPositionApproximately(view.transform.localPosition, fallbackPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpExactLandingWithTopologyMotion_BlocksUntilLandingCompletion()
        {
            var rootObject = new GameObject(
                "GameplayTickPresentationCoordinator_JumpExactLandingWithTopologyMotion_BlocksUntilLandingCompletion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var landingCell = new SurfaceCell(FaceId.Front, 1, 0);

                presenter.Initialize(binder, boardBounds, initialTopology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, initialTopology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, initialTopology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 1,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var poseBeforeLanding = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { CreateEnemyUnit(20, landingCell) },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Cooldown,
                                    false,
                                    false,
                                    true,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: landingCell,
                                    presentationTargetCell: landingCell,
                                    facing: Direction.Right,
                                    landingTick: 2,
                                    remainingAirborneTicks: 0,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                var trackState = GetPresentationTrackState(presenter);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(GetJumpDetachedVisibilityStateCount(presenter), Is.Zero);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds * 0.5f);

                var landingPosition = GetProjectedEntityPosition(boardBounds, rotatedTopology, landingCell, EntityType.Unit);
                AssertPositionApproximately(view.transform.localPosition, poseBeforeLanding);
                Assert.That(Vector3.Distance(view.transform.localPosition, landingPosition), Is.GreaterThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.True);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.True);

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(trackState.JumpLandingCompletionHoldEntityIds.Contains(20), Is.False);
                Assert.That(trackState.JumpTracks.ContainsKey(20), Is.False);
                AssertPositionApproximately(view.transform.localPosition, landingPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var firstTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
                var retryTargetCell = new SurfaceCell(FaceId.Floor, 3, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    true,
                                    false,
                                    false,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: firstTargetCell,
                                    presentationTargetCell: firstTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 3,
                                    remainingAirborneTicks: 2,
                                    retryCount: 0),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var midRetryPose = view.transform.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            new[]
                            {
                                new TickEnemyJumpPresentationSignal(
                                    20,
                                    1,
                                    EnemyJumpPhase.Airborne,
                                    false,
                                    false,
                                    false,
                                    true,
                                    sourceCell: sourceCell,
                                    lockedTargetCell: firstTargetCell,
                                    presentationTargetCell: retryTargetCell,
                                    facing: Direction.Right,
                                    landingTick: 4,
                                    remainingAirborneTicks: 2,
                                    retryCount: 1),
                            },
                            Array.Empty<TickEntityExitPresentationSignal>())));

                AssertPositionApproximately(view.transform.localPosition, midRetryPose);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(timingProfile.SimulationTickIntervalSeconds);

                var retryTargetPosition = GetProjectedEntityPosition(boardBounds, topology, retryTargetCell, EntityType.Unit);
                Assert.That(view.transform.localPosition.x, Is.GreaterThan(midRetryPose.x + 0.001f));
                Assert.That(view.transform.localPosition.x, Is.LessThan(retryTargetPosition.x + 0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_NonJumpDetach_StillHides()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_NonJumpDetach_StillHides");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
                var timingProfile = CreateTimingProfile();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_ItemConsumeEffect_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float playerMoveOverrideSeconds = 0.4f;
                const float itemConsumeEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: itemConsumeEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var itemCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var motionAuthoring = playerViewPrefab.GetComponent<UnitLocomotionPresentationAuthoring>();
                Assert.That(motionAuthoring, Is.Not.Null);
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "moveMotionDurationSeconds",
                    playerMoveOverrideSeconds);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                        CreateBox(20, itemCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, destinationCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                            },
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, itemCell, topology, Direction.Right),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, itemCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.ItemConsume,
                                    itemCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(registry.TryGetView(20, out var itemView), Is.True);
                Assert.That(itemView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(itemConsumeEffectDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                Assert.That(playerView.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float pushOverrideSeconds = 0.45f;
                const float boxDestroyEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: boxDestroyEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(20, boxCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                var motionAuthoring = boxView.gameObject.AddComponent<EntityMotionPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(
                    motionAuthoring,
                    "pushMotionDurationSeconds",
                    pushOverrideSeconds);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, boxCell, topology, Direction.Right),
                                new TickVisibilityChange(20, TickVisibilityChangeKind.Remove, boxCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.BoxDestroy,
                                    boxCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(boxView.gameObject.activeSelf, Is.False);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_KeepsOriginalViewVisibleUntilMotionCompletes()
        {
            var rootObject = new GameObject(
                "GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_KeepsOriginalViewVisibleUntilMotionCompletes");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, sourceCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(30, out var boxView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Push,
                                    sourceCell,
                                    destroyTileCell),
                            },
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    30,
                                    TickEntityExitCause.BoxDestroy,
                                    destroyTileCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    timing: EntityExitPresentationTiming.AfterEntityMotion),
                            })));

                Assert.That(boxView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destroyTileCell, EntityType.Box);
                var travel = destinationPosition - sourcePosition;
                var progress = Vector3.Dot(
                    boxView.transform.localPosition - sourcePosition,
                    travel.normalized);
                Assert.That(boxView.gameObject.activeSelf, Is.True);
                Assert.That(progress, Is.GreaterThan(0.001f));
                Assert.That(progress, Is.LessThan(travel.magnitude - 0.001f));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f + 0.01f);

                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_WithoutMotionFallsBackToImmediateHide()
        {
            var rootObject = new GameObject(
                "GameplayTickViewPresenter_AfterEntityMotionBoxDestroy_WithoutMotionFallsBackToImmediateHide");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);
                var boxCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(30, boxCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(30, out var boxView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    30,
                                    TickEntityExitCause.BoxDestroy,
                                    boxCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Box,
                                    timing: EntityExitPresentationTiming.AfterEntityMotion),
                            })));

                Assert.That(boxView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockDestroyRespawn_IsNonBlockingAndRebindsLiveViewToRespawnPose()
        {
            var rootObject = new GameObject(
                nameof(GameplayTickViewPresenter_MoonBlockDestroyRespawn_IsNonBlockingAndRebindsLiveViewToRespawnPose));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var generatorCell = new SurfaceCell(FaceId.Floor, 3, 0);
                var vfxState = new RecordingDestroyShrinkVfxStateExtension();
                vfxState.SetState(40, 9001, DestroyShrinkVfxSequenceState.ScheduledDelay);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachPresentationExtension(vfxState);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(40, sourceCell),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateBox(40, generatorCell) },
                        topology,
                        CreateMoonBlockDestroyAndGeneratedPresentationData(
                            entityId: 40,
                            sourceCell,
                            destroyTileCell,
                            generatorCell,
                            topology,
                            presentationSeed: 9001)));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(40, out var moonBlockView), Is.True);
                Assert.That(moonBlockView.gameObject.activeSelf, Is.True);
                AssertPositionApproximately(
                    moonBlockView.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, generatorCell, EntityType.Box));
                Assert.That(
                    Vector3.Distance(
                        moonBlockView.transform.localPosition,
                        GetProjectedEntityPosition(boardBounds, topology, destroyTileCell, EntityType.Box)),
                    Is.GreaterThan(0.1f));
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(1));
                Assert.That(moonBlockView.GetComponent<MoonBlockEmergencePresentationDriver>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockDestroy_UsesTokenGhostSourceWithoutBlockingLiveEmergence()
        {
            var rootObject = new GameObject(
                nameof(GameplayTickViewPresenter_MoonBlockDestroy_UsesTokenGhostSourceWithoutBlockingLiveEmergence));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var generatorCell = new SurfaceCell(FaceId.Floor, 3, 0);
                var vfxState = new RecordingDestroyShrinkVfxStateExtension();
                vfxState.SetState(40, 9002, DestroyShrinkVfxSequenceState.ScheduledDelay);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachPresentationExtension(vfxState);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(40, sourceCell),
                    },
                    topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateBox(40, generatorCell) },
                        topology,
                        CreateMoonBlockDestroyAndGeneratedPresentationData(
                            entityId: 40,
                            sourceCell,
                            destroyTileCell,
                            generatorCell,
                            topology,
                            presentationSeed: 9002)));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);

                Assert.That(registry.TryGetView(40, out var moonBlockView), Is.True);
                AssertPositionApproximately(
                    moonBlockView.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, generatorCell, EntityType.Box));
                var driver = moonBlockView.GetComponent<MoonBlockEmergencePresentationDriver>();
                Assert.That(driver, Is.Not.Null);
                Assert.That(driver.DebugPlayCount, Is.EqualTo(1));
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(1));

                vfxState.SetState(40, 9002, DestroyShrinkVfxSequenceState.SourceCloneCaptured);
                presenter.UpdatePresentation(0f);

                AssertPositionApproximately(
                    moonBlockView.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, generatorCell, EntityType.Box));
                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(0));
                Assert.That(presenter.HasBlockingPresentation, Is.False);

                vfxState.SetState(40, 9002, DestroyShrinkVfxSequenceState.Playing);
                presenter.UpdatePresentation(0.01f);

                AssertPositionApproximately(
                    moonBlockView.transform.localPosition,
                    GetProjectedEntityPosition(boardBounds, topology, generatorCell, EntityType.Box));
                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(0));

                vfxState.SetState(40, 9002, DestroyShrinkVfxSequenceState.Completed);
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_MoonBlockDestroyGhost_CleansUpOnViewUnregister()
        {
            var rootObject = new GameObject(
                nameof(GameplayTickViewPresenter_MoonBlockDestroyGhost_CleansUpOnViewUnregister));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = CreateTimingProfile();
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var generatorCell = new SurfaceCell(FaceId.Floor, 3, 0);
                var vfxState = new RecordingDestroyShrinkVfxStateExtension();
                vfxState.SetState(40, 9003, DestroyShrinkVfxSequenceState.ScheduledDelay);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachPresentationExtension(vfxState);
                presenter.PresentInitial(new[] { CreateBox(40, sourceCell) }, topology);
                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateBox(40, generatorCell) },
                        topology,
                        CreateMoonBlockDestroyAndGeneratedPresentationData(
                            entityId: 40,
                            sourceCell,
                            destroyTileCell,
                            generatorCell,
                            topology,
                            presentationSeed: 9003)));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);

                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.EqualTo(1));

                Assert.That(registry.Unregister(40), Is.True);

                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.Zero);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickViewPresenter_TopologyTransitionClearsMoonBlockGhostAndKeepsTopologyBlocking()
        {
            var rootObject = new GameObject(
                nameof(GameplayTickViewPresenter_TopologyTransitionClearsMoonBlockGhostAndKeepsTopologyBlocking));

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(registry.transform));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0));
                var sourceTopology = new CubeTopologyState(FaceId.Floor);
                var destinationTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destroyTileCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var generatorCell = new SurfaceCell(FaceId.Floor, 3, 0);
                var vfxState = new RecordingDestroyShrinkVfxStateExtension();
                vfxState.SetState(40, 9100, DestroyShrinkVfxSequenceState.ScheduledDelay);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    sourceTopology,
                    1f,
                    CreateTimingProfile());
                presenter.AttachPresentationExtension(vfxState);
                presenter.PresentInitial(new[] { CreateBox(40, sourceCell) }, sourceTopology);

                presenter.Present(
                    CreateTickResult(
                        1,
                        new[] { CreateBox(40, generatorCell) },
                        destinationTopology,
                        CreateMoonBlockDestroyAndGeneratedPresentationData(
                            entityId: 40,
                            sourceCell,
                            destroyTileCell,
                            generatorCell,
                            sourceTopology,
                            presentationSeed: 9100,
                            topologyMotion: new TickTopologyMotion(
                                sourceTopology,
                                destinationTopology,
                                CubeRotationKind.Forward))));

                Assert.That(presenter.ActiveMoonBlockDestructionGhostCount, Is.Zero);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.CurrentTilePresentationRequests, Has.Count.EqualTo(1));
                Assert.That(
                    presenter.CurrentTilePresentationRequests[0].RequestKind,
                    Is.EqualTo(TilePresentationRequestKind.MoonBlockGenerated));
                Assert.That(
                    presenter.DebugCaptureEntityPresentationLifecycle(40, 0f)
                        .RetainedLocalTargetPosesContainsEntityId,
                    Is.True);
                Assert.That(registry.TryGetView(40, out var moonBlockView), Is.True);
                AssertPositionApproximately(
                    moonBlockView.transform.localPosition,
                    GetProjectedTransitionEntityPosition(
                        boardBounds,
                        sourceTopology,
                        destinationTopology,
                        generatorCell,
                        EntityType.Box));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_FlipDestroySelfImpactTransient_HidesAuthoritativeView_WithoutCommittedMove()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_FlipDestroySelfImpactTransient_HidesAuthoritativeView_WithoutCommittedMove");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float flipMotionDurationSeconds = 0.2f;
                const float boxDestroyEffectDurationSeconds = 0.18f;
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    moveMotionDurationSeconds: 0.1f,
                    pushMotionDurationSeconds: 0.05f,
                    topologyMotionDurationSeconds: 0.05f,
                    flipMotionDurationSeconds: flipMotionDurationSeconds,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8,
                    itemConsumeEffectDurationSeconds: 0.25f,
                    boxDestroyEffectDurationSeconds: boxDestroyEffectDurationSeconds);
                var boardBounds = new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, -1, 0);
                var impactCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateBox(20, sourceCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(20, out var boxView), Is.True);
                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    20,
                                    TickEntityExitCause.BoxDestroy,
                                    sourceCell,
                                    topology,
                                    Direction.Left,
                                    EntityType.Box,
                                    sourceActorEntityId: 10),
                            },
                            impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                            flipImpactSignals: new[]
                            {
                                new FlipImpactPresentationSignal(
                                    sourceActionPlanId: 1,
                                    boxEntityId: 20,
                                    impactTargetEntityId: 30,
                                    actorEntityId: 10,
                                    sourceCell,
                                    impactCell,
                                    topology,
                                    Direction.Left,
                                    Direction.Right,
                                    FlipImpactPresentationDisposition.DestroySelf),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(boxView.gameObject.activeSelf, Is.False);

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds + 0.01f);

                presenter.UpdatePresentation((flipMotionDurationSeconds - boxDestroyEffectDurationSeconds) + 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_PlayerAcceptedHit_DoesNotSpawnLegacyHitEffect()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerAcceptedHit_DoesNotSpawnLegacyHitEffect");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerAcceptedHit_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitEffectDurationSeconds", 0.2f);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab: playerViewPrefab));

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 2),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            Array.Empty<TickVisibilityChange>(),
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));

                presenter.UpdatePresentation(0.21f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_SummonWindupWarning_MissingAuthoringNoOps()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_SummonWindupWarning_MissingAuthoringNoOps");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));

                presenter.Initialize(binder, boardBounds, topology, 1f, CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);

                Assert.DoesNotThrow(
                    () => presenter.Present(
                        CreateTickResult(
                            tickIndex: 1,
                            new[] { CreateEnemyUnit(20, sourceCell) },
                            topology,
                            CreateSummonWindupPresentationData(
                                new[] { CreateSummonWindupWarningSignal(20, sourceCell, topology, tickIndex: 1) }))));

                Assert.That(CountDescendantsByNamePrefix(rootObject.transform, "SummonWindupWarning_20"), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_GravityFieldContinuousVisualState_RespectsArchitectureBoundaries()
        {
            var hostRuntimeDirectory = Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Host/Runtime");
            var loopRuntimeDirectory = Path.Combine(Application.dataPath, "_Features/Gameplay/Gameplay_Loop/Runtime");
            var gravityFieldVisualConsumers = new[]
            {
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualPresentationController.cs"),
                Path.Combine(hostRuntimeDirectory, "IGravityFieldVisualTarget.cs"),
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualTargetView.cs"),
                Path.Combine(hostRuntimeDirectory, "GravityFieldLockedTargetVisualTargetView.cs"),
            };

            foreach (var path in gravityFieldVisualConsumers)
            {
                var source = File.ReadAllText(path);

                Assert.That(source, Does.Not.Contain("CreateSnapshot"), path);
                Assert.That(source, Does.Not.Contain("WorldState"), path);
                Assert.That(source, Does.Not.Contain("Play3D"), path);
                Assert.That(source, Does.Not.Contain("StagePresentationDefinition"), path);
            }

            var gravityFieldVisualController = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualPresentationController.cs"));
            var gravityFieldTargetView = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldVisualTargetView.cs"));
            var gravityFieldLockedTargetView = File.ReadAllText(
                Path.Combine(hostRuntimeDirectory, "GravityFieldLockedTargetVisualTargetView.cs"));
            Assert.That(gravityFieldVisualController, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("GameplayCubeProjector"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("GameplayCubeProjector"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("TryProject"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("TryProject"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain(".material"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain(".material"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain(".materials"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain(".materials"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("sharedMaterial"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("sharedMaterial"));
            Assert.That(gravityFieldTargetView, Does.Not.Contain("sharedMaterials"));
            Assert.That(gravityFieldLockedTargetView, Does.Not.Contain("sharedMaterials"));

            var coordinatorSource = File.ReadAllText(Path.Combine(hostRuntimeDirectory, "GameplayTickPresentationCoordinator.cs"));
            var presenterSource = File.ReadAllText(Path.Combine(hostRuntimeDirectory, "GameplayTickViewPresenter.cs"));
            Assert.That(coordinatorSource, Does.Not.Contain("WorldState.CreateSnapshot"));
            Assert.That(presenterSource, Does.Not.Contain("WorldState.CreateSnapshot"));

            var tickPipelineSource = File.ReadAllText(Path.Combine(loopRuntimeDirectory, "TickPipeline.cs"));
            Assert.That(tickPipelineSource, Does.Not.Contain("GravityFieldVisualTargetView"));
            Assert.That(tickPipelineSource, Does.Not.Contain("GravityFieldLockedTargetVisualTargetView"));
            Assert.That(tickPipelineSource, Does.Not.Contain("IGravityFieldContinuousVisualTarget"));
            Assert.That(tickPipelineSource, Does.Not.Contain("IGravityFieldLockedTargetVisualTarget"));
            Assert.That(tickPipelineSource, Does.Not.Contain("Play3D"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerDeathTick_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitEffectDurationSeconds", 0.2f);

                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab: playerViewPrefab));

                presenter.Initialize(binder, boardBounds, topology, 1f, timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, playerCell, topology, Direction.Right),
                            },
                            Array.Empty<TickTransitionVisibilityChange>(),
                            Array.Empty<TickPlayerActionPresentationSignal>(),
                            Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            new[]
                            {
                                new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1),
                            },
                            Array.Empty<TickEnemyActionPresentationSignal>(),
                            Array.Empty<TickEnemyJumpPresentationSignal>(),
                            new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    playerCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
                            }),
                        string.Empty,
                        TickTrace.Empty));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_Settles_AndClearsOnRespawn()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_Settles_AndClearsOnRespawn");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickPresentationCoordinator_PlayerDeathBackOffset_UsesModelRootOnly_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var enemyCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                        CreateEnemyUnit(20, enemyCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var rootPositionBeforeDeath = playerView.transform.localPosition;
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 0),
                            CreateEnemyUnit(20, enemyCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 20,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: true,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                Assert.That(
                    Vector3.Distance(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath),
                    Is.GreaterThan(0.001f));
                Assert.That(
                    GetPlayerDeathDisplacementTrackState(presenter, 10),
                    Is.EqualTo(PlayerDeathDisplacementTrackState.Animating));
                Assert.That(GetPresentationActivityInspector(presenter).HasActiveEntityPresentationClips(), Is.True);

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds);

                Assert.That(
                    GetPlayerDeathDisplacementTrackState(presenter, 10),
                    Is.EqualTo(PlayerDeathDisplacementTrackState.Settled));
                Assert.That(GetPresentationActivityInspector(presenter).HasActiveEntityPresentationClips(), Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell),
                            CreateEnemyUnit(20, enemyCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, playerCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                AssertPositionApproximately(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath);
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttackerProjectsAlongSurfaceTangent()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttackerProjectsAlongSurfaceTangent");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickPresentationCoordinator_PlayerDeathBackOffset_MissingAttacker_PlayerPrefab");
            var cameraObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathBackOffset_OutputCamera");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var outputCamera = cameraObject.AddComponent<Camera>();
                outputCamera.transform.SetParent(rootObject.transform, worldPositionStays: false);
                outputCamera.transform.localPosition = new Vector3(0.35f, 0.5f, -4f);
                outputCamera.transform.localRotation = Quaternion.identity;

                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Front);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var playerCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.AttachOutputCamera(outputCamera);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, playerCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var rootPositionBeforeDeath = playerView.transform.localPosition;
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, playerCell, hp: 0),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 999,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: true,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));

                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                var offset = playerView.ModelRoot.localPosition - modelRootPositionBeforeDeath;
                AssertPositionApproximately(playerView.transform.localPosition, rootPositionBeforeDeath);
                Assert.That(offset.sqrMagnitude, Is.GreaterThan(0.000001f));
                Assert.That(float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z), Is.False);

                var surfaceNormal = -(playerView.transform.localRotation * Vector3.forward).normalized;
                var tangentAlignment = Mathf.Abs(Vector3.Dot(offset.normalized, surfaceNormal));
                Assert.That(tangentAlignment, Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerActionHold_KeepsEntityMotionPhaseUntilHoldCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerActionHold_KeepsEntityMotionPhaseUntilHoldCompletes");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerActionHold_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                    },
                    topology);
                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);
                var holdDurationSeconds = driver.PushPresentationDurationSeconds;
                Assert.That(holdDurationSeconds, Is.GreaterThan(0.01f));

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.Push,
                                    1,
                                    startedThisTick: true,
                                    completedThisTick: false,
                                    canceledThisTick: false),
                            },
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.Present(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, sourceCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: new[]
                            {
                                new TickPlayerActionPresentationSignal(
                                    10,
                                    PlayerActionKind.None,
                                    0,
                                    startedThisTick: false,
                                    completedThisTick: true,
                                    canceledThisTick: false),
                            },
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(holdDurationSeconds + 0.05f);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerRespawn_SpawnVisibilityShowsExistingPlayerViewAgain()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerRespawn_SpawnVisibilityShowsExistingPlayerViewAgain");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerRespawn_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var spawnCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.gameObject.activeSelf, Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(10f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryGetView(10, out var respawnedView), Is.True);
                Assert.That(respawnedView, Is.SameAs(playerView));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_Completes_ClearsDeathAnimationState");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "GameplayTickViewPresenter_PlayerRespawnBeforeDeathHide_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var topology = new CubeTopologyState(FaceId.Floor);
                var spawnCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.TryGetComponent<PlayerAnimatorDriver>(out var driver), Is.True);

                presenter.Present(
                    new TickResult(
                        1,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        Array.Empty<EntityState>(),
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    spawnCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
                            },
                            impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                            flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                            playerDeathHoldSignals: new[]
                            {
                                new TickPlayerDeathHoldPresentationSignal(
                                    10,
                                    startTick: 1,
                                    eligibleTick: 2,
                                    remainingTicks: 1,
                                    startedThisTick: true),
                            }),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));

                presenter.Present(
                    new TickResult(
                        2,
                        Array.Empty<TickPhase>(),
                        Array.Empty<string>(),
                        MovementPhaseResult.Empty,
                        AttackPhaseResult.Empty,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        Array.Empty<string>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, topology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>()),
                        string.Empty,
                        TickTrace.Empty));

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset()
        {
            var rootObject = new GameObject("DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "DelayedRespawn_ReusedPlayerViewRevealsAfterTopologyReset_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var hiddenTopology = new CubeTopologyState(FaceId.Back);
                var respawnTopology = new CubeTopologyState(FaceId.Front);
                var spawnCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                    initialTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, spawnCell),
                    },
                    initialTopology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        Array.Empty<EntityState>(),
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, spawnCell, initialTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(10f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(hiddenTopology, initialTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(playerView.gameObject.activeSelf, Is.False);
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);
                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, respawnTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);
                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        new[]
                        {
                            CreatePlayerUnit(10, spawnCell),
                        },
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, spawnCell, respawnTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryGetView(10, out var respawnedView), Is.True);
                Assert.That(respawnedView, Is.SameAs(playerView));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn()
        {
            var rootObject = new GameObject("DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab(
                "DelayedRespawn_ClearsDeathAnimationAndDisplacementOnSpawn_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var hiddenTopology = new CubeTopologyState(FaceId.Back);
                var respawnTopology = new CubeTopologyState(FaceId.Front);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var deathCell = new SurfaceCell(FaceId.Back, 0, 0);
                var respawnCell = new SurfaceCell(FaceId.Front, 0, 0);
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10,
                        playerViewPrefab));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    hiddenTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, deathCell),
                    },
                    hiddenTopology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(playerView.TryGetComponent<PlayerAnimatorDriver>(out var driver), Is.True);
                var modelRootPositionBeforeDeath = playerView.ModelRoot.localPosition;

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 1,
                        new[]
                        {
                            CreatePlayerUnit(10, deathCell, hp: 0),
                        },
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: new[]
                            {
                                new TickPlayerDeathPresentationSignal(
                                    10,
                                    didDieThisTick: true,
                                    sourceEntityId: 0,
                                    fallbackFacing: Direction.Right,
                                    resolvedDamageSourceAvailable: false,
                                    damageAmountAtFatalHit: 1,
                                    deathDirectionHintKind: DeathDirectionHintKind.Unknown),
                            },
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.PlayerDeathDisplacementDurationSeconds * 0.5f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Death));
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.True);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 2,
                        Array.Empty<EntityState>(),
                        hiddenTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Remove, deathCell, hiddenTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    deathCell,
                                    hiddenTopology,
                                    Direction.Right,
                                    EntityType.Unit),
                            })));
                presenter.UpdatePresentation(0f);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 3,
                        Array.Empty<EntityState>(),
                        initialTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(hiddenTopology, initialTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 4,
                        Array.Empty<EntityState>(),
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, respawnTopology, CubeRotationKind.Forward),
                            visibilityChanges: Array.Empty<TickVisibilityChange>(),
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(playerView.gameObject.activeSelf, Is.False);

                presenter.Present(
                    CreateTickResult(
                        tickIndex: 5,
                        new[]
                        {
                            CreatePlayerUnit(10, respawnCell),
                        },
                        respawnTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(10, TickVisibilityChangeKind.Spawn, respawnCell, respawnTopology, Direction.Right),
                            },
                            transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                            playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                            playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                            playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                            playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                            enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>())));
                presenter.UpdatePresentation(0f);

                Assert.That(playerView.gameObject.activeSelf, Is.True);
                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.LastCrossFadedStateName, Is.EqualTo("Idle"));
                AssertPositionApproximately(playerView.ModelRoot.localPosition, modelRootPositionBeforeDeath);
                Assert.That(HasPlayerDeathDisplacementTrack(presenter, 10), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_PresentInitialStackedUnits_UsesSharedCenterPoseWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentInitialStackedUnits_UsesSharedCenterPoseWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var stackedCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, stackedCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = GetProjectedEntityPosition(boardBounds, topology, stackedCell, EntityType.Unit);
                var playerPosition = playerView.transform.localPosition;
                var enemyPosition = enemyView.transform.localPosition;

                AssertPositionApproximately(playerPosition, center);
                AssertPositionApproximately(enemyPosition, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_MoveIntoOccupiedCell_KeepsSharedCenterPoseWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveIntoOccupiedCell_KeepsSharedCenterPoseWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = CreateTimingProfile();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var stackedCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, sourceCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                presenter.Present(
                    CreateMotionTickResult(
                        new[]
                        {
                            CreatePlayerUnit(10, stackedCell),
                            CreateEnemyUnit(20, stackedCell),
                        },
                        topology,
                        new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, stackedCell)));
                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = GetProjectedEntityPosition(boardBounds, topology, stackedCell, EntityType.Unit);
                var playerPosition = playerView.transform.localPosition;
                var enemyPosition = enemyView.transform.localPosition;

                AssertPositionApproximately(playerPosition, center);
                AssertPositionApproximately(enemyPosition, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayCenteredWhenOffsetsDisabled()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayCenteredWhenOffsetsDisabled");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var stackedCell = new SurfaceCell(FaceId.Ceiling, 0, 0);
                var projector = new GameplayCubeProjector(boardBounds, 1f);

                Assert.That(projector.TryProjectEntityCell(stackedCell, topology, EntityType.Unit, out var projectedPose), Is.True);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, stackedCell),
                        CreateEnemyUnit(20, stackedCell),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                Assert.That(registry.TryGetView(20, out var enemyView), Is.True);

                var center = projectedPose.LocalPosition;
                var playerOffset = playerView.transform.localPosition - center;
                var enemyOffset = enemyView.transform.localPosition - center;

                Assert.That(playerOffset.sqrMagnitude, Is.LessThan(0.000001f));
                Assert.That(enemyOffset.sqrMagnitude, Is.LessThan(0.000001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedFrontEnemy_StoresFrontFactsWithInactiveSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedFrontEnemy_StoresFrontFactsWithInactiveSemantic");
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_CommittedFrontEnemy_FloatingPrefab",
                0.8f,
                0.8f);

            try
            {
                var floatingDriver = enemyPrefab.gameObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(floatingDriver, "target", enemyPrefab.ModelRoot);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.IsTransitionOnlyVisible, Is.False);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Front));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
                Assert.That(view.GetComponent<EnemyFloatingPresentationDriver>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_AppliesEnemyVisualSemanticStateToPresentationDrivers()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_AppliesEnemyVisualSemanticStateToPresentationDrivers));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayEntityPresentationApplier_SemanticDriverPrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.gameObject.AddComponent<RecordingEnemySemanticPresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var recordingDriver = view.GetComponent<RecordingEnemySemanticPresentationDriver>();
                Assert.That(recordingDriver, Is.Not.Null);
                Assert.That(recordingDriver.ApplyCount, Is.GreaterThan(0));
                Assert.That(recordingDriver.LastState.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(recordingDriver.LastState.ShouldPauseAutonomousPresentation, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_SummonScalePulseFreezesDuringFrontFaceInactive()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_SummonScalePulseFreezesDuringFrontFaceInactive));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_SummonScalePulseFreezePrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.ModelRoot.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                enemyPrefab.gameObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemySummonPresentationData(new[]
                    {
                        new TickEnemySummonPresentationSignal(
                            20,
                            EnemySummonPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 10),
                    })));
                presenter.UpdatePresentation(0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var modelRoot = view.ModelRoot;
                var inactiveController = view.GetComponent<EnemyInactiveVisualController>();
                Assert.That(inactiveController, Is.Not.Null);
                var activeScale = modelRoot.localScale;
                Assert.That(activeScale.x, Is.GreaterThan(0.4f));

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateEnemyUnit(20, frontCell) },
                    topology,
                    TickPresentationData.Empty));
                presenter.UpdatePresentation(0.5f);

                Assert.That(modelRoot.localScale.x, Is.EqualTo(activeScale.x).Within(0.0001f));
                Assert.That(modelRoot.localScale.y, Is.EqualTo(activeScale.y).Within(0.0001f));
                Assert.That(inactiveController.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(inactiveController.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));

                var revealBefore = inactiveController.CurrentInactiveNoiseReveal;
                inactiveController.AdvanceInactiveNoiseReveal(0.1f);
                Assert.That(inactiveController.CurrentInactiveNoiseReveal, Is.GreaterThan(revealBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayTickPresentationCoordinator_SummonCanceled_NormalizesScalePulse()
        {
            var rootObject = new GameObject(nameof(GameplayTickPresentationCoordinator_SummonCanceled_NormalizesScalePulse));
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_SummonScalePulseCancelPrefab",
                0.8f,
                0.8f);

            try
            {
                enemyPrefab.ModelRoot.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                enemyPrefab.gameObject.AddComponent<EnemySummonScalePulsePresentationDriver>();

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemySummonPresentationData(new[]
                    {
                        new TickEnemySummonPresentationSignal(
                            20,
                            EnemySummonPresentationPhase.WindupStarted,
                            startTick: 1,
                            executeTick: 2,
                            durationTicks: 10),
                    })));
                presenter.UpdatePresentation(1.7f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var modelRoot = view.ModelRoot;
                Assert.That(modelRoot.localScale.x, Is.Not.EqualTo(0.4f).Within(0.0001f));

                presenter.Present(CreateTickResult(
                    2,
                    new[] { CreateEnemyUnit(20, bottomCell) },
                    topology,
                    CreateEnemySummonPresentationData(new[]
                    {
                        new TickEnemySummonPresentationSignal(
                            20,
                            EnemySummonPresentationPhase.Canceled,
                            startTick: 2,
                            executeTick: 2,
                            durationTicks: 0),
                    })));

                Assert.That(modelRoot.localScale.x, Is.EqualTo(0.4f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        [TestCase(FaceId.Front, FaceId.Ceiling, GameplayProjectedFaceSlot.Top)]
        [TestCase(FaceId.Ceiling, FaceId.Back, GameplayProjectedFaceSlot.Back)]
        [TestCase(FaceId.Back, FaceId.Floor, GameplayProjectedFaceSlot.Bottom)]
        public void GameplayTickPresentationCoordinator_CommittedVisualFrontEnemy_StoresInactiveSemantic(FaceId bottomFace, FaceId enemyFace, GameplayProjectedFaceSlot expectedProjectedSlot)
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedVisualFrontEnemy_StoresInactiveSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(bottomFace);
                var enemyCell = new SurfaceCell(enemyFace, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, enemyCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(expectedProjectedSlot));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>().CurrentActivityState,
                    Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedTopEnemy_SuspendsFloatingWithoutFrontInactiveSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedTopEnemy_SuspendsFloatingWithoutFrontInactiveSemantic");
            var enemyPrefab = CreateEnemyViewPrefab(
                "GameplayTickPresentationCoordinator_CommittedTopEnemy_FloatingPrefab",
                0.8f,
                0.8f);

            try
            {
                var floatingDriver = enemyPrefab.gameObject.AddComponent<EnemyFloatingPresentationDriver>();
                PlayerViewPrefabTestUtility.SetSerializedField(floatingDriver, "target", enemyPrefab.ModelRoot);

                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = CreateEnemyPrefabBinder(registry, enemyPrefab);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var topCell = new SurfaceCell(FaceId.Ceiling, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, topCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                if (stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts))
                {
                    Assert.That(facts.IsEnemy, Is.True);
                    Assert.That(facts.IsVisible, Is.True);
                    Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Top));
                    Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                    Assert.That(facts.IsOnVisualFrontFace, Is.False);
                }

                if (stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic))
                {
                    Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                    Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                    Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
                }

                if (registry.TryGetView(20, out var view))
                {
                    Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                    Assert.That(view.GetComponent<EnemyFloatingPresentationDriver>(), Is.Not.Null);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedPhysicalFrontBottomEnemy_StoresNormalSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedPhysicalFrontBottomEnemy_StoresNormalSemantic");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Front);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsVisible, Is.True);
                Assert.That(facts.IsCommittedVisible, Is.True);
                Assert.That(facts.ProjectedSlot, Is.EqualTo(GameplayProjectedFaceSlot.Front));
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedFrontRoleEnemyWithNoneAiMode_StoresInactiveEnemyFacts()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedFrontRoleEnemyWithNoneAiMode_StoresInactiveEnemyFacts");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var frontCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, frontCell, EnemyAiMode.None) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.True);
                Assert.That(facts.IsOnVisualFrontFace, Is.True);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_CommittedBottomRoleEnemyWithNoneAiMode_StoresUnsuppressedEnemyFacts()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedBottomRoleEnemyWithNoneAiMode_StoresUnsuppressedEnemyFacts");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new DefaultGameplayEntityViewFactory(
                        registry.transform,
                        1f,
                        playerEntityId: 10));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var bottomCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    CreateTimingProfile());
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, bottomCell, EnemyAiMode.None) }, topology);

                var stateStore = GetPresentationStateStore(presenter);

                Assert.That(stateStore.EnemyVisualFactsByEntityId.TryGetValue(20, out var facts), Is.True);
                Assert.That(facts.IsEnemy, Is.True);
                Assert.That(facts.IsGameplayAutonomySuppressed, Is.False);
                Assert.That(facts.IsOnVisualFrontFace, Is.False);

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
                Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyAnimatorDriver>().IsPlaybackSuppressed, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_UnchangedEnemy_SkipsExpensiveApply()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_UnchangedEnemy_SkipsExpensiveApply));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out var registry, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.Zero);
                Assert.That(diagnostics.SignatureUnchangedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ChangedEnemyVisualState_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ChangedEnemyVisualState_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out var registry, out var topology);
                presenter.PresentInitial(
                    new[] { CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    topology);
                presenter.UpdatePresentation(0f);

                var frontEnemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Front, 0, 0));
                presenter.Present(CreateTickResult(1, new[] { frontEnemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.SignatureChangedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>().CurrentActivityState,
                    Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ChangedPose_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ChangedPose_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(
                    rootObject,
                    out var registry,
                    out var topology,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)));
                presenter.PresentInitial(
                    new[] { CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)) },
                    topology);
                presenter.UpdatePresentation(0f);

                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var movedEnemy = CreateEnemyUnit(20, destinationCell);
                presenter.Present(CreateTickResult(1, new[] { movedEnemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(registry.TryGetView(20, out var view), Is.True);
                AssertPositionApproximately(
                    view.transform.localPosition,
                    GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)),
                        topology,
                        destinationCell,
                        EntityType.Unit));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_ActiveMotion_DoesNotSkip()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_ActiveMotion_DoesNotSkip));

            try
            {
                var presenter = CreatePrimitivePresenter(
                    rootObject,
                    out _,
                    out var topology,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 0)));
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, sourceCell) }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateMotionTickResult(
                    CreateEnemyUnit(20, destinationCell),
                    topology,
                    sourceCell,
                    destinationCell,
                    TickEntityMotionKind.Move));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_NewlyBoundEntity_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_NewlyBoundEntity_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));

                presenter.Present(CreateTickResult(1, new[] { enemy }, topology, TickPresentationData.Empty));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_VisibilityChanged_Applies()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_VisibilityChanged_Applies));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var cell = new SurfaceCell(FaceId.Floor, 0, 0);
                presenter.PresentInitial(new[] { CreateEnemyUnit(20, cell) }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateTickResult(
                    1,
                    Array.Empty<EntityState>(),
                    topology,
                    CreateVisibilityPresentationData(new TickVisibilityChange(
                        20,
                        TickVisibilityChangeKind.Remove,
                        cell,
                        topology,
                        Direction.Right))));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_PresentationEventTarget_DoesNotSkip()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_PresentationEventTarget_DoesNotSkip));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                presenter.PresentInitial(new[] { enemy }, topology);
                presenter.UpdatePresentation(0f);

                presenter.Present(CreateTickResult(
                    1,
                    new[] { enemy },
                    topology,
                    CreateEnemyDamagePresentationData(new TickEnemyDamagePresentationSignal(20, true, 1))));

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemySkippedCount, Is.Zero);
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ActiveBypassCount, Is.EqualTo(1));
                Assert.That(diagnostics.SignatureUnchangedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_PlayerPath_Preserved()
        {
            var rootObject = new GameObject(nameof(GameplayEntityPresentationApplier_PlayerPath_Preserved));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var player = CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0));
                presenter.PresentInitial(new[] { player }, topology);

                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.EnemyCandidateCount, Is.Zero);
                Assert.That(diagnostics.SkippedCount, Is.Zero);
                Assert.That(diagnostics.PlayerExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void PresentationDirtyOnly_Diagnostics_CountsSkippedAndExecuted()
        {
            var rootObject = new GameObject(nameof(PresentationDirtyOnly_Diagnostics_CountsSkippedAndExecuted));

            try
            {
                var presenter = CreatePrimitivePresenter(rootObject, out _, out var topology);
                var entities = new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0)),
                };
                presenter.PresentInitial(entities, topology);

                presenter.UpdatePresentation(0f);

                var diagnostics = presenter.DebugLastEntityPresentationApplyDiagnostics;
                Assert.That(diagnostics.CandidateCount, Is.EqualTo(2));
                Assert.That(diagnostics.EnemyCandidateCount, Is.EqualTo(1));
                Assert.That(diagnostics.EnemySkippedCount, Is.EqualTo(1));
                Assert.That(diagnostics.PlayerExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.ExecutedCount, Is.EqualTo(1));
                Assert.That(diagnostics.SkippedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_CommittedFrontEnemy_ResolvesFrontFaceInactive()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: true);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_TransitionFrontEnemy_ResolvesFrontFaceInactive()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: false,
                isTransitionVisible: true,
                isTransitionOnlyVisible: true,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: true);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_PhysicalFrontBottomEnemy_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
        }

        [Test]
        [Category("Core")]
        public void DefaultEnemyVisualSemanticResolver_PhysicalFrontSuppressedNonVisualFront_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_VisibleNonFrontEnemy_ResolvesNormal()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_VisibleSuppressedNonFrontEnemy_PausesAutonomousPresentation()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: false,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void DefaultEnemyVisualSemanticResolver_JumpLandingCompletionHold_PausesAnimatorPlayback()
        {
            var resolver = new DefaultEnemyVisualSemanticResolver();
            var facts = new EnemyVisualPresentationFacts(
                entityId: 20,
                isEnemy: true,
                isVisible: true,
                isCommittedVisible: true,
                isTransitionVisible: false,
                isTransitionOnlyVisible: false,
                isJumpDetachedVisible: false,
                isJumpLandingCompletionHeld: true,
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false,
                isOnVisualFrontFace: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
            Assert.That(semantic.ShouldPauseAutonomousPresentation, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_AppliesInactiveBlendAndColorOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_AppliesInactiveBlendAndColorOverride");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.ConfigureLegacyColorFallback(true);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(controller.CurrentInactiveBlend, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_DefaultPolicy_DoesNotApplyLegacyBaseColorOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_DefaultPolicy_DoesNotApplyLegacyBaseColorOverride");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/3Startis/Startis.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                var expectedLegacyColor = ResolveExpectedInactiveColor(
                    material.GetColor("_BaseColor"),
                    inactiveTint: new Color(0.62f, 0.64f, 0.68f, 1f),
                    desaturateStrength: 0.85f,
                    inactiveBlend: 1f);

                Assert.That(controller.AllowLegacyColorFallback, Is.False);
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(propertyBlock.GetColor("_BaseColor"), Is.Not.EqualTo(expectedLegacyColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_StopsAndClearsChildParticles()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_FrontFaceInactive_StopsAndClearsChildParticles");

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                var particleObject = new GameObject("ChildParticles");
                particleObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var particles = particleObject.AddComponent<ParticleSystem>();
                particles.Play(withChildren: true);
                particles.Emit(5);

                Assert.That(particles.particleCount, Is.GreaterThan(0));

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(particles.isPlaying, Is.False);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectController_StopResumePolicy_StopsAndResumesManagedParticle()
        {
            var rootObject = new GameObject(nameof(EnemySemanticParticleEffectController_StopResumePolicy_StopsAndResumesManagedParticle));

            try
            {
                var particles = AddParticleSystem(rootObject, "ManagedParticles");
                particles.Play(withChildren: true);
                particles.Emit(5);
                var controller = AddSemanticParticleController(
                    rootObject,
                    particles,
                    EnemyPresentationEffectInactivePolicy.StopOnInactiveResumeOnNormal);

                Assert.That(particles.isPlaying, Is.True);
                Assert.That(particles.particleCount, Is.GreaterThan(0));

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(particles.isPlaying, Is.False);
                Assert.That(particles.particleCount, Is.Zero);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(particles.isPlaying, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectController_StopDoNotResumePolicy_DoesNotPlayOnNormal()
        {
            var rootObject = new GameObject(nameof(EnemySemanticParticleEffectController_StopDoNotResumePolicy_DoesNotPlayOnNormal));

            try
            {
                var particles = AddParticleSystem(rootObject, "ManagedParticles");
                particles.Play(withChildren: true);
                particles.Emit(5);
                var controller = AddSemanticParticleController(
                    rootObject,
                    particles,
                    EnemyPresentationEffectInactivePolicy.StopOnInactiveDoNotResume);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(particles.isPlaying, Is.False);
                Assert.That(particles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectController_IgnorePolicy_DoesNotStopOrClearManagedParticle()
        {
            var rootObject = new GameObject(nameof(EnemySemanticParticleEffectController_IgnorePolicy_DoesNotStopOrClearManagedParticle));

            try
            {
                var particles = AddParticleSystem(rootObject, "ManagedParticles");
                particles.Play(withChildren: true);
                particles.Emit(5);
                var controller = AddSemanticParticleController(
                    rootObject,
                    particles,
                    EnemyPresentationEffectInactivePolicy.IgnoreInactiveSemantic);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(particles.isPlaying, Is.True);
                Assert.That(particles.particleCount, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectController_HideRendererOnlyPolicy_PreservesPlaybackAndRestoresRenderer()
        {
            var rootObject = new GameObject(nameof(EnemySemanticParticleEffectController_HideRendererOnlyPolicy_PreservesPlaybackAndRestoresRenderer));

            try
            {
                var particles = AddParticleSystem(rootObject, "ManagedParticles");
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                particles.Play(withChildren: true);
                var controller = AddSemanticParticleController(
                    rootObject,
                    particles,
                    EnemyPresentationEffectInactivePolicy.HideRendererOnly,
                    renderer);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(particles.isPlaying, Is.True);
                Assert.That(renderer.enabled, Is.False);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(particles.isPlaying, Is.True);
                Assert.That(renderer.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_FrontFaceInactive_DoesNotLegacyStopManagedParticles()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_FrontFaceInactive_DoesNotLegacyStopManagedParticles));

            try
            {
                var inactiveController = rootObject.AddComponent<EnemyInactiveVisualController>();
                var managedParticles = AddParticleSystem(rootObject, "ManagedParticles");
                managedParticles.Play(withChildren: true);
                managedParticles.Emit(5);
                AddSemanticParticleController(
                    rootObject,
                    managedParticles,
                    EnemyPresentationEffectInactivePolicy.IgnoreInactiveSemantic);

                var legacyParticles = AddParticleSystem(rootObject, "LegacyParticles");
                legacyParticles.Play(withChildren: true);
                legacyParticles.Emit(5);

                inactiveController.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(managedParticles.isPlaying, Is.True);
                Assert.That(managedParticles.particleCount, Is.GreaterThan(0));
                Assert.That(legacyParticles.isPlaying, Is.False);
                Assert.That(legacyParticles.particleCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectController_RepeatedApply_IsIdempotent()
        {
            var rootObject = new GameObject(nameof(EnemySemanticParticleEffectController_RepeatedApply_IsIdempotent));

            try
            {
                var resumeParticles = AddParticleSystem(rootObject, "ResumeParticles");
                resumeParticles.Play(withChildren: true);
                resumeParticles.Emit(5);
                var noResumeParticles = AddParticleSystem(rootObject, "NoResumeParticles");
                noResumeParticles.Play(withChildren: true);
                noResumeParticles.Emit(5);
                var hiddenParticles = AddParticleSystem(rootObject, "HiddenParticles");
                hiddenParticles.Play(withChildren: true);
                var hiddenRenderer = hiddenParticles.GetComponent<ParticleSystemRenderer>();
                var controller = rootObject.AddComponent<EnemySemanticParticleEffectController>();
                PlayerViewPrefabTestUtility.SetSerializedField(
                    controller,
                    "bindings",
                    new[]
                    {
                        new EnemySemanticParticleEffectBinding(
                            resumeParticles,
                            EnemyPresentationEffectInactivePolicy.StopOnInactiveResumeOnNormal),
                        new EnemySemanticParticleEffectBinding(
                            noResumeParticles,
                            EnemyPresentationEffectInactivePolicy.StopOnInactiveDoNotResume),
                        new EnemySemanticParticleEffectBinding(
                            hiddenParticles,
                            EnemyPresentationEffectInactivePolicy.HideRendererOnly,
                            hiddenRenderer),
                    });

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(resumeParticles.isPlaying, Is.False);
                Assert.That(noResumeParticles.isPlaying, Is.False);
                Assert.That(hiddenParticles.isPlaying, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.False);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(resumeParticles.isPlaying, Is.True);
                Assert.That(noResumeParticles.isPlaying, Is.False);
                Assert.That(hiddenParticles.isPlaying, Is.True);
                Assert.That(hiddenRenderer.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemySemanticParticleEffectRuntime_DoesNotDependOnSecBotOrGlowChildNames()
        {
            var semanticControllerSource = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemySemanticParticleEffectController.cs");
            var inactiveControllerSource = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_EnemyPresentation/Runtime/EnemyInactiveVisualController.cs");

            Assert.That(semanticControllerSource, Does.Not.Contain("SecBot"));
            Assert.That(semanticControllerSource, Does.Not.Contain("Glow (1)"));
            Assert.That(inactiveControllerSource, Does.Not.Contain("SecBot"));
            Assert.That(inactiveControllerSource, Does.Not.Contain("Glow (1)"));
        }

        private static ParticleSystem AddParticleSystem(GameObject rootObject, string name)
        {
            var particleObject = new GameObject(name);
            particleObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            return particleObject.AddComponent<ParticleSystem>();
        }

        private static EnemySemanticParticleEffectController AddSemanticParticleController(
            GameObject rootObject,
            ParticleSystem particles,
            EnemyPresentationEffectInactivePolicy policy,
            ParticleSystemRenderer renderer = null)
        {
            var controller = rootObject.AddComponent<EnemySemanticParticleEffectController>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                controller,
                "bindings",
                new[]
                {
                    new EnemySemanticParticleEffectBinding(
                        particles,
                        policy,
                        renderer),
                });
            return controller;
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_ConfigureSettings_AppliesInactiveTintAndStrengths()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_ConfigureSettings_AppliesInactiveTintAndStrengths));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.25f, 0.5f, 0.75f, 1f),
                desaturateStrength: 0.35f,
                emissionOmission: 0.45f);

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Configure(settings);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                AssertColorApproximately(
                    new Color(0.25f, 0.5f, 0.75f, 1f),
                    GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionOmissionProperty), Is.EqualTo(0.45f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_ConfigureSettings_DoesNotResetRevealState()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_ConfigureSettings_DoesNotResetRevealState));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.1f, 0.2f, 0.3f, 1f),
                desaturateStrength: 0.4f,
                emissionOmission: 0.5f);

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);
                var revealBeforeConfigure = controller.CurrentInactiveNoiseReveal;

                controller.Configure(settings);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(revealBeforeConfigure).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_FrontFaceInactive_StartsNoiseReveal()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_FrontFaceInactive_StartsNoiseReveal));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                Assert.That(controller, Is.InstanceOf<IEnemyVisualSemanticPresentationDriver>());

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(controller.CurrentInactiveBlend, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_FrontFaceInactive_AdvancesRevealIn()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_FrontFaceInactive_AdvancesRevealIn));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f + 0.01f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_Clear_StartsRevealOutButKeepsGate()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_Clear_StartsRevealOutButKeepsGate));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);

                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                Assert.That(controller.CurrentActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
                Assert.That(controller.IsInactiveGateEnabled, Is.True);
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_Clear_AdvancesRevealOutAndDisablesGateAfterCompletion()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_Clear_AdvancesRevealOutAndDisablesGateAfterCompletion));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealOutSeconds * 0.5f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));

                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealOutSeconds * 0.5f + 0.01f);

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.False);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_OnDisable_ResetsRevealImmediately()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_OnDisable_ResetsRevealImmediately));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds + 0.01f);

                typeof(EnemyInactiveVisualController)
                    .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, Array.Empty<object>());

                Assert.That(controller.CurrentInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.TargetInactiveNoiseReveal, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(controller.IsInactiveGateEnabled, Is.False);
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_PreservesExistingPropertyBlockValues()
        {
            const string existingPropertyName = "_ExistingEnemyInactiveMpbValue";
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_PreservesExistingPropertyBlockValues));
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();

            try
            {
                var existingBlock = new MaterialPropertyBlock();
                existingBlock.SetFloat(existingPropertyName, 0.75f);
                renderer.SetPropertyBlock(existingBlock);

                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds * 0.5f);

                Assert.That(GetRendererFloat(renderer, existingPropertyName), Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveNoiseRevealProperty), Is.GreaterThan(0f).And.LessThan(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Core")]
        public void EnemyInactiveVisualController_NullAndEmptyRenderers_NoOp()
        {
            var rootObject = new GameObject(nameof(EnemyInactiveVisualController_NullAndEmptyRenderers_NoOp));

            try
            {
                var controller = rootObject.AddComponent<EnemyInactiveVisualController>();
                PlayerViewPrefabTestUtility.SetSerializedField(controller, "targetRenderers", new Renderer[] { null });

                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive)));
                Assert.DoesNotThrow(() => controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds));
                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal)));

                PlayerViewPrefabTestUtility.SetSerializedField(controller, "targetRenderers", Array.Empty<Renderer>());

                Assert.DoesNotThrow(() => controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive)));
                Assert.DoesNotThrow(() => controller.AdvanceInactiveNoiseReveal(EnemyInactiveRevealInSeconds));
                Assert.DoesNotThrow(() => controller.ResetVisual());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveEnemy_AppliesInactiveVisualSettingsAndKeepsGenericExpansionOwned()
        {
            var rootObject = new GameObject(nameof(DefaultGameplayEntityViewFactory_PrimitiveEnemy_AppliesInactiveVisualSettingsAndKeepsGenericExpansionOwned));
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.18f, 0.28f, 0.38f, 1f),
                desaturateStrength: 0.22f,
                emissionOmission: 0.66f);

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyInactiveVisualSettings: settings);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);
                var renderer = view.GetComponentInChildren<Renderer>();

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller.AllowLegacyColorFallback, Is.True);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                AssertColorApproximately(new Color(0.18f, 0.28f, 0.38f, 1f), GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionOmissionProperty), Is.EqualTo(0.66f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_PrefabEnemy_AppliesInactiveVisualSettingsWithoutGenericExpansionOwned()
        {
            var rootObject = new GameObject(nameof(DefaultGameplayEntityViewFactory_PrefabEnemy_AppliesInactiveVisualSettingsWithoutGenericExpansionOwned));
            var prefabObject = new GameObject("EnemyPrefabWithInactiveSettings");
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.42f, 0.33f, 0.24f, 1f),
                desaturateStrength: 0.31f,
                emissionOmission: 0.72f);

            try
            {
                var prefabView = prefabObject.AddComponent<GameplayEntityView>();
                prefabObject.AddComponent<EnemyAnimatorDriver>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(prefabObject.transform, worldPositionStays: false);

                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        [20] = prefabView,
                    },
                    enemyInactiveVisualSettings: settings);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);
                var renderer = view.GetComponentInChildren<Renderer>();

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller.AllowLegacyColorFallback, Is.False);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                AssertColorApproximately(new Color(0.42f, 0.33f, 0.24f, 1f), GetRendererColor(renderer, EnemyInactiveTintProperty));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveDesaturateStrengthProperty), Is.EqualTo(0.31f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, EnemyInactiveEmissionOmissionProperty), Is.EqualTo(0.72f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(prefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveEnemy_EnablesLegacyColorFallback()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_PrimitiveEnemy_EnablesLegacyColorFallback");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.AllowLegacyColorFallback, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void DefaultGameplayEntityViewFactory_PrimitiveRoleEnemyWithNoneAiMode_AddsEnemyPresentationComponents()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_PrimitiveRoleEnemyWithNoneAiMode_AddsEnemyPresentationComponents");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10);

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.None);
                var view = factory.CreateView(enemy);

                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void DefaultGameplayEntityViewFactory_StartisPrefabEnemy_KeepsLegacyColorFallbackDisabled()
        {
            var rootObject = new GameObject("DefaultGameplayEntityViewFactory_StartisPrefabEnemy_KeepsLegacyColorFallbackDisabled");

            try
            {
                var startisPrefab = AssetDatabase.LoadAssetAtPath<GameplayEntityView>(
                    StageContentPaths.SharedEnemyPresentationRoot + "/Prefabs/EnemyView_Startis.prefab");
                Assert.That(startisPrefab, Is.Not.Null);

                var factory = new DefaultGameplayEntityViewFactory(
                    rootObject.transform,
                    cellSize: 1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        [20] = startisPrefab,
                    });

                var enemy = CreateEnemyUnit(20, new SurfaceCell(FaceId.Floor, 0, 0));
                var view = factory.CreateView(enemy);

                Assert.That(view.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.AllowLegacyColorFallback, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence()
        {
            var rootObject = new GameObject("EnemyPupilVisualController_WindupAttackRecover_AnimatesBorderSequence");

            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/2BlackEye/BE_LS_M1.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var controller = rootObject.AddComponent<EnemyPupilVisualController>();

                controller.Advance(0f);
                var defaultBorder = controller.CurrentBorder;
                Assert.That(defaultBorder, Is.EqualTo(0.44f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0.5f);
                Assert.That(controller.CurrentBorder, Is.GreaterThan(0.44f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0f);
                var recoveryBorder = controller.CurrentBorder;
                Assert.That(recoveryBorder, Is.EqualTo(0.22f).Within(0.0001f));

                controller.Advance(0.02f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(recoveryBorder).Within(0.0001f));

                controller.Advance(0.5f);
                Assert.That(controller.CurrentBorder, Is.GreaterThanOrEqualTo(recoveryBorder));
                Assert.That(controller.CurrentBorder, Is.LessThan(defaultBorder));

                controller.Advance(1f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(defaultBorder).Within(0.0001f));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(defaultBorder).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPupilVisualController_ForwardCellProjectileRecover_DoesNotSnapToContractedBorder()
        {
            var rootObject = new GameObject("EnemyPupilVisualController_ForwardCellProjectileRecover_DoesNotSnapToContractedBorder");

            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/2BlackEye/BE_LS_M1.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var controller = rootObject.AddComponent<EnemyPupilVisualController>();

                controller.Advance(0f);
                var defaultBorder = controller.CurrentBorder;
                Assert.That(defaultBorder, Is.EqualTo(0.44f).Within(0.0001f));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 1,
                    aiMode: EnemyAiMode.Attack,
                    activeActionKind: EnemyActionKind.ForwardCellProjectile,
                    isMoving: false,
                    startedWindupThisTick: true,
                    executedThisTick: false,
                    startedRecoveryThisTick: false,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0.9f);
                var preReleaseBorder = controller.CurrentBorder;
                Assert.That(preReleaseBorder, Is.GreaterThan(0.22f));
                Assert.That(preReleaseBorder, Is.LessThan(defaultBorder));

                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.ForwardCellProjectile,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));

                controller.Advance(0f);
                var recoverStartBorder = controller.CurrentBorder;
                Assert.That(recoverStartBorder, Is.EqualTo(preReleaseBorder).Within(0.0001f));
                Assert.That(recoverStartBorder, Is.GreaterThan(0.22f));

                controller.Advance(0.05f);
                Assert.That(controller.CurrentBorder, Is.GreaterThan(recoverStartBorder));
                Assert.That(controller.CurrentBorder, Is.LessThan(defaultBorder));

                controller.Advance(1f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(defaultBorder).Within(0.0001f));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(defaultBorder).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyInactiveVisualController_NormalState_PreservesPupilBorderOverride()
        {
            var rootObject = new GameObject("EnemyInactiveVisualController_NormalState_PreservesPupilBorderOverride");

            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/3DM/2BlackEye/BE_LS_M1.mat");
                Assert.That(material, Is.Not.Null);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(rootObject.transform, worldPositionStays: false);
                var renderer = visual.GetComponent<Renderer>();
                renderer.sharedMaterial = material;

                var driver = rootObject.AddComponent<EnemyAnimatorDriver>();
                var pupilController = rootObject.AddComponent<EnemyPupilVisualController>();
                var inactiveController = rootObject.AddComponent<EnemyInactiveVisualController>();

                pupilController.Advance(0f);
                driver.Apply(new EnemyViewPresentationState(
                    entityId: 20,
                    tickIndex: 2,
                    aiMode: EnemyAiMode.Recover,
                    activeActionKind: EnemyActionKind.Melee,
                    isMoving: false,
                    startedWindupThisTick: false,
                    executedThisTick: true,
                    startedRecoveryThisTick: true,
                    tookDamage: false,
                    didDie: false));
                pupilController.Advance(0f);
                inactiveController.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.Normal));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);

                Assert.That(pupilController.CurrentBorder, Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(0.22f).Within(0.0001f));
                Assert.That(propertyBlock.GetFloat("_InactiveBlend"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathExitEffectPlanBuilder_Build_UsesStableSeededResult()
        {
            var parentObject = new GameObject("EnemyDeathExitEffectPlanBuilder_Build_UsesStableSeededResult");
            var cameraObject = new GameObject("EnemyDeathExitEffectPlanBuilder_Build_OutputCamera");

            try
            {
                var outputCamera = cameraObject.AddComponent<Camera>();
                outputCamera.transform.position = new Vector3(0.5f, 0.25f, -10f);
                outputCamera.transform.rotation = Quaternion.identity;
                outputCamera.orthographic = true;
                outputCamera.orthographicSize = 3f;
                outputCamera.nearClipPlane = 0.1f;
                outputCamera.farClipPlane = 50f;

                var sourcePose = new GameplayEntityPose(new Vector3(0f, 0f, 0f), Quaternion.identity);
                var targetPose = new GameplayEntityPose(new Vector3(1.25f, 0.5f, 0f), Quaternion.identity);
                var firstPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    12345);
                var secondPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    12345);
                var differentSeedPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    parentObject.transform,
                    sourcePose,
                    targetPose,
                    outputCamera,
                    1f,
                    54321);
                var startCameraLocalPosition = outputCamera.transform.InverseTransformPoint(
                    parentObject.transform.TransformPoint(sourcePose.Position));
                var targetCameraLocalPosition = outputCamera.transform.InverseTransformPoint(
                    parentObject.transform.TransformPoint(firstPlan.TargetLocalPosition));

                AssertPositionApproximately(firstPlan.TargetLocalPosition, secondPlan.TargetLocalPosition);
                Assert.That(firstPlan.ArcHeight, Is.EqualTo(secondPlan.ArcHeight).Within(0.0001f));
                Assert.That(firstPlan.SpinDegrees, Is.EqualTo(secondPlan.SpinDegrees).Within(0.0001f));
                Assert.That(targetCameraLocalPosition.z, Is.LessThan(startCameraLocalPosition.z));
                Assert.That(targetCameraLocalPosition.z, Is.GreaterThan(outputCamera.nearClipPlane));
                Assert.That(targetCameraLocalPosition.z, Is.LessThan(outputCamera.nearClipPlane + 0.25f));

                var hasDifferentTarget = Vector3.Distance(firstPlan.TargetLocalPosition, differentSeedPlan.TargetLocalPosition) > 0.001f;
                var hasDifferentArc = Mathf.Abs(firstPlan.ArcHeight - differentSeedPlan.ArcHeight) > 0.001f;
                var hasDifferentSpin = Mathf.Abs(firstPlan.SpinDegrees - differentSeedPlan.SpinDegrees) > 0.001f;
                Assert.That(hasDifferentTarget || hasDifferentArc || hasDifferentSpin, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        private static GameplayTickPresentationCoordinator CreateInitializedTopologyCoordinator(
            GameObject rootObject,
            RecordingTopologyTransitionPlaybackPort port,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                (_, guard) => CreateRecordingTopologyExecutionPipeline(
                    guard,
                    port,
                    duplicateExecutors));

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedDamageDeathVfxCoordinator(
            GameObject rootObject,
            IDamageDeathVfxPlaybackPort playbackPort,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                (port, guard) => CreateRecordingDamageDeathVfxExecutionPipeline(
                    guard,
                    port,
                    duplicateExecutors));

            coordinator.ConfigureDamageDeathVfxPlaybackPort(playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static Type ResolveType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static GameplayTickPresentationCoordinator CreateInitializedDefaultDamageDeathVfxCoordinator(
            GameObject rootObject,
            IDamageDeathVfxPlaybackPort playbackPort,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                (_, guard) => CreateRecordingDamageDeathVfxExecutionPipeline(
                    guard,
                    playbackPort,
                    duplicateExecutors));

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedFactoryConfiguredDamageDeathVfxCoordinator(
            GameObject rootObject,
            IDamageDeathVfxPlaybackPort playbackPort,
            CubeTopologyState initialTopology)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                topologyExecutionPipelineFactory: GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                damageDeathVfxExecutionPipelineFactory: (port, guard) =>
                    CreateRecordingDamageDeathVfxExecutionPipeline(
                        guard,
                        port,
                        duplicateExecutors: false),
                damageDeathVfxPlaybackPort: playbackPort);

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedBoxMotionCoordinator(
            GameObject rootObject,
            IGameplayMotionPlaybackPort playbackPort,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false,
            IGameplayEntityViewFactory viewFactory = null)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                viewFactory ?? new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                (port, guard) => CreateRecordingBoxMotionExecutionPipeline(
                    guard,
                    port,
                    duplicateExecutors));

            coordinator.ConfigureBoxMotionPlaybackPort(playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedBoxMotionCoordinatorUsingProductionDefault(
            GameObject rootObject,
            IGameplayMotionPlaybackPort playbackPort,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false,
            IGameplayEntityViewFactory viewFactory = null)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                viewFactory ?? new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                (port, guard) => CreateRecordingBoxMotionExecutionPipeline(
                    guard,
                    port,
                    duplicateExecutors));

            coordinator.ConfigureBoxMotionPlaybackPort(playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedDefaultBoxMotionCoordinator(
            GameObject rootObject,
            CubeTopologyState initialTopology,
            IGameplayEntityViewFactory viewFactory)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, viewFactory);
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedBoxMotionCoordinatorWithNullExecutorPort(
            GameObject rootObject,
            CubeTopologyState initialTopology)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                (_, guard) => CreateRecordingBoxMotionExecutionPipeline(
                    guard,
                    playbackPort: null,
                    duplicateExecutors: false));

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayTickPresentationCoordinator CreateInitializedPlayerActionAnimationCoordinator(
            GameObject rootObject,
            PlayerActionAnimationExecutionMode mode,
            IGameplayAnimationPlaybackPort playbackPort,
            CubeTopologyState initialTopology,
            bool duplicateExecutors = false,
            IReadOnlyList<PresentationCue> overrideCues = null,
            IGameplayEntityViewFactory viewFactory = null)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                viewFactory ?? new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator(
                GameplayHostPresentationPipelineFactory.CreateTopologyExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateDamageDeathVfxExecutionPipeline,
                GameplayHostPresentationPipelineFactory.CreateBoxMotionExecutionPipeline,
                (pipelineMode, port, guard) => CreateRecordingPlayerActionAnimationExecutionPipeline(
                    pipelineMode,
                    guard,
                    port,
                    duplicateExecutors,
                    overrideCues));

            coordinator.ConfigurePlayerActionAnimationExecution(mode, playbackPort);
            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                initialTopology,
                1f,
                CreateTimingProfile());
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static GameplayPresentationPipeline CreateRecordingPlayerActionAnimationExecutionPipeline(
            PlayerActionAnimationExecutionMode mode,
            PlayerActionAnimationExecutionGuard guard,
            IGameplayAnimationPlaybackPort playbackPort,
            bool duplicateExecutors,
            IReadOnlyList<PresentationCue> overrideCues)
        {
            if (mode != PlayerActionAnimationExecutionMode.OrchestrationAnimationExecutor)
            {
                return null;
            }

            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplayAnimationPresentationExecutor(playbackPort, mode, guard),
                    new GameplayAnimationPresentationExecutor(playbackPort, mode, guard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplayAnimationPresentationExecutor(playbackPort, mode, guard),
                };
            var cuePlanner = overrideCues == null
                ? (IPresentationCuePlanner)new AnimationCuePlanner()
                : new StaticAnimationCuePlanner(overrideCues);

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new[]
                {
                    cuePlanner,
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static GameplayPresentationPipeline CreateRecordingBoxMotionExecutionPipeline(
            BoxMotionExecutionGuard guard,
            IGameplayMotionPlaybackPort playbackPort,
            bool duplicateExecutors)
        {
            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplayMotionPresentationExecutor(playbackPort, guard),
                    new GameplayMotionPresentationExecutor(playbackPort, guard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplayMotionPresentationExecutor(playbackPort, guard),
                };

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new MotionCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static GameplayPresentationPipeline CreateRecordingDamageDeathVfxExecutionPipeline(
            DamageDeathVfxExecutionGuard guard,
            IDamageDeathVfxPlaybackPort port,
            bool duplicateExecutors)
        {
            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new GameplayVfxPresentationExecutor(port, guard),
                    new GameplayVfxPresentationExecutor(port, guard),
                }
                : new IPresentationExecutor[]
                {
                    new GameplayVfxPresentationExecutor(port, guard),
                };

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new VfxCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static GameplayTickPresentationCoordinator CreateInitializedDefaultTopologyCoordinator(
            GameObject rootObject,
            CubeTopologyState initialTopology,
            GameplayTimingProfile timingProfile)
        {
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();

            coordinator.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                timingProfile);
            coordinator.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return coordinator;
        }

        private static TickResult CreateDamageDeathVfxResult(
            int tickIndex,
            CubeTopologyState topology,
            int enemyDamageEntityId = 0,
            int enemyDeathEntityId = 0,
            SurfaceCell enemyDeathCell = default,
            int presentationSeed = 0)
        {
            var enemyDamageSignals = enemyDamageEntityId > 0
                ? new[]
                {
                    new TickEnemyDamagePresentationSignal(
                        enemyDamageEntityId,
                        tookDamageThisTick: true,
                        damageAmount: 2),
                }
                : Array.Empty<TickEnemyDamagePresentationSignal>();
            var entityExitSignals = enemyDeathEntityId > 0
                ? new[]
                {
                    new TickEntityExitPresentationSignal(
                        enemyDeathEntityId,
                        TickEntityExitCause.Killed,
                        enemyDeathCell,
                        topology,
                        Direction.Right,
                        EntityType.Unit,
                        sourceActorEntityId: 10,
                        presentationSeed: presentationSeed),
                }
                : Array.Empty<TickEntityExitPresentationSignal>();
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyDamageSignals,
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals);

            return CreateTickResult(
                tickIndex,
                Array.Empty<EntityState>(),
                topology,
                presentationData);
        }

        private static TickResult CreateBoxMotionResult(
            int tickIndex,
            CubeTopologyState topology,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            TickEntityMotionKind motionKind,
            bool includeFinalBox = true)
        {
            var presentationData = new TickPresentationData(new[]
            {
                new TickEntityMotion(
                    boxEntityId,
                    motionKind,
                    sourceCell,
                    destinationCell,
                    topology,
                    topology,
                    Direction.Right,
                    Direction.Right),
            });
            var finalEntities = includeFinalBox
                ? new[]
                {
                    CreateBox(boxEntityId, destinationCell),
                }
                : Array.Empty<EntityState>();

            return CreateTickResult(tickIndex, finalEntities, topology, presentationData);
        }

        private static TickResult CreateBoxFlipImpactResult(
            int tickIndex,
            CubeTopologyState topology,
            int boxEntityId,
            int impactTargetEntityId,
            SurfaceCell sourceCell,
            SurfaceCell impactCell)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals: new[]
                {
                    new FlipImpactPresentationSignal(
                        sourceActionPlanId: 700 + tickIndex,
                        boxEntityId,
                        impactTargetEntityId,
                        actorEntityId: 10,
                        sourceCell,
                        impactCell,
                        topology,
                        Direction.Right,
                        Direction.Right,
                        FlipImpactPresentationDisposition.Stay,
                        hasLandingCell: true,
                        landingCell: sourceCell),
                });

            return CreateTickResult(
                tickIndex,
                new[]
                {
                    CreateBox(boxEntityId, sourceCell),
                },
                topology,
                presentationData);
        }

        private static TickResult CreatePlayerActionAnimationResult(
            int tickIndex,
            CubeTopologyState topology)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        101,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Right,
                        actionPlanId: 1001),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        102,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Success,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: 1002),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        103,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Right,
                        actionPlanId: 1003),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        104,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: 1004),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        201,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Up,
                        actionPlanId: 2001),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        202,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Success,
                        targetEntityId: 40,
                        direction: Direction.Up,
                        actionPlanId: 2002),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        203,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Up,
                        actionPlanId: 2003),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        204,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Impact,
                        targetEntityId: 40,
                        direction: Direction.Up,
                        actionPlanId: 2004),
                },
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals: new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        targetEntityId: 40,
                        hasTarget: true),
                });

            return CreateTickResult(
                tickIndex,
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                topology,
                presentationData);
        }

        private static TickResult CreateAllPlayerActionAnimationSemanticResult(
            int tickIndex,
            CubeTopologyState topology)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        301,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Right,
                        actionPlanId: 3001),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        302,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Success,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: 3002),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        303,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Right,
                        actionPlanId: 3003),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        304,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: 3004),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        305,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Impact,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: 3005),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        401,
                        startedThisTick: true,
                        completedThisTick: false,
                        canceledThisTick: false,
                        direction: Direction.Up,
                        actionPlanId: 4001),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        402,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Success,
                        targetEntityId: 40,
                        direction: Direction.Up,
                        actionPlanId: 4002),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        403,
                        startedThisTick: false,
                        completedThisTick: true,
                        canceledThisTick: false,
                        isRecoveryPhase: true,
                        direction: Direction.Up,
                        actionPlanId: 4003),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        404,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Blocked,
                        targetEntityId: 40,
                        direction: Direction.Up,
                        actionPlanId: 4004),
                    new TickPlayerActionPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        405,
                        startedThisTick: false,
                        completedThisTick: false,
                        canceledThisTick: false,
                        executedThisTick: true,
                        resolutionKind: TickPlayerActionResolutionKind.Impact,
                        targetEntityId: 40,
                        direction: Direction.Up,
                        actionPlanId: 4005),
                },
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals: new[]
                {
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Push,
                        Direction.Right,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        targetEntityId: 40,
                        hasTarget: true),
                    new TickPlayerActionAttemptPresentationSignal(
                        10,
                        PlayerActionKind.Flip,
                        Direction.Up,
                        PlayerActionAttemptFeedbackKind.Invalid,
                        targetEntityId: 40,
                        hasTarget: true),
                });

            return CreateTickResult(
                tickIndex,
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                topology,
                presentationData);
        }

        private static TickResult CreateSinglePlayerActionAnimationResult(
            int tickIndex,
            CubeTopologyState topology,
            PlayerActionKind actionKind,
            int sequenceId,
            bool startedThisTick = false,
            bool executedThisTick = false,
            bool recoveryPhase = false,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.Success)
        {
            var presentationData = new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                new[]
                {
                    new TickPlayerActionPresentationSignal(
                        10,
                        actionKind,
                        sequenceId,
                        startedThisTick,
                        completedThisTick: recoveryPhase,
                        canceledThisTick: false,
                        executedThisTick: executedThisTick,
                        isRecoveryPhase: recoveryPhase,
                        resolutionKind: resolutionKind,
                        targetEntityId: 40,
                        direction: Direction.Right,
                        actionPlanId: sequenceId + 9000),
                });

            return CreateTickResult(
                tickIndex,
                new[]
                {
                    CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                topology,
                presentationData);
        }

        private static PresentationCue CreatePlayerActionAnimationCue(
            PresentationAnimationCueKey cueKey,
            PresentationAnimationPhaseKind phase,
            PresentationAnimationOutcomeKind outcome,
            int tickIndex,
            int sequenceId,
            PresentationTarget? target = null,
            PresentationAnchor? anchor = null)
        {
            var actionKind = cueKey == PresentationAnimationCueKey.PlayerFlipWindup ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipExecute ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipRecovery ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipBlocked ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipImpactContact ||
                             cueKey == PresentationAnimationCueKey.PlayerFlipFailed
                ? PresentationAnimationActionKind.Flip
                : PresentationAnimationActionKind.Push;
            var payload = new PresentationAnimationPayload(
                PresentationAnimationFactKind.PlayerAction,
                10,
                actionKind,
                phase,
                outcome,
                tickIndex,
                sequenceId,
                sourceActionPlanId: sequenceId + 9000,
                targetEntityId: 40,
                direction: Direction.Right);
            return new PresentationCue(
                PresentationDomain.Animation,
                PresentationCueKey.ForAnimation(cueKey),
                new PresentationSource(
                    tickIndex,
                    PresentationSemanticSource.PlayerAction,
                    sourceEntityId: 10,
                    sourceActionKind: (int)actionKind,
                    sourceSequence: sequenceId),
                target ?? PresentationTarget.Entity(10),
                anchor ?? PresentationAnchor.ForEntityVisualRoot(10),
                PresentationPlaybackPolicyHint.OneShot(sequenceId + 10000),
                animationPayload: payload);
        }

        private static int CountPlayerActionAnimationCues(TickResult result)
        {
            var factFrame = new TickPresentationFactExtractor().Extract(result);
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new AnimationCuePlanner(),
            }).Plan(factFrame);
            return cueFrame.Cues.Count(cue => cue.Domain == PresentationDomain.Animation);
        }

        private static void AssertBoxMotionRequest(
            in GameplayMotionPlaybackRequest request,
            PresentationMotionCueKey cueKey,
            int tickIndex,
            int boxEntityId,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            CubeTopologyState topology)
        {
            Assert.That(request.CueKey, Is.EqualTo(cueKey));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.EntityId, Is.EqualTo(boxEntityId));
            Assert.That(request.Target.Kind, Is.EqualTo(PresentationTargetKind.Entity));
            Assert.That(request.Target.EntityId, Is.EqualTo(boxEntityId));
            Assert.That(request.Anchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityVisualRoot));
            Assert.That(request.MotionPayload.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(request.MotionPayload.DestinationCell, Is.EqualTo(destinationCell));
            Assert.That(request.MotionPayload.Topology, Is.EqualTo(topology));
            Assert.That(request.MotionPayload.HasTopology, Is.True);
            Assert.That(request.MotionPayload.SourceFacing, Is.EqualTo(Direction.Right));
            Assert.That(request.MotionPayload.DestinationFacing, Is.EqualTo(Direction.Right));
            Assert.That(
                request.MotionPayload.ActionKind,
                Is.EqualTo(cueKey == PresentationMotionCueKey.BoxSlide
                    ? PresentationMotionActionKind.Push
                    : PresentationMotionActionKind.Flip));
            Assert.That(request.OwnershipKey.EntityId, Is.EqualTo(boxEntityId));
            Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(cueKey));
            Assert.That(request.OwnershipKey.SourceCell, Is.EqualTo(sourceCell));
            Assert.That(request.OwnershipKey.DestinationCell, Is.EqualTo(destinationCell));
            Assert.That(request.OwnershipKey.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(
                request.OwnershipKey.SemanticSource,
                Is.EqualTo(cueKey == PresentationMotionCueKey.BoxSlide
                    ? PresentationSemanticSource.BoxSlideMotion
                    : cueKey == PresentationMotionCueKey.BoxFlip
                        ? PresentationSemanticSource.BoxFlipMotion
                        : PresentationSemanticSource.BoxFlipImpactMotion));
        }

        private static void AssertPlayerActionAnimationRequest(
            IReadOnlyList<GameplayAnimationPlaybackRequest> requests,
            PresentationAnimationCueKey cueKey,
            int tickIndex,
            int sequenceId,
            PresentationAnimationActionKind actionKind,
            PresentationAnimationPhaseKind phaseKind,
            PresentationAnimationOutcomeKind outcomeKind,
            PresentationSemanticSource expectedSemanticSource = PresentationSemanticSource.PlayerAction,
            int? expectedActionPlanId = null)
        {
            var request = requests.Single(candidate =>
                candidate.CueKey == cueKey &&
                candidate.AnimationPayload.SourceSequenceId == sequenceId &&
                candidate.OwnershipKey.SemanticSource == expectedSemanticSource);
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.PlayerEntityId, Is.EqualTo(10));
            Assert.That(request.Target, Is.EqualTo(PresentationTarget.Entity(10)));
            Assert.That(request.Anchor, Is.EqualTo(PresentationAnchor.ForEntityVisualRoot(10)));
            Assert.That(request.AnimationPayload.ActionKind, Is.EqualTo(actionKind));
            Assert.That(request.AnimationPayload.PhaseKind, Is.EqualTo(phaseKind));
            Assert.That(request.AnimationPayload.OutcomeKind, Is.EqualTo(outcomeKind));
            Assert.That(request.AnimationPayload.SourceTickIndex, Is.EqualTo(tickIndex));
            var defaultActionPlanId = actionKind == PresentationAnimationActionKind.Flip
                ? sequenceId + 1800
                : sequenceId + 900;
            Assert.That(request.AnimationPayload.SourceActionPlanId, Is.EqualTo(expectedActionPlanId ?? defaultActionPlanId));
            Assert.That(request.OwnershipKey.CueKey, Is.EqualTo(cueKey));
            Assert.That(request.OwnershipKey.PlayerEntityId, Is.EqualTo(10));
            Assert.That(request.OwnershipKey.ActionKind, Is.EqualTo(actionKind));
            Assert.That(request.OwnershipKey.PhaseKind, Is.EqualTo(phaseKind));
        }

        private static GameplayPresentationStateStore CreateStateStoreWithView(GameplayEntityView view)
        {
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ViewsByEntityId[view.EntityId] = view;
            return stateStore;
        }

        private static PlayerAnimatorDriver GetPlayerAnimatorDriver(GameObject rootObject)
        {
            var registry = rootObject.GetComponent<GameplayEntityViewRegistry>();
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.TryGetView(10, out var view), Is.True);
            var driver = view.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver, Is.Not.Null);
            return driver;
        }

        private static void AssertDamageVfxRequest(
            in GameplayVfxPlaybackRequest request,
            int tickIndex,
            int entityId)
        {
            Assert.That(request.CueKey, Is.EqualTo(PresentationVfxCueKey.DamageHit));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Damage)));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(entityId));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.Target.EntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationAnchor.Kind, Is.EqualTo(PresentationAnchorKind.EntityCenter));
            Assert.That(request.VfxAnchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.VfxAnchor.EntityId, Is.EqualTo(entityId));
            Assert.That(request.VfxAnchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
        }

        private static void AssertDeathVfxRequest(
            in GameplayVfxPlaybackRequest request,
            int tickIndex,
            int entityId,
            SurfaceCell deathCell,
            int presentationSeed)
        {
            Assert.That(request.CueKey, Is.EqualTo(PresentationVfxCueKey.EnemyDeath));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Death)));
            Assert.That(request.TickIndex, Is.EqualTo(tickIndex));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(presentationSeed));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.Target.EntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationAnchor.Kind, Is.EqualTo(PresentationAnchorKind.SurfaceCellCenter));
            Assert.That(request.PresentationAnchor.Target.Cell, Is.EqualTo(deathCell));
            Assert.That(request.VfxAnchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.VfxAnchor.Cell, Is.EqualTo(deathCell));
            Assert.That(request.VfxAnchor.Topology, Is.EqualTo(new CubeTopologyState(deathCell.face)));
            Assert.That(request.VfxAnchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
        }

        private static void AssertSemanticTelemetry(
            DamageDeathVfxExecutorDiagnostics diagnostics,
            PresentationVfxCueKey cueKey,
            int planned,
            int requested,
            int succeeded,
            int entityId,
            PresentationAnchorKind anchorKind)
        {
            var semantic = diagnostics.SemanticDiagnostics.Single(candidate => candidate.CueKey == cueKey);
            Assert.That(semantic.PlannedCount, Is.EqualTo(planned));
            Assert.That(semantic.RequestedCount, Is.EqualTo(requested));
            Assert.That(semantic.SucceededCount, Is.EqualTo(succeeded));
            Assert.That(semantic.DuplicateOmittedCount, Is.Zero);
            Assert.That(semantic.LastDedupeKey, Is.GreaterThan(0));
            Assert.That(semantic.LastTargetEntityId, Is.EqualTo(entityId));
            Assert.That(semantic.LastAnchorKind, Is.EqualTo(anchorKind));
        }

        private static void AssertPlayerActionAnimationSemanticTelemetry(
            PlayerActionAnimationProductionTelemetrySnapshot telemetry,
            PresentationAnimationCueKey cueKey,
            int planned,
            int requested,
            int applied,
            int ignored,
            int? observed = null)
        {
            var semantic = telemetry.SemanticDiagnostics.Single(candidate => candidate.CueKey == cueKey);
            Assert.That(semantic.PlannedCount, Is.EqualTo(planned), cueKey.ToString());
            Assert.That(semantic.ObservedCount, Is.EqualTo(observed ?? planned), cueKey.ToString());
            Assert.That(semantic.RequestedCount, Is.EqualTo(requested), cueKey.ToString());
            Assert.That(semantic.AppliedCount, Is.EqualTo(applied), cueKey.ToString());
            Assert.That(semantic.IgnoredCount, Is.EqualTo(ignored), cueKey.ToString());
        }

        private static void AssertPlayerActionAnimationSemanticDiagnostics(
            GameplayAnimationExecutorDiagnostics diagnostics,
            PresentationAnimationCueKey cueKey,
            int planned,
            int observed,
            int requested,
            int applied,
            int ignored)
        {
            var semantic = diagnostics.SemanticDiagnostics.Single(candidate => candidate.CueKey == cueKey);
            Assert.That(semantic.PlannedCount, Is.EqualTo(planned), cueKey.ToString());
            Assert.That(semantic.ObservedCount, Is.EqualTo(observed), cueKey.ToString());
            Assert.That(semantic.RequestedCount, Is.EqualTo(requested), cueKey.ToString());
            Assert.That(semantic.AppliedCount, Is.EqualTo(applied), cueKey.ToString());
            Assert.That(semantic.IgnoredCount, Is.EqualTo(ignored), cueKey.ToString());
        }

        private static IEnumerable<PresentationAnimationCueKey> PlayerActionAnimationSemanticCueKeys()
        {
            yield return PresentationAnimationCueKey.PlayerPushWindup;
            yield return PresentationAnimationCueKey.PlayerPushExecute;
            yield return PresentationAnimationCueKey.PlayerPushRecovery;
            yield return PresentationAnimationCueKey.PlayerPushBlocked;
            yield return PresentationAnimationCueKey.PlayerPushImpactContact;
            yield return PresentationAnimationCueKey.PlayerPushFailed;
            yield return PresentationAnimationCueKey.PlayerFlipWindup;
            yield return PresentationAnimationCueKey.PlayerFlipExecute;
            yield return PresentationAnimationCueKey.PlayerFlipRecovery;
            yield return PresentationAnimationCueKey.PlayerFlipBlocked;
            yield return PresentationAnimationCueKey.PlayerFlipImpactContact;
            yield return PresentationAnimationCueKey.PlayerFlipFailed;
        }

        private static PresentationPlaybackPlan CreatePlayerActionAnimationPlanFromStaticPlanner(
            int tickIndex,
            IReadOnlyList<PresentationCue> cues)
        {
            var cueFrame = new PresentationCuePlannerSet(new IPresentationCuePlanner[]
            {
                new StaticAnimationCuePlanner(cues),
            }).Plan(PresentationFactFrame.Empty(tickIndex));
            return new PresentationPlaybackPlanner().Plan(cueFrame);
        }

        private static DamageDeathVfxExecutorDiagnostics PlayDamageDeathVfxCueDirectly(PresentationCue cue)
        {
            var port = new RecordingDamageDeathVfxPlaybackPort();
            var guard = new DamageDeathVfxExecutionGuard();
            var executor = new GameplayVfxPresentationExecutor(
                port,
                guard);
            var plan = new PresentationPlaybackPlanner().Plan(new PresentationCueFrame(
                cue.Source.TickIndex,
                new[] { cue },
                new PresentationCueFrameDiagnostics(1, 1, 1)));

            executor.Play(plan);

            return executor.Diagnostics;
        }

        private static PresentationCue CreateDamageDeathVfxCue(
            PresentationVfxCueKey cueKey,
            PresentationTarget target,
            PresentationAnchor anchor,
            int tickIndex)
        {
            var key = PresentationCueKey.ForVfx(cueKey);
            return new PresentationCue(
                PresentationDomain.Vfx,
                key,
                new PresentationSource(
                    tickIndex,
                    cueKey == PresentationVfxCueKey.DamageHit
                        ? PresentationSemanticSource.EnemyDamage
                        : PresentationSemanticSource.EntityExit,
                    target.EntityId,
                    sourceSequence: Math.Max(1, target.EntityId)),
                target,
                anchor,
                PresentationPlaybackPolicyHint.OneShot(tickIndex * 1000 + (int)cueKey));
        }

        private static GameplayTickViewPresenter CreateInitializedTopologyPresenter(
            GameObject rootObject,
            CubeTopologyState initialTopology,
            GameplayTimingProfile timingProfile)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));

            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                initialTopology,
                1f,
                timingProfile);
            presenter.PresentInitial(Array.Empty<EntityState>(), initialTopology);
            return presenter;
        }

        private static GameplayTickViewPresenter CreateInitializedBoxMotionPresenter(
            GameObject rootObject,
            CubeTopologyState initialTopology,
            BoardBounds boardBounds,
            GameplayTimingProfile timingProfile,
            out GameplayEntityViewRegistry registry)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new BoxFlipDriverViewFactory(registry.transform));

            presenter.Initialize(
                binder,
                boardBounds,
                initialTopology,
                1f,
                timingProfile);
            return presenter;
        }

        private static GameplayPresentationPipeline CreateRecordingTopologyExecutionPipeline(
            TopologyPresentationExecutionGuard guard,
            ITopologyTransitionPlaybackPort port,
            bool duplicateExecutors)
        {
            var executors = duplicateExecutors
                ? new IPresentationExecutor[]
                {
                    new TopologyPresentationExecutor(port, guard),
                    new TopologyPresentationExecutor(port, guard),
                }
                : new IPresentationExecutor[]
                {
                    new TopologyPresentationExecutor(port, guard),
                };

            return new GameplayPresentationPipeline(
                new TickPresentationFactExtractor(),
                new PresentationCuePlannerSet(new IPresentationCuePlanner[]
                {
                    new TopologyCuePlanner(),
                }),
                new PresentationPlaybackPlanner(),
                new PresentationPlaybackScheduler(),
                executors);
        }

        private static TickResult CreateTopologyTransitionResult(
            int tickIndex,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            CubeRotationKind rotationKind)
        {
            return CreateTickResult(
                tickIndex,
                Array.Empty<EntityState>(),
                destinationTopology,
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    new TickTopologyMotion(sourceTopology, destinationTopology, rotationKind),
                    Array.Empty<TickVisibilityChange>()));
        }

        private static void AssertTopologyVisualStateEquivalent(
            TopologyTransitionVisualState expected,
            TopologyTransitionVisualState actual)
        {
            Assert.That(actual.IsActive, Is.EqualTo(expected.IsActive));
            Assert.That(actual.SourceTopology, Is.EqualTo(expected.SourceTopology));
            Assert.That(actual.DestinationTopology, Is.EqualTo(expected.DestinationTopology));
            Assert.That(actual.RotationKind, Is.EqualTo(expected.RotationKind));
            Assert.That(actual.Progress01, Is.EqualTo(expected.Progress01).Within(0.001f));
            Assert.That(
                Quaternion.Angle(actual.PresentedVisualRotation, expected.PresentedVisualRotation),
                Is.LessThan(0.001f));
        }

        private static void AssertTopologyBlockingSnapshotParity(
            PresentationBlockingSnapshot snapshot,
            bool expectedPlanned,
            bool expectedActive,
            int expectedTickIndex)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.EqualTo(expectedPlanned));
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.EqualTo(expectedActive));
            Assert.That(snapshot.PlannedBlockingBarrierCount, Is.EqualTo(expectedPlanned ? 1 : 0));
            Assert.That(snapshot.ActiveBlockingSourceCount, Is.EqualTo(expectedActive ? 1 : 0));
            Assert.That(snapshot.TopologyPlannedBarrierCount, Is.EqualTo(expectedPlanned ? 1 : 0));
            Assert.That(snapshot.TopologyActiveBlockingCount, Is.EqualTo(expectedActive ? 1 : 0));
            Assert.That(
                snapshot.LastReason,
                Is.EqualTo(expectedActive
                    ? PresentationBlockingReason.TopologyTransitionActive
                    : expectedPlanned
                        ? PresentationBlockingReason.TopologyTransitionPlanned
                        : PresentationBlockingReason.None));
            Assert.That(
                snapshot.LastOwnerDomain,
                Is.EqualTo(expectedPlanned || expectedActive
                    ? PresentationDomain.Topology
                    : PresentationDomain.None));
            Assert.That(snapshot.LastTickIndex, Is.EqualTo(expectedPlanned || expectedActive ? expectedTickIndex : 0));
        }

        private static void AssertBlockingSnapshotCleared(PresentationBlockingSnapshot snapshot)
        {
            Assert.That(snapshot.HasPlannedBlockingBarrier, Is.False);
            Assert.That(snapshot.HasActiveBlockingPresentation, Is.False);
            Assert.That(snapshot.PlannedBlockingBarrierCount, Is.Zero);
            Assert.That(snapshot.ActiveBlockingSourceCount, Is.Zero);
            Assert.That(snapshot.TopologyPlannedBarrierCount, Is.Zero);
            Assert.That(snapshot.TopologyActiveBlockingCount, Is.Zero);
            Assert.That(snapshot.Sources, Is.Empty);
            Assert.That(snapshot.LastReason, Is.EqualTo(PresentationBlockingReason.None));
            Assert.That(snapshot.LastOwnerDomain, Is.EqualTo(PresentationDomain.None));
            Assert.That(snapshot.LastTickIndex, Is.Zero);
        }

        private static GameplayTimingProfile CreateTimingProfile(float topologyMotionDurationSeconds = 0.2f)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: topologyMotionDurationSeconds,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }

        private sealed class RecordingTopologyTransitionPlaybackPort : ITopologyTransitionPlaybackPort
        {
            public int BeginOrRefreshCallCount { get; private set; }

            public int UpdatePresentationCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public bool IsTransitionActive { get; private set; }

            public TopologyTransitionPlaybackRequest LastRequest { get; private set; }

            public void BeginOrRefreshTopologyTransition(TopologyTransitionPlaybackRequest request)
            {
                BeginOrRefreshCallCount++;
                IsTransitionActive = true;
                LastRequest = request;
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdatePresentationCallCount++;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                IsTransitionActive = false;
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                IsTransitionActive = false;
            }
        }

        private sealed class RecordingDamageDeathVfxPlaybackPort : IDamageDeathVfxPlaybackPort
        {
            private readonly GameplayVfxPlaybackResultKind _resultKind;
            private readonly List<GameplayVfxPlaybackRequest> _requests = new();

            public RecordingDamageDeathVfxPlaybackPort(
                GameplayVfxPlaybackResultKind resultKind = GameplayVfxPlaybackResultKind.Succeeded)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int UpdatePresentationCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public IReadOnlyList<GameplayVfxPlaybackRequest> Requests => _requests;

            public bool TryPlayDamageDeathVfx(
                in GameplayVfxPlaybackRequest request,
                out GameplayVfxPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayVfxPlaybackResult(_resultKind);
                return _resultKind == GameplayVfxPlaybackResultKind.Succeeded;
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdatePresentationCallCount++;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }
        }

        private sealed class RecordingGameplayMotionPlaybackPort : IGameplayMotionPlaybackPort
        {
            private readonly GameplayMotionPlaybackResultKind _resultKind;
            private readonly List<GameplayMotionPlaybackRequest> _requests = new();

            public RecordingGameplayMotionPlaybackPort(
                GameplayMotionPlaybackResultKind resultKind = GameplayMotionPlaybackResultKind.Started)
            {
                _resultKind = resultKind;
            }

            public int TryPlayCallCount { get; private set; }

            public int UpdatePresentationCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public int CleanupRequestedCount { get; private set; }

            public int CleanupSucceededCount { get; private set; }

            public IReadOnlyList<GameplayMotionPlaybackRequest> Requests => _requests;

            public bool TryPlayBoxMotion(
                in GameplayMotionPlaybackRequest request,
                out GameplayMotionPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                result = new GameplayMotionPlaybackResult(_resultKind);
                return _resultKind == GameplayMotionPlaybackResultKind.Started ||
                       _resultKind == GameplayMotionPlaybackResultKind.Requested;
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdatePresentationCallCount++;
            }

            public void ResetSession()
            {
                CleanupRequestedCount++;
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                UpdatePresentationCallCount = 0;
                _requests.Clear();
                CleanupSucceededCount++;
            }

            public void HardCleanup()
            {
                CleanupRequestedCount++;
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                UpdatePresentationCallCount = 0;
                _requests.Clear();
                CleanupSucceededCount++;
            }
        }

        private sealed class RecordingGameplayAnimationPlaybackPort : IGameplayAnimationPlaybackPort
        {
            private readonly Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResultKind> _resultFactory;
            private readonly List<GameplayAnimationPlaybackRequest> _requests = new();

            public RecordingGameplayAnimationPlaybackPort(
                GameplayAnimationPlaybackResultKind resultKind = GameplayAnimationPlaybackResultKind.Applied)
                : this(_ => resultKind)
            {
            }

            public RecordingGameplayAnimationPlaybackPort(
                Func<GameplayAnimationPlaybackRequest, GameplayAnimationPlaybackResultKind> resultFactory)
            {
                _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
            }

            public int TryPlayCallCount { get; private set; }

            public int ResetSessionCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public IReadOnlyList<GameplayAnimationPlaybackRequest> Requests => _requests;

            public bool TryPlayPlayerActionAnimation(
                in GameplayAnimationPlaybackRequest request,
                out GameplayAnimationPlaybackResult result)
            {
                TryPlayCallCount++;
                _requests.Add(request);
                var resultKind = _resultFactory(request);
                result = new GameplayAnimationPlaybackResult(resultKind);
                return resultKind == GameplayAnimationPlaybackResultKind.Applied ||
                       resultKind == GameplayAnimationPlaybackResultKind.Requested;
            }

            public void ResetSession()
            {
                ResetSessionCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }

            public void HardCleanup()
            {
                HardCleanupCallCount++;
                TryPlayCallCount = 0;
                _requests.Clear();
            }
        }

        private sealed class StaticAnimationCuePlanner : IPresentationCuePlanner
        {
            private readonly IReadOnlyList<PresentationCue> _cues;

            public StaticAnimationCuePlanner(IReadOnlyList<PresentationCue> cues)
            {
                _cues = cues ?? Array.Empty<PresentationCue>();
            }

            public void Plan(in PresentationFactFrame facts, PresentationCueFrameBuilder builder)
            {
                for (var i = 0; i < _cues.Count; i++)
                {
                    builder.Add(_cues[i]);
                    if (_cues[i].Key.TryGetAnimationCueKey(out var cueKey))
                    {
                        builder.RecordPlayerActionAnimationPlanned(cueKey);
                    }
                }
            }
        }

        private readonly struct PlayerActionAnimationSemanticCase
        {
            public PlayerActionAnimationSemanticCase(
                PlayerActionKind actionKind,
                PresentationAnimationCueKey cueKey,
                PresentationAnimationPhaseKind phaseKind,
                PresentationAnimationOutcomeKind outcomeKind,
                int sequenceId,
                bool startedThisTick = false,
                bool executedThisTick = false,
                bool recoveryPhase = false,
                TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.Success)
            {
                ActionKind = actionKind;
                CueKey = cueKey;
                PhaseKind = phaseKind;
                OutcomeKind = outcomeKind;
                SequenceId = sequenceId;
                StartedThisTick = startedThisTick;
                ExecutedThisTick = executedThisTick;
                RecoveryPhase = recoveryPhase;
                ResolutionKind = resolutionKind;
            }

            public PlayerActionKind ActionKind { get; }

            public PresentationAnimationCueKey CueKey { get; }

            public PresentationAnimationPhaseKind PhaseKind { get; }

            public PresentationAnimationOutcomeKind OutcomeKind { get; }

            public int SequenceId { get; }

            public bool StartedThisTick { get; }

            public bool ExecutedThisTick { get; }

            public bool RecoveryPhase { get; }

            public TickPlayerActionResolutionKind ResolutionKind { get; }
        }

        private static Color ResolveExpectedInactiveColor(
            Color sourceColor,
            Color inactiveTint,
            float desaturateStrength,
            float inactiveBlend)
        {
            var luminance = (sourceColor.r * 0.2126f) + (sourceColor.g * 0.7152f) + (sourceColor.b * 0.0722f);
            var grayscale = new Color(luminance, luminance, luminance, sourceColor.a);
            var tinted = Color.Lerp(grayscale, inactiveTint, inactiveBlend * 0.35f);
            var desaturated = Color.Lerp(sourceColor, tinted, inactiveBlend * desaturateStrength);
            desaturated.a = sourceColor.a;
            return desaturated;
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position, EnemyAiMode aiMode = EnemyAiMode.Patrol)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
            };
        }

        private static EntityState WithBoardPresence(EntityState entity, EntityBoardPresence boardPresence)
        {
            entity.boardPresence = boardPresence;
            return entity;
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static TickResult CreateMotionTickResult(
            EntityState finalEntity,
            CubeTopologyState topology,
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            TickEntityMotionKind motionKind)
        {
            return CreateMotionTickResult(
                new[]
                {
                    finalEntity,
                },
                topology,
                new TickEntityMotion(finalEntity.entityId, motionKind, sourceCell, destinationCell));
        }

        private static TickResult CreateMotionTickResult(
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            params TickEntityMotion[] motions)
        {
            return CreateTickResult(
                tickIndex: 1,
                finalEntities,
                topology,
                new TickPresentationData(motions));
        }

        private static GameplayTickViewPresenter CreateInitializedPresenter(
            GameObject rootObject,
            out CubeTopologyState topology)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new MotionOverrideViewFactory(registry.transform));
            topology = new CubeTopologyState(FaceId.Floor);

            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                topology,
                1f,
                CreateTimingProfile());
            presenter.PresentInitial(Array.Empty<EntityState>(), topology);
            return presenter;
        }

        private static GameplayTickViewPresenter CreatePrimitivePresenter(
            GameObject rootObject,
            out GameplayEntityViewRegistry registry,
            out CubeTopologyState topology,
            BoardBounds? boardBounds = null)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10));
            topology = new CubeTopologyState(FaceId.Floor);
            presenter.Initialize(
                binder,
                boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0)),
                topology,
                1f,
                CreateTimingProfile());
            presenter.PresentInitial(Array.Empty<EntityState>(), topology);
            return presenter;
        }

        private sealed class RecordingInitialPresentationExtension :
            IGameplayTickPresentationExtension,
            IGameplayInitialPresentationExtension
        {
            public int InitialPresentCallCount { get; private set; }

            public int TickPresentCallCount { get; private set; }

            public InitialPresentationData CapturedInitialPresentationData { get; private set; }

            public bool HadPlayerViewDuringInitial { get; private set; }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
                TickPresentCallCount++;
            }

            public void PresentInitial(in GameplayInitialPresentationExtensionContext context)
            {
                InitialPresentCallCount++;
                CapturedInitialPresentationData = context.PresentationData;
                HadPlayerViewDuringInitial =
                    context.StateStore != null &&
                    context.StateStore.ViewsByEntityId.ContainsKey(10);
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
            }
        }

        private sealed class RecordingPausablePresentationExtension :
            IGameplayTickPresentationExtension,
            IGameplayPresentationPausable
        {
            public int UpdateCallCount { get; private set; }

            public float AdvancedSeconds { get; private set; }

            public bool IsPaused { get; private set; }

            public int PauseCallCount { get; private set; }

            public int ResumeCallCount { get; private set; }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
            }

            public void UpdatePresentation(float deltaTime)
            {
                UpdateCallCount++;
                AdvancedSeconds += deltaTime;
            }

            public void HardCleanup()
            {
            }

            public void SetPresentationPaused(bool paused)
            {
                IsPaused = paused;
                if (paused)
                {
                    PauseCallCount++;
                    return;
                }

                ResumeCallCount++;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            return (T)target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            target.GetType()
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, args);
        }

        private static RecordingTileFeatureVisualTarget AttachTileVisualTarget(
            GameObject rootObject,
            GameplayTickViewPresenter presenter,
            int tileId,
            SurfaceCell cell)
        {
            var registry = rootObject.GetComponent<TileFeatureVisualRegistry>() ??
                rootObject.AddComponent<TileFeatureVisualRegistry>();
            var targetObject = new GameObject($"TileFeatureVisualTarget_{tileId}");
            targetObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
            var target = targetObject.AddComponent<RecordingTileFeatureVisualTarget>();
            target.Configure(tileId, cell);
            registry.ConfigureSearchRoot(rootObject.transform);
            presenter.AttachTileFeatureVisualRegistry(registry);
            return target;
        }

        private static TickPresentationData CreateTilePresentationData(params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }

        private static TickPresentationData CreateTilePresentationDataWithTopologyMotion(
            TickTopologyMotion topologyMotion,
            params TilePresentationEvent[] tileEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents);
        }

        private static TickPresentationData CreateMoonBlockDestroyAndGeneratedPresentationData(
            int entityId,
            SurfaceCell sourceCell,
            SurfaceCell destroyTileCell,
            SurfaceCell generatorCell,
            CubeTopologyState topology,
            int presentationSeed,
            TickTopologyMotion? topologyMotion = null)
        {
            return new TickPresentationData(
                new[]
                {
                    new TickEntityMotion(
                        entityId,
                        TickEntityMotionKind.Push,
                        sourceCell,
                        destroyTileCell),
                },
                topologyMotion: topologyMotion,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                new[]
                {
                    new TickEntityExitPresentationSignal(
                        entityId,
                        TickEntityExitCause.BoxDestroy,
                        destroyTileCell,
                        topology,
                        Direction.Right,
                        EntityType.Box,
                        presentationSeed: presentationSeed,
                        timing: EntityExitPresentationTiming.AfterEntityMotion,
                        hasPresentationTargetCell: true,
                        presentationTargetCell: destroyTileCell),
                },
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: new[]
                {
                    CreateMoonBlockGeneratedTileEvent(
                        100,
                        generatorCell,
                        entityId,
                        spawnTick: 1),
                });
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return CreateGravityFieldPresentationData(
                Array.Empty<GravityFieldVisualState>(),
                gravityFieldEvents);
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            IReadOnlyList<GravityFieldVisualState> gravityFieldVisualStates,
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates);
        }

        private static TickPresentationData CreateGravityFieldVisualPresentationData(
            params GravityFieldVisualState[] gravityFieldVisualStates)
        {
            return CreateGravityFieldPresentationData(
                gravityFieldVisualStates,
                Array.Empty<GravityFieldPresentationEvent>());
        }

        private static TickPresentationData CreateEnemyGravityFieldAuraPresentationData(
            params TickEnemyGravityFieldAuraVisualState[] enemyGravityFieldAuraVisualStates)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyGravityFieldAuraVisualStates: enemyGravityFieldAuraVisualStates);
        }

        private static TickPresentationData CreateVisibilityPresentationData(
            params TickVisibilityChange[] visibilityChanges)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: visibilityChanges,
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickPresentationData CreateEnemyDamagePresentationData(
            params TickEnemyDamagePresentationSignal[] enemyDamageSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: enemyDamageSignals,
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>());
        }

        private static GravityFieldVisualState CreateGravityFieldVisualState(
            int emitterEntityId,
            SurfaceCell cell,
            GravityFieldPhase phase,
            int timerTicks,
            int durationTicks,
            IReadOnlyList<SurfaceCell> areaCells = null,
            int slotVisibilityMask = 0,
            IReadOnlyList<int> lockedTargetEntityIds = null)
        {
            var progress = durationTicks <= 0
                ? 0f
                : (durationTicks - Math.Max(0, timerTicks)) / (float)durationTicks;
            return new GravityFieldVisualState(
                emitterEntityId,
                cell,
                phase,
                Math.Max(0, timerTicks),
                durationTicks,
                progress,
                areaCells == null && slotVisibilityMask == 0
                    ? GravityFieldAreaFootprint.Empty
                    : new GravityFieldAreaFootprint(areaCells ?? Array.Empty<SurfaceCell>(), slotVisibilityMask),
                lockedTargetEntityIds ?? Array.Empty<int>());
        }

        private static TilePresentationEvent CreateButtonActivatedTileEvent(int tileId, SurfaceCell cell)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.ButtonActivated,
                tileId,
                cell,
                TileFeatureKind.Button,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3);
        }

        private static TilePresentationEvent CreateDestroyTileTriggeredTileEvent(
            int tileId,
            SurfaceCell cell,
            int targetEntityId)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.DestroyTileTriggered,
                tileId,
                cell,
                TileFeatureKind.Destroy,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: targetEntityId);
        }

        private static TilePresentationEvent CreateMoonBlockGeneratedTileEvent(
            int tileId,
            SurfaceCell cell,
            int moonBlockEntityId,
            int spawnTick)
        {
            return new TilePresentationEvent(
                TilePresentationEventKind.MoonBlockGenerated,
                tileId,
                cell,
                TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: tileId + 1,
                ownerEntityId: tileId + 2,
                teamId: tileId + 3,
                targetEntityId: moonBlockEntityId,
                direction: Direction.None,
                spawnTick: spawnTick,
                spawnInteractionLockTicks: MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks);
        }

        private static TilePresentationRequest CreateMoonBlockGeneratedRequest(int moonBlockEntityId, int spawnTick)
        {
            return new TilePresentationRequest(
                TilePresentationRequestKind.MoonBlockGenerated,
                tileId: 100,
                cell: new SurfaceCell(FaceId.Floor, 1, 1),
                tileFeatureKind: TileFeatureKind.MoonBlockGenerator,
                sourceEntityId: 101,
                ownerEntityId: 102,
                teamId: 7,
                targetEntityId: moonBlockEntityId,
                direction: Direction.None,
                spawnTick: spawnTick,
                spawnInteractionLockTicks: MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks);
        }

        private static TickPresentationData CreateKinematicPresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickKinematicMotionTrack> kinematicMotionTracks,
            IReadOnlyList<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null,
            IReadOnlyList<TickEnemyGlidePresentationSignal> enemyGlideSignals = null)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                kinematicMotionTracks: kinematicMotionTracks,
                playerDeathHoldSignals: playerDeathHoldSignals,
                enemyGlideSignals: enemyGlideSignals);
        }

        private static TickPresentationData CreateGlidePresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickEnemyGlidePresentationSignal> enemyGlideSignals)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                enemyGlideSignals: enemyGlideSignals);
        }

        private static TickPresentationData CreateContinuousPresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickContinuousLocomotionTrack> continuousLocomotionTracks,
            IReadOnlyList<TickPlayerDeathHoldPresentationSignal> playerDeathHoldSignals = null)
        {
            return new TickPresentationData(
                entityMotions,
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                continuousLocomotionTracks: continuousLocomotionTracks,
                playerDeathHoldSignals: playerDeathHoldSignals);
        }

        private static TickKinematicMotionTrack CreateKinematicTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            int sourceLocalX,
            SurfaceCell destinationAnchorCell,
            int destinationLocalX,
            CubeTopologyState topology,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
        {
            return new TickKinematicMotionTrack(
                entityId,
                sourceAnchorCell,
                CreateKinematicOffset(sourceLocalX, 0),
                destinationAnchorCell,
                CreateKinematicOffset(destinationLocalX, 0),
                terminalKind == TickKinematicMotionTerminalKind.None
                    ? MotionMode.Voluntary
                    : terminalKind == TickKinematicMotionTerminalKind.Interrupted
                        ? MotionMode.Interrupted
                        : MotionMode.Voluntary,
                ForcedMotionOp.None,
                EntityType.Unit,
                topology,
                topology,
                Direction.Right,
                Direction.Right,
                terminalKind);
        }

        private static TickContinuousLocomotionTrack CreateContinuousTrack(
            int entityId,
            SurfaceCell sourceAnchorCell,
            int sourceLocalX,
            SurfaceCell destinationAnchorCell,
            int destinationLocalX,
            ContinuousLocomotionMode mode,
            TickKinematicMotionTerminalKind terminalKind = TickKinematicMotionTerminalKind.None)
        {
            return new TickContinuousLocomotionTrack(
                entityId,
                sourceAnchorCell,
                CreateKinematicOffset(sourceLocalX, 0),
                destinationAnchorCell,
                CreateKinematicOffset(destinationLocalX, 0),
                Direction.Right,
                Direction.Right,
                mode,
                terminalKind);
        }

        private static TickResult CreateTickResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                string.Empty,
                TickTrace.Empty);
        }

        private static Vector3 GetProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.LocalPosition;
        }

        private static Vector3 GetProjectedTransitionEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            EntityType entityType)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    entityType,
                    out var projectedPose),
                Is.True);
            return projectedPose.LocalPosition;
        }

        private static Vector3 GetProjectedEntityNormal(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.Normal;
        }

        private static TickEnemyGlidePresentationSignal CreateGlideSignal(
            int entityId,
            SurfaceCell anchorCell,
            EnemyGlidePhase phase,
            int currentHeightUnits)
        {
            return new TickEnemyGlidePresentationSignal(
                entityId,
                anchorCell,
                phase,
                sequence: 1,
                phaseElapsedTicks: 0,
                phaseTotalTicks: 1,
                normalizedPhaseProgress: 0f,
                liftHeightUnits: SimulationFixed.UnitsPerCell / 4,
                recoveryDipHeightUnits: 0,
                currentHeightUnits,
                isAirborneVisual: currentHeightUnits != 0,
                wantsRecover: false,
                isTerminalZero: currentHeightUnits == 0);
        }

        private static Vector3 GetProjectedKinematicEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType,
            int localX,
            int localY)
        {
            var projector = new GameplayCubeProjector(boardBounds, 1f);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            var localOffset = CreateKinematicOffset(localX, localY);
            return projectedPose.LocalPosition +
                   (projectedPose.LocalRotation * new Vector3(
                       localOffset.X.RawValue / (float)SimulationFixed.UnitsPerCell,
                       localOffset.Y.RawValue / (float)SimulationFixed.UnitsPerCell,
                       0f));
        }

        private static SimulationOffset2 CreateKinematicOffset(int localX, int localY)
        {
            return new SimulationOffset2(
                SimulationFixed.FromRaw(localX),
                SimulationFixed.FromRaw(localY));
        }

        private static Vector3 ResolveLinearPosition(
            Vector3 source,
            Vector3 destination,
            float elapsedSeconds,
            float durationSeconds)
        {
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            return Vector3.LerpUnclamped(source, destination, normalizedTime);
        }

        private static Vector3 ResolveEasedLinearPosition(
            Vector3 source,
            Vector3 destination,
            float elapsedSeconds,
            float durationSeconds)
        {
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / durationSeconds);
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 2f);
            return Vector3.LerpUnclamped(source, destination, easedTime);
        }

        private static void AssertPositionApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static void AssertScaleApproximately(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private static int GetJumpDetachedVisibilityStateCount(GameplayTickViewPresenter presenter)
        {
            return GetPresentationStateStore(presenter).JumpDetachedVisibilityStates.Count;
        }

        private static GameplayPresentationStateStore GetPresentationStateStore(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            var stateStoreField = coordinator.GetType()
                .GetField("_stateStore", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stateStoreField, Is.Not.Null);
            return (GameplayPresentationStateStore)stateStoreField.GetValue(coordinator);
        }

        private static GameplayPresentationTrackState GetPresentationTrackState(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            return GetPresentationTrackState((GameplayTickPresentationCoordinator)coordinator);
        }

        private static GameplayPresentationTrackState GetPresentationTrackState(
            GameplayTickPresentationCoordinator coordinator)
        {
            var trackStateField = coordinator.GetType()
                .GetField("_trackState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(trackStateField, Is.Not.Null);
            return (GameplayPresentationTrackState)trackStateField.GetValue(coordinator);
        }

        private static GameplayPresentationActivityInspector GetPresentationActivityInspector(GameplayTickViewPresenter presenter)
        {
            var coordinator = GetPresentationCoordinator(presenter);
            var inspectorField = coordinator.GetType()
                .GetField("_presentationActivityInspector", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(inspectorField, Is.Not.Null);
            return (GameplayPresentationActivityInspector)inspectorField.GetValue(coordinator);
        }

        private static bool HasPlayerDeathDisplacementTrack(GameplayTickViewPresenter presenter, int entityId)
        {
            return GetPresentationTrackState(presenter).PlayerDeathDisplacementTracks.ContainsKey(entityId);
        }

        private static PlayerDeathDisplacementTrackState GetPlayerDeathDisplacementTrackState(
            GameplayTickViewPresenter presenter,
            int entityId)
        {
            Assert.That(
                GetPresentationTrackState(presenter).PlayerDeathDisplacementTracks.TryGetValue(entityId, out var track),
                Is.True);
            Assert.That(track, Is.Not.Null);
            return track.State;
        }

        private static object GetPresentationCoordinator(GameplayTickViewPresenter presenter)
        {
            var coordinatorField = typeof(GameplayTickViewPresenter)
                .GetField("_presentationCoordinator", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(coordinatorField, Is.Not.Null);
            return coordinatorField.GetValue(presenter);
        }

        private static GameplayEntityViewBinder CreateEnemyPrefabBinder(
            GameplayEntityViewRegistry registry,
            GameplayEntityView enemyPrefab)
        {
            return new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10,
                    enemyViewPrefabsByEntityId: new Dictionary<int, GameplayEntityView>
                    {
                        { 20, enemyPrefab },
                    }));
        }

        private static TickResult CreateJumpAirborneTick(
            int tickIndex,
            CubeTopologyState finalTopology,
            EntityState finalEntity,
            TickTopologyMotion? topologyMotion,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            SurfaceCell sourceCell,
            SurfaceCell landingCell,
            bool startedAirborneThisTick,
            int remainingAirborneTicks)
        {
            return CreateTickResult(
                tickIndex,
                new[] { finalEntity },
                finalTopology,
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion,
                    visibilityChanges ?? Array.Empty<TickVisibilityChange>(),
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            20,
                            sequence: 1,
                            phase: EnemyJumpPhase.Airborne,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: startedAirborneThisTick,
                            landedThisTick: false,
                            retryThisTick: false,
                            sourceCell: sourceCell,
                            lockedTargetCell: landingCell,
                            presentationTargetCell: landingCell,
                            facing: Direction.Right,
                            landingTick: tickIndex + remainingAirborneTicks,
                            remainingAirborneTicks: remainingAirborneTicks,
                            retryCount: 0),
                    },
                    Array.Empty<TickEntityExitPresentationSignal>()));
        }

        private static TickResult CreateEnemyAirborneTick(
            int tickIndex,
            CubeTopologyState finalTopology,
            SurfaceCell sourceCell,
            SurfaceCell landingCell,
            int sequence = 1,
            bool startedAirborneThisTick = false,
            int landingTick = 0,
            int remainingAirborneTicks = 1)
        {
            return CreateTickResult(
                tickIndex,
                new[] { WithBoardPresence(CreateEnemyUnit(20, sourceCell), EntityBoardPresence.Detached) },
                finalTopology,
                new TickPresentationData(
                    Array.Empty<TickEntityMotion>(),
                    topologyMotion: null,
                    new[]
                    {
                        new TickVisibilityChange(20, TickVisibilityChangeKind.Detach, sourceCell, finalTopology, Direction.Right),
                    },
                    Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(),
                    Array.Empty<TickEnemyActionPresentationSignal>(),
                    new[]
                    {
                        new TickEnemyJumpPresentationSignal(
                            20,
                            sequence,
                            EnemyJumpPhase.Airborne,
                            startedWindupThisTick: false,
                            startedAirborneThisTick: startedAirborneThisTick,
                            landedThisTick: false,
                            retryThisTick: false,
                            sourceCell: sourceCell,
                            lockedTargetCell: landingCell,
                            presentationTargetCell: landingCell,
                            facing: Direction.Right,
                            landingTick: landingTick,
                            remainingAirborneTicks: remainingAirborneTicks,
                            retryCount: 0),
                    },
                    Array.Empty<TickEntityExitPresentationSignal>()));
        }

        private static EnemyViewPresentationState CreateEnemyAirbornePresentationState(bool startedAirborneThisTick)
        {
            return new EnemyViewPresentationState(
                entityId: 20,
                tickIndex: 1,
                aiMode: EnemyAiMode.Chase,
                activeActionKind: EnemyActionKind.None,
                jumpPhase: EnemyJumpPhase.Airborne,
                isMoving: false,
                startedWindupThisTick: false,
                executedThisTick: false,
                startedRecoveryThisTick: false,
                startedJumpWindupThisTick: false,
                startedJumpAirborneThisTick: startedAirborneThisTick,
                landedFromJumpThisTick: false,
                retryingJumpAirborneThisTick: false,
                tookDamage: false,
                didDie: false);
        }

        private static TickPresentationData CreateSummonWindupPresentationData(
            IReadOnlyList<TickSummonWindupWarningSignal> summonWindupWarnings)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                summonWindupWarnings: summonWindupWarnings);
        }

        private static TickPresentationData CreateEnemyUtilityPresentationData(
            IReadOnlyList<TickEnemyUtilityPresentationSignal> enemyUtilitySignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                enemyUtilitySignals: enemyUtilitySignals);
        }

        private static TickPresentationData CreateEnemySummonPresentationData(
            IReadOnlyList<TickEnemySummonPresentationSignal> enemySummonSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                visibilityChanges: Array.Empty<TickVisibilityChange>(),
                transitionVisibilityChanges: Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals: Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                enemyChargeSignals: Array.Empty<TickEnemyChargePresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                impactTransientSignals: Array.Empty<TickImpactTransientPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                summonedEnemyPresentationBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                enemySummonSignals: enemySummonSignals);
        }

        private static TickSummonWindupWarningSignal CreateSummonWindupWarningSignal(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            int tickIndex)
        {
            return new TickSummonWindupWarningSignal(
                sourceEntityId,
                0,
                sourceCell,
                topology,
                Direction.Right,
                tickIndex,
                tickIndex + 1,
                1,
                tickIndex,
                tickIndex * 31 + sourceEntityId);
        }

        private static GameObject[] CreateAreaSlots(Transform parent, int slotCount)
        {
            var slots = new GameObject[slotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"AreaSlot_{i}");
                slot.transform.SetParent(parent, worldPositionStays: false);
                slots[i] = slot;
            }

            return slots;
        }

        private static int CountDescendantsByNamePrefix(Transform root, string prefix)
        {
            var count = 0;
            var descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null &&
                    descendants[i] != root &&
                    descendants[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TryFindDescendantByNamePrefix(Transform root, string prefix, out Transform descendant)
        {
            var descendants = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (var i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null &&
                    descendants[i] != root &&
                    descendants[i].name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    descendant = descendants[i];
                    return true;
                }
            }

            descendant = null;
            return false;
        }

        private static GameplayEntityView CreateEnemyViewPrefab(
            string name,
            float unitMoveDurationSeconds,
            float entityMoveDurationSeconds)
        {
            var prefabObject = new GameObject(name);
            var view = prefabObject.AddComponent<GameplayEntityView>();
            view.Initialize(20);
            prefabObject.AddComponent<EnemyAnimatorDriver>();
            prefabObject.AddComponent<EnemyAnimationTimingAuthoring>();

            var unitAuthoring = prefabObject.AddComponent<UnitLocomotionPresentationAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                unitAuthoring,
                "moveMotionDurationSeconds",
                unitMoveDurationSeconds);

            var entityAuthoring = prefabObject.AddComponent<EntityMotionPresentationAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                entityAuthoring,
                "moveMotionDurationSeconds",
                entityMoveDurationSeconds);

            return view;
        }

        private static GameplayEntityView CreateEnemyJumpAnimatorViewPrefab(
            string name,
            out AnimationClip jumpReferenceClip,
            RuntimeAnimatorController runtimeAnimatorController = null)
        {
            var view = CreateEnemyViewPrefab(name, 0.2f, 0.2f);
            var animator = view.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = runtimeAnimatorController != null
                ? runtimeAnimatorController
                : AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyJumpAnimatorControllerPath);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null, EnemyJumpAnimatorControllerPath);

            jumpReferenceClip = CreateReferenceClip($"{name}_JumpAirborneReference", 1f);
            var authoring = view.GetComponent<EnemyAnimationTimingAuthoring>();
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "stateTransitionCrossFadeDurationSeconds",
                0f);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneAnimatorDurationSeconds",
                1f);
            PlayerViewPrefabTestUtility.SetSerializedField(
                authoring,
                "jumpAirborneReferenceClip",
                jumpReferenceClip);
            return view;
        }

        private static AnimatorController CreateJumpAirborneExitTimeAnimatorController(
            string name,
            out AnimationClip jumpClip,
            out AnimationClip moveClip)
        {
            jumpClip = CreateReferenceClip($"{name}_JumpAirborneClip", 1f);
            moveClip = CreateReferenceClip($"{name}_MoveClip", 1f);

            var stateMachine = new AnimatorStateMachine
            {
                name = $"{name}_StateMachine",
            };
            var moveState = stateMachine.AddState("Move");
            moveState.motion = moveClip;
            var jumpState = stateMachine.AddState("JumpAirborne");
            jumpState.motion = jumpClip;
            stateMachine.defaultState = moveState;

            var exitTransition = jumpState.AddTransition(moveState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 0.75f;
            exitTransition.duration = 0f;
            exitTransition.hasFixedDuration = true;

            var controller = new AnimatorController
            {
                name = $"{name}_Controller",
                layers = new[]
                {
                    new AnimatorControllerLayer
                    {
                        name = "Base Layer",
                        defaultWeight = 1f,
                        stateMachine = stateMachine,
                    },
                },
            };
            controller.AddParameter("JumpAirborne", AnimatorControllerParameterType.Trigger);
            return controller;
        }

        private static AnimationClip CreateReferenceClip(string clipName, float lengthSeconds)
        {
            var clip = new AnimationClip
            {
                name = clipName,
                frameRate = 60f,
            };

            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "m_LocalPosition.x",
                AnimationCurve.Linear(0f, 0f, lengthSeconds, 1f));
            return clip;
        }

        private static Animator GetRequiredAnimator(GameplayEntityView view)
        {
            var animator = view.GetComponentInChildren<Animator>(includeInactive: true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            return animator;
        }

        private static void AssertJumpAirborneAnimatorState(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var expectedHash = Animator.StringToHash("JumpAirborne");
            if (stateInfo.shortNameHash == 0 &&
                TryGetJumpAirborneDriverSnapshot(animator, out var driverHash, out _))
            {
                Assert.That(driverHash, Is.EqualTo(expectedHash));
                return;
            }

            Assert.That(stateInfo.shortNameHash, Is.EqualTo(expectedHash));
        }

        private static void AssertJumpAirborneAnimatorStateAndNormalizedTime(
            Animator animator,
            float expectedNormalizedTime)
        {
            AssertJumpAirborneAnimatorState(animator);
            Assert.That(GetAnimatorNormalizedTime(animator), Is.EqualTo(expectedNormalizedTime).Within(0.0001f));
        }

        private static float GetAnimatorNormalizedTime(Animator animator)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.shortNameHash == 0 &&
                   TryGetJumpAirborneDriverSnapshot(animator, out _, out var normalizedTime)
                ? normalizedTime
                : stateInfo.normalizedTime;
        }

        private static bool TryGetJumpAirborneDriverSnapshot(
            Animator animator,
            out int stateHash,
            out float normalizedTime)
        {
            stateHash = 0;
            normalizedTime = 0f;
            var driver = animator.GetComponentInParent<EnemyAnimatorDriver>(includeInactive: true);
            if (driver == null ||
                driver.LastPresentationState.JumpPhase != EnemyJumpPhase.Airborne)
            {
                return false;
            }

            stateHash = driver.DebugLastJumpAirborneStateShortNameHash;
            normalizedTime = driver.DebugLastJumpAirborneNormalizedTime;
            return true;
        }

        private static float GetRendererFloat(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetFloat(propertyName);
        }

        private static Color GetRendererColor(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor(propertyName);
        }

        private static EnemyInactiveVisualSettings CreateEnemyInactiveVisualSettings(
            Color inactiveTint,
            float desaturateStrength,
            float emissionOmission,
            float revealInSeconds = 0.25f,
            float revealOutSeconds = 0.18f)
        {
            var settings = ScriptableObject.CreateInstance<EnemyInactiveVisualSettings>();
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveTint", inactiveTint);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "desaturateStrength", desaturateStrength);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "emissionOmission", emissionOmission);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveRevealInSeconds", revealInSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveRevealOutSeconds", revealOutSeconds);
            PlayerViewPrefabTestUtility.SetSerializedField(
                settings,
                "inactiveRevealCurve",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return settings;
        }

        private static void AssertColorApproximately(Color expected, Color actual, float tolerance = 0.0001f)
        {
            Assert.That(
                actual.r,
                Is.EqualTo(expected.r).Within(tolerance),
                $"Color.r expected {expected.r:R} but was {actual.r:R}");
            Assert.That(
                actual.g,
                Is.EqualTo(expected.g).Within(tolerance),
                $"Color.g expected {expected.g:R} but was {actual.g:R}");
            Assert.That(
                actual.b,
                Is.EqualTo(expected.b).Within(tolerance),
                $"Color.b expected {expected.b:R} but was {actual.b:R}");
            Assert.That(
                actual.a,
                Is.EqualTo(expected.a).Within(tolerance),
                $"Color.a expected {expected.a:R} but was {actual.a:R}");
        }

        private static Renderer[] ReadDimRenderers(GravityFieldLockedTargetVisualTargetView target)
        {
            var serializedObject = new SerializedObject(target);
            var dimRenderersProperty = serializedObject.FindProperty("dimRenderers");
            Assert.That(dimRenderersProperty, Is.Not.Null);

            var renderers = new Renderer[dimRenderersProperty.arraySize];
            for (var i = 0; i < dimRenderersProperty.arraySize; i++)
            {
                renderers[i] = dimRenderersProperty
                    .GetArrayElementAtIndex(i)
                    .objectReferenceValue as Renderer;
            }

            return renderers;
        }

        private sealed class ActiveStateRecordingTileFeatureTarget :
            MonoBehaviour,
            ITileFeatureVisualTarget,
            ITileFeatureVisualCueSink
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public int PlayButtonActivatedCount { get; private set; }

            public bool WasActiveInHierarchyWhenButtonActivated { get; private set; }

            public void Configure(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public bool TryHandle(in TileFeatureVisualRequest request)
            {
                if (request.CueId != TileFeatureVisualCueId.ButtonActivated)
                {
                    return false;
                }

                PlayButtonActivatedCount++;
                WasActiveInHierarchyWhenButtonActivated = gameObject.activeInHierarchy;
                return true;
            }
        }

        private sealed class RecordingTileFeatureVisualTarget :
            MonoBehaviour,
            ITileFeatureVisualTarget,
            ITileFeatureVisualCueSink
        {
            public int TileId { get; private set; }

            public SurfaceCell Cell { get; private set; }

            public int DebugPlayButtonActivatedCount { get; private set; }

            public int DebugPlayDestroyTileTriggeredCount { get; private set; }

            public int DebugMoonBlockGeneratedCount { get; private set; }

            public int DebugLastMoonBlockGeneratedEntityId { get; private set; }

            public void Configure(int tileId, SurfaceCell cell)
            {
                TileId = tileId;
                Cell = cell;
            }

            public bool TryHandle(in TileFeatureVisualRequest request)
            {
                switch (request.CueId)
                {
                    case TileFeatureVisualCueId.ButtonActivated:
                        DebugPlayButtonActivatedCount++;
                        return true;
                    case TileFeatureVisualCueId.DestroyTileTriggered:
                        DebugPlayDestroyTileTriggeredCount++;
                        return true;
                    case TileFeatureVisualCueId.MoonBlockGenerated:
                        DebugMoonBlockGeneratedCount++;
                        DebugLastMoonBlockGeneratedEntityId = request.TargetEntityId;
                        return true;
                    default:
                        return false;
                }
            }
        }

        private sealed class RecordingGravityFieldVisualTarget :
            MonoBehaviour,
            IGravityFieldActivatedVisualTarget,
            IGravityFieldExpiredVisualTarget,
            IGravityFieldContinuousVisualTarget,
            IGravityFieldLockedTargetVisualTarget,
            IGravityFieldLockedBoxOneShotVisualTarget
        {
            public int ActivatedCount { get; private set; }

            public int ExpiredCount { get; private set; }

            public int ApplyContinuousCount { get; private set; }

            public int ClearContinuousCount { get; private set; }

            public int ApplyLockedTargetCount { get; private set; }

            public int ClearLockedTargetCount { get; private set; }

            public int LockedBoxOneShotCount { get; private set; }

            public GravityFieldVisualState LastContinuousState { get; private set; }

            public int LastLockedTargetEmitterEntityId { get; private set; }

            public GravityFieldLockedBoxPayload LastLockedBoxPayload { get; private set; }

            public void PlayGravityFieldActivated()
            {
                ActivatedCount++;
            }

            public void PlayGravityFieldExpired()
            {
                ExpiredCount++;
            }

            public void ApplyGravityFieldVisualState(GravityFieldVisualState state)
            {
                ApplyContinuousCount++;
                LastContinuousState = state;
            }

            public void ClearGravityFieldVisualState()
            {
                ClearContinuousCount++;
                LastContinuousState = default;
            }

            public void ApplyGravityFieldLockedTarget(int emitterEntityId)
            {
                ApplyLockedTargetCount++;
                LastLockedTargetEmitterEntityId = emitterEntityId;
            }

            public void ClearGravityFieldLockedTarget(int emitterEntityId)
            {
                ClearLockedTargetCount++;
                LastLockedTargetEmitterEntityId = emitterEntityId;
            }

            public void PlayGravityFieldLockedBox(GravityFieldLockedBoxPayload payload)
            {
                LockedBoxOneShotCount++;
                LastLockedBoxPayload = payload;
            }
        }

        private sealed class RecordingDestroyShrinkVfxStateExtension :
            IGameplayTickPresentationExtension,
            IGameplayDestroyShrinkVfxSequenceStateProvider
        {
            private readonly Dictionary<(int SourceEntityId, int SequenceId), DestroyShrinkVfxSequenceState> _states = new();

            public void SetState(
                int sourceEntityId,
                int sequenceId,
                DestroyShrinkVfxSequenceState state)
            {
                _states[(sourceEntityId, sequenceId)] = state;
            }

            public DestroyShrinkVfxSequenceState GetDestroyShrinkState(int sourceEntityId, int sequenceId)
            {
                return _states.TryGetValue((sourceEntityId, sequenceId), out var state)
                    ? state
                    : DestroyShrinkVfxSequenceState.None;
            }

            public void ResetSession()
            {
            }

            public void Present(in GameplayTickPresentationExtensionContext context)
            {
            }

            public void UpdatePresentation(float deltaTime)
            {
            }

            public void HardCleanup()
            {
                _states.Clear();
            }
        }

        private sealed class MotionOverrideViewFactory : IGameplayEntityViewFactory
        {
            private readonly IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> _entityMotionOverridesByEntityId;
            private readonly IReadOnlyDictionary<int, EnemyJumpMotionPresentationSnapshot> _jumpMotionOverridesByEntityId;
            private readonly Transform _parent;
            private readonly IReadOnlyDictionary<int, float> _unitMoveOverridesByEntityId;

            public MotionOverrideViewFactory(
                Transform parent,
                IReadOnlyDictionary<int, float> unitMoveOverridesByEntityId = null,
                IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> entityMotionOverridesByEntityId = null,
                IReadOnlyDictionary<int, EnemyJumpMotionPresentationSnapshot> jumpMotionOverridesByEntityId = null)
            {
                _parent = parent;
                _unitMoveOverridesByEntityId = unitMoveOverridesByEntityId;
                _entityMotionOverridesByEntityId = entityMotionOverridesByEntityId;
                _jumpMotionOverridesByEntityId = jumpMotionOverridesByEntityId;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                viewObject.transform.localPosition = Vector3.zero;
                viewObject.transform.localRotation = Quaternion.identity;
                viewObject.transform.localScale = Vector3.one;

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_unitMoveOverridesByEntityId != null &&
                    _unitMoveOverridesByEntityId.TryGetValue(entity.entityId, out var moveDurationSeconds))
                {
                    var authoring = viewObject.AddComponent<UnitLocomotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", moveDurationSeconds);
                }

                if (_entityMotionOverridesByEntityId != null &&
                    _entityMotionOverridesByEntityId.TryGetValue(entity.entityId, out var entityMotionOverride))
                {
                    var authoring = viewObject.AddComponent<EntityMotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "moveMotionDurationSeconds", entityMotionOverride.MoveMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushMotionDurationSeconds", entityMotionOverride.PushMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipMotionDurationSeconds", entityMotionOverride.FlipMotionDurationSeconds);
                }

                if (_jumpMotionOverridesByEntityId != null &&
                    _jumpMotionOverridesByEntityId.TryGetValue(entity.entityId, out var jumpMotionOverride))
                {
                    var authoring = viewObject.AddComponent<EnemyJumpMotionPresentationAuthoring>();
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "horizontalHoldBias", jumpMotionOverride.HorizontalHoldBias);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "apexHoldPower", jumpMotionOverride.ApexHoldPower);
                }

                return view;
            }
        }

        private sealed class BoxFlipDriverViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public BoxFlipDriverViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                viewObject.transform.localPosition = Vector3.zero;
                viewObject.transform.localRotation = Quaternion.identity;
                viewObject.transform.localScale = Vector3.one;

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                if (entity.type == EntityType.Box)
                {
                    var gripPoint = new GameObject("GripPoint").transform;
                    gripPoint.SetParent(view.ModelRoot, worldPositionStays: false);
                    gripPoint.localPosition = new Vector3(0.25f, 0f, 0f);
                    var driver = viewObject.AddComponent<BoxFlipInteractionDriver>();
                    PlayerViewPrefabTestUtility.SetSerializedField(driver, "visualRoot", view.ModelRoot);
                    PlayerViewPrefabTestUtility.SetSerializedField(driver, "gripPoint", gripPoint);
                }

                return view;
            }
        }

        private sealed class RecordingEnemySemanticPresentationDriver :
            MonoBehaviour,
            IEnemyVisualSemanticPresentationDriver
        {
            public int ApplyCount { get; private set; }

            public EnemyVisualSemanticState LastState { get; private set; }

            public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state)
            {
                ApplyCount++;
                LastState = state;
            }
        }
    }

    public sealed class PresentationPoseArbitrationTests
    {
        private const string ApplierPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayEntityPresentationApplier.cs";
        private const string CoordinatorPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs";
        private const string TrackStatePath =
            "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationTrackState.cs";

        [Test]
        [Category("Core")]
        public void CompatibilityPolicy_RejectsCrossOwnerRoleSpecificSourcesBeforePriority()
        {
            AssertCompatible(PresentationOwnerRole.Player, PresentationPoseSourceKind.PlayerContinuousLocomotion);
            AssertCompatible(PresentationOwnerRole.Player, PresentationPoseSourceKind.PlayerDeathHold, PresentationPoseChannel.TerminalHold);
            AssertRejected(
                PresentationOwnerRole.Player,
                PresentationPoseSourceKind.EnemyKinematicMotion,
                PresentationPoseChannel.BasePose,
                PresentationPoseRejectionReason.OwnerRoleMismatch);

            AssertCompatible(PresentationOwnerRole.Enemy, PresentationPoseSourceKind.EnemyKinematicMotion);
            AssertCompatible(PresentationOwnerRole.Enemy, PresentationPoseSourceKind.EnemyDeathHold, PresentationPoseChannel.TerminalHold);
            AssertRejected(
                PresentationOwnerRole.Enemy,
                PresentationPoseSourceKind.PlayerContinuousLocomotion,
                PresentationPoseChannel.BasePose,
                PresentationPoseRejectionReason.OwnerRoleMismatch);

            AssertRejected(
                PresentationOwnerRole.Unknown,
                PresentationPoseSourceKind.PlayerContinuousLocomotion,
                PresentationPoseChannel.BasePose,
                PresentationPoseRejectionReason.MissingRoleMetadata);
        }

        [Test]
        [Category("Core")]
        public void BasePoseArbitration_PlayerDeathHold_RejectsSameIdEnemyKinematicCandidate()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var playerPose = PoseAt(1f);
            var enemyPose = PoseAt(9f);
            stateStore.CommittedLocalTargetPoses[10] = playerPose;
            trackState.PlayerDeathHoldPoses[10] = playerPose;
            trackState.EnemyKinematicPresentationPoseOverrides[10] = new KinematicPresentationPose(
                enemyPose,
                MotionMode.Voluntary,
                TickKinematicMotionTerminalKind.None);

            var frames = Resolve(stateStore, trackState, sourceTick: 42);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(playerPose.Position));
            Assert.That(frame.BasePose.Position, Is.Not.EqualTo(enemyPose.Position));
            Assert.That(frame.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(frame.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(
                frames.Rejections.Any(rejection =>
                    rejection.Entity.EntityId == 10 &&
                    rejection.SourceKind == PresentationPoseSourceKind.EnemyKinematicMotion &&
                    rejection.Channel == PresentationPoseChannel.BasePose &&
                    rejection.Reason == PresentationPoseRejectionReason.OwnerRoleMismatch),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void PresentationPoseCandidateCollector_PlayerDeathHoldPoses_EmitsTypedTerminalHoldCandidate()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var playerPose = PoseAt(1f);
            trackState.PlayerDeathHoldPoses[10] = playerPose;

            var collector = new PresentationPoseCandidateCollector(stateStore, trackState);
            var candidates = new List<PresentationPoseCandidate>();
            collector.CollectCandidatesForEntity(10, sourceTick: 42, candidates);

            Assert.That(candidates, Has.Count.EqualTo(1));
            var candidate = candidates[0];
            Assert.That(candidate.Entity.EntityId, Is.EqualTo(10));
            Assert.That(candidate.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
            Assert.That(candidate.SourceKind, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(candidate.Channel, Is.EqualTo(PresentationPoseChannel.TerminalHold));
            Assert.That(candidate.Pose.Position, Is.EqualTo(playerPose.Position));
            Assert.That(candidate.SourceTick, Is.EqualTo(42));
            Assert.That(candidate.IsTerminal, Is.True);
            Assert.That(candidate.IsActiveLocomotion, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PresentationBasePoseFrameResolver_ChecksCompatibilityBeforePrioritySelection()
        {
            var resolverSource = File.ReadAllText(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayPresentationTrackState.cs"));

            var compatibilityIndex = resolverSource.IndexOf(
                "PresentationPoseCompatibilityPolicy.IsCompatible",
                StringComparison.Ordinal);
            var terminalSelectionIndex = resolverSource.IndexOf(
                "terminalCandidate = SelectTerminalCandidate",
                StringComparison.Ordinal);
            var liveSelectionIndex = resolverSource.IndexOf(
                "liveCandidate = SelectLiveCandidate",
                StringComparison.Ordinal);

            Assert.That(compatibilityIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(terminalSelectionIndex, Is.GreaterThan(compatibilityIndex));
            Assert.That(liveSelectionIndex, Is.GreaterThan(compatibilityIndex));
        }

        [Test]
        [Category("Core")]
        public void PresentationBasePoseFrameResolver_TerminalHoldBeatsLiveLocomotionAfterCompatibility()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var terminalPose = PoseAt(1f);
            var livePose = PoseAt(2f);
            trackState.PlayerDeathHoldPoses[10] = terminalPose;
            trackState.PlayerContinuousLocomotionPresentationPoseOverrides[10] =
                new PlayerContinuousLocomotionPresentationPose(
                    livePose,
                    ContinuousLocomotionMode.Moving,
                    TickKinematicMotionTerminalKind.None);

            var frames = Resolve(stateStore, trackState, sourceTick: 42);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(terminalPose.Position));
            Assert.That(frame.BasePose.Position, Is.Not.EqualTo(livePose.Position));
            Assert.That(frame.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(frame.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(frame.IsActiveLocomotion, Is.False);
            Assert.That(frames.Rejections, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void BasePoseArbitration_PlayerContinuous_BeatsCommittedFallback()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var committedPose = PoseAt(1f);
            var continuousPose = PoseAt(2f);
            stateStore.CommittedLocalTargetPoses[10] = committedPose;
            trackState.PlayerContinuousLocomotionPresentationPoseOverrides[10] =
                new PlayerContinuousLocomotionPresentationPose(
                    continuousPose,
                    ContinuousLocomotionMode.Moving,
                    TickKinematicMotionTerminalKind.None);

            var frames = Resolve(stateStore, trackState, sourceTick: 7);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(continuousPose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.PlayerContinuousLocomotion));
            Assert.That(frame.IsActiveLocomotion, Is.True);
        }

        [Test]
        [Category("Core")]
        public void BasePoseArbitration_EnemyKinematic_BeatsCommittedFallback()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var committedPose = PoseAt(1f);
            var kinematicPose = PoseAt(3f);
            stateStore.CommittedLocalTargetPoses[40] = committedPose;
            trackState.EnemyKinematicPresentationPoseOverrides[40] = new KinematicPresentationPose(
                kinematicPose,
                MotionMode.Voluntary,
                TickKinematicMotionTerminalKind.None);

            var frames = Resolve(stateStore, trackState, sourceTick: 7);

            Assert.That(frames.TryGetFrame(40, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(kinematicPose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.EnemyKinematicMotion));
            Assert.That(frame.IsActiveLocomotion, Is.True);
        }

        [Test]
        [Category("Core")]
        public void BasePoseArbitration_CommittedFallback_BeatsRetainedFallback()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var committedPose = PoseAt(1f);
            var retainedPose = PoseAt(4f);
            stateStore.CommittedLocalTargetPoses[10] = committedPose;
            stateStore.RetainedLocalTargetPoses[10] = retainedPose;

            var frames = Resolve(stateStore, trackState, sourceTick: 7);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(committedPose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.CommittedPose));
        }

        [Test]
        [Category("Core")]
        public void BasePoseArbitration_StaticWallCommittedPose_ResolvesStaticOwner()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            stateStore.EntityTypesByEntityId[30] = EntityType.None;
            var committedPose = PoseAt(1f);
            stateStore.CommittedLocalTargetPoses[30] = committedPose;

            var frames = Resolve(stateStore, trackState, sourceTick: 7);

            Assert.That(frames.TryGetFrame(30, out var frame), Is.True);
            Assert.That(frame.OwnerRole, Is.EqualTo(PresentationOwnerRole.Static));
            Assert.That(frame.BasePose.Position, Is.EqualTo(committedPose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.CommittedPose));
            Assert.That(frames.Rejections, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void JumpAdditiveLocalOffset_DoesNotReplaceResolvedBasePose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            var jumpEndPose = PoseAt(5f);
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            var jumpTrack = new JumpTrack();
            jumpTrack.Replace(JumpClip.Create(basePose, jumpEndPose, durationSeconds: 1f, arcHeightWorld: 0f));
            trackState.JumpTracks[40] = jumpTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(frames.TryGetFrame(40, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(basePose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.CommittedPose));
            Assert.That(channels.TryGetAdditiveLocalOffset(40, out var offset), Is.True);
            Assert.That(offset.Channel, Is.EqualTo(PresentationPoseChannel.AdditiveLocalOffset));
            Assert.That(offset.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.Jump));
            Assert.That(offset.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.None));
            Assert.That(offset.Offset.x, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void TerminalHold_SuppressesLiveJumpAdditive()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var terminalPose = PoseAt(1f);
            var jumpEndPose = PoseAt(5f);
            trackState.PlayerDeathHoldPoses[10] = terminalPose;
            var jumpTrack = new JumpTrack();
            jumpTrack.Replace(JumpClip.Create(terminalPose, jumpEndPose, durationSeconds: 1f, arcHeightWorld: 0f));
            trackState.JumpTracks[10] = jumpTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 42);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 42);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(channels.TryGetAdditiveLocalOffset(10, out _), Is.False);
            Assert.That(channels.TryGetAdditiveRotation(10, out _), Is.False);
            Assert.That(jumpTrack.ElapsedSeconds, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void GlideAdditiveOffset_IsResolvedBeforeApplication()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            var glideOffset = new Vector3(0f, 2f, 0f);
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            trackState.GlidePresentationOffsetsByEntityId[40] = glideOffset;

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(channels.TryGetAdditiveLocalOffset(40, out var offset), Is.True);
            Assert.That(offset.Channel, Is.EqualTo(PresentationPoseChannel.AdditiveLocalOffset));
            Assert.That(offset.OwnerRole, Is.EqualTo(PresentationOwnerRole.Enemy));
            Assert.That(offset.Offset, Is.EqualTo(glideOffset));
        }

        [Test]
        [Category("Core")]
        public void GlideAdditiveOffset_DoesNotReplaceResolvedBasePose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            trackState.GlidePresentationOffsetsByEntityId[40] = new Vector3(0f, 2f, 0f);

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(frames.TryGetFrame(40, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(basePose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.CommittedPose));
            Assert.That(channels.TryGetAdditiveLocalOffset(40, out var offset), Is.True);
            Assert.That(offset.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.GlideOffset));
        }

        [Test]
        [Category("Core")]
        public void TerminalHold_SuppressesLiveGlideAdditiveOffset()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var frames = new ResolvedPresentationFrameSet();
            frames.SetFrame(new ResolvedEntityPresentationFrame(
                new PresentationEntityKey(40),
                PresentationOwnerRole.Enemy,
                PoseAt(1f),
                new PresentationPoseProvenance(
                    PresentationOwnerRole.Enemy,
                    PresentationPoseSourceKind.EnemyDeathHold,
                    PresentationPoseSourceKind.EnemyDeathHold,
                    sourceTick: 42),
                isActiveLocomotion: false));
            trackState.GlidePresentationOffsetsByEntityId[40] = new Vector3(0f, 2f, 0f);

            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 42);

            Assert.That(channels.TryGetAdditiveLocalOffset(40, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void DeathOrExitRetained_SuppressesLiveGlideAdditiveOffset()
        {
            AssertGlideSuppressedByRetainedState(trackState => trackState.DeferredExitRetainedEntityIds.Add(40));
            AssertGlideSuppressedByRetainedState(trackState => trackState.ContactDelayedRetainedEntityIds.Add(40));
            AssertGlideSuppressedByRetainedState(trackState => trackState.DeathPresentationPlayingEntityIds.Add(40));
        }

        [Test]
        [Category("Core")]
        public void GlideAdditiveOffset_PreservesSourceKindOwnerAndOffset()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var glideOffset = new Vector3(0f, 2.5f, 0f);
            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            trackState.GlidePresentationOffsetsByEntityId[40] = glideOffset;

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 77);

            Assert.That(channels.TryGetAdditiveLocalOffset(40, out var offset), Is.True);
            Assert.That(offset.Entity.EntityId, Is.EqualTo(40));
            Assert.That(offset.OwnerRole, Is.EqualTo(PresentationOwnerRole.Enemy));
            Assert.That(offset.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.GlideOffset));
            Assert.That(offset.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.None));
            Assert.That(offset.Provenance.SourceTick, Is.EqualTo(77));
            Assert.That(offset.Offset, Is.EqualTo(glideOffset));
        }

        [Test]
        [Category("Core")]
        public void GlideAdditiveOffset_DoesNotSilentlyOverwriteJumpAdditiveOffset_WhenInvalidCoexistence()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            var jumpEndPose = PoseAt(5f);
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            var jumpTrack = new JumpTrack();
            jumpTrack.Replace(JumpClip.Create(basePose, jumpEndPose, durationSeconds: 1f, arcHeightWorld: 0f));
            trackState.JumpTracks[40] = jumpTrack;
            trackState.GlidePresentationOffsetsByEntityId[40] = new Vector3(0f, 2f, 0f);

            var frames = Resolve(stateStore, trackState, sourceTick: 7);

            Assert.Throws<System.InvalidOperationException>(() => ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7));
        }

        [Test]
        [Category("Core")]
        public void JumpWindupRotation_IsResolvedBeforeApplication()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            var windupTrack = CreateRotationTrack(
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                durationSeconds: 1f);
            trackState.JumpWindupRotationTracks[40] = windupTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(frames.TryGetFrame(40, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(basePose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.CommittedPose));
            Assert.That(channels.TryGetAdditiveRotation(40, out var rotation), Is.True);
            Assert.That(rotation.Channel, Is.EqualTo(PresentationPoseChannel.AdditiveRotation));
            Assert.That(rotation.OwnerRole, Is.EqualTo(PresentationOwnerRole.Enemy));
            Assert.That(rotation.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.EnemyJumpWindup));
            Assert.That(rotation.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.None));
            Assert.That(Quaternion.Angle(Quaternion.identity, rotation.Rotation), Is.GreaterThan(0f));
        }

        [Test]
        [Category("Core")]
        public void TerminalHold_SuppressesLiveJumpWindupRotation()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var terminalPose = PoseAt(1f);
            trackState.PlayerDeathHoldPoses[10] = terminalPose;
            var windupTrack = CreateRotationTrack(
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                durationSeconds: 1f);
            trackState.JumpWindupRotationTracks[10] = windupTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 42);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 42);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(channels.TryGetAdditiveRotation(10, out _), Is.False);
            Assert.That(windupTrack.HasClips, Is.True);
        }

        [Test]
        [Category("Core")]
        public void JumpWindupRotation_AndJumpTrackRotation_HaveDeterministicOrder()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            var jumpEndPose = new GameplayEntityPose(
                new Vector3(5f, 0f, 0f),
                Quaternion.Euler(0f, 45f, 0f));
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            var jumpTrack = new JumpTrack();
            jumpTrack.Replace(JumpClip.Create(basePose, jumpEndPose, durationSeconds: 1f, arcHeightWorld: 0f));
            trackState.JumpTracks[40] = jumpTrack;
            trackState.JumpWindupRotationTracks[40] = CreateRotationTrack(
                Quaternion.identity,
                Quaternion.Euler(0f, 90f, 0f),
                durationSeconds: 1f);

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(channels.TryGetAdditiveRotation(40, out var rotation), Is.True);
            Assert.That(rotation.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.Jump));
            Assert.That(Quaternion.Angle(Quaternion.identity, rotation.Rotation), Is.EqualTo(22.5f).Within(0.001f));
        }

        [Test]
        [Category("Core")]
        public void FlipResultTurn_IsResolvedAsAdditiveRotation()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var basePose = PoseAt(1f);
            stateStore.CommittedLocalTargetPoses[10] = basePose;
            trackState.PlayerFlipResultTurnTracks[10] = new PlayerFlipResultTurnTrackEntry(
                actionSequence: 7,
                startTick: 11,
                contactFacing: Direction.Left,
                resultFacing: Direction.Right,
                CreateRotationTrack(
                    Quaternion.identity,
                    Quaternion.Euler(0f, 90f, 0f),
                    durationSeconds: 1f));

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(channels.TryGetAdditiveRotation(10, out var rotation), Is.True);
            Assert.That(rotation.Channel, Is.EqualTo(PresentationPoseChannel.AdditiveRotation));
            Assert.That(rotation.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
            Assert.That(rotation.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.FlipResultTurn));
            Assert.That(rotation.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.None));
            Assert.That(Quaternion.Angle(Quaternion.identity, rotation.Rotation), Is.EqualTo(67.5f).Within(0.001f));
        }

        [Test]
        [Category("Core")]
        public void FlipResultTurn_WinsOverExistingAdditiveRotation()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var basePose = PoseAt(1f);
            var jumpEndPose = new GameplayEntityPose(
                new Vector3(5f, 0f, 0f),
                Quaternion.Euler(0f, 45f, 0f));
            stateStore.CommittedLocalTargetPoses[40] = basePose;
            var jumpTrack = new JumpTrack();
            jumpTrack.Replace(JumpClip.Create(basePose, jumpEndPose, durationSeconds: 1f, arcHeightWorld: 0f));
            trackState.JumpTracks[40] = jumpTrack;
            trackState.PlayerFlipResultTurnTracks[40] = new PlayerFlipResultTurnTrackEntry(
                actionSequence: 7,
                startTick: 11,
                contactFacing: Direction.Left,
                resultFacing: Direction.Right,
                CreateRotationTrack(
                    Quaternion.identity,
                    Quaternion.Euler(0f, 90f, 0f),
                    durationSeconds: 1f));

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(channels.TryGetAdditiveRotation(40, out var rotation), Is.True);
            Assert.That(rotation.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.FlipResultTurn));
            Assert.That(Quaternion.Angle(Quaternion.identity, rotation.Rotation), Is.EqualTo(67.5f).Within(0.001f));
        }

        [Test]
        [Category("Core")]
        public void FlipResultTurn_CompletedTrackId_IsRegisteredByResolver()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            stateStore.CommittedLocalTargetPoses[10] = PoseAt(1f);
            trackState.PlayerFlipResultTurnTracks[10] = new PlayerFlipResultTurnTrackEntry(
                actionSequence: 7,
                startTick: 11,
                contactFacing: Direction.Left,
                resultFacing: Direction.Right,
                CreateRotationTrack(
                    Quaternion.identity,
                    Quaternion.Euler(0f, 90f, 0f),
                    durationSeconds: 0.25f));

            var frames = Resolve(stateStore, trackState, sourceTick: 7);
            ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 7);

            Assert.That(trackState.CompletedPlayerFlipResultTurnTrackIds, Is.EquivalentTo(new[] { 10 }));
        }

        [Test]
        [Category("Core")]
        public void JumpDetachedVisibility_DoesNotBecomeTerminalSelection()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var detachedPose = PoseAt(3f);
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    detachedPose,
                    new SurfaceCell(FaceId.Floor, 0, 0));

            var collector = new PresentationPoseCandidateCollector(stateStore, trackState);
            var candidates = new List<PresentationPoseCandidate>();
            collector.CollectCandidatesForEntity(40, sourceTick: 9, candidates);

            Assert.That(candidates, Has.Count.EqualTo(1));
            var candidate = candidates[0];
            Assert.That(candidate.SourceKind, Is.EqualTo(PresentationPoseSourceKind.JumpDetachedPose));
            Assert.That(candidate.Channel, Is.EqualTo(PresentationPoseChannel.BasePose));
            Assert.That(candidate.IsTerminal, Is.False);
        }

        [Test]
        [Category("Core")]
        public void TerminalHold_BeatsJumpDetachedPoseCompatibilityBridge()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var terminalPose = PoseAt(1f);
            var detachedPose = PoseAt(9f);
            trackState.PlayerDeathHoldPoses[10] = terminalPose;
            stateStore.JumpDetachedVisibilityStates[10] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    detachedPose,
                    new SurfaceCell(FaceId.Floor, 0, 0));

            var frames = Resolve(stateStore, trackState, sourceTick: 42);

            Assert.That(frames.TryGetFrame(10, out var frame), Is.True);
            Assert.That(frame.BasePose.Position, Is.EqualTo(terminalPose.Position));
            Assert.That(frame.BasePose.Position, Is.Not.EqualTo(detachedPose.Position));
            Assert.That(frame.Provenance.BaseSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
            Assert.That(frame.Provenance.TerminalSource, Is.EqualTo(PresentationPoseSourceKind.PlayerDeathHold));
        }

        [Test]
        [Category("Core")]
        public void JumpDetachedVisibility_IsResolvedBeforeApplication()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 3);
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    PoseAt(3f),
                    sourceCell);

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var visibility = ResolveVisibility(
                stateStore,
                trackState,
                frames,
                new CubeTopologyState(FaceId.Floor),
                sourceTick: 77);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.EntityKey.EntityId, Is.EqualTo(40));
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(resolved.Provenance.OwnerRole, Is.EqualTo(PresentationOwnerRole.Enemy));
            Assert.That(resolved.Provenance.Cell, Is.EqualTo(sourceCell));
            Assert.That(resolved.Provenance.Face, Is.EqualTo(FaceId.Floor));
            Assert.That(resolved.Provenance.LifetimeToken, Is.EqualTo(77));
        }

        [Test]
        [Category("Core")]
        public void JumpDetachedVisibility_CandidateIntegrationPreservesResolvedOutput()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 3);
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    PoseAt(3f),
                    sourceCell);

            var topology = new CubeTopologyState(FaceId.Floor);
            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = CollectVisibilityCandidates(
                stateStore,
                trackState,
                frames,
                topology,
                sourceTick: 77);
            var visibility = ResolveVisibility(
                stateStore,
                trackState,
                frames,
                topology,
                sourceTick: 77);

            Assert.That(candidates.Count, Is.EqualTo(1));
            Assert.That(candidates.CandidateCount, Is.EqualTo(1));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(1));
            var candidate = entityCandidates[0];
            Assert.That(candidate.EntityKey.EntityId, Is.EqualTo(40));
            Assert.That(candidate.IsVisible, Is.True);
            Assert.That(candidate.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(candidate.Provenance.OwnerRole, Is.EqualTo(PresentationOwnerRole.Enemy));
            Assert.That(candidate.Provenance.Cell, Is.EqualTo(sourceCell));
            Assert.That(candidate.Provenance.Face, Is.EqualTo(FaceId.Floor));
            Assert.That(candidate.Provenance.LifetimeToken, Is.EqualTo(77));
            Assert.That(candidate.Priority, Is.EqualTo(200));
            Assert.That(candidate.IsFallback, Is.False);
            Assert.That(candidate.IsStatefulTrackSample, Is.False);
            Assert.That(candidate.IsHighPrioritySuppressionSource, Is.False);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.EntityKey, Is.EqualTo(candidate.EntityKey));
            Assert.That(resolved.IsVisible, Is.EqualTo(candidate.IsVisible));
            Assert.That(resolved.Provenance, Is.EqualTo(candidate.Provenance));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TrackSampleWritesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var sourceCell = new SurfaceCell(FaceId.Floor, 2, 3);
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    PoseAt(3f),
                    sourceCell);
            trackState.VisibilityTracks[40] = VisibilityTrack.CreateHide(durationSeconds: 1f);

            var topology = new CubeTopologyState(FaceId.Floor);
            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = CollectVisibilityCandidates(
                stateStore,
                trackState,
                frames,
                topology,
                sourceTick: 77);
            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime: 1f,
                sourceTick: 77);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.False);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(2));
            var trackCandidate = entityCandidates.Single(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.VisibilityTrackSample);
            Assert.That(trackCandidate.IsVisible, Is.False);
            Assert.That(trackCandidate.Priority, Is.EqualTo(500));
            Assert.That(trackCandidate.IsFallback, Is.False);
            Assert.That(trackCandidate.IsStatefulTrackSample, Is.True);
            Assert.That(trackCandidate.IsHighPrioritySuppressionSource, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackCandidateCollection_DoesNotAdvanceTrack()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            var visibilityTrack = VisibilityTrack.CreateHide(durationSeconds: 1f);
            trackState.VisibilityTracks[40] = visibilityTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime: 1f,
                sourceTick: 77);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().IsVisible, Is.False);
            Assert.That(visibilityTrack.SampleAndAdvance(0.5f, fallbackVisibility: true), Is.True);
            Assert.That(visibilityTrack.IsComplete, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackCandidateCollection_DoesNotCompleteTracks()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            var visibilityTrack = VisibilityTrack.CreateHide(durationSeconds: 1f);
            trackState.VisibilityTracks[40] = visibilityTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime: 1f,
                sourceTick: 77);

            Assert.That(visibilityTrack.IsComplete, Is.False);
            Assert.That(trackState.CompletedVisibilityTrackIds, Is.Empty);
            Assert.That(trackState.VisibilityTracks.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackCandidateCollection_DoesNotClearHiddenMetadata()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            var visibilityTrack = VisibilityTrack.CreateHide(durationSeconds: 1f);
            trackState.VisibilityTracks[40] = visibilityTrack;

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime: 1f,
                sourceTick: 77);

            Assert.That(stateStore.CommittedLocalTargetPoses.ContainsKey(40), Is.True);
            Assert.That(stateStore.RetainedLocalTargetPoses.ContainsKey(40), Is.True);
            Assert.That(trackState.VisibilityTracks.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PostResolveVisibilityCarrierCollection_PreservesRemoveDetachSpawnPriorityMetadata()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var changes = new[]
            {
                new TickVisibilityChange(40, TickVisibilityChangeKind.Spawn, cell, topology, Direction.Right),
                new TickVisibilityChange(40, TickVisibilityChangeKind.Detach, cell, topology, Direction.Right),
                new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, cell, topology, Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(3));
            AssertGenericVisibilityCandidate(
                entityCandidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn),
                isVisible: true,
                priority: 200,
                ownerRole: PresentationOwnerRole.Enemy,
                cell,
                FaceId.Floor,
                sourceTick: 88);
            AssertGenericVisibilityCandidate(
                entityCandidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityDetach),
                isVisible: false,
                priority: 300,
                ownerRole: PresentationOwnerRole.Enemy,
                cell,
                FaceId.Floor,
                sourceTick: 88);
            AssertGenericVisibilityCandidate(
                entityCandidates.Single(candidate =>
                    candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityRemove),
                isVisible: false,
                priority: 400,
                ownerRole: PresentationOwnerRole.Enemy,
                cell,
                FaceId.Floor,
                sourceTick: 88);
        }

        [Test]
        [Category("Core")]
        public void PostResolveVisibilityCarrierCollection_DoesNotChangeFinalVisibility()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(EnemyJumpPhase.Airborne, PoseAt(3f), cell);
            var frames = Resolve(stateStore, trackState, sourceTick: 88);
            var candidates = CollectVisibilityCandidates(
                stateStore,
                trackState,
                frames,
                new CubeTopologyState(FaceId.Floor),
                sourceTick: 88);
            var resolver = new PresentationResolvedVisibilityResolver();
            var visibility = new ResolvedPresentationVisibilitySet();
            resolver.ResolveCandidates(candidates, visibility);

            CollectPostResolveVisibilityCarriers(
                stateStore,
                trackState,
                new[]
                {
                    new TickVisibilityChange(
                        40,
                        TickVisibilityChangeKind.Remove,
                        cell,
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Right),
                },
                sourceTick: 88,
                candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityRemove), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PostResolveVisibilityCarrierCollection_DoesNotRemoveTickVisibilityChangeEvents()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);

            var cell = new SurfaceCell(FaceId.Floor, 0, 1);
            var changes = new List<TickVisibilityChange>
            {
                new TickVisibilityChange(
                    10,
                    TickVisibilityChangeKind.Spawn,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Up),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 12, candidates);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].EntityId, Is.EqualTo(10));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(TickVisibilityChangeKind.Spawn));
            Assert.That(candidates.TryGetCandidates(10, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().Provenance.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
        }

        [Test]
        [Category("Core")]
        public void PostResolveVisibilityCarrierCollection_DoesNotOwnCleanupLifecycle()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.VisibilityTracks[40] = VisibilityTrack.CreateHide(durationSeconds: 1f);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectPostResolveVisibilityCarriers(
                stateStore,
                trackState,
                new[]
                {
                    new TickVisibilityChange(
                        40,
                        TickVisibilityChangeKind.Remove,
                        new SurfaceCell(FaceId.Floor, 2, 3),
                        new CubeTopologyState(FaceId.Floor),
                        Direction.Right),
                },
                sourceTick: 88,
                candidates);

            Assert.That(trackState.CompletedVisibilityTrackIds, Is.Empty);
            Assert.That(trackState.VisibilityTracks.ContainsKey(40), Is.True);
            Assert.That(stateStore.RetainedLocalTargetPoses.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SpawnOnlyWritesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    TickVisibilityChangeKind.Spawn,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(1));
            AssertGenericVisibilityCandidate(
                entityCandidates.Single(),
                isVisible: true,
                priority: 200,
                ownerRole: PresentationOwnerRole.Enemy,
                cell,
                FaceId.Floor,
                sourceTick: 88);
            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilitySpawn));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_DoesNotFinalWriteGenericDetach()
        {
            AssertGenericVisibilitySpawnOnlyDoesNotCollect(TickVisibilityChangeKind.Detach);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_DoesNotFinalWriteGenericRemove()
        {
            AssertGenericVisibilitySpawnOnlyDoesNotCollect(TickVisibilityChangeKind.Remove);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_GenericDetachRemoveRemainPostResolveCarriers()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var changes = new[]
            {
                new TickVisibilityChange(40, TickVisibilityChangeKind.Detach, cell, topology, Direction.Right),
                new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, cell, topology, Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(2));
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityDetach), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityRemove), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SuppressesSpawnWhenSameEntityDetachExists()
        {
            AssertGenericSpawnSuppressedBySameEntityConflict(TickVisibilityChangeKind.Detach);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SuppressesSpawnWhenSameEntityRemoveExists()
        {
            AssertGenericSpawnSuppressedBySameEntityConflict(TickVisibilityChangeKind.Remove);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SuppressesSpawnWhenSameEntityDetachAndRemoveExist()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var changes = new[]
            {
                new TickVisibilityChange(40, TickVisibilityChangeKind.Spawn, cell, topology, Direction.Right),
                new TickVisibilityChange(40, TickVisibilityChangeKind.Detach, cell, topology, Direction.Right),
                new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, cell, topology, Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(3));
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityDetach), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilityRemove), Is.True);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_DifferentEntityDetachDoesNotSuppressSpawn()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            MarkEnemy(stateStore, 41);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var changes = new[]
            {
                new TickVisibilityChange(40, TickVisibilityChangeKind.Spawn, cell, topology, Direction.Right),
                new TickVisibilityChange(41, TickVisibilityChangeKind.Detach, cell, topology, Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilitySpawn));
            Assert.That(candidates.TryGetCandidates(41, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_SpawnCandidateAlsoRemainsInPostResolveCarrierCollection()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    TickVisibilityChangeKind.Spawn,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilitySpawn));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Count(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn), Is.EqualTo(2));
            Assert.That(candidates.CandidateCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_CollectsDeathPresentationPlayingWithRetainedPose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeathPresentationPlayingEntityIds.Add(40);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            AssertRetainedDeathExitCandidate(entityCandidates.Single(), PresentationOwnerRole.Enemy, sourceTick: 91);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_CollectsDeferredExitRetainedWithRetainedPose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeferredExitRetainedEntityIds.Add(40);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            AssertRetainedDeathExitCandidate(entityCandidates.Single(), PresentationOwnerRole.Enemy, sourceTick: 91);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_CollectsContactDelayedRetainedWithRetainedPose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.ContactDelayedRetainedEntityIds.Add(40);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            AssertRetainedDeathExitCandidate(entityCandidates.Single(), PresentationOwnerRole.Enemy, sourceTick: 91);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotCollectPlainRetainedLocalTargetPose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(40, out _), Is.False);
            Assert.That(candidates.CandidateCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotCollectDeathStateWithoutRetainedPose()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            trackState.DeathPresentationPlayingEntityIds.Add(40);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(40, out _), Is.False);
            Assert.That(candidates.CandidateCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_CollectsTerminalSuppressionFromResolvedTerminalProvenance()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);
            trackState.PlayerDeathHoldPoses[10] = PoseAt(4f);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(candidates.TryGetCandidates(10, out var entityCandidates), Is.True);
            var candidate = entityCandidates.Single();
            Assert.That(candidate.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(candidate.IsVisible, Is.True);
            Assert.That(candidate.Priority, Is.EqualTo(900));
            Assert.That(candidate.Provenance.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
            Assert.That(candidate.Provenance.LifetimeToken, Is.EqualTo(91));
            Assert.That(candidate.IsHighPrioritySuppressionSource, Is.True);
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_TerminalSuppressionWritesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);
            trackState.PlayerDeathHoldPoses[10] = PoseAt(4f);

            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();
            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(10, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(resolved.Provenance.OwnerRole, Is.EqualTo(PresentationOwnerRole.Player));
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotReadPlayerDeathHoldRawStore()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", TrackStatePath));
            var methodStart = source.IndexOf(
                "public void CollectRetainedDeathOrExitVisibility",
                StringComparison.Ordinal);
            var nextMethodStart = source.IndexOf(
                "public void CollectVisibilityTrackSamples",
                methodStart,
                StringComparison.Ordinal);
            var methodBlock = source.Substring(methodStart, nextMethodStart - methodStart);

            Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(nextMethodStart, Is.GreaterThan(methodStart));
            Assert.That(methodBlock, Does.Contain("resolvedFrame.Provenance.TerminalSource"));
            Assert.That(methodBlock, Does.Not.Contain("PlayerDeathHoldPoses"));
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotChangeFinalVisibility()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeathPresentationPlayingEntityIds.Add(40);
            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var visibility = new ResolvedPresentationVisibilitySet();
            visibility.SetVisibility(new ResolvedEntityPresentationVisibility(
                new PresentationEntityKey(40),
                isVisible: false,
                new PresentationVisibilityProvenance(
                    PresentationVisibilitySourceKind.GenericVisibilityRemove,
                    PresentationOwnerRole.Enemy,
                    null,
                    null,
                    lifetimeToken: 90)));
            var candidates = new PresentationVisibilityCandidateSet();

            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.False);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilityRemove));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            AssertRetainedDeathExitCandidate(entityCandidates.Single(), PresentationOwnerRole.Enemy, sourceTick: 91);
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_RetainedDeathExitWritesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeferredExitRetainedEntityIds.Add(40);
            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(candidates.CandidateCount, Is.EqualTo(1));
            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotOwnExitCleanup()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeferredExitRetainedEntityIds.Add(40);
            trackState.ContactDelayedRetainedEntityIds.Add(40);
            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(trackState.DeferredExitRetainedEntityIds.Contains(40), Is.True);
            Assert.That(trackState.ContactDelayedRetainedEntityIds.Contains(40), Is.True);
            Assert.That(stateStore.RetainedLocalTargetPoses.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotOwnDeathPresentationState()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeathPresentationPlayingEntityIds.Add(40);
            var frames = Resolve(stateStore, trackState, sourceTick: 91);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectRetainedDeathOrExitVisibility(stateStore, trackState, frames, sourceTick: 91, candidates);

            Assert.That(trackState.DeathPresentationPlayingEntityIds.Contains(40), Is.True);
            Assert.That(stateStore.RetainedLocalTargetPoses.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void RetainedDeathExitCandidateCollection_DoesNotTouchBasePoseTerminalResolver()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", TrackStatePath));
            var collectorStart = source.IndexOf(
                "internal sealed class PresentationVisibilityCandidateCollector",
                StringComparison.Ordinal);
            var resolverStart = source.IndexOf(
                "internal sealed class PresentationResolvedVisibilityResolver",
                collectorStart,
                StringComparison.Ordinal);
            var collectorBlock = source.Substring(collectorStart, resolverStart - collectorStart);

            Assert.That(collectorBlock, Does.Not.Contain("PresentationBasePoseFrameResolver"));
            Assert.That(collectorBlock, Does.Not.Contain("PresentationPoseCandidateCollector"));
            Assert.That(collectorBlock, Does.Not.Contain("PresentationPoseChannel.TerminalHold"));
            Assert.That(collectorBlock, Does.Not.Contain("BaseSource ="));
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_CollectsTransitionVisibilityState()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.TransitionVisibilityStates[40] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(4f),
                projectedSlot: null,
                FaceId.Floor);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            AssertTransitionEntityVisibilityCandidate(
                entityCandidates.Single(),
                entityId: 40,
                ownerRole: PresentationOwnerRole.Enemy,
                face: FaceId.Floor,
                sourceTick: 92);
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_PreservesEntityAndSurfaceMetadata()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkPlayer(stateStore, 10);
            stateStore.TransitionVisibilityStates[10] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(1f),
                projectedSlot: null,
                FaceId.Front);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 93, candidates);

            Assert.That(candidates.TryGetCandidates(10, out var entityCandidates), Is.True);
            AssertTransitionEntityVisibilityCandidate(
                entityCandidates.Single(),
                entityId: 10,
                ownerRole: PresentationOwnerRole.Player,
                face: FaceId.Front,
                sourceTick: 93);
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_StaticWallUsesStaticOwner()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            stateStore.EntityTypesByEntityId[30] = EntityType.None;
            stateStore.TransitionVisibilityStates[30] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(1f),
                projectedSlot: null,
                FaceId.Front);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 93, candidates);

            Assert.That(candidates.TryGetCandidates(30, out var entityCandidates), Is.True);
            AssertTransitionEntityVisibilityCandidate(
                entityCandidates.Single(),
                entityId: 30,
                ownerRole: PresentationOwnerRole.Static,
                face: FaceId.Front,
                sourceTick: 93);
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_DoesNotCollectWhenNoTransitionVisibilityState()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);

            Assert.That(candidates.Count, Is.Zero);
            Assert.That(candidates.CandidateCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_TransitionEntityWritesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.TransitionVisibilityStates[40] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(4f),
                projectedSlot: null,
                FaceId.Floor);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(candidates.CandidateCount, Is.EqualTo(1));
            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_DoesNotChangeApplierFallback()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.TransitionVisibilityStates[40] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(4f),
                projectedSlot: null,
                FaceId.Floor);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);

            Assert.That(
                ResolveCurrentFinalVisible(40, hasTransitionVisibility: true),
                Is.True);
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_DoesNotOwnTransitionCleanup()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.TransitionVisibilityStates[40] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(4f),
                projectedSlot: null,
                FaceId.Floor);
            trackState.CompletedTransitionVisibilityStateIds.Add(40);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);

            Assert.That(stateStore.TransitionVisibilityStates.ContainsKey(40), Is.True);
            Assert.That(trackState.CompletedTransitionVisibilityStateIds, Has.Count.EqualTo(1));
            Assert.That(trackState.CompletedTransitionVisibilityStateIds[0], Is.EqualTo(40));
        }

        [Test]
        [Category("Core")]
        public void TransitionEntityVisibilityCandidateCollection_DoesNotRemoveTransitionVisibilityStates()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.TransitionVisibilityStates[40] = new TransitionVisibilityState(
                TickTransitionVisibilityMode.ShowAtTransitionStart,
                PoseAt(4f),
                projectedSlot: null,
                FaceId.Floor);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectTransitionEntityVisibility(stateStore, trackState, sourceTick: 92, candidates);

            Assert.That(stateStore.TransitionVisibilityStates, Has.Count.EqualTo(1));
            Assert.That(stateStore.TransitionVisibilityStates[40].SurfaceFace, Is.EqualTo(FaceId.Floor));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_DoesNotFinalWriteGenericSpawnThroughPostResolveCarrierCollector()
        {
            AssertGenericVisibilityChangeDoesNotFinalWrite(TickVisibilityChangeKind.Spawn);
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_DoesNotFinalWriteGenericDetach()
        {
            AssertGenericVisibilityChangeDoesNotFinalWrite(TickVisibilityChangeKind.Detach);
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_DoesNotFinalWriteGenericRemove()
        {
            AssertGenericVisibilityChangeDoesNotFinalWrite(TickVisibilityChangeKind.Remove);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TrackSampleResolvesProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            trackState.VisibilityTracks[40] = VisibilityTrack.CreateHide(durationSeconds: 1f);
            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime: 1f,
                sourceTick: 77);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.False);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_TerminalSuppressionBeatsRetainedAndTransition()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.RetainedDeathOrExit,
                isVisible: true,
                priority: 800,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                isVisible: true,
                priority: 900,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_RetainedDeathExitBeatsTransition()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.RetainedDeathOrExit,
                isVisible: true,
                priority: 800,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_TransitionBeatsJumpDetached()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_TerminalSuppressionBeatsGenericSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                isVisible: true,
                priority: 900,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_RetainedDeathExitBeatsGenericSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.RetainedDeathOrExit,
                isVisible: true,
                priority: 800,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_TransitionEntityBeatsGenericSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));

            var visibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
        }

        [Test]
        [Category("Core")]
        public void GenericSpawnFinalWriteBoundary_JumpDetachedBeatsGenericSpawn()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    TickVisibilityChangeKind.Spawn,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(EnemyJumpPhase.Cooldown, PoseAt(3f), cell);
            var frames = Resolve(stateStore, trackState, sourceTick: 88);
            var candidates = CollectVisibilityCandidates(
                stateStore,
                trackState,
                frames,
                new CubeTopologyState(FaceId.Floor),
                sourceTick: 88);
            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);

            CollectPostResolveVisibilityCarriers(
                stateStore,
                trackState,
                changes,
                sourceTick: 88,
                candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.False);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Count(
                candidate => candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn), Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RemoveBeatsDetachAndSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                isVisible: false,
                priority: 300,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilityRemove));
            Assert.That(winner.IsVisible, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_DetachBeatsSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                isVisible: false,
                priority: 300,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilityDetach));
            Assert.That(winner.IsVisible, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_VisibilityTrackBeatsGenericChange()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: true,
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(winner.IsVisible, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TrackSampleBeatsGenericDetach()
        {
            AssertVisibilityTrackSampleBeatsGeneric(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                genericPriority: 300);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TrackSampleBeatsGenericSpawn()
        {
            AssertVisibilityTrackSampleBeatsGeneric(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                genericPriority: 200);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TransitionEntityBeatsTrackSample()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: false,
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
            Assert.That(winner.IsVisible, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_TerminalSuppressionBeatsTrackSample()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: false,
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                isVisible: true,
                priority: 900,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(winner.IsVisible, Is.True);
            Assert.That(winner.IsHighPrioritySuppressionSource, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TerminalDeathOrExitSuppressionBeatsGenericSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                isVisible: true,
                priority: 900,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(winner.IsVisible, Is.True);
            Assert.That(winner.IsHighPrioritySuppressionSource, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RetainedDeathExitBeatsGenericSpawn()
        {
            AssertRetainedDeathExitBeats(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                isStatefulTrackSample: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RetainedDeathExitBeatsGenericRemove()
        {
            AssertRetainedDeathExitBeats(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400,
                isStatefulTrackSample: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RetainedDeathExitBeatsJumpDetached()
        {
            AssertRetainedDeathExitBeats(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                isStatefulTrackSample: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RetainedDeathExitBeatsVisibilityTrackSample()
        {
            AssertRetainedDeathExitBeats(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: false,
                priority: 500,
                isStatefulTrackSample: true);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TerminalSuppressionBeatsTransitionEntityVisibility()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression,
                isVisible: true,
                priority: 900,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TerminalDeathOrExitSuppression));
            Assert.That(winner.IsHighPrioritySuppressionSource, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_RetainedDeathExitBeatsTransitionEntityVisibility()
        {
            AssertRetainedDeathExitBeats(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                isStatefulTrackSample: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TransitionEntityVisibilityBeatsGenericRemove()
        {
            AssertTransitionEntityVisibilityBeats(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TransitionEntityVisibilityBeatsGenericSpawn()
        {
            AssertTransitionEntityVisibilityBeats(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TransitionEntityVisibilityBeatsJumpDetached()
        {
            AssertTransitionEntityVisibilityBeats(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_JumpDetachedTieBreaksOverGenericSpawn()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(winner.IsVisible, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_TieBreakIsNotInsertionOrderDependent()
        {
            var firstOrder = new PresentationVisibilityCandidateSet();
            firstOrder.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            firstOrder.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));

            var reversedOrder = new PresentationVisibilityCandidateSet();
            reversedOrder.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            reversedOrder.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(firstOrder, 40, out var firstWinner), Is.True);
            Assert.That(resolver.TryResolveCandidateWinner(reversedOrder, 40, out var reversedWinner), Is.True);
            Assert.That(firstWinner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(reversedWinner.Provenance.SourceKind, Is.EqualTo(firstWinner.Provenance.SourceKind));
            Assert.That(reversedWinner.IsVisible, Is.EqualTo(firstWinner.IsVisible));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_SameSourceTieUsesSourceTick()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                isVisible: false,
                priority: 300,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityDetach,
                isVisible: false,
                priority: 300,
                sourceTick: 89));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilityDetach));
            Assert.That(winner.Provenance.LifetimeToken, Is.EqualTo(89));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_DoesNotWriteFinalResolvedVisibilitySet()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: false,
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));
            var visibility = new ResolvedPresentationVisibilitySet();
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);

            Assert.That(winner.IsVisible, Is.False);
            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowResolver_DoesNotOwnCleanupLifecycle()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.VisibilityTracks[40] = VisibilityTrack.CreateHide(durationSeconds: 1f);
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out _), Is.True);

            Assert.That(trackState.CompletedVisibilityTrackIds, Is.Empty);
            Assert.That(trackState.VisibilityTracks.ContainsKey(40), Is.True);
            Assert.That(stateStore.RetainedLocalTargetPoses.ContainsKey(40), Is.True);
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowComparison_JumpDetachedOnlyMatchesCurrentFinalVisibility()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            var resolvedVisibility = ResolveVisibilityFromCandidates(candidates);

            var winner = ResolveShadowWinner(candidates);
            var currentFinalVisible = ResolveCurrentFinalVisible(
                entityId: 40,
                resolvedVisibility: resolvedVisibility);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(currentFinalVisible, Is.EqualTo(winner.IsVisible));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowComparison_GenericSpawnOnlyMatchesCurrentCommittedFallback()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));

            var winner = ResolveShadowWinner(candidates);
            var currentFinalVisible = ResolveCurrentFinalVisible(
                entityId: 40,
                hasCommittedLocalTargetPose: true);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.GenericVisibilitySpawn));
            Assert.That(currentFinalVisible, Is.EqualTo(winner.IsVisible));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_DetachHideTrackRemainsVisibleBeforeCompletion()
        {
            AssertGenericHideTrackTiming(
                TickVisibilityChangeKind.Detach,
                expectedGenericSourceKind: PresentationVisibilitySourceKind.GenericVisibilityDetach,
                deltaTime: 0f,
                expectedVisible: true);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_DetachHideTrackHiddenAfterCompletion()
        {
            AssertGenericHideTrackTiming(
                TickVisibilityChangeKind.Detach,
                expectedGenericSourceKind: PresentationVisibilitySourceKind.GenericVisibilityDetach,
                deltaTime: 1f,
                expectedVisible: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_RemoveHideTrackRemainsVisibleBeforeCompletion()
        {
            AssertGenericHideTrackTiming(
                TickVisibilityChangeKind.Remove,
                expectedGenericSourceKind: PresentationVisibilitySourceKind.GenericVisibilityRemove,
                deltaTime: 0f,
                expectedVisible: true);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_RemoveHideTrackHiddenAfterCompletion()
        {
            AssertGenericHideTrackTiming(
                TickVisibilityChangeKind.Remove,
                expectedGenericSourceKind: PresentationVisibilitySourceKind.GenericVisibilityRemove,
                deltaTime: 1f,
                expectedVisible: false);
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_VisibilityTrackSampleOnlyMatchesResolvedFinalVisibility()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            var shadowTrack = VisibilityTrack.CreateHide(durationSeconds: 1f);
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                shadowTrack.SampleWithoutAdvance(deltaTime: 1f, fallbackVisibility: true),
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));

            var winner = ResolveShadowWinner(candidates);
            var resolvedVisibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(resolvedVisibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.EqualTo(winner.IsVisible));
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowComparison_TransitionEntityVisibilityOnlyMatchesCurrentTransitionFallback()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));

            var winner = ResolveShadowWinner(candidates);
            var currentFinalVisible = ResolveCurrentFinalVisible(
                entityId: 40,
                hasTransitionVisibility: true);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
            Assert.That(currentFinalVisible, Is.EqualTo(winner.IsVisible));
        }

        [Test]
        [Category("Core")]
        public void VisibilityCandidateShadowComparison_JumpDetachedBeatsGenericSpawnAndMatchesCurrentFinalVisibility()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                isVisible: true,
                priority: 200,
                sourceTick: 88));
            var finalCandidates = new PresentationVisibilityCandidateSet();
            finalCandidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.JumpDetached,
                isVisible: false,
                priority: 200,
                sourceTick: 88));
            var resolvedVisibility = ResolveVisibilityFromCandidates(finalCandidates);

            var winner = ResolveShadowWinner(candidates);
            var currentFinalVisible = ResolveCurrentFinalVisible(
                entityId: 40,
                resolvedVisibility: resolvedVisibility);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.JumpDetached));
            Assert.That(currentFinalVisible, Is.EqualTo(winner.IsVisible));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrackFinalMigration_VisibilityTrackSampleBeatsGenericRemoveInResolvedFinalVisibility()
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.GenericVisibilityRemove,
                isVisible: false,
                priority: 400,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                VisibilityTrack.CreateHide(durationSeconds: 1f).SampleWithoutAdvance(
                    deltaTime: 0f,
                    fallbackVisibility: true),
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));

            var winner = ResolveShadowWinner(candidates);
            var resolvedVisibility = ResolveVisibilityFromCandidates(candidates);

            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(resolvedVisibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.EqualTo(winner.IsVisible));
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
        }

        [Test]
        [Category("Core")]
        public void RetainedTransitionFinalWriteOnly_RetainedDeathExitBeatsJumpDetachedInProductionFinalSet()
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.JumpDetachedVisibilityStates[40] =
                new JumpDetachedVisibilityState(
                    EnemyJumpPhase.Airborne,
                    PoseAt(3f),
                    new SurfaceCell(FaceId.Floor, 2, 3));
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.DeathPresentationPlayingEntityIds.Add(40);

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var visibility = ResolveVisibility(
                stateStore,
                trackState,
                frames,
                new CubeTopologyState(FaceId.Floor),
                sourceTick: 77);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.True);
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotUseJumpDetachedVisibilityForBasePoseSelection()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));

            var resolvedBasePoseIndex = source.IndexOf(
                "var localPose = resolvedFrame.BasePose;",
                StringComparison.Ordinal);
            var visibilityDecisionIndex = source.IndexOf(
                "var hasResolvedVisibility =",
                StringComparison.Ordinal);
            var jumpDetachedLookupIndex = source.IndexOf(
                "_stateStore.JumpDetachedVisibilityStates",
                StringComparison.Ordinal);

            Assert.That(resolvedBasePoseIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(visibilityDecisionIndex, Is.GreaterThan(resolvedBasePoseIndex));
            Assert.That(jumpDetachedLookupIndex, Is.GreaterThan(resolvedBasePoseIndex));
            Assert.That(source, Does.Not.Contain("PresentationPoseSourceKind.JumpDetachedPose,\n                    PresentationPoseChannel.TerminalHold"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotReadJumpDetachedVisibilityStatesForFinalVisibilityAfterMigration()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));
            var visibilityDecisionIndex = source.IndexOf(
                "var hasResolvedVisibility =",
                StringComparison.Ordinal);
            var visibilityTrackIndex = source.IndexOf(
                "if (!hasPlayerDeathHoldPose &&",
                visibilityDecisionIndex,
                StringComparison.Ordinal);
            var finalVisibilityBlock = source.Substring(
                visibilityDecisionIndex,
                visibilityTrackIndex - visibilityDecisionIndex);

            Assert.That(source, Does.Contain("ResolvedPresentationVisibilitySet"));
            Assert.That(finalVisibilityBlock, Does.Contain("TryGetVisibility"));
            Assert.That(finalVisibilityBlock, Does.Contain("resolvedEntityVisibility.IsVisible"));
            Assert.That(finalVisibilityBlock, Does.Not.Contain("_stateStore.JumpDetachedVisibilityStates"));
            Assert.That(finalVisibilityBlock, Does.Not.Contain("IsJumpDetachedVisibleForTopology"));
        }

        [Test]
        [Category("Core")]
        public void JumpPresentationChannels_AreTypedBeforeApplication()
        {
            var coordinatorSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", CoordinatorPath));
            var trackStateSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", TrackStatePath));
            var applierSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));

            var channelResolveIndex = coordinatorSource.IndexOf(
                "_resolvedChannelResolver.ResolveAdditiveChannels",
                StringComparison.Ordinal);
            var applierIndex = coordinatorSource.IndexOf(
                "_entityPresentationApplier.Apply",
                StringComparison.Ordinal);

            Assert.That(channelResolveIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(applierIndex, Is.GreaterThan(channelResolveIndex));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseChannel.AdditiveLocalOffset"));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseSourceKind.Jump"));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseSourceKind.EnemyJumpWindup"));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseSourceKind.GlideOffset"));
            Assert.That(applierSource, Does.Not.Contain("_trackState.JumpWindupRotationTracks.TryGetValue"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotRawSamplePlayerFlipResultTurnTracks()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));
            var trackStateSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", TrackStatePath));
            var applyStart = source.IndexOf("public void Apply(", StringComparison.Ordinal);
            var applyEnd = source.IndexOf(
                "private IReadOnlyList<int> BuildProcessingEntityIds",
                applyStart,
                StringComparison.Ordinal);
            var processingStart = applyEnd;
            var processingEnd = source.IndexOf(
                "private void AddProcessingEntityId",
                processingStart,
                StringComparison.Ordinal);

            Assert.That(applyStart, Is.GreaterThanOrEqualTo(0));
            Assert.That(applyEnd, Is.GreaterThan(applyStart));
            Assert.That(processingEnd, Is.GreaterThan(processingStart));

            var applyBlock = source.Substring(applyStart, applyEnd - applyStart);
            var processingBlock = source.Substring(processingStart, processingEnd - processingStart);
            Assert.That(applyBlock, Does.Contain("resolvedChannels.TryGetAdditiveRotation"));
            Assert.That(applyBlock, Does.Not.Contain("_trackState.PlayerFlipResultTurnTracks.TryGetValue"));
            Assert.That(applyBlock, Does.Not.Contain("playerFlipResultTurnTrack.Track.SampleAndAdvance"));
            Assert.That(processingBlock, Does.Contain("resolvedChannels.EntityIds"));
            Assert.That(processingBlock, Does.Not.Contain("PlayerFlipResultTurnTracks"));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseSourceKind.FlipResultTurn"));
            Assert.That(trackStateSource, Does.Contain("channelSet.SetAdditiveRotation"));
            Assert.That(source, Does.Contain("CleanupCompletedPlayerFlipResultTurnTracks"));
            Assert.That(source, Does.Contain("CompletedPlayerFlipResultTurnTrackIds"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotReadGlidePresentationOffsetsForFinalPositionAfterMigration()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));

            Assert.That(source, Does.Contain("resolvedChannels.TryGetAdditiveLocalOffset"));
            Assert.That(source, Does.Not.Contain("_trackState.GlidePresentationOffsetsByEntityId"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotUseGlideOffsetForBasePoseSelection()
        {
            var applierSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));
            var trackStateSource = File.ReadAllText(Path.Combine(Application.dataPath, "..", TrackStatePath));

            Assert.That(applierSource, Does.Not.Contain("GlidePresentationOffsetsByEntityId.TryGetValue"));
            Assert.That(trackStateSource, Does.Not.Contain("PresentationPoseSourceKind.GlideOffset,\n                    PresentationPoseChannel.BasePose"));
            Assert.That(trackStateSource, Does.Contain("PresentationPoseChannel.AdditiveLocalOffset"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotOwnBasePoseStoreSelection()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));

            Assert.That(source, Does.Not.Contain("TryGetPresentationPoseOverride"));
            Assert.That(source, Does.Not.Contain("PlayerContinuousLocomotionPresentationPoseOverrides"));
            Assert.That(source, Does.Not.Contain("EnemyKinematicPresentationPoseOverrides"));
            Assert.That(source, Does.Not.Contain("PlayerDeathHoldPoses"));
            Assert.That(source, Does.Contain("ResolvedPresentationFrameSet"));
            Assert.That(source, Does.Contain("resolvedFrame.BasePose"));
        }

        [Test]
        [Category("Core")]
        public void GameplayEntityPresentationApplier_DoesNotReadJumpTracksForBasePose()
        {
            var source = File.ReadAllText(Path.Combine(Application.dataPath, "..", ApplierPath));

            Assert.That(source, Does.Not.Contain("_trackState.JumpTracks.TryGetValue"));
            Assert.That(source, Does.Not.Contain("_trackState.JumpWindupRotationTracks.TryGetValue"));
            Assert.That(source, Does.Contain("ResolvedPresentationChannelSet"));
            Assert.That(source, Does.Contain("resolvedChannels.TryGetAdditiveLocalOffset"));
            Assert.That(source, Does.Contain("resolvedChannels.TryGetAdditiveRotation"));
        }

        private static void AssertCompatible(
            PresentationOwnerRole ownerRole,
            PresentationPoseSourceKind sourceKind,
            PresentationPoseChannel channel = PresentationPoseChannel.BasePose)
        {
            Assert.That(
                PresentationPoseCompatibilityPolicy.IsCompatible(
                    ownerRole,
                    sourceKind,
                    channel,
                    out var rejectionReason),
                Is.True);
            Assert.That(rejectionReason, Is.EqualTo(PresentationPoseRejectionReason.None));
        }

        private static void AssertRejected(
            PresentationOwnerRole ownerRole,
            PresentationPoseSourceKind sourceKind,
            PresentationPoseChannel channel,
            PresentationPoseRejectionReason expectedReason)
        {
            Assert.That(
                PresentationPoseCompatibilityPolicy.IsCompatible(
                    ownerRole,
                    sourceKind,
                    channel,
                    out var rejectionReason),
                Is.False);
            Assert.That(rejectionReason, Is.EqualTo(expectedReason));
        }

        private static ResolvedPresentationFrameSet Resolve(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            int sourceTick)
        {
            var collector = new PresentationPoseCandidateCollector(stateStore, trackState);
            var resolver = new PresentationBasePoseFrameResolver(collector);
            var frames = new ResolvedPresentationFrameSet();
            resolver.Resolve(sourceTick, frames);
            return frames;
        }

        private static ResolvedPresentationChannelSet ResolveChannels(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            ResolvedPresentationFrameSet frames,
            float deltaTime,
            bool hasActiveBoardRotationTween,
            int sourceTick)
        {
            var resolver = new PresentationResolvedChannelResolver(stateStore, trackState);
            var channels = new ResolvedPresentationChannelSet();
            resolver.ResolveAdditiveChannels(deltaTime, hasActiveBoardRotationTween, sourceTick, frames, channels);
            return channels;
        }

        private static void AssertGlideSuppressedByRetainedState(Action<GameplayPresentationTrackState> configureRetainedState)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);

            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            stateStore.RetainedLocalTargetPoses[40] = PoseAt(9f);
            trackState.GlidePresentationOffsetsByEntityId[40] = new Vector3(0f, 2f, 0f);
            configureRetainedState(trackState);

            var frames = Resolve(stateStore, trackState, sourceTick: 77);
            var channels = ResolveChannels(
                stateStore,
                trackState,
                frames,
                deltaTime: 0.5f,
                hasActiveBoardRotationTween: false,
                sourceTick: 77);

            Assert.That(channels.TryGetAdditiveLocalOffset(40, out _), Is.False);
        }

        private static ResolvedPresentationVisibilitySet ResolveVisibility(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            ResolvedPresentationFrameSet frames,
            CubeTopologyState topology,
            int sourceTick)
        {
            var candidates = CollectVisibilityCandidates(stateStore, trackState, frames, topology, sourceTick);
            var resolver = new PresentationResolvedVisibilityResolver();
            var visibility = new ResolvedPresentationVisibilitySet();
            resolver.ResolveCandidates(candidates, visibility);
            return visibility;
        }

        private static PresentationVisibilityCandidateSet CollectVisibilityCandidates(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            ResolvedPresentationFrameSet frames,
            CubeTopologyState topology,
            int sourceTick)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            var candidates = new PresentationVisibilityCandidateSet();
            collector.CollectJumpDetachedVisibility(topology, sourceTick, frames, candidates);
            collector.CollectRetainedDeathOrExitVisibility(sourceTick, frames, candidates);
            collector.CollectTransitionEntityVisibility(sourceTick, candidates);
            return candidates;
        }

        private static void CollectVisibilityTrackSamples(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            ResolvedPresentationFrameSet frames,
            PresentationVisibilityCandidateSet candidates,
            float deltaTime,
            int sourceTick)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            collector.CollectVisibilityTrackSamples(deltaTime, sourceTick, frames, candidates);
        }

        private static void CollectPostResolveVisibilityCarriers(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            int sourceTick,
            PresentationVisibilityCandidateSet candidates)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            collector.CollectPostResolveVisibilityCarriers(visibilityChanges, sourceTick, candidates);
        }

        private static void CollectGenericVisibilitySpawnOnly(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            IReadOnlyList<TickVisibilityChange> visibilityChanges,
            int sourceTick,
            PresentationVisibilityCandidateSet candidates)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            collector.CollectGenericVisibilitySpawnOnly(visibilityChanges, sourceTick, candidates);
        }

        private static void CollectRetainedDeathOrExitVisibility(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            ResolvedPresentationFrameSet frames,
            int sourceTick,
            PresentationVisibilityCandidateSet candidates)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            collector.CollectRetainedDeathOrExitVisibility(sourceTick, frames, candidates);
        }

        private static void CollectTransitionEntityVisibility(
            GameplayPresentationStateStore stateStore,
            GameplayPresentationTrackState trackState,
            int sourceTick,
            PresentationVisibilityCandidateSet candidates)
        {
            var collector = new PresentationVisibilityCandidateCollector(stateStore, trackState);
            collector.CollectTransitionEntityVisibility(sourceTick, candidates);
        }

        private static PresentationVisibilityCandidate ResolveShadowWinner(
            PresentationVisibilityCandidateSet candidates,
            int entityId = 40)
        {
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, entityId, out var winner), Is.True);
            return winner;
        }

        private static ResolvedPresentationVisibilitySet ResolveVisibilityFromCandidates(
            PresentationVisibilityCandidateSet candidates)
        {
            var resolver = new PresentationResolvedVisibilityResolver();
            var visibility = new ResolvedPresentationVisibilitySet();
            resolver.ResolveCandidates(candidates, visibility);
            return visibility;
        }

        private static void AssertGenericVisibilityChangeDoesNotFinalWrite(TickVisibilityChangeKind changeKind)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    changeKind,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();
            var visibility = ResolveVisibilityFromCandidates(candidates);

            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().Provenance.SourceKind, Is.EqualTo(changeKind switch
            {
                TickVisibilityChangeKind.Spawn => PresentationVisibilitySourceKind.GenericVisibilitySpawn,
                TickVisibilityChangeKind.Detach => PresentationVisibilitySourceKind.GenericVisibilityDetach,
                TickVisibilityChangeKind.Remove => PresentationVisibilitySourceKind.GenericVisibilityRemove,
                _ => PresentationVisibilitySourceKind.None,
            }));
            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
        }

        private static void AssertGenericVisibilitySpawnOnlyDoesNotCollect(TickVisibilityChangeKind changeKind)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    changeKind,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Single().Provenance.SourceKind, Is.EqualTo(changeKind switch
            {
                TickVisibilityChangeKind.Detach => PresentationVisibilitySourceKind.GenericVisibilityDetach,
                TickVisibilityChangeKind.Remove => PresentationVisibilitySourceKind.GenericVisibilityRemove,
                _ => PresentationVisibilitySourceKind.None,
            }));
        }

        private static void AssertGenericSpawnSuppressedBySameEntityConflict(TickVisibilityChangeKind conflictKind)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var topology = new CubeTopologyState(FaceId.Floor);
            var changes = new[]
            {
                new TickVisibilityChange(40, TickVisibilityChangeKind.Spawn, cell, topology, Direction.Right),
                new TickVisibilityChange(40, conflictKind, cell, topology, Direction.Right),
            };
            var candidates = new PresentationVisibilityCandidateSet();

            CollectGenericVisibilitySpawnOnly(stateStore, trackState, changes, sourceTick: 88, candidates);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out _), Is.False);
            Assert.That(visibility.Count, Is.Zero);
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates, Has.Count.EqualTo(2));
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.GenericVisibilitySpawn), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == (conflictKind == TickVisibilityChangeKind.Detach
                    ? PresentationVisibilitySourceKind.GenericVisibilityDetach
                    : PresentationVisibilitySourceKind.GenericVisibilityRemove)), Is.True);
        }

        private static void AssertGenericHideTrackTiming(
            TickVisibilityChangeKind changeKind,
            PresentationVisibilitySourceKind expectedGenericSourceKind,
            float deltaTime,
            bool expectedVisible)
        {
            var stateStore = new GameplayPresentationStateStore();
            var trackState = new GameplayPresentationTrackState();
            MarkEnemy(stateStore, 40);
            stateStore.CommittedLocalTargetPoses[40] = PoseAt(1f);
            trackState.VisibilityTracks[40] = VisibilityTrack.CreateHide(durationSeconds: 1f);
            var cell = new SurfaceCell(FaceId.Floor, 2, 3);
            var changes = new[]
            {
                new TickVisibilityChange(
                    40,
                    changeKind,
                    cell,
                    new CubeTopologyState(FaceId.Floor),
                    Direction.Right),
            };
            var frames = Resolve(stateStore, trackState, sourceTick: 88);
            var candidates = new PresentationVisibilityCandidateSet();

            CollectVisibilityTrackSamples(
                stateStore,
                trackState,
                frames,
                candidates,
                deltaTime,
                sourceTick: 88);
            var visibility = ResolveVisibilityFromCandidates(candidates);
            CollectPostResolveVisibilityCarriers(stateStore, trackState, changes, sourceTick: 88, candidates);

            Assert.That(visibility.TryGetVisibility(40, out var resolved), Is.True);
            Assert.That(resolved.IsVisible, Is.EqualTo(expectedVisible));
            Assert.That(resolved.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(candidates.TryGetCandidates(40, out var entityCandidates), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == expectedGenericSourceKind), Is.True);
            Assert.That(entityCandidates.Any(candidate =>
                candidate.Provenance.SourceKind == PresentationVisibilitySourceKind.VisibilityTrackSample), Is.True);
        }

        private static void AssertVisibilityTrackSampleBeatsGeneric(
            PresentationVisibilitySourceKind genericSourceKind,
            int genericPriority)
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                genericSourceKind,
                isVisible: false,
                priority: genericPriority,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.VisibilityTrackSample,
                isVisible: true,
                priority: 500,
                sourceTick: 88,
                isStatefulTrackSample: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.VisibilityTrackSample));
            Assert.That(winner.IsVisible, Is.True);
            Assert.That(winner.IsStatefulTrackSample, Is.True);
        }

        private static bool ResolveCurrentFinalVisible(
            int entityId,
            ResolvedPresentationVisibilitySet resolvedVisibility = null,
            bool hasCommittedLocalTargetPose = false,
            bool hasPresentationPoseOverride = false,
            bool hasPlayerDeathHoldPose = false,
            bool hasActiveLocalMotion = false,
            bool hasActiveOriginalViewMotion = false,
            bool isDeferredExitRetained = false,
            bool isContactDelayedRetained = false,
            bool isDeathPresentationPlaying = false,
            bool hasTransitionVisibility = false,
            VisibilityTrack visibilityTrack = null,
            float deltaTime = 0f)
        {
            var resolvedEntityVisibility = default(ResolvedEntityPresentationVisibility);
            var hasResolvedVisibility =
                resolvedVisibility != null &&
                resolvedVisibility.TryGetVisibility(entityId, out resolvedEntityVisibility);
            var isVisible = PresentationVisibilityFallbackResolver.Resolve(
                new PresentationVisibilityFallbackInputs(
                    hasPresentationPoseOverride,
                    hasPlayerDeathHoldPose,
                    hasCommittedLocalTargetPose,
                    hasActiveLocalMotion,
                    hasActiveOriginalViewMotion,
                    isDeferredExitRetained,
                    isContactDelayedRetained,
                    isDeathPresentationPlaying,
                    hasResolvedVisibility,
                    hasResolvedVisibility && resolvedEntityVisibility.IsVisible,
                    hasTransitionVisibility));

            if (!hasPlayerDeathHoldPose && visibilityTrack != null)
            {
                visibilityTrack.AdvanceAndReportCompletion(deltaTime);
            }

            return isVisible;
        }

        private static void AssertGenericVisibilityCandidate(
            PresentationVisibilityCandidate candidate,
            bool isVisible,
            int priority,
            PresentationOwnerRole ownerRole,
            SurfaceCell cell,
            FaceId face,
            int sourceTick)
        {
            Assert.That(candidate.EntityKey.EntityId, Is.GreaterThan(0));
            Assert.That(candidate.IsVisible, Is.EqualTo(isVisible));
            Assert.That(candidate.Provenance.OwnerRole, Is.EqualTo(ownerRole));
            Assert.That(candidate.Provenance.Cell, Is.EqualTo(cell));
            Assert.That(candidate.Provenance.Face, Is.EqualTo(face));
            Assert.That(candidate.Provenance.LifetimeToken, Is.EqualTo(sourceTick));
            Assert.That(candidate.Priority, Is.EqualTo(priority));
            Assert.That(candidate.IsFallback, Is.False);
            Assert.That(candidate.IsStatefulTrackSample, Is.False);
            Assert.That(candidate.IsHighPrioritySuppressionSource, Is.False);
        }

        private static void AssertRetainedDeathExitCandidate(
            PresentationVisibilityCandidate candidate,
            PresentationOwnerRole ownerRole,
            int sourceTick)
        {
            Assert.That(candidate.EntityKey.EntityId, Is.EqualTo(40));
            Assert.That(candidate.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
            Assert.That(candidate.IsVisible, Is.True);
            Assert.That(candidate.Priority, Is.EqualTo(800));
            Assert.That(candidate.Provenance.OwnerRole, Is.EqualTo(ownerRole));
            Assert.That(candidate.Provenance.Cell, Is.Null);
            Assert.That(candidate.Provenance.Face, Is.Null);
            Assert.That(candidate.Provenance.LifetimeToken, Is.EqualTo(sourceTick));
            Assert.That(candidate.IsFallback, Is.False);
            Assert.That(candidate.IsStatefulTrackSample, Is.False);
            Assert.That(candidate.IsHighPrioritySuppressionSource, Is.True);
        }

        private static void AssertTransitionEntityVisibilityCandidate(
            PresentationVisibilityCandidate candidate,
            int entityId,
            PresentationOwnerRole ownerRole,
            FaceId face,
            int sourceTick)
        {
            Assert.That(candidate.EntityKey.EntityId, Is.EqualTo(entityId));
            Assert.That(candidate.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
            Assert.That(candidate.IsVisible, Is.True);
            Assert.That(candidate.Priority, Is.EqualTo(600));
            Assert.That(candidate.Provenance.OwnerRole, Is.EqualTo(ownerRole));
            Assert.That(candidate.Provenance.Cell, Is.Null);
            Assert.That(candidate.Provenance.Face, Is.EqualTo(face));
            Assert.That(candidate.Provenance.LifetimeToken, Is.EqualTo(sourceTick));
            Assert.That(candidate.IsFallback, Is.False);
            Assert.That(candidate.IsStatefulTrackSample, Is.False);
            Assert.That(candidate.IsHighPrioritySuppressionSource, Is.False);
        }

        private static void AssertRetainedDeathExitBeats(
            PresentationVisibilitySourceKind contenderSourceKind,
            bool isVisible,
            int priority,
            bool isStatefulTrackSample)
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                contenderSourceKind,
                isVisible,
                priority,
                sourceTick: 88,
                isStatefulTrackSample: isStatefulTrackSample));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.RetainedDeathOrExit,
                isVisible: true,
                priority: 800,
                sourceTick: 88,
                isHighPrioritySuppressionSource: true));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.RetainedDeathOrExit));
            Assert.That(winner.IsVisible, Is.True);
            Assert.That(winner.IsHighPrioritySuppressionSource, Is.True);
        }

        private static void AssertTransitionEntityVisibilityBeats(
            PresentationVisibilitySourceKind contenderSourceKind,
            bool isVisible,
            int priority)
        {
            var candidates = new PresentationVisibilityCandidateSet();
            candidates.AddCandidate(CreateVisibilityCandidate(
                contenderSourceKind,
                isVisible,
                priority,
                sourceTick: 88));
            candidates.AddCandidate(CreateVisibilityCandidate(
                PresentationVisibilitySourceKind.TransitionEntityVisibility,
                isVisible: true,
                priority: 600,
                sourceTick: 88));
            var resolver = new PresentationVisibilityCandidateWinnerResolver();

            Assert.That(resolver.TryResolveCandidateWinner(candidates, 40, out var winner), Is.True);
            Assert.That(winner.Provenance.SourceKind, Is.EqualTo(PresentationVisibilitySourceKind.TransitionEntityVisibility));
            Assert.That(winner.IsVisible, Is.True);
            Assert.That(winner.Priority, Is.EqualTo(600));
        }

        private static PresentationVisibilityCandidate CreateVisibilityCandidate(
            PresentationVisibilitySourceKind sourceKind,
            bool isVisible,
            int priority,
            int sourceTick,
            bool isStatefulTrackSample = false,
            bool isHighPrioritySuppressionSource = false)
        {
            return new PresentationVisibilityCandidate(
                new PresentationEntityKey(40),
                isVisible,
                new PresentationVisibilityProvenance(
                    sourceKind,
                    PresentationOwnerRole.Enemy,
                    new SurfaceCell(FaceId.Floor, 2, 3),
                    FaceId.Floor,
                    sourceTick),
                priority,
                isFallback: false,
                isStatefulTrackSample,
                isHighPrioritySuppressionSource);
        }

        private static void MarkPlayer(GameplayPresentationStateStore stateStore, int entityId)
        {
            stateStore.EntityTypesByEntityId[entityId] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[entityId] = UnitRole.Player;
        }

        private static void MarkEnemy(GameplayPresentationStateStore stateStore, int entityId)
        {
            stateStore.EntityTypesByEntityId[entityId] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[entityId] = UnitRole.Enemy;
        }

        private static GameplayEntityPose PoseAt(float x)
        {
            return new GameplayEntityPose(new Vector3(x, 0f, 0f), Quaternion.identity);
        }

        private static RotationTrack CreateRotationTrack(
            Quaternion startRotation,
            Quaternion endRotation,
            float durationSeconds)
        {
            var track = new RotationTrack();
            track.Append(RotationClip.Create(startRotation, endRotation, durationSeconds));
            return track;
        }
    }
}
