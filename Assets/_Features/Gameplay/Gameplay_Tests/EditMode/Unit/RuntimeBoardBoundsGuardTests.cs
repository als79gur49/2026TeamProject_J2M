using System;
using System.Collections.Generic;
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
        public void GameplayCompositionRoot_PublicApi_DoesNotExposeUnboundedWorldFactory()
        {
            var publicDefaultFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateWorldState),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(IEnumerable<EntityState>) },
                modifiers: null);
            var publicLegacyFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateLegacyUnboundedWorldState",
                BindingFlags.Static | BindingFlags.Public);

            Assert.That(publicDefaultFactory, Is.Null);
            Assert.That(publicLegacyFactory, Is.Null);
        }

        [Test]
        public void GameplayCompositionRoot_LegacyUnboundedHelper_IsInternalOnly_AndStillAvailableToTests()
        {
            var internalLegacyFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateLegacyUnboundedWorldState",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(IEnumerable<EntityState>) },
                modifiers: null);

            Assert.That(internalLegacyFactory, Is.Not.Null);
            Assert.That(internalLegacyFactory.IsAssembly, Is.True);

            var worldState = GameplayCompositionRoot.CreateLegacyUnboundedWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            Assert.That(worldState.CreateSnapshot().BoardBounds.IsBounded, Is.False);
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position)
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
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
            };
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
                    1f);
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
                    1f);
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
                    1f);
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
                    1f);
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
                    1f);
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

                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(initialTarget + new Vector3(0f, 2f, 0f)));
                Assert.That(GetViewPosition(host, 10), Is.EqualTo(new Vector3(0f, 2f, 0f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        private static TickResult CreateTickResult(EntityState[] finalEntities, CubeTopologyState topology)
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
