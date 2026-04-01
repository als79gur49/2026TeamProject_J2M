using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.Attack.Intents;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
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
        public void GameplaySceneHost_Initialize_NormalizesPreExistingProjectileCadence()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_NormalizesPreExistingProjectileCadence");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 20,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 1,
                                maxHp = 1,
                                teamId = 1,
                                type = EntityType.Projectile,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Right,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        SimulationTicksPerSecond = 10,
                        ProjectileStepIntervalSeconds = 0.3f,
                    });

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetProjectileAt(new SurfaceCell(FaceId.Floor, 0, 0), out var projectile), Is.True);
                Assert.That(projectile.entityId, Is.EqualTo(20));
                Assert.That(projectile.stateTimer, Is.EqualTo(host.TimingProfile.ProjectileStepIntervalTicks));
                Assert.That(projectile.stateTimer, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplaySceneHostConfiguration_CreateTimingProfile_DefaultsTopologyMotionDurationToPushAndAllowsOverride()
        {
            var configuration = new GameplaySceneHostConfiguration
            {
                PushMotionDurationSeconds = 0.25f,
            };

            var defaultProfile = configuration.CreateTimingProfile();

            Assert.That(defaultProfile.PushMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(defaultProfile.TopologyMotionDurationSeconds, Is.EqualTo(0.25f));

            configuration.TopologyMotionDurationSeconds = 0.45f;

            var overriddenProfile = configuration.CreateTimingProfile();

            Assert.That(overriddenProfile.PushMotionDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(overriddenProfile.TopologyMotionDurationSeconds, Is.EqualTo(0.45f));
        }

        [Test]
        public void GameplaySceneHostConfiguration_DefaultsTopologyRotationVisualMappingToForwardUsesNegativeX()
        {
            var configuration = new GameplaySceneHostConfiguration();

            Assert.That(
                configuration.TopologyRotationVisualMapping,
                Is.EqualTo(TopologyRotationVisualMapping.ForwardUsesNegativeX));
        }

        [Test]
        public void GameplayTimingProfile_CreateDefault_PreservesDefaultTimeMeaningAtSixtyTps()
        {
            var profile = GameplayTimingProfile.CreateDefault();

            Assert.That(profile.SimulationTicksPerSecond, Is.EqualTo(60));
            Assert.That(profile.InitialMoveDelaySeconds, Is.EqualTo(0f));
            Assert.That(profile.InitialMoveDelayTicks, Is.EqualTo(0));
            Assert.That(profile.RepeatedMoveIntervalSeconds, Is.EqualTo(0.4f));
            Assert.That(profile.RepeatedMoveIntervalTicks, Is.EqualTo(24));
            Assert.That(profile.BoxSlideStepIntervalSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.BoxSlideStepIntervalTicks, Is.EqualTo(12));
            Assert.That(profile.ProjectileStepIntervalSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.ProjectileStepIntervalTicks, Is.EqualTo(12));
            Assert.That(profile.PlayerMoveCooldownSeconds, Is.EqualTo(0.4f));
            Assert.That(profile.PlayerMoveCooldownTicks, Is.EqualTo(24));
            Assert.That(profile.PlayerPushContactThresholdSeconds, Is.EqualTo(GameplayTimingProfile.DefaultPlayerPushContactThresholdSeconds));
            Assert.That(profile.PlayerPushContactThresholdTicks, Is.EqualTo(GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks));
            Assert.That(profile.PlayerPushInteractionLockTicks, Is.EqualTo(12));
            Assert.That(profile.PlayerFlipInteractionLockTicks, Is.EqualTo(12));
            Assert.That(profile.PushMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.TopologyMotionDurationSeconds, Is.EqualTo(0.2f));
            Assert.That(profile.FlipMotionDurationSeconds, Is.EqualTo(0.2f));
        }

        [Test]
        public void GameplaySceneHostConfiguration_CreateTimingProfile_ChangingSimulationTicksPerSecondPreservesTimeMeaning()
        {
            var sixtyTpsProfile = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 60,
            }.CreateTimingProfile();
            var oneTwentyTpsProfile = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = 120,
            }.CreateTimingProfile();

            Assert.That(sixtyTpsProfile.RepeatedMoveIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.RepeatedMoveIntervalSeconds));
            Assert.That(sixtyTpsProfile.BoxSlideStepIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.BoxSlideStepIntervalSeconds));
            Assert.That(sixtyTpsProfile.ProjectileStepIntervalSeconds, Is.EqualTo(oneTwentyTpsProfile.ProjectileStepIntervalSeconds));
            Assert.That(sixtyTpsProfile.PlayerMoveCooldownSeconds, Is.EqualTo(oneTwentyTpsProfile.PlayerMoveCooldownSeconds));
            Assert.That(sixtyTpsProfile.PlayerPushContactThresholdSeconds, Is.EqualTo(oneTwentyTpsProfile.PlayerPushContactThresholdSeconds));
            Assert.That(sixtyTpsProfile.RepeatedMoveIntervalTicks, Is.EqualTo(24));
            Assert.That(oneTwentyTpsProfile.RepeatedMoveIntervalTicks, Is.EqualTo(48));
            Assert.That(sixtyTpsProfile.BoxSlideStepIntervalTicks, Is.EqualTo(12));
            Assert.That(oneTwentyTpsProfile.BoxSlideStepIntervalTicks, Is.EqualTo(24));
            Assert.That(sixtyTpsProfile.ProjectileStepIntervalTicks, Is.EqualTo(12));
            Assert.That(oneTwentyTpsProfile.ProjectileStepIntervalTicks, Is.EqualTo(24));
            Assert.That(sixtyTpsProfile.PlayerMoveCooldownTicks, Is.EqualTo(24));
            Assert.That(oneTwentyTpsProfile.PlayerMoveCooldownTicks, Is.EqualTo(48));
            Assert.That(sixtyTpsProfile.PlayerPushContactThresholdTicks, Is.EqualTo(2));
            Assert.That(oneTwentyTpsProfile.PlayerPushContactThresholdTicks, Is.EqualTo(4));
            Assert.That(sixtyTpsProfile.PlayerPushInteractionLockTicks, Is.EqualTo(12));
            Assert.That(oneTwentyTpsProfile.PlayerPushInteractionLockTicks, Is.EqualTo(24));
            Assert.That(sixtyTpsProfile.PlayerFlipInteractionLockTicks, Is.EqualTo(12));
            Assert.That(oneTwentyTpsProfile.PlayerFlipInteractionLockTicks, Is.EqualTo(24));
        }

        [Test]
        public void GameplaySceneHost_Initialize_CreatesBoardRootHierarchyAndParentsViewsUnderEntityRoot()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_CreatesBoardRootHierarchyAndParentsViewsUnderEntityRoot");
            hostObject.transform.position = new Vector3(4f, -2f, 0f);

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            new EntityState
                            {
                                entityId = 10,
                                position = new SurfaceCell(FaceId.Floor, 0, 0),
                                hp = 3,
                                maxHp = 3,
                                teamId = 1,
                                type = EntityType.Unit,
                                state = EntityPhaseState.Idle,
                                facing = Direction.Up,
                                boardPresence = EntityBoardPresence.Occupying,
                            },
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                Assert.That(host.BoardRoot, Is.Not.Null);
                Assert.That(host.BoardRoot.transform.parent, Is.EqualTo(host.transform));
                Assert.That(host.BoardRoot.BoardSurfaceRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardRoot.EntityRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardRoot.CameraTargetRoot.parent, Is.EqualTo(host.BoardRoot.transform));
                Assert.That(host.BoardSurfaceRenderer, Is.Not.Null);
                Assert.That(host.BoardSurfaceRenderer.transform, Is.EqualTo(host.BoardRoot.BoardSurfaceRoot));
                Assert.That(host.ViewCameraTarget, Is.SameAs(host.BoardRoot.CameraTargetRoot));
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(hostObject.transform.position));
                Assert.That(host.ViewRegistry.SearchRoot, Is.SameAs(host.BoardRoot.EntityRoot));

                Assert.That(host.ViewRegistry.TryGetView(10, out var view), Is.True);
                Assert.That(view.transform.parent, Is.EqualTo(host.BoardRoot.EntityRoot));
                var projector = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new CubeTopologyState(FaceId.Floor),
                        EntityType.Unit,
                        out var projectedPose),
                    Is.True);
                Assert.That(view.transform.localPosition, Is.EqualTo(projectedPose.LocalPosition));
                Assert.That(view.transform.position, Is.EqualTo(host.BoardRoot.transform.TransformPoint(projectedPose.LocalPosition)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
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

    // Final presentation guardrail:
    // Keep lifecycle, visibility, and motion sequencing coverage in this class so
    // the board-local cube contract stays fixed after the strip migration removal.
    public sealed class GameplayViewProjectionTests
    {
        [Test]
        public void GameplayEntityView_ApplyLocalPose_UsesLocalTransformSpace()
        {
            var rootObject = new GameObject("GameplayEntityView_ApplyLocalPose_UsesLocalTransformSpace");
            rootObject.transform.position = new Vector3(5f, 7f, 0f);

            try
            {
                var parentObject = new GameObject("EntityRoot");
                parentObject.transform.SetParent(rootObject.transform, worldPositionStays: false);
                parentObject.transform.localPosition = new Vector3(2f, -3f, 0f);

                var viewObject = new GameObject("EntityView");
                viewObject.transform.SetParent(parentObject.transform, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);

                view.ApplyLocalPose(new Vector3(1f, 4f, 0f), Quaternion.Euler(0f, 0f, 90f));

                Assert.That(view.transform.localPosition, Is.EqualTo(new Vector3(1f, 4f, 0f)));
                Assert.That(view.transform.position, Is.EqualTo(new Vector3(8f, 8f, 0f)));
                Assert.That(
                    Quaternion.Angle(view.transform.localRotation, Quaternion.Euler(0f, 0f, 90f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayEntityView_ConfigureModelRoot_CreatesDedicatedModelPivot()
        {
            var viewObject = new GameObject("GameplayEntityView_ConfigureModelRoot_CreatesDedicatedModelPivot");

            try
            {
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(10);
                var expectedRotation = Quaternion.Euler(15f, 25f, 35f);

                view.ConfigureModelRoot(new Vector3(0.1f, 0.2f, 0.3f), expectedRotation);

                Assert.That(view.ModelRoot, Is.Not.Null);
                Assert.That(view.ModelRoot.parent, Is.EqualTo(view.transform));
                Assert.That(view.ModelRoot.localPosition, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                Assert.That(Quaternion.Angle(view.ModelRoot.localRotation, expectedRotation), Is.LessThan(0.001f));
                Assert.That(view.ModelRoot.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        public void DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_CreatesCubeEntityVisualProfilesWithoutColliders");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);

                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0))),
                    GameplayEntityVisualProfile.Create(EntityType.Unit, 1f),
                    new Color(0.2f, 0.85f, 0.35f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right)),
                    GameplayEntityVisualProfile.Create(EntityType.Box, 1f),
                    new Color(0.72f, 0.5f, 0.24f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right)),
                    GameplayEntityVisualProfile.Create(EntityType.Projectile, 1f),
                    new Color(0.9f, 0.4f, 0.2f));
                AssertVisualMatchesProfile(
                    factory.CreateView(CreateSurfaceWall(40, new SurfaceCell(FaceId.Floor, 0, 0))),
                    GameplayEntityVisualProfile.Create(EntityType.None, 1f),
                    new Color(0.25f, 0.28f, 0.33f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void DefaultGameplayEntityViewFactory_AiControlledUnit_AddsEnemyAnimatorDriver()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_AiControlledUnit_AddsEnemyAnimatorDriver");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);
                var enemyView = factory.CreateView(CreateSurfaceUnit(20, new SurfaceCell(FaceId.Floor, 0, 0), aiMode: EnemyAiMode.Patrol));
                var playerView = factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)));

                Assert.That(enemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(playerView.GetComponent<EnemyAnimatorDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void DefaultGameplayEntityViewFactory_PlayerUnit_AddsPlayerAnimatorDriverOnlyToPlayer()
        {
            var parentObject = new GameObject("DefaultGameplayEntityViewFactory_PlayerUnit_AddsPlayerAnimatorDriverOnlyToPlayer");

            try
            {
                var factory = new DefaultGameplayEntityViewFactory(parentObject.transform, 1f, playerEntityId: 10);
                var playerView = factory.CreateView(CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)));
                var enemyView = factory.CreateView(CreateSurfaceUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), aiMode: EnemyAiMode.Patrol));

                Assert.That(playerView.GetComponent<PlayerAnimatorDriver>(), Is.Not.Null);
                Assert.That(enemyView.GetComponent<PlayerAnimatorDriver>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void GameplayEntityVisualProfile_BoxVisualRecedesIntoFaceInterior()
        {
            var profile = GameplayEntityVisualProfile.Create(EntityType.Box, 1f);

            Assert.That(profile.ModelLocalPosition.z, Is.LessThan(0f));
            Assert.That(profile.ModelLocalPosition.z, Is.GreaterThan(-(profile.ModelLocalScale.z * 0.5f)));
            Assert.That(profile.ModelLocalScale.z, Is.GreaterThan(0f));
        }

        [Test]
        public void GameplayEntityVisualProfile_WallVisualRecedesIntoFaceInterior()
        {
            var profile = GameplayEntityVisualProfile.Create(EntityType.None, 1f);

            Assert.That(profile.ModelLocalPosition.z, Is.LessThan(0f));
            Assert.That(profile.ModelLocalPosition.z, Is.GreaterThan(-(profile.ModelLocalScale.z * 0.5f)));
            Assert.That(profile.ModelLocalScale.z, Is.GreaterThan(0f));
        }

        [Test]
        public void ProjectedCellPose_NormalizesNormalVector()
        {
            var pose = new ProjectedCellPose(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(0f, 0f, 45f),
                new Vector3(0f, 3f, 0f));

            Assert.That(pose.LocalPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(
                Quaternion.Angle(pose.LocalRotation, Quaternion.Euler(0f, 0f, 45f)),
                Is.LessThan(0.001f));
            Assert.That(pose.Normal, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void GameplayCubeProjector_ProjectsBottomFaceToHorizontalPlane()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 1, 2),
                    topology,
                    EntityType.Unit,
                    out var bottomPose),
                Is.True);

            Assert.That(bottomPose.LocalPosition.y, Is.EqualTo(-1.42f).Within(0.001f));
            Assert.That(bottomPose.LocalPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bottomPose.LocalPosition.z, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(bottomPose.Normal, Is.EqualTo(Vector3.down));
            Assert.That(
                Quaternion.Angle(bottomPose.LocalRotation, Quaternion.LookRotation(Vector3.down, Vector3.forward)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_ProjectsFrontFaceToVerticalPlane()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 1, 0),
                    topology,
                    EntityType.Unit,
                    out var frontPose),
                Is.True);

            Assert.That(frontPose.LocalPosition.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(frontPose.LocalPosition.y, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(frontPose.LocalPosition.z, Is.EqualTo(1.42f).Within(0.001f));
            Assert.That(frontPose.Normal, Is.EqualTo(Vector3.forward));
            Assert.That(
                Quaternion.Angle(frontPose.LocalRotation, Quaternion.LookRotation(Vector3.forward, Vector3.up)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_TransitionProjection_UsesSourceAndDestinationVisibleFaceUnion()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = new CubeTopologyState(FaceId.Front);

            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Ceiling, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    new SurfaceCell(FaceId.Back, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    EntityType.Unit,
                    out _),
                Is.False);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    new SurfaceCell(FaceId.Ceiling, 1, 0),
                    sourceTopology,
                    destinationTopology,
                    Direction.Right,
                    out _),
                Is.True);
        }

        [Test]
        public void GameplayCubeProjector_TransitionProjection_MatchesOrdinaryProjection_WhenTopologyDoesNotChange()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);
            var cell = new SurfaceCell(FaceId.Front, 1, 0);

            Assert.That(
                projector.TryProjectEntityCell(
                    cell,
                    topology,
                    EntityType.Box,
                    out var ordinaryPose),
                Is.True);
            Assert.That(
                projector.TryProjectTransitionEntityCell(
                    cell,
                    topology,
                    topology,
                    EntityType.Box,
                    out var transitionPose),
                Is.True);
            Assert.That(
                projector.TryResolveEntityRotation(
                    cell,
                    topology,
                    Direction.Left,
                    out var ordinaryRotation),
                Is.True);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    topology,
                    topology,
                    Direction.Left,
                    out var transitionRotation),
                Is.True);

            Assert.That(Vector3.Distance(ordinaryPose.LocalPosition, transitionPose.LocalPosition), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(ordinaryPose.Normal, transitionPose.Normal), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(ordinaryPose.LocalRotation, transitionPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(ordinaryRotation, transitionRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_FrontFaceRows_RiseAwayFromFloor()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    EntityType.Unit,
                    out var lowerRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Front, 0, 1),
                    topology,
                    EntityType.Unit,
                    out var upperRowPose),
                Is.True);

            Assert.That(upperRowPose.LocalPosition.y, Is.GreaterThan(lowerRowPose.LocalPosition.y));
            Assert.That(upperRowPose.LocalPosition.z, Is.EqualTo(lowerRowPose.LocalPosition.z).Within(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_FloorRows_AdvanceTowardPositiveZ()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    topology,
                    EntityType.Unit,
                    out var backRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    topology,
                    EntityType.Unit,
                    out var frontRowPose),
                Is.True);

            Assert.That(frontRowPose.LocalPosition.z, Is.GreaterThan(backRowPose.LocalPosition.z));
            Assert.That(frontRowPose.LocalPosition.y, Is.EqualTo(backRowPose.LocalPosition.y).Within(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_SeparatesFloorAndFrontFacesWithOneCellSeamGap()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    topology,
                    out var floorTopRowPose),
                Is.True);
            Assert.That(
                projector.TryProjectSurfaceCell(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    out var frontBottomRowPose),
                Is.True);

            Assert.That(floorTopRowPose.LocalPosition.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(frontBottomRowPose.LocalPosition.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_FloorRightFacing_AlignsWithPositiveXAxis()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryResolveEntityRotation(
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    topology,
                    Direction.Right,
                    out var rotation),
                Is.True);

            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.right), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.down), Is.LessThan(0.001f));
        }

        [Test]
        public void GameplayCubeProjector_FrontRightFacing_AlignsWithPositiveXAxis()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryResolveEntityRotation(
                    new SurfaceCell(FaceId.Front, 0, 0),
                    topology,
                    Direction.Right,
                    out var rotation),
                Is.True);

            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.right), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.forward), Is.LessThan(0.001f));
        }

        [Test]
        public void GameplayCameraRig_DefaultPose_ProjectsFloorPositiveXToScreenRight()
        {
            var rigObject = new GameObject("GameplayCameraRig_DefaultPose_ProjectsFloorPositiveXToScreenRight");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                var projector = new GameplayCubeProjector(boardBounds, 1f);
                rig.Initialize(viewCamera, targetObject.transform, projector.GetVisibleCubeBounds(topology));

                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        topology,
                        EntityType.Unit,
                        out var leftPose),
                    Is.True);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        topology,
                        EntityType.Unit,
                        out var rightPose),
                    Is.True);

                var leftViewport = viewCamera.WorldToViewportPoint(leftPose.LocalPosition);
                var rightViewport = viewCamera.WorldToViewportPoint(rightPose.LocalPosition);

                Assert.That(leftViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.x, Is.GreaterThan(leftViewport.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void GameplayCameraRig_DefaultPose_ProjectsFrontPositiveXToScreenRight()
        {
            var rigObject = new GameObject("GameplayCameraRig_DefaultPose_ProjectsFrontPositiveXToScreenRight");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                var projector = new GameplayCubeProjector(boardBounds, 1f);
                rig.Initialize(viewCamera, targetObject.transform, projector.GetVisibleCubeBounds(topology));

                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Front, 0, 0),
                        topology,
                        EntityType.Unit,
                        out var leftPose),
                    Is.True);
                Assert.That(
                    projector.TryProjectEntityCell(
                        new SurfaceCell(FaceId.Front, 1, 0),
                        topology,
                        EntityType.Unit,
                        out var rightPose),
                    Is.True);

                var leftViewport = viewCamera.WorldToViewportPoint(leftPose.LocalPosition);
                var rightViewport = viewCamera.WorldToViewportPoint(rightPose.LocalPosition);

                Assert.That(leftViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.z, Is.GreaterThan(0f));
                Assert.That(rightViewport.x, Is.GreaterThan(leftViewport.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void GameplayCameraRig_ManualDistance_KeepsPositionWhenFieldOfViewChanges()
        {
            var rigObject = new GameObject("GameplayCameraRig_ManualDistance_KeepsPositionWhenFieldOfViewChanges");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var visibleBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 60f,
                    FramingPadding = 1.2f,
                    DistanceMode = GameplayCameraRig.DistanceMode.Manual,
                    ManualDistance = 7f,
                });
                rig.Initialize(viewCamera, targetObject.transform, visibleBounds);
                var initialPosition = viewCamera.transform.position;

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 30f,
                    FramingPadding = 1.2f,
                    DistanceMode = GameplayCameraRig.DistanceMode.Manual,
                    ManualDistance = 7f,
                });

                Assert.That(Vector3.Distance(viewCamera.transform.position, initialPosition), Is.LessThan(0.0001f));
                Assert.That(viewCamera.fieldOfView, Is.EqualTo(30f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void GameplayCameraRig_AutoFit_ChangesDistanceWhenFieldOfViewChanges()
        {
            var rigObject = new GameObject("GameplayCameraRig_AutoFit_ChangesDistanceWhenFieldOfViewChanges");
            var cameraObject = new GameObject("ViewCamera");
            var targetObject = new GameObject("CameraTarget");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.aspect = 16f / 9f;
                var rig = rigObject.AddComponent<GameplayCameraRig>();
                var visibleBounds = new Bounds(Vector3.zero, new Vector3(4f, 4f, 4f));

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 60f,
                    FramingPadding = 1.2f,
                    DistanceMode = GameplayCameraRig.DistanceMode.AutoFit,
                });
                rig.Initialize(viewCamera, targetObject.transform, visibleBounds);
                var initialDistance = Vector3.Distance(viewCamera.transform.position, targetObject.transform.position);

                rig.ApplySettings(new GameplayCameraSettings
                {
                    PitchDegrees = 35f,
                    YawDegrees = 0f,
                    PerspectiveFieldOfView = 30f,
                    FramingPadding = 1.2f,
                    DistanceMode = GameplayCameraRig.DistanceMode.AutoFit,
                });
                var narrowedDistance = Vector3.Distance(viewCamera.transform.position, targetObject.transform.position);

                Assert.That(narrowedDistance, Is.GreaterThan(initialDistance));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void GameplayCubeProjector_RejectsInactiveFaceEntityProjection()
        {
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            var topology = new CubeTopologyState(FaceId.Floor);

            Assert.That(
                projector.TryProjectEntityCell(
                    new SurfaceCell(FaceId.Ceiling, 1, 1),
                    topology,
                    EntityType.Unit,
                    out _),
                Is.False);
        }

        [Test]
        public void GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles()
        {
            var rootObject = new GameObject("GameplayBoardSurfaceRenderer_CreatesExpectedVisibleFaceTiles");

            try
            {
                var renderer = rootObject.AddComponent<GameplayBoardSurfaceRenderer>();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var topology = new CubeTopologyState(FaceId.Floor);
                rootObject.transform.position = new Vector3(2f, 1f, -3f);

                renderer.Initialize(boardBounds, 1f, topology);

                Assert.That(renderer.VisibleTilePoolRoot.parent, Is.EqualTo(renderer.transform));
                Assert.That(renderer.VisibleTilePoolRoot.childCount, Is.EqualTo(8));
                Assert.That(renderer.ActiveTileCount, Is.EqualTo(8));

                var bottomTile = FindSurfaceTile(renderer, "ActiveBottom_Floor_0_0");
                var frontTile = FindSurfaceTile(renderer, "ActiveFront_Front_1_1");

                AssertSurfaceTileMatchesProjection(bottomTile, boardBounds, topology, new SurfaceCell(FaceId.Floor, 0, 0));
                AssertSurfaceTileMatchesProjection(frontTile, boardBounds, topology, new SurfaceCell(FaceId.Front, 1, 1));

                Assert.That(bottomTile.GetComponent<Collider>(), Is.Null);
                Assert.That(frontTile.GetComponent<Collider>(), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("DecorativeTop_Ceiling_0_1"), Is.Null);
                Assert.That(renderer.VisibleTilePoolRoot.Find("DecorativeBack_Back_1_0"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentsOnlyActiveFaceEntitiesIn3D");

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
                Assert.That(bottomView.transform.localPosition, Is.EqualTo(GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 1))));
                Assert.That(bottomView.transform.position, Is.EqualTo(bottomView.transform.localPosition));

                Assert.That(registry.TryGetView(20, out var frontView), Is.True);
                Assert.That(frontView.gameObject.activeSelf, Is.True);
                Assert.That(frontView.transform.localPosition, Is.EqualTo(GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    new SurfaceCell(FaceId.Front, 1, 0))));
                Assert.That(frontView.transform.position, Is.EqualTo(frontView.transform.localPosition));

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
        public void GameplayTickViewPresenter_ForwardTopologyChange_UsesProjectedCubePose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ForwardTopologyChange_UsesProjectedCubePose");

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

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        new CubeTopologyState(FaceId.Front),
                        new SurfaceCell(FaceId.Front, 0, 0))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_BackwardTopologyChange_UsesProjectedCubePose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BackwardTopologyChange_UsesProjectedCubePose");

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

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        new CubeTopologyState(FaceId.Floor),
                        new SurfaceCell(FaceId.Floor, 0, 1))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PresentationPhase_TransitionsBetweenIdleEntityMotionAndTopologyTransition()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentationPhase_TransitionsBetweenIdleEntityMotionAndTopologyTransition");

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
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    initialTopology);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        initialTopology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Front, 1, 0)),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.True);
                Assert.That(presenter.IsTopologyTransitionActive, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(presenter.IsPresentationActive, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_PresentationPhase_TreatsVisibilityTrackAsEntityMotion()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_PresentationPhase_TreatsVisibilityTrackAsEntityMotion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    10,
                                    TickVisibilityChangeKind.Remove,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    topology,
                                    Direction.Up),
                            })));

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.EntityMotion));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
                Assert.That(presenter.IsTopologyTransitionActive, Is.False);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_MapsEnemyAttackHitAndMoveSignalsToAnimatorDrivers");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, sourceCell, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
                        CreateSurfaceUnit(50, targetCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var attackerView), Is.True);
                Assert.That(registry.TryGetView(50, out var targetView), Is.True);

                var attackerDriver = attackerView.GetComponent<EnemyAnimatorDriver>();
                var targetDriver = targetView.GetComponent<EnemyAnimatorDriver>();
                Assert.That(attackerDriver, Is.Not.Null);
                Assert.That(targetDriver, Is.Not.Null);

                var attackGroup = new ActionGroup(intentId: 1, sourceId: 40, priority: 100, ActionGroupKind.Attack);
                attackGroup.Damages.Add(new DamageAction(targetId: 50, amount: 1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(40, destinationCell, aiMode: EnemyAiMode.Recover, facing: Direction.Right),
                        CreateSurfaceUnit(50, targetCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(40, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>()),
                    attackPhaseResult: CreateAttackPhaseResult(attackGroup)));
                presenter.UpdatePresentation(0f);

                Assert.That(attackerDriver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(attackerDriver.AttackSignalCount, Is.EqualTo(1));
                Assert.That(attackerDriver.IsMoving, Is.True);
                Assert.That(targetDriver.CurrentAiMode, Is.EqualTo(EnemyAiMode.Chase));
                Assert.That(targetDriver.HitSignalCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_Present_RemovedEnemy_MapsDeathSignalToAnimatorDriver()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_RemovedEnemy_MapsDeathSignalToAnimatorDriver");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(40, sourceCell, aiMode: EnemyAiMode.Chase),
                    },
                    topology);

                Assert.That(registry.TryGetView(40, out var view), Is.True);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    Array.Empty<EntityState>(),
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        new[]
                        {
                            new TickVisibilityChange(40, TickVisibilityChangeKind.Remove, sourceCell, topology, Direction.Up),
                        }),
                    cleanupPhaseResult: new CleanupPhaseResult(
                        new[] { 40 },
                        Array.Empty<string>(),
                        Array.Empty<string>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.DeathSignalCount, Is.EqualTo(1));
                Assert.That(view.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_Present_PlayerMoveMotion_ResolvesWalkThenIdleAfterTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerMoveMotion_ResolvesWalkThenIdleAfterTrackCompletes");

            try
            {
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
                var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        new[]
                        {
                            new TickEntityMotion(10, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Walk));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);
                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, destinationCell, facing: Direction.Right),
                    },
                    topology,
                    TickPresentationData.Empty));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_Present_PlayerActionSignals_ResolvePushAndFlip()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_Present_PlayerActionSignals_ResolvePushAndFlip");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(
                    registry,
                    new TestViewFactory(registry.transform, attachPlayerAnimatorDriver: true));
                var topology = new CubeTopologyState(FaceId.Floor);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(new[] { CreateSurfaceUnit(10, sourceCell, facing: Direction.Right) }, topology);

                Assert.That(registry.TryGetView(10, out var playerView), Is.True);
                var driver = playerView.GetComponent<PlayerAnimatorDriver>();
                Assert.That(driver, Is.Not.Null);

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Push, 1, startedThisTick: true, completedThisTick: false, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Push, 1, startedThisTick: false, completedThisTick: false, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Push));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.None, 0, startedThisTick: false, completedThisTick: true, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Idle));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(1));

                presenter.Present(CreateTickResult(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell, facing: Direction.Right),
                    },
                    topology,
                    new TickPresentationData(
                        Array.Empty<TickEntityMotion>(),
                        topologyMotion: null,
                        Array.Empty<TickVisibilityChange>(),
                        Array.Empty<TickTransitionVisibilityChange>(),
                        new[]
                        {
                            new TickPlayerActionPresentationSignal(10, PlayerActionKind.Flip, 2, startedThisTick: true, completedThisTick: false, canceledThisTick: false),
                        })));
                presenter.UpdatePresentation(0f);

                Assert.That(driver.CurrentState, Is.EqualTo(PlayerViewAnimationState.Flip));
                Assert.That(driver.ActionStartSignalCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyMotion_InterpolatesBoardRootRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_InterpolatesBoardRootRotation");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
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
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var cubeCenter = new Vector3(2f, 1f, -3f);
                rootObject.transform.position = cubeCenter;

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
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
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.GreaterThan(0.1f));
                Assert.That(Quaternion.Angle(boardRoot.transform.localRotation, presenter.PresentedBoardRotation), Is.LessThan(0.001f));
                Assert.That(boardRoot.CameraTargetRoot.position, Is.EqualTo(cubeCenter));
                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    rotatedTopology,
                    new SurfaceCell(FaceId.Front, 0, 0));
                Assert.That(
                    Vector3.Distance(view.transform.position, boardRoot.transform.TransformPoint(destinationPosition)),
                    Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(view.transform.position, destinationPosition), Is.GreaterThan(0.01f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds * 0.5f);

                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(
                    Vector3.Distance(view.transform.position, boardRoot.transform.TransformPoint(destinationPosition)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyMotion_UsesDedicatedDuration()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_UsesDedicatedDuration");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.4f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
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
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.TopologyTransition));
                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.GreaterThan(0.1f));

                presenter.UpdatePresentation(
                    timingProfile.TopologyMotionDurationSeconds - timingProfile.PushMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.identity), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyMotion_DefaultForwardRotationUsesNegativeXMapping()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_DefaultForwardRotationUsesNegativeXMapping");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
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
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.Euler(-90f, 0f, 0f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyMotion_ConfiguredPositiveXMappingOverridesDefault()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyMotion_ConfiguredPositiveXMappingOverridesDefault");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    initialTopology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    boardRoot: null,
                    topologyRotationVisualMapping: TopologyRotationVisualMapping.ForwardUsesPositiveX);
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
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));

                Assert.That(
                    Quaternion.Angle(presenter.PresentedBoardRotation, Quaternion.Euler(90f, 0f, 0f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyTransition_RetainsSourceOnlyEntityAndCleansUpAtCompletion()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyTransition_RetainsSourceOnlyEntityAndCleansUpAtCompletion");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = new GameplayTimingProfile(
                    simulationTicksPerSecond: 60,
                    initialMoveDelaySeconds: 0f,
                    repeatedMoveIntervalSeconds: 0.4f,
                    boxSlideStepIntervalSeconds: 0.2f,
                    projectileStepIntervalSeconds: 0.2f,
                    pushMotionDurationSeconds: 0.2f,
                    topologyMotionDurationSeconds: 0.4f,
                    flipMotionDurationSeconds: 0.2f,
                    flipArcHeightInCells: 0.65f,
                    maxTicksPerFrame: 8);
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var retainedCell = new SurfaceCell(FaceId.Floor, 1, 0);
                var shownCell = new SurfaceCell(FaceId.Ceiling, 0, 1);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(20, retainedCell, Direction.Left),
                        CreateSurfaceBox(30, shownCell, Direction.Right),
                    },
                    initialTopology);
                Assert.That(registry.TryGetView(20, out var initialRetainedView), Is.True);
                var initialRetainedWorldPosition = initialRetainedView.transform.position;
                var initialRetainedWorldRotation = initialRetainedView.transform.rotation;

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(20, retainedCell, Direction.Left),
                            CreateSurfaceBox(30, shownCell, Direction.Right),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>(),
                            new[]
                            {
                                new TickTransitionVisibilityChange(
                                    20,
                                    TickTransitionVisibilityMode.RetainUntilTransitionComplete,
                                    retainedCell,
                                    initialTopology,
                                    Direction.Left),
                                new TickTransitionVisibilityChange(
                                    30,
                                    TickTransitionVisibilityMode.ShowAtTransitionStart,
                                    shownCell,
                                    rotatedTopology,
                                    Direction.Right),
                            })));
                presenter.UpdatePresentation(0f);

                Assert.That(registry.TryGetView(20, out var retainedView), Is.True);
                Assert.That(retainedView.gameObject.activeSelf, Is.True);
                Assert.That(registry.TryGetView(30, out var shownView), Is.True);
                Assert.That(shownView.gameObject.activeSelf, Is.True);

                var retainedTransitionPosition = GetPresenterTransitionLocalPosition(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    retainedCell,
                    EntityType.Box);
                var retainedTransitionRotation = GetPresenterTransitionLocalRotation(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    retainedCell,
                    Direction.Left);

                Assert.That(Vector3.Distance(retainedView.transform.position, initialRetainedWorldPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(retainedView.transform.rotation, initialRetainedWorldRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(retainedView.transform.localPosition, retainedTransitionPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(retainedView.transform.localRotation, retainedTransitionRotation), Is.LessThan(0.001f));

                presenter.UpdatePresentation(timingProfile.TopologyMotionDurationSeconds);

                Assert.That(presenter.CurrentPresentationPhase, Is.EqualTo(GameplayPresentationPhase.Idle));
                Assert.That(retainedView.gameObject.activeSelf, Is.False);
                Assert.That(shownView.gameObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_TopologyTransition_PreservesMotionStartWorldPose()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_TopologyTransition_PreservesMotionStartWorldPose");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var boardRoot = rootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();
                registry.ConfigureSearchRoot(boardRoot.EntityRoot);
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1));
                var initialTopology = new CubeTopologyState(FaceId.Floor);
                var rotatedTopology = new CubeTopologyState(FaceId.Front);
                var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
                var destinationCell = new SurfaceCell(FaceId.Front, 0, 0);

                presenter.Initialize(
                    binder,
                    boardBounds,
                    initialTopology,
                    1f,
                    timingProfile,
                    boardRoot);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, sourceCell),
                    },
                    initialTopology);
                Assert.That(registry.TryGetView(10, out var actorView), Is.True);
                var initialWorldPosition = actorView.transform.position;
                var initialWorldRotation = actorView.transform.rotation;

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, destinationCell),
                        },
                        rotatedTopology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    sourceCell,
                                    destinationCell,
                                    initialTopology,
                                    rotatedTopology,
                                    Direction.Up,
                                    Direction.Up),
                            },
                            new TickTopologyMotion(initialTopology, rotatedTopology, CubeRotationKind.Forward),
                            Array.Empty<TickVisibilityChange>())));
                presenter.UpdatePresentation(0f);

                var expectedTransitionLocalPosition = GetPresenterTransitionLocalPosition(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    sourceCell);
                var expectedTransitionLocalRotation = GetPresenterTransitionLocalRotation(
                    boardBounds,
                    initialTopology,
                    rotatedTopology,
                    sourceCell,
                    Direction.Up);

                Assert.That(Vector3.Distance(actorView.transform.position, initialWorldPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(actorView.transform.rotation, initialWorldRotation), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(actorView.transform.localPosition, expectedTransitionLocalPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(actorView.transform.localRotation, expectedTransitionLocalRotation), Is.LessThan(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringBoardRotation()
        {
            var hostObject = new GameObject("GameplaySceneHost_CameraTarget_StaysOnCubeCenterDuringBoardRotation");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                var initialTarget = host.ViewCameraTarget.position;
                var expectedCenter = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f).GetCubeCenter();

                host.InputHost.SetRawMoveInput(Vector2.up);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(initialTarget, Is.EqualTo(expectedCenter));
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(expectedCenter));
                Assert.That(FindSurfaceTile(host.BoardSurfaceRenderer, "ActiveBottom_Front_0_0"), Is.Not.Null);
                Assert.That(FindSurfaceTile(host.BoardSurfaceRenderer, "ActiveFront_Ceiling_0_0"), Is.Not.Null);
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.GreaterThan(0.1f));
                Assert.That(
                    Vector3.Distance(
                        GetViewPosition(host, 10),
                        GetProjectedEntityPosition(
                            new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                            new CubeTopologyState(FaceId.Front),
                            new SurfaceCell(FaceId.Front, 0, 0))),
                    Is.GreaterThan(0.01f));

                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(expectedCenter));
                Assert.That(Quaternion.Angle(host.BoardRoot.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(
                    GetViewPosition(host, 10),
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        new CubeTopologyState(FaceId.Front),
                        new SurfaceCell(FaceId.Front, 0, 0))));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplaySceneHost_Initialize_UsesPerspectiveCameraRigAndTracksCubeCenter()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_UsesPerspectiveCameraRigAndTracksCubeCenter");
            hostObject.transform.position = new Vector3(1.5f, -0.5f, 2f);
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                viewCamera.orthographic = true;

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        ViewCamera = viewCamera,
                    });

                var expectedCenter = new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                    1f).GetCubeCenter();
                expectedCenter = host.BoardRoot.transform.TransformPoint(expectedCenter);

                Assert.That(host.GetComponent<GameplayCameraRig>(), Is.Not.Null);
                Assert.That(viewCamera.orthographic, Is.False);
                Assert.That(host.ViewCameraTarget.position, Is.EqualTo(expectedCenter));
                Assert.That(
                    Vector3.Angle(viewCamera.transform.forward, (expectedCenter - viewCamera.transform.position).normalized),
                    Is.LessThan(0.1f));
                Assert.That(viewCamera.transform.position.y, Is.GreaterThan(expectedCenter.y));
                Assert.That(viewCamera.transform.position.z, Is.LessThan(expectedCenter.z));
                Assert.That(viewCamera.transform.forward.y, Is.LessThan(-0.25f));
                Assert.That(viewCamera.transform.forward.z, Is.GreaterThan(0.9f));
                Assert.That(Mathf.Abs(viewCamera.transform.forward.x), Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void GameplaySceneHost_Initialize_AppliesConfiguredCameraSettingsToRigAndCamera()
        {
            var hostObject = new GameObject("GameplaySceneHost_Initialize_AppliesConfiguredCameraSettingsToRigAndCamera");
            var cameraObject = new GameObject("ViewCamera");

            try
            {
                var viewCamera = cameraObject.AddComponent<Camera>();
                var expectedCameraSettings = new GameplayCameraSettings
                {
                    PitchDegrees = 18f,
                    YawDegrees = 31f,
                    DistanceMode = GameplayCameraRig.DistanceMode.Manual,
                    ManualDistance = 9f,
                    FramingPadding = 1.35f,
                    PerspectiveFieldOfView = 47f,
                    NearClipPlane = 0.15f,
                    FarClipPlane = 77f,
                    ClearFlags = CameraClearFlags.SolidColor,
                    BackgroundColor = new Color(0.1f, 0.2f, 0.3f),
                };

                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CameraSettings = expectedCameraSettings,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(1, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        SnapViewCameraToTarget = true,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        ViewCamera = viewCamera,
                    });

                var rig = host.GetComponent<GameplayCameraRig>();
                Assert.That(rig, Is.Not.Null);
                Assert.That(rig.PitchDegrees, Is.EqualTo(expectedCameraSettings.PitchDegrees).Within(0.0001f));
                Assert.That(rig.YawDegrees, Is.EqualTo(expectedCameraSettings.YawDegrees).Within(0.0001f));
                Assert.That(rig.CurrentDistanceMode, Is.EqualTo(expectedCameraSettings.DistanceMode));
                Assert.That(rig.ManualDistance, Is.EqualTo(expectedCameraSettings.ManualDistance).Within(0.0001f));
                Assert.That(rig.FramingPadding, Is.EqualTo(expectedCameraSettings.FramingPadding).Within(0.0001f));
                Assert.That(rig.PerspectiveFieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(rig.NearClipPlane, Is.EqualTo(expectedCameraSettings.NearClipPlane).Within(0.0001f));
                Assert.That(rig.FarClipPlane, Is.EqualTo(expectedCameraSettings.FarClipPlane).Within(0.0001f));
                Assert.That(rig.ClearFlags, Is.EqualTo(expectedCameraSettings.ClearFlags));
                Assert.That(rig.BackgroundColor, Is.EqualTo(expectedCameraSettings.BackgroundColor));
                Assert.That(viewCamera.fieldOfView, Is.EqualTo(expectedCameraSettings.PerspectiveFieldOfView).Within(0.0001f));
                Assert.That(viewCamera.clearFlags, Is.EqualTo(expectedCameraSettings.ClearFlags));
                Assert.That(viewCamera.backgroundColor, Is.EqualTo(expectedCameraSettings.BackgroundColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void GameplaySceneHost_MoveMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates()
        {
            var hostObject = new GameObject("GameplaySceneHost_MoveMotion_KeepsWorldQueriesOnCommittedDestinationWhileViewInterpolates");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();
                host.Initialize(
                    new GameplaySceneHostConfiguration
                    {
                        AutoAdvanceTicks = false,
                        AutoCreateViews = true,
                        CellSize = 1f,
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out var movedUnit), Is.True);
                Assert.That(movedUnit.entityId, Is.EqualTo(10));
                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 0, 0), out _), Is.False);

                var renderedPosition = GetViewPosition(host, 10);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 0, 0));
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0));
                Assert.That(renderedPosition.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
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
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerPushContactThresholdSeconds = 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
                host.InputHost.RunSingleTick();
                host.Presenter.UpdatePresentation(host.TimingProfile.PushMotionDurationSeconds * 0.5f);

                var snapshot = host.WorldState.CreateSnapshot();

                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 2, 0), out var pushedBox), Is.True);
                Assert.That(pushedBox.entityId, Is.EqualTo(20));
                Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
                Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.True);

                var renderedPosition = GetViewPosition(host, 20);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(renderedPosition.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
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
                        InitialBoardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                            CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 2, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        PlayerPushContactThresholdSeconds = 1f / GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
                    });

                host.InputHost.SetRawMoveInput(Vector2.right);
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
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(renderedPosition.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
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
                        InitialBoardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        InitialEntities = new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                            CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, -1, 0), facing: Direction.Left),
                        },
                        InitialTopology = new CubeTopologyState(FaceId.Floor),
                        PlayerEntityId = 10,
                        StaticEntityLogics = Array.Empty<IEntityLogic>(),
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
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, -1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                    new CubeTopologyState(FaceId.Floor),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                Assert.That(renderedPosition.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(renderedPosition.x, Is.LessThan(destinationPosition.x));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_MoveMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_MoveMotion_MidpointInterpolatesBetweenSourceAndDestination");

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
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(10, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0));
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0));
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_ProjectileMoveMotion_MidpointInterpolatesBetweenSourceAndDestination()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_ProjectileMoveMotion_MidpointInterpolatesBetweenSourceAndDestination");

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
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 0, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.ProjectileMove,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.ProjectileStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    EntityType.Projectile);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Projectile);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_DetachVisibility_KeepsTargetVisibleUntilTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_DetachVisibility_KeepsTargetVisibleUntilTrackCompletes");

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
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                        CreateSurfaceBox(20, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Up),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceUnit(10, new SurfaceCell(FaceId.Floor, 1, 0)),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    10,
                                    TickEntityMotionKind.Move,
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0)),
                            },
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    20,
                                    TickVisibilityChangeKind.Detach,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    topology,
                                    Direction.Up),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var detachedView), Is.True);
                Assert.That(detachedView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(detachedView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_RemoveVisibility_KeepsTargetVisibleUntilTrackCompletes()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_RemoveVisibility_KeepsTargetVisibleUntilTrackCompletes");

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
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceProjectile(30, new SurfaceCell(FaceId.Floor, 1, 0), facing: Direction.Right),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        Array.Empty<EntityState>(),
                        topology,
                        new TickPresentationData(
                            Array.Empty<TickEntityMotion>(),
                            topologyMotion: null,
                            visibilityChanges: new[]
                            {
                                new TickVisibilityChange(
                                    30,
                                    TickVisibilityChangeKind.Remove,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    topology,
                                    Direction.Right),
                            })));
                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var removedView), Is.True);
                Assert.That(removedView.gameObject.activeSelf, Is.True);

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(removedView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
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
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_BoxSlideMotion_MidpointUsesLinearInterpolation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_BoxSlideMotion_MidpointUsesLinearInterpolation");

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
                var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 1));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
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
                                    TickEntityMotionKind.BoxSlide,
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, 2, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.BoxSlideStepIntervalSeconds * 0.5f);

                Assert.That(registry.TryGetView(20, out var view), Is.True);
                var sourcePosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                var expectedMidpoint = (sourcePosition + destinationPosition) * 0.5f;

                Assert.That(view.transform.position.x, Is.EqualTo(expectedMidpoint.x).Within(0.001f));
                Assert.That(view.transform.position.y, Is.EqualTo(expectedMidpoint.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(expectedMidpoint.z).Within(0.001f));
                Assert.That(presenter.IsPresentationActive, Is.True);
                Assert.That(presenter.HasBlockingPresentation, Is.False);
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
                var firstSourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    EntityType.Box);
                var firstDestinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(firstSourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(firstDestinationPosition.x));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                Assert.That(view.transform.position, Is.EqualTo(firstDestinationPosition));

                presenter.UpdatePresentation(timingProfile.PushMotionDurationSeconds * 0.5f);
                var secondDestinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 1)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 2, 0),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(firstDestinationPosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(secondDestinationPosition.x));
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
                Assert.That(
                    view.transform.position.y,
                    Is.GreaterThan(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        topology,
                        new SurfaceCell(FaceId.Floor, -1, 0),
                        EntityType.Box).y + 0.2f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayTickViewPresenter_FlipMotion_OnFrontFace_UsesFaceRelativeArcAndRotation()
        {
            var rootObject = new GameObject("GameplayTickViewPresenter_FlipMotion_OnFrontFace_UsesFaceRelativeArcAndRotation");

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
                var boardBounds = new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 2));

                presenter.Initialize(
                    binder,
                    boardBounds,
                    topology,
                    1f,
                    timingProfile);
                presenter.PresentInitial(
                    new[]
                    {
                        CreateSurfaceBox(30, new SurfaceCell(FaceId.Front, -1, 0), facing: Direction.Left),
                    },
                    topology);

                presenter.Present(
                    CreateTickResult(
                        new[]
                        {
                            CreateSurfaceBox(30, new SurfaceCell(FaceId.Front, 1, 0), facing: Direction.Right),
                        },
                        topology,
                        new TickPresentationData(
                            new[]
                            {
                                new TickEntityMotion(
                                    30,
                                    TickEntityMotionKind.Flip,
                                    new SurfaceCell(FaceId.Front, -1, 0),
                                    new SurfaceCell(FaceId.Front, 1, 0)),
                            })));
                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(30, out var view), Is.True);

                var sourcePosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Front, -1, 0),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    boardBounds,
                    topology,
                    new SurfaceCell(FaceId.Front, 1, 0),
                    EntityType.Box);

                Assert.That(view.transform.position.x, Is.EqualTo((sourcePosition.x + destinationPosition.x) * 0.5f).Within(0.15f));
                Assert.That(view.transform.position.z, Is.LessThan(sourcePosition.z - 0.2f));
                Assert.That(Vector3.Angle(view.transform.forward, Vector3.forward), Is.GreaterThan(30f));
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
                Assert.That(
                    view.transform.position,
                    Is.EqualTo(GetProjectedEntityPosition(
                        new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                        topology,
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        EntityType.Box)));
                Assert.That(
                    Quaternion.Angle(
                        view.transform.rotation,
                        GetProjectedEntityRotation(
                            new BoardBounds(new Vector2Int(-2, 0), new Vector2Int(2, 1)),
                            topology,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            Direction.Right,
                            EntityType.Box)),
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
                var sourcePosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    EntityType.Box);
                var destinationPosition = GetProjectedEntityPosition(
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                    topology,
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    EntityType.Box);
                Assert.That(view.transform.position.x, Is.GreaterThan(sourcePosition.x));
                Assert.That(view.transform.position.x, Is.LessThan(destinationPosition.x));
                Assert.That(view.transform.position.y, Is.EqualTo(sourcePosition.y).Within(0.001f));
                Assert.That(view.transform.position.z, Is.EqualTo(sourcePosition.z).Within(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        view.transform.rotation,
                        GetProjectedEntityRotation(
                            new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(2, 2)),
                            topology,
                            new SurfaceCell(FaceId.Floor, 1, 1),
                            Direction.Right,
                            EntityType.Box)),
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
            TickPresentationData presentationData = null,
            MovementPhaseResult movementPhaseResult = null,
            AttackPhaseResult attackPhaseResult = null,
            CleanupPhaseResult cleanupPhaseResult = null)
        {
            return new TickResult(
                1,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                movementPhaseResult ?? MovementPhaseResult.Empty,
                attackPhaseResult ?? AttackPhaseResult.Empty,
                cleanupPhaseResult ?? CleanupPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData ?? TickPresentationData.Empty,
                string.Empty,
                TickTrace.Empty);
        }

        private static AttackPhaseResult CreateAttackPhaseResult(params ActionGroup[] selectedGroups)
        {
            return new AttackPhaseResult(
                Array.Empty<RawAttackIntent>(),
                Array.Empty<ImpactReservation>(),
                Array.Empty<AttackIntent>(),
                Array.Empty<ActionGroup>(),
                selectedGroups,
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        private static EntityState CreateSurfaceUnit(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying,
            bool markedForDeath = false,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Up)
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
                facing = facing,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                aiMode = aiMode,
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

        private static EntityState CreateSurfaceWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static Vector3 GetProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryProjectEntityCell(cell, topology, entityType, out var projectedPose), Is.True);
            return projectedPose.LocalPosition;
        }

        private static Vector3 GetTransitionProjectedEntityPosition(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
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

        private static Quaternion GetProjectedEntityRotation(
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            Direction facing,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryResolveEntityRotation(cell, topology, facing, out var rotation), Is.True);
            return rotation;
        }

        private static Quaternion GetTransitionProjectedEntityRotation(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            Direction facing,
            float cellSize = 1f)
        {
            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(
                projector.TryResolveTransitionEntityRotation(
                    cell,
                    sourceTopology,
                    destinationTopology,
                    facing,
                    out var rotation),
                Is.True);
            return rotation;
        }

        private static Vector3 GetPresenterTransitionLocalPosition(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            EntityType entityType = EntityType.Unit,
            float cellSize = 1f,
            TopologyRotationVisualMapping mapping = TopologyRotationVisualMapping.ForwardUsesNegativeX)
        {
            var sourceVisiblePosition = GetTransitionProjectedEntityPosition(
                boardBounds,
                destinationTopology,
                sourceTopology,
                cell,
                entityType,
                cellSize);
            return Quaternion.Inverse(ResolveTopologyTransitionStartRotation(sourceTopology, destinationTopology, mapping)) *
                   sourceVisiblePosition;
        }

        private static Quaternion GetPresenterTransitionLocalRotation(
            BoardBounds boardBounds,
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            SurfaceCell cell,
            Direction facing,
            float cellSize = 1f,
            TopologyRotationVisualMapping mapping = TopologyRotationVisualMapping.ForwardUsesNegativeX)
        {
            var sourceVisibleRotation = GetTransitionProjectedEntityRotation(
                boardBounds,
                destinationTopology,
                sourceTopology,
                cell,
                facing,
                cellSize);
            return Quaternion.Inverse(ResolveTopologyTransitionStartRotation(sourceTopology, destinationTopology, mapping)) *
                   sourceVisibleRotation;
        }

        private static Quaternion ResolveTopologyTransitionStartRotation(
            CubeTopologyState sourceTopology,
            CubeTopologyState destinationTopology,
            TopologyRotationVisualMapping mapping)
        {
            var forwardDegrees = mapping == TopologyRotationVisualMapping.ForwardUsesPositiveX
                ? 90f
                : -90f;

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Forward)))
            {
                return Quaternion.Euler(forwardDegrees, 0f, 0f);
            }

            if (destinationTopology.Equals(sourceTopology.Rotate(CubeRotationKind.Backward)))
            {
                return Quaternion.Euler(-forwardDegrees, 0f, 0f);
            }

            return Quaternion.identity;
        }

        private static GameObject FindSurfaceTile(GameplayBoardSurfaceRenderer renderer, string tileName)
        {
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.VisibleTilePoolRoot, Is.Not.Null);
            var tile = renderer.VisibleTilePoolRoot.Find(tileName);
            Assert.That(tile, Is.Not.Null, $"Expected board surface tile '{tileName}' to exist.");
            return tile.gameObject;
        }

        private static void AssertSurfaceTileMatchesProjection(
            GameObject tile,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            SurfaceCell cell,
            float cellSize = 1f)
        {
            Assert.That(tile, Is.Not.Null);

            var projector = new GameplayCubeProjector(boardBounds, cellSize);
            Assert.That(projector.TryProjectSurfaceCell(cell, topology, out var projectedPose), Is.True);

            var tileTransform = tile.transform;
            var expectedPosition = projectedPose.LocalPosition - (projectedPose.Normal * (tileTransform.localScale.z * 0.5f));

            Assert.That(tileTransform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(tileTransform.localRotation, projectedPose.LocalRotation), Is.LessThan(0.001f));
            Assert.That(tileTransform.localScale.x, Is.EqualTo(cellSize * 0.98f).Within(0.001f));
            Assert.That(tileTransform.localScale.y, Is.EqualTo(cellSize * 0.98f).Within(0.001f));
            Assert.That(tileTransform.localScale.z, Is.EqualTo(cellSize * 0.08f).Within(0.001f));
        }

        private static Vector3 GetViewPosition(GameplaySceneHost host, int entityId)
        {
            Assert.That(host.ViewRegistry.TryGetView(entityId, out var view), Is.True);
            return view.transform.position;
        }

        private static void AssertVisualMatchesProfile(
            GameplayEntityView view,
            GameplayEntityVisualProfile expectedProfile,
            Color expectedColor)
        {
            Assert.That(view, Is.Not.Null);
            Assert.That(view.transform.parent, Is.Not.Null);
            Assert.That(view.GetComponent<Renderer>(), Is.Null);
            Assert.That(view.ModelRoot, Is.Not.Null);
            Assert.That(view.ModelRoot.parent, Is.EqualTo(view.transform));
            Assert.That(view.ModelRoot.localPosition, Is.EqualTo(expectedProfile.ModelLocalPosition));
            Assert.That(
                Quaternion.Angle(view.ModelRoot.localRotation, expectedProfile.ModelLocalRotation),
                Is.LessThan(0.001f));
            Assert.That(view.ModelRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.ModelRoot.childCount, Is.EqualTo(1));

            var visual = view.ModelRoot.GetChild(0);
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.identity), Is.LessThan(0.001f));
            Assert.That(visual.localScale, Is.EqualTo(expectedProfile.ModelLocalScale));
            Assert.That(visual.GetComponent<Collider>(), Is.Null);

            var meshFilter = visual.GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.name, Does.Contain("Cube").IgnoreCase);

            var renderer = visual.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null);
            AssertColorApproximately(renderer.sharedMaterial.color, expectedColor);
        }

        private static void AssertColorApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }

        private sealed class TestViewFactory : IGameplayEntityViewFactory
        {
            private readonly bool _attachEnemyAnimatorDriver;
            private readonly bool _attachPlayerAnimatorDriver;
            private readonly Transform _parent;

            public TestViewFactory(
                Transform parent,
                bool attachEnemyAnimatorDriver = false,
                bool attachPlayerAnimatorDriver = false)
            {
                _parent = parent;
                _attachEnemyAnimatorDriver = attachEnemyAnimatorDriver;
                _attachPlayerAnimatorDriver = attachPlayerAnimatorDriver;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_attachEnemyAnimatorDriver &&
                    entity.aiMode != EnemyAiMode.None)
                {
                    viewObject.AddComponent<EnemyAnimatorDriver>();
                }

                if (_attachPlayerAnimatorDriver &&
                    entity.aiMode == EnemyAiMode.None &&
                    entity.type == EntityType.Unit)
                {
                    viewObject.AddComponent<PlayerAnimatorDriver>();
                }

                return view;
            }
        }
    }
}
