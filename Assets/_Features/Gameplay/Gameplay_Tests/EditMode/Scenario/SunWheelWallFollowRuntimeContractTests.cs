using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class SunWheelWallFollowRuntimeContractTests
    {
        private const int EnemyId = 40;
        private const string SunWheelProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_WallFollower/EnemyAi_WallFollower.asset";
        private static readonly SurfaceCell WallFollowSource = new(FaceId.Floor, 1, 0);
        private static readonly SurfaceCell WallFollowDestination = new(FaceId.Floor, 1, -1);

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_UsesWallFollowPatrolLane_NotMovementSkillOrCharge()
        {
            var profile = LoadSunWheelProfile();
            var runtime = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(profile.PatrolStrategyKind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(runtime.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(runtime.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.WallFollow));
            Assert.That(runtime.Capabilities.TryGetPassiveContact(out _), Is.True);
            Assert.That(runtime.Capabilities.TryGetCombat(out _), Is.False);
            Assert.That(runtime.Capabilities.TryGetMovementSkill(out _), Is.False);
            Assert.That(runtime.Core.ChargeTimingSettings.WindupTicks, Is.Zero);
            Assert.That(runtime.Core.ChargeTimingSettings.ActiveStepCooldownTicks, Is.Zero);
            Assert.That(runtime.Core.ChargeTimingSettings.RecoverTicks, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_UsesSharedGroundStepIntent_NotRelocationOrSettlement()
        {
            var worldState = CreateWallFollowWorld();
            var pipeline = CreatePipeline(worldState, LoadSunWheelProfile());
            var tick = pipeline.RunTick(new TickInput(1));

            var intent = tick.MovementPhaseResult.RawIntents.Single(raw => raw.SourceId == EnemyId);
            Assert.That(intent.CommandKind, Is.EqualTo(MovementCommandKind.Move));
            Assert.That(intent.Destination, Is.EqualTo(WallFollowDestination.PlanarPosition));
            RunUntilEntityAt(pipeline, worldState, startTick: 2, destination: WallFollowDestination);
            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(WallFollowDestination));
            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(EnemyId, out var chargeState), Is.False);
            Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.None));
            Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(EnemyId, out _), Is.False);
            AssertNoGhostOrUnitSolidOverlap(worldState, WallFollowSource, WallFollowDestination);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_PreservesSurfaceCellFaceIdentity()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 1)),
                CreateWall(91, new SurfaceCell(FaceId.Front, 1, -1)),
                CreateEnemy(WallFollowSource, Direction.Left),
            });

            RunUntilEntityAt(
                CreatePipeline(worldState, LoadSunWheelProfile()),
                worldState,
                startTick: 1,
                destination: WallFollowDestination);

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(WallFollowDestination));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(new SurfaceCell(FaceId.Front, 1, -1), out _), Is.True);
            AssertNoGhostOrUnitSolidOverlap(worldState, WallFollowDestination, new SurfaceCell(FaceId.Front, 1, -1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_OffBottomDoesNotCommitWallFollowStep()
        {
            var staleDestination = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateWall(90, new SurfaceCell(FaceId.Front, 1, 1)),
                    CreateEnemy(new SurfaceCell(FaceId.Front, 1, 0), Direction.Left),
                },
                topology: new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipeline(worldState, LoadSunWheelProfile());

            var offBottom = pipeline.RunTick(new TickInput(1));
            Assert.That(offBottom.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));

            worldState.CreateWriteContext().SpawnEntity(CreateWall(91, staleDestination));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.Not.EqualTo(staleDestination));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(staleDestination, out _), Is.True);
            AssertNoGhostOrUnitSolidOverlap(worldState, new SurfaceCell(FaceId.Front, 1, 0), staleDestination);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_ActivatedBarricadeDoesNotEnterFeatureCellButMayUseFallback_CurrentPolicy()
        {
            var barricade = CreateTileFeature(100, WallFollowDestination, TileFeatureKind.Barricade);
            var samePlanarOtherFaceBarricade = CreateTileFeature(
                101,
                new SurfaceCell(FaceId.Front, WallFollowDestination.x, WallFollowDestination.y),
                TileFeatureKind.Barricade);

            var otherFaceWorld = CreateWallFollowWorld(initialTileFeatures: new[] { samePlanarOtherFaceBarricade });
            RunUntilEntityAt(
                CreatePipeline(
                    otherFaceWorld,
                    LoadSunWheelProfile(),
                    CreateActiveDefinitions(samePlanarOtherFaceBarricade)),
                otherFaceWorld,
                startTick: 1,
                destination: WallFollowDestination);

            Assert.That(GetEntity(otherFaceWorld, EnemyId).position, Is.EqualTo(WallFollowDestination));
            AssertNoGhostOrUnitSolidOverlap(
                otherFaceWorld,
                WallFollowSource,
                WallFollowDestination,
                samePlanarOtherFaceBarricade.Cell);

            var worldState = CreateWallFollowWorld(initialTileFeatures: new[] { barricade });
            var pipeline = CreatePipeline(worldState, LoadSunWheelProfile(), CreateActiveDefinitions(barricade));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(WallFollowSource));
            Assert.That(
                EnemyMovementStrategyShared.CanTraverseStep(
                    worldState.CreateSnapshot(),
                    GetEntity(worldState, EnemyId),
                    WallFollowDestination.PlanarPosition - WallFollowSource.PlanarPosition,
                    CreateActiveDefinitions(barricade)),
                Is.False,
                "Activated Barricade remains a hard TileFeature blocker for its own cell.");

            // SunWheel is WallFollow traversal: blocked own cell is StrongContract; legal fallback is CurrentPolicy.
            var fallbackCell = RunTicksAndAssertEnemyNeverReachesUntilMoved(
                pipeline,
                worldState,
                startTick: 1,
                sourceCell: WallFollowSource,
                forbiddenCell: WallFollowDestination,
                maxTicks: 80);

            AssertFallbackCellHasNoSolidOrTileFeature(worldState, fallbackCell, TileFeatureKind.Barricade);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(WallFollowDestination, out _), Is.False);
            AssertNoGhostOrUnitSolidOverlap(
                worldState,
                WallFollowSource,
                WallFollowDestination,
                fallbackCell,
                samePlanarOtherFaceBarricade.Cell);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_InactiveBarricadeDoesNotBlockWallFollowDestination()
        {
            var barricade = CreateTileFeature(106, WallFollowDestination, TileFeatureKind.Barricade);
            var definitions = CreateInactiveDefinitions(barricade);
            var worldState = CreateWallFollowWorld(initialTileFeatures: new[] { barricade });

            var tick = RunUntilEntityAt(
                CreatePipeline(worldState, LoadSunWheelProfile(), definitions),
                worldState,
                startTick: 1,
                WallFollowDestination);

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(WallFollowDestination));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(WallFollowDestination, out _), Is.False);
            Assert.That(
                tick.MovementPhaseResult.RejectedReasons.Any(reason => reason.Contains("TileFeature", StringComparison.Ordinal)),
                Is.False);
            AssertNoGhostOrUnitSolidOverlap(worldState, WallFollowSource, WallFollowDestination);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_MoonBlockGeneratorFeatureAloneDoesNotBlock_GeneratedSolidCandidateMayFallback_CurrentPolicy()
        {
            var generator = CreateTileFeature(101, WallFollowDestination, TileFeatureKind.MoonBlockGenerator, boundEntityId: 50);
            var generatorOnlyWorld = CreateWallFollowWorld(initialTileFeatures: new[] { generator });

            RunUntilEntityAt(
                CreatePipeline(generatorOnlyWorld, LoadSunWheelProfile(), CreateActiveDefinitions(generator)),
                generatorOnlyWorld,
                startTick: 1,
                WallFollowDestination);

            Assert.That(GetEntity(generatorOnlyWorld, EnemyId).position, Is.EqualTo(WallFollowDestination));
            AssertNoGhostOrUnitSolidOverlap(generatorOnlyWorld, WallFollowSource, WallFollowDestination);

            var generatedSolidWorld = CreateWorldState(
                new[]
                {
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 1)),
                    CreateBox(50, WallFollowDestination, BoxArchetype.Moon),
                    CreateEnemy(WallFollowSource, Direction.Left),
                },
                initialTileFeatures: new[] { generator });

            Assert.That(generatedSolidWorld.CreateSnapshot().TryGetSolidSemanticAt(WallFollowDestination, out _), Is.True);
            Assert.That(
                EnemyMovementStrategyShared.CanTraverseStep(
                    generatedSolidWorld.CreateSnapshot(),
                    GetEntity(generatedSolidWorld, EnemyId),
                    WallFollowDestination.PlanarPosition - WallFollowSource.PlanarPosition,
                    CreateActiveDefinitions(generator)),
                Is.False,
                "Generated MoonBlock Solid remains a Solid blocker for its own cell.");

            // MoonBlockGenerator feature alone is non-blocking; generated Solid own cell is StrongContract; fallback is CurrentPolicy.
            var fallbackCell = RunTicksAndAssertEnemyNeverReachesUntilMoved(
                CreatePipeline(generatedSolidWorld, LoadSunWheelProfile(), CreateActiveDefinitions(generator)),
                generatedSolidWorld,
                startTick: 1,
                sourceCell: WallFollowSource,
                forbiddenCell: WallFollowDestination,
                maxTicks: 80);

            AssertFallbackCellHasNoSolidOrTileFeature(generatedSolidWorld, fallbackCell, TileFeatureKind.MoonBlockGenerator);
            AssertNoGhostOrUnitSolidOverlap(
                generatedSolidWorld,
                WallFollowSource,
                WallFollowDestination,
                fallbackCell);
        }

        [Test]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_ActivatedDestroyTileDoesNotHardBlockTraversal_CurrentPolicy()
        {
            var destroyTile = CreateTileFeature(102, WallFollowDestination, TileFeatureKind.Destroy);
            var worldState = CreateWallFollowWorld(initialTileFeatures: new[] { destroyTile });

            var tick = RunUntilEnemyAtOrRemoved(
                CreatePipeline(worldState, LoadSunWheelProfile(), CreateActiveDefinitions(destroyTile)),
                worldState,
                startTick: 1,
                WallFollowDestination);

            Assert.That(
                tick.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("TileFeature", StringComparison.Ordinal) ||
                    reason.Contains("TraversalBlocked", StringComparison.Ordinal)),
                Is.False,
                "CurrentPolicy: active DestroyTile is not a Solid-equivalent hard traversal blocker for SunWheel ordinary movement.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(WallFollowDestination, out _), Is.False);
        }

        [TestCase("HpZero")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        [Category("Extended")]
        public void EnemyWallFollow_SunWheelProfile_DeadOrMarkedDoesNotCommitPendingWallFollowMove(string invalidationKind)
        {
            var enemy = CreateEnemy(WallFollowSource, Direction.Left);
            if (invalidationKind == "HpZero")
            {
                enemy.hp = 0;
            }
            else if (invalidationKind == "MarkedForDeath")
            {
                enemy.markedForDeath = true;
            }
            else if (invalidationKind == "Detached")
            {
                enemy.boardPresence = EntityBoardPresence.Detached;
            }

            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 1)),
                enemy,
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var remainingEnemy))
            {
                Assert.That(remainingEnemy.position, Is.EqualTo(WallFollowSource));
            }

            AssertNoGhostOrUnitSolidOverlap(worldState, WallFollowSource, WallFollowDestination);
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_LeftHand_LeftOpen_ChoosesLeftBeforeForward()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 2, 1)),
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(0, 1));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_LeftHand_LeftBlockedForwardOpen_ChoosesForward()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 0, 1)),
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(1, 2));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_LeftHand_LeftForwardBlockedRightOpen_ChoosesRightNotBack()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 0, 1)),
                CreateWall(91, new SurfaceCell(FaceId.Floor, 1, 2)),
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(2, 1));
            Assert.That(
                tick.MovementPhaseResult.RawIntents.Single(raw => raw.SourceId == EnemyId).Destination,
                Is.Not.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_LeftHand_LeftForwardRightBlockedBackOpen_ChoosesBack()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 0, 1)),
                CreateWall(91, new SurfaceCell(FaceId.Floor, 1, 2)),
                CreateWall(92, new SurfaceCell(FaceId.Floor, 2, 1)),
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(1, 0));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_LeftHand_BackBoundaryContextDoesNotReorderRightCandidate()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateWall(90, new SurfaceCell(FaceId.Floor, 0, 1)),
                CreateWall(91, new SurfaceCell(FaceId.Floor, 1, 2)),
                CreateWall(92, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateWall(93, new SurfaceCell(FaceId.Floor, 2, 0)),
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(2, 1));
            Assert.That(
                tick.MovementPhaseResult.RawIntents.Single(raw => raw.SourceId == EnemyId).Destination,
                Is.Not.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_BoardEdgeOnlyStraight_LeftHand_MovesForward()
        {
            var source = new SurfaceCell(FaceId.Floor, -2, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(-2, 1));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_BoardEdgeOnlyCorner_LeftHand_ChoosesRightNotBack()
        {
            var source = new SurfaceCell(FaceId.Floor, -2, 4);
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(source, Direction.Up),
            });

            var tick = CreatePipeline(worldState, LoadSunWheelProfile()).RunTick(new TickInput(1));

            AssertSunWheelIntentDestination(tick, new Vector2Int(-1, 4));
            Assert.That(
                tick.MovementPhaseResult.RawIntents.Single(raw => raw.SourceId == EnemyId).Destination,
                Is.Not.EqualTo(new Vector2Int(-2, 3)));
        }

        [Test]
        [Category("Extended")]
        public void SunWheel_NoTrackableBoundary_PreservesPositionAndFacingAcrossTicks()
        {
            var source = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(source, Direction.Up),
            });
            var pipeline = CreatePipeline(worldState, LoadSunWheelProfile());

            for (var tickIndex = 1; tickIndex <= 3; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                var enemy = GetEntity(worldState, EnemyId);

                Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
                Assert.That(enemy.position, Is.EqualTo(source));
                Assert.That(enemy.facing, Is.EqualTo(Direction.Up));
            }
        }

        private static EnemyAiProfile LoadSunWheelProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(SunWheelProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing SunWheel WallFollow profile at '{SunWheelProfilePath}'.");
            return profile;
        }

        private static void AssertSunWheelIntentDestination(TickResult tick, Vector2Int destination)
        {
            var intent = tick.MovementPhaseResult.RawIntents.Single(raw => raw.SourceId == EnemyId);

            Assert.That(intent.CommandKind, Is.EqualTo(MovementCommandKind.Move));
            Assert.That(intent.Destination, Is.EqualTo(destination));
        }

        private static WorldState CreateWallFollowWorld(IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return CreateWorldState(
                new[]
                {
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 1)),
                    CreateEnemy(WallFollowSource, Direction.Left),
                },
                initialTileFeatures: initialTileFeatures);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            EnemyAiProfile profile,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                playerKinematicLocomotionTiming: CreateOneTickKinematicTiming(timingProfile),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming(GameplayTimingProfile timingProfile)
        {
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            GameplayTerrainData terrainData = null,
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
        }

        private static EntityState CreateEnemy(SurfaceCell position, Direction facing)
        {
            return new EntityState
            {
                entityId = EnemyId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
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
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxArchetype archetype)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxArchetype = archetype,
                boxCapabilities = BoxCapabilities.Push,
                state = EntityPhaseState.Idle,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            int boundEntityId = 0)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: boundEntityId);
        }

        private static TileFeatureRuntimeDefinition[] CreateActiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.Always, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateInactiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.InactiveFaceOnly, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateDefinitions(
            TileFeatureActivationRule activationRule,
            params TileFeatureState[] tileFeatures)
        {
            return tileFeatures
                .Select(tileFeature => new TileFeatureRuntimeDefinition(
                    tileFeature.TileId,
                    activationRule,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: tileFeature.Charges,
                    presentationKey: string.Empty))
                .ToArray();
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static TickResult RunUntilEntityAt(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell destination,
            int maxTicks = 60)
        {
            TickResult result = null;
            if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var initialEnemy) &&
                initialEnemy.position == destination)
            {
                return result;
            }

            for (var tick = startTick; tick < startTick + maxTicks; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick));
                if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy) &&
                    enemy.position == destination)
                {
                    return result;
                }
            }

            Assert.Fail($"SunWheel WallFollow did not reach {destination} within {maxTicks} ticks. Last trace: {result?.Trace.Text}");
            return result;
        }

        private static TickResult RunUntilEnemyAtOrRemoved(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell destination,
            int maxTicks = 60)
        {
            TickResult result = null;
            for (var tick = startTick; tick < startTick + maxTicks; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick));
                if (!worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy) ||
                    enemy.position == destination)
                {
                    return result;
                }
            }

            Assert.Fail($"SunWheel WallFollow did not enter or resolve {destination} within {maxTicks} ticks. Last trace: {result?.Trace.Text}");
            return result;
        }

        private static string RunTicksAndAssertEnemyNeverReaches(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell forbiddenCell,
            int tickCount)
        {
            var evidence = new List<string>();
            for (var tick = startTick; tick < startTick + tickCount; tick++)
            {
                var result = pipeline.RunTick(new TickInput(tick));
                evidence.AddRange(result.MovementPhaseResult.RejectedReasons);
                evidence.Add(result.Trace.Text);
                if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy))
                {
                    Assert.That(enemy.position, Is.Not.EqualTo(forbiddenCell), result.Trace.Text);
                }
            }

            return string.Join("\n", evidence);
        }

        private static SurfaceCell RunTicksAndAssertEnemyNeverReachesUntilMoved(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell sourceCell,
            SurfaceCell forbiddenCell,
            int maxTicks = 60)
        {
            TickResult result = null;
            for (var tick = startTick; tick < startTick + maxTicks; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick));
                if (!worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy))
                {
                    Assert.Fail($"SunWheel WallFollow entity was removed before leaving {sourceCell}. Last trace: {result.Trace.Text}");
                }

                Assert.That(enemy.position, Is.Not.EqualTo(forbiddenCell), result.Trace.Text);
                if (enemy.position != sourceCell)
                {
                    return enemy.position;
                }
            }

            Assert.Fail($"SunWheel WallFollow did not use a fallback candidate within {maxTicks} ticks. Last trace: {result?.Trace.Text}");
            return default;
        }

        private static void AssertFallbackCellHasNoSolidOrTileFeature(
            WorldState worldState,
            SurfaceCell fallbackCell,
            TileFeatureKind blockedFeatureKind)
        {
            var snapshot = worldState.CreateSnapshot();
            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(fallbackCell, tileFeatures);

            Assert.That(snapshot.TryGetSolidSemanticAt(fallbackCell, out _), Is.False, $"Fallback cell {fallbackCell} must not be Solid-blocked.");
            Assert.That(
                tileFeatures.Any(tileFeature => tileFeature.Kind == blockedFeatureKind),
                Is.False,
                $"Fallback cell {fallbackCell} must not contain the skipped blocker feature.");
        }

        private static void AssertNoGhostOrUnitSolidOverlap(WorldState worldState, params SurfaceCell[] cells)
        {
            var snapshot = worldState.CreateSnapshot();
            foreach (var cell in cells)
            {
                var units = new List<EntityState>();
                snapshot.EnumerateUnitsAt(cell, units);
                var hasSolid = snapshot.TryGetSolidSemanticAt(cell, out _);
                Assert.That(
                    units.Count(unit => unit.boardPresence == EntityBoardPresence.Occupying),
                    Is.LessThanOrEqualTo(1),
                    $"Unexpected duplicate Unit occupancy at {cell}.");
                Assert.That(
                    units.Any(unit => unit.boardPresence == EntityBoardPresence.Occupying) && hasSolid,
                    Is.False,
                    $"Unexpected Unit/Solid overlap at {cell}.");
            }
        }
    }
}
