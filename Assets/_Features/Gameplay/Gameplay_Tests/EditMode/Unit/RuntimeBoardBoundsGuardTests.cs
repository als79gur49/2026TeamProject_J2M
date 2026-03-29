using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class RuntimeBoardBoundsGuardTests
    {
        [Test]
        public void GameplaySceneHost_Initialize_UnboundedBoard_Throws()
        {
            var gameObject = new GameObject("RuntimeBoardBoundsGuardTests");

            try
            {
                var host = gameObject.AddComponent<GameplaySceneHost>();

                Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(new GameplaySceneHostConfiguration()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GameplayCompositionRoot_CreateWorldState_RejectsUnboundedBoard()
        {
            Assert.Throws<InvalidOperationException>(
                () => GameplayCompositionRoot.CreateWorldState(
                    Array.Empty<EntityState>(),
                    BoardBounds.Unbounded,
                    GameplayTerrainData.Empty));
        }

        [Test]
        public void GameplayCompositionRoot_DeclaresOnlyBoundedWorldFactory()
        {
            var worldFactories = typeof(GameplayCompositionRoot)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.ReturnType == typeof(WorldState))
                .Select(method => method.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    nameof(GameplayCompositionRoot.CreateWorldState),
                },
                worldFactories);
        }

    }

    public sealed class GameplayViewProjectionTests
    {
        [Test]
        public void GameplaySurfaceProjector_ProjectsFrontFaceAboveBottomFace()
        {
            var projector = new GameplaySurfaceProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                Vector3.zero,
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProject(new SurfaceCell(FaceId.Floor, 1, 2), topology, Vector3.zero, out var bottomWorld),
                Is.True);
            Assert.That(
                projector.TryProject(new SurfaceCell(FaceId.Front, 1, 0), topology, Vector3.zero, out var frontWorld),
                Is.True);

            Assert.That(bottomWorld, Is.EqualTo(new Vector3(1f, 2f, 0f)));
            Assert.That(frontWorld, Is.EqualTo(new Vector3(1f, 3f, 0f)));
        }

        [Test]
        public void GameplayTickViewPresenter_PresentsOnlyBottomAndFrontFaces()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentsOnlyBottomAndFrontFaces");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateSurfaceUnit(20, new SurfaceCell(FaceId.Front, 1, 0)),
                        CreateSurfaceUnit(30, new SurfaceCell(FaceId.Ceiling, 0, 0)),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var bottomView), Is.True);
                Assert.That(bottomView.gameObject.activeSelf, Is.True);
                Assert.That(bottomView.transform.position, Is.EqualTo(new Vector3(0f, 1f, 0f)));

                Assert.That(registry.TryGetView(20, out var frontView), Is.True);
                Assert.That(frontView.gameObject.activeSelf, Is.True);
                Assert.That(frontView.transform.position, Is.EqualTo(new Vector3(1f, 2f, 0f)));

                Assert.That(registry.TryGetView(30, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_HidesDetachedEntitiesBeforeCleanupRemoval()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_HidesDetachedEntitiesBeforeCleanupRemoval");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), boardPresence: EntityBoardPresence.Detached),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_KeepsMarkedForDeathEntityVisibleWhileStillOccupying()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_KeepsMarkedForDeathEntityVisibleWhileStillOccupying");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), markedForDeath: true),
                    },
                    topology);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_ShiftsContinuityAnchorOnForwardRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ShiftsContinuityAnchorOnForwardRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    Vector3.zero,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                        },
                        new CubeTopologyState(FaceId.Front)));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.ContinuityAnchor, Is.EqualTo(new Vector3(0f, 2f, 0f)));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(0f, 2f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_ShiftsContinuityAnchorOnBackwardRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ShiftsContinuityAnchorOnBackwardRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    Vector3.zero,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    initialTopology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        new CubeTopologyState(FaceId.Floor)));
                presenter.UpdatePresentation(0f);

                Assert.That(presenter.ContinuityAnchor, Is.EqualTo(new Vector3(0f, -2f, 0f)));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(0f, -1f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplaySceneHost_SynchronizesCameraTargetWithPresentedTopology()
        {
            var hostObject = new GameObject("GameplaySceneHost_SynchronizesCameraTargetWithPresentedTopology");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        GridOrigin = Vector3.zero,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TickIntervalSeconds = 0.2f,
                    });

                var initialTarget = host.ViewCameraTarget.position;

                host.InputHost.SetRawMoveInput(Vector2.up);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(0f);

                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(initialTarget + new Vector3(0f, 2f, 0f)));
                Assert.That(GetViewPosition(host, 10), Is.EqualTo(new Vector3(0f, 2f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplaySceneHost_PushMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_PushMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        GridOrigin = Vector3.zero,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TickIntervalSeconds = 0.2f,
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 2, 0), out var pushedBox), Is.True);
                Assert.That(pushedBox.entityId, Is.EqualTo(20));
                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
                Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.True);

                var renderedPosition = GetViewPosition(host, 20);
                Assert.That(renderedPosition.x, Is.GreaterThan(1f));
                Assert.That(renderedPosition.x, Is.LessThan(2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplaySceneHost_PushMotion_KeepsProjectileLayerQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_PushMotion_KeepsProjectileLayerQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        GridOrigin = Vector3.zero,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                            CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TickIntervalSeconds = 0.2f,
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.BufferPush();
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 2, 0), out var pushedBox), Is.True);
                Assert.That(pushedBox.entityId, Is.EqualTo(20));
                Assert.That(snapshot.TryGetProjectileAt(new SurfaceCell(FaceId.Floor, 2, 0), out var projectile), Is.True);
                Assert.That(projectile.entityId, Is.EqualTo(30));
                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
                Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.True);

                var renderedPosition = GetViewPosition(host, 20);
                Assert.That(renderedPosition.x, Is.GreaterThan(1f));
                Assert.That(renderedPosition.x, Is.LessThan(2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplaySceneHost_FlipMotion_KeepsWorldQueriesOnCommittedLandingCellWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_FlipMotion_KeepsWorldQueriesOnCommittedLandingCellWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        GridOrigin = Vector3.zero,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        TickIntervalSeconds = 0.2f,
                    });

                host.InputHost.SetRawMoveInput(Vector2.left);
                host.InputHost.BufferFlip();
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.FlipMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out var flippedBox), Is.True);
                Assert.That(flippedBox.entityId, Is.EqualTo(20));
                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, -1, 0), out _), Is.False);
                Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.True);

                var renderedPosition = GetViewPosition(host, 20);
                Assert.That(renderedPosition.x, Is.GreaterThan(-1f));
                Assert.That(renderedPosition.x, Is.Not.EqualTo(1f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PushMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PushMotion_MidpointInterpolatesBetweenSourceAndDestination");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.transform.position.x, Is.GreaterThan(1f));
                Assert.That(view.transform.position.x, Is.LessThan(2f));
                Assert.That(view.transform.position.y, Is.EqualTo(0f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_QueuedPushMotions_PreserveSequentialStepsAcrossCatchUp()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_QueuedPushMotions_PreserveSequentialStepsAcrossCatchUp");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    20,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                Assert.That(view.transform.position.x, Is.GreaterThan(0f));
                Assert.That(view.transform.position.x, Is.LessThan(1f));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(view.transform.position.x, Is.EqualTo(1f).Within(0.001f));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(view.transform.position.x, Is.GreaterThan(1f));
                Assert.That(view.transform.position.x, Is.LessThan(2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_FlipMotion_MidpointTravelsAlongArc()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_MidpointTravelsAlongArc");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, -1, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(view.transform.position.x, Is.EqualTo(0f).Within(0.15f));
                Assert.That(view.transform.position.y, Is.GreaterThan(0.2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_FlipMotion_CompletesAtLandingCellAndRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_CompletesAtLandingCellAndRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    topology,
                    Vector3.zero,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, -1, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(
                    Quaternion.Angle(view.transform.rotation, Quaternion.Euler(0f, 0f, -90f)),
                    Is.LessThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PushAfterFlip_DoesNotInterpolateRotationWhenFacingChanges()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PushAfterFlip_DoesNotInterpolateRotationWhenFacingChanges");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    Vector3.zero,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Down),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 0, 1)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Floor, 1, 1), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Push,
                                    new SurfaceCell(FaceId.Floor, 0, 1),
                                    new SurfaceCell(FaceId.Floor, 1, 1)),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                Assert.That(view.transform.position.x, Is.GreaterThan(0f));
                Assert.That(view.transform.position.x, Is.LessThan(1f));
                Assert.That(view.transform.position.y, Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(view.transform.rotation, Quaternion.Euler(0f, 0f, -90f)),
                    Is.LessThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static TickResult CreateTickResult(
            EntityState[] finalEntities,
            CubeTopologyState topology,
            TickPresentationData presentationData = null)
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
                presentationData ?? TickPresentationData.Empty,
                string.Empty,
                TickTrace.Empty);
        }

        private static EntityState CreateSurfaceUnit(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            bool markedForDeath = false)
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
                facing = Direction.Up,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
            };
        }

        private static EntityState CreateSurfaceBox(int entityId, SurfaceCell position, Direction facing)
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
                facing = facing,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static EntityState CreateSurfaceProjectile(int entityId, SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Projectile,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static Vector3 GetViewPosition(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view.transform.position;
        }

        private sealed class TestViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public TestViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }
    }
}
