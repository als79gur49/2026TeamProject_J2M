using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTickPresentationCoordinatorTests
    {
        [Test]
        public void GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveMotionWithoutEntityOverride_UsesGlobalDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        public void GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PlayerMoveMotionOverride_UsesPlayerPrefabDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_PlayerMoveMotionOverride_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        public void GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionOverride_PrefersUnitLocomotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        public void GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_UnitMoveMotionWithoutUnitAuthoring_FallsBackToEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        public void GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxPushMotionOverride_UsesLegacyEntityMotionAuthoring");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        public void GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ItemConsumeEffect_DoesNotUseMoveOverrideDuration");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickViewPresenter_ItemConsumeEffect_PlayerPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
                        CleanupPhaseResult.Empty,
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
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation(itemConsumeEffectDurationSeconds + 0.01f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(0));

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
        public void GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxDestroyEffect_DoesNotUsePushOverrideDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
                        CleanupPhaseResult.Empty,
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
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PresentInitialStackedUnits_AssignsDistinctSurfaceOffsets()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentInitialStackedUnits_AssignsDistinctSurfaceOffsets");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
                var midpoint = (playerPosition + enemyPosition) * 0.5f;

                Assert.That(playerPosition.x, Is.LessThan(center.x));
                Assert.That(enemyPosition.x, Is.GreaterThan(center.x));
                Assert.That(Vector3.Distance(playerPosition, enemyPosition), Is.GreaterThan(0.01f));
                Assert.That(playerPosition.y, Is.EqualTo(center.y).Within(0.001f));
                Assert.That(enemyPosition.y, Is.EqualTo(center.y).Within(0.001f));
                AssertPositionApproximately(midpoint, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_MoveIntoOccupiedCell_ReflowsStackedUnitOffsets()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveIntoOccupiedCell_ReflowsStackedUnitOffsets");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
                var midpoint = (playerPosition + enemyPosition) * 0.5f;

                Assert.That(playerPosition.x, Is.LessThan(center.x));
                Assert.That(enemyPosition.x, Is.GreaterThan(center.x));
                Assert.That(Vector3.Distance(playerPosition, enemyPosition), Is.GreaterThan(0.01f));
                AssertPositionApproximately(midpoint, center);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayOnFaceTangentPlane()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_StackedUnitsOnCeilingFace_StayOnFaceTangentPlane");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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

                Assert.That(Vector3.Distance(playerView.transform.localPosition, enemyView.transform.localPosition), Is.GreaterThan(0.01f));
                Assert.That(Mathf.Abs(Vector3.Dot(playerOffset, projectedPose.Normal)), Is.LessThan(0.001f));
                Assert.That(Mathf.Abs(Vector3.Dot(enemyOffset, projectedPose.Normal)), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayTimingProfile CreateTimingProfile()
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 60,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                moveMotionDurationSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8);
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
            };
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
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
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
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
            return new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                CleanupPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                new TickPresentationData(
                    motions),
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

        private sealed class MotionOverrideViewFactory : IGameplayEntityViewFactory
        {
            private readonly IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> _entityMotionOverridesByEntityId;
            private readonly Transform _parent;
            private readonly IReadOnlyDictionary<int, float> _unitMoveOverridesByEntityId;

            public MotionOverrideViewFactory(
                Transform parent,
                IReadOnlyDictionary<int, float> unitMoveOverridesByEntityId = null,
                IReadOnlyDictionary<int, EntityMotionPresentationSnapshot> entityMotionOverridesByEntityId = null)
            {
                _parent = parent;
                _unitMoveOverridesByEntityId = unitMoveOverridesByEntityId;
                _entityMotionOverridesByEntityId = entityMotionOverridesByEntityId;
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

                return view;
            }
        }
    }
}
