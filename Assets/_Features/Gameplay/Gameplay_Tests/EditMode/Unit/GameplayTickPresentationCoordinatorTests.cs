using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayTickPresentationCoordinatorTests
    {
        [Test]
        [Category("Extended")]
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
        [Category("Full")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void GameplayTickViewPresenter_EnemyChargeMoveMotionOverride_UsesEntityChargeMoveAuthoring_AndIgnoresUnitMoveOverride()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyChargeMoveMotionOverride_UsesEntityChargeMoveAuthoring_AndIgnoresUnitMoveOverride");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float unitMoveOverrideSeconds = 0.1f;
                const float entityChargeMoveOverrideSeconds = 0.4f;
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
                            {
                                20,
                                new EntityMotionPresentationSnapshot(
                                    moveMotionDurationSeconds: 0.8f,
                                    chargeMoveMotionDurationSeconds: entityChargeMoveOverrideSeconds,
                                    pushMotionDurationSeconds: EntityMotionPresentationAuthoring.UseGlobalTimingSentinel,
                                    flipMotionDurationSeconds: EntityMotionPresentationAuthoring.UseGlobalTimingSentinel)
                            },
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
                        CreateEnemyUnit(20, sourceCell, EnemyAiMode.Charge),
                    },
                    topology);

                presenter.Present(
                    CreateMotionTickResult(
                        CreateEnemyUnit(20, destinationCell, EnemyAiMode.Charge),
                        topology,
                        sourceCell,
                        destinationCell,
                        TickEntityMotionKind.ChargeMove));
                presenter.UpdatePresentation(0.2f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: 0.2f,
                    durationSeconds: entityChargeMoveOverrideSeconds);

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
        public void GameplayTickViewPresenter_EnemyChargeMoveWithoutExplicitOverride_UsesGlobalChargeMoveDurationWithMoveInterpolation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_EnemyChargeMoveWithoutExplicitOverride_UsesGlobalChargeMoveDurationWithMoveInterpolation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                const float unitMoveOverrideSeconds = 0.1f;
                const float globalChargeMoveDurationSeconds = 0.5f;
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new MotionOverrideViewFactory(
                        registry.transform,
                        unitMoveOverridesByEntityId: new Dictionary<int, float>
                        {
                            { 20, unitMoveOverrideSeconds },
                        }));
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = new GameplayTimingProfile(
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
                    maxTicksPerFrame: 8,
                    chargeMoveDurationSeconds: globalChargeMoveDurationSeconds);
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
                        CreateEnemyUnit(20, sourceCell, EnemyAiMode.Charge),
                    },
                    topology);

                presenter.Present(
                    CreateMotionTickResult(
                        CreateEnemyUnit(20, destinationCell, EnemyAiMode.Charge),
                        topology,
                        sourceCell,
                        destinationCell,
                        TickEntityMotionKind.ChargeMove));
                presenter.UpdatePresentation(globalChargeMoveDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(boardBounds, topology, sourceCell, EntityType.Unit);
                var destinationPosition = GetProjectedEntityPosition(boardBounds, topology, destinationCell, EntityType.Unit);
                var expectedLinearPosition = ResolveLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: globalChargeMoveDurationSeconds * 0.5f,
                    durationSeconds: globalChargeMoveDurationSeconds);
                var easedPosition = ResolveEasedLinearPosition(
                    sourcePosition,
                    destinationPosition,
                    elapsedSeconds: globalChargeMoveDurationSeconds * 0.5f,
                    durationSeconds: globalChargeMoveDurationSeconds);

                Assert.That(view.transform.localPosition.x, Is.LessThan(destinationPosition.x - 0.001f));
                AssertPositionApproximately(view.transform.localPosition, expectedLinearPosition);
                Assert.That(Mathf.Abs(view.transform.localPosition.x - easedPosition.x), Is.GreaterThan(0.01f));
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
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpAirborneVisibility_ClearsOnLanding");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_JumpRetry_RetargetsWithoutSnap");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
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
        [Category("Extended")]
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
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds + 0.01f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds - timingProfile.PushMotionDurationSeconds);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(0));
                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
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
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation(boxDestroyEffectDurationSeconds + 0.01f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation((flipMotionDurationSeconds - boxDestroyEffectDurationSeconds) + 0.05f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_PlayerAcceptedHit_WithHitVfxPrefab_SpawnsTransientHitEffect()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerAcceptedHit_WithHitVfxPrefab_SpawnsTransientHitEffect");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerAcceptedHit_PlayerPrefab");
            var hitVfxPrefab = new GameObject("GameplayTickPresentationCoordinator_PlayerAcceptedHit_HitVfxPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitVfxPrefab", hitVfxPrefab);
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

                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(1));

                presenter.UpdatePresentation(0.21f);
                Assert.That(presenter.ActiveTransientEffectCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hitVfxPrefab);
                UnityEngine.Object.DestroyImmediate(playerViewPrefab.gameObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathTick_SuppressesHitVfx");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("GameplayTickPresentationCoordinator_PlayerDeathTick_PlayerPrefab");
            var hitVfxPrefab = new GameObject("GameplayTickPresentationCoordinator_PlayerDeathTick_HitVfxPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0));
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
                PlayerViewPrefabTestUtility.SetSerializedField(effectAuthoring, "hitVfxPrefab", hitVfxPrefab);
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

                Assert.That(presenter.ActiveTransientEffectCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hitVfxPrefab);
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
                            enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                            enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                            entityExitSignals: new[]
                            {
                                new TickEntityExitPresentationSignal(
                                    exitedEntityId: 10,
                                    TickEntityExitCause.Killed,
                                    spawnCell,
                                    topology,
                                    Direction.Right,
                                    EntityType.Unit),
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
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
        public void GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic()
        {
            var rootObject = new GameObject("GameplayTickPresentationCoordinator_CommittedBottomEnemy_StoresUnsuppressedFactsWithoutPauseSemantic");

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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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

                Assert.That(stateStore.EnemyVisualSemanticStatesByEntityId.TryGetValue(20, out var semantic), Is.True);
                Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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
                projectedSlot: GameplayProjectedFaceSlot.Front,
                isGameplayAutonomySuppressed: true,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.FrontFaceInactive));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.True);
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
                projectedSlot: GameplayProjectedFaceSlot.Top,
                isGameplayAutonomySuppressed: false,
                aiMode: EnemyAiMode.Patrol,
                hasActiveMotion: false);

            var semantic = resolver.Resolve(facts);

            Assert.That(semantic.ActivityState, Is.EqualTo(EnemyVisualActivityState.Normal));
            Assert.That(semantic.ShouldPauseAnimatorPlayback, Is.False);
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
                    "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/EnemyView_Startis.prefab");
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
                Assert.That(controller.CurrentBorder, Is.EqualTo(0.44f).Within(0.0001f));

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
                Assert.That(controller.CurrentBorder, Is.EqualTo(0.22f).Within(0.0001f));

                controller.Advance(0.02f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(0.22f).Within(0.0001f));

                controller.Advance(0.5f);
                Assert.That(controller.CurrentBorder, Is.GreaterThan(0.22f));
                Assert.That(controller.CurrentBorder, Is.LessThan(0.44f));

                controller.Advance(1f);
                Assert.That(controller.CurrentBorder, Is.EqualTo(0.44f).Within(0.0001f));

                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                Assert.That(propertyBlock.GetFloat("_Border"), Is.EqualTo(0.44f).Within(0.0001f));
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
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "chargeMoveMotionDurationSeconds", entityMotionOverride.ChargeMoveMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "pushMotionDurationSeconds", entityMotionOverride.PushMotionDurationSeconds);
                    PlayerViewPrefabTestUtility.SetSerializedField(authoring, "flipMotionDurationSeconds", entityMotionOverride.FlipMotionDurationSeconds);
                }

                return view;
            }
        }
    }
}
