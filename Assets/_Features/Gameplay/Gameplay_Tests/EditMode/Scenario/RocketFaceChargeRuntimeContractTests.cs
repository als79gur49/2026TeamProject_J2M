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

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class RocketFaceChargeRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string RocketFaceProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Charger/EnemyAi_Charger.asset";

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_UsesChargeResolverAndRuntimeChargeState()
        {
            var profile = LoadRocketFaceProfile();
            var runtime = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(profile.StateResolverKind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(profile.AttackDecisionStrategyKind, Is.EqualTo(AttackDecisionStrategyKind.None));
            Assert.That(profile.MovementSkillStrategyKind, Is.EqualTo(MovementSkillStrategyKind.None));
            Assert.That(runtime.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(runtime.TryGetChargeBehavior(out var charge), Is.True);
            Assert.That(charge.Timing.WindupTicks, Is.GreaterThanOrEqualTo(0));
            Assert.That(runtime.Capabilities.TryGetPassiveContact(out _), Is.True);
            Assert.That(runtime.Capabilities.TryGetCombat(out _), Is.False);
            Assert.That(runtime.Capabilities.TryGetMovementSkill(out _), Is.False);

            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            });

            CreatePipeline(worldState, profile).RunTick(new TickInput(1));

            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(EnemyId, out var chargeState), Is.True);
            Assert.That(chargeState.phase, Is.Not.EqualTo(EnemyChargePhase.None));
            Assert.That(chargeState.lockedDirection, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_WindupLocksDirectionWithoutMovementOrContact()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateEnemy(source, Direction.Right, EnemyAiMode.Charge),
            });
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());

            var windupTick = pipeline.RunTick(new TickInput(1));
            var started = GetChargeState(worldState);
            worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 4));
            var stillWindupTick = pipeline.RunTick(new TickInput(2));
            var stillWindup = GetChargeState(worldState);

            Assert.That(started.phase, Is.EqualTo(EnemyChargePhase.Windup));
            Assert.That(started.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(stillWindup.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(source));
            Assert.That(windupTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(stillWindupTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(windupTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(stillWindupTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ActiveLoopsUntilReachableStepsConsumedOrBlocked()
        {
            var worldState = CreateOpenActiveChargeWorld(remainingActiveSteps: 3);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 1, 0));
            var afterFirst = GetChargeState(worldState);
            Assert.That(afterFirst.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(afterFirst.remainingActiveSteps, Is.EqualTo(2));

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 2, 0));
            var afterSecond = GetChargeState(worldState);
            Assert.That(afterSecond.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(afterSecond.remainingActiveSteps, Is.EqualTo(1));
            Assert.That(afterSecond.lockedDirection, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ActiveKeepsLockedDirectionAndDoesNotRetarget()
        {
            var worldState = CreateOpenActiveChargeWorld(remainingActiveSteps: 2);
            worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 3));
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 1, 0));

            var chargeState = GetChargeState(worldState);
            Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.Active));
            Assert.That(chargeState.lockedDirection, Is.EqualTo(Direction.Right));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [TestCase("BoardEdge")]
        [TestCase("Solid")]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ActiveStopsOnBoardEdgeOrSolidAndThenRecovers(string blockerKind)
        {
            var worldState = CreateBlockedActiveChargeWorld(blockerKind);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());

            var tick = pipeline.RunTick(new TickInput(1));
            var chargeState = GetChargeState(worldState);

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == EnemyId), Is.False);
            Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.Recover), "CurrentPolicy: exact stop-to-recover tick follows authored RocketFace recover timing.");
            Assert.That(chargeState.recoverRemainingTicks, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_UnitOverlapDoesNotStop_CurrentPolicy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 1, 0), hp: 5),
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 1, 0));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Active));
            AssertNoUnitSolidOverlap(worldState, new SurfaceCell(FaceId.Floor, 1, 0));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_GeneratedMoonBlockSolidStops_MoonBlockGeneratorFeatureAloneDoesNot()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var generator = CreateTileFeature(101, destination, TileFeatureKind.MoonBlockGenerator, boundEntityId: 50);
            var generatorOnlyWorld = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { generator });
            SeedActiveCharge(generatorOnlyWorld, remainingActiveSteps: 2);
            var generatorPipeline = CreatePipeline(generatorOnlyWorld, LoadRocketFaceProfile(), CreateActiveDefinitions(generator));
            var tick = 1;

            RunUntilChargeStepSettles(generatorPipeline, generatorOnlyWorld, ref tick, destination);
            Assert.That(GetEntity(generatorOnlyWorld, EnemyId).position, Is.EqualTo(destination));

            var solidWorld = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateBox(50, destination, BoxArchetype.Moon),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { generator });
            SeedActiveCharge(solidWorld, remainingActiveSteps: 2);

            var blockedTick = CreatePipeline(solidWorld, LoadRocketFaceProfile(), CreateActiveDefinitions(generator)).RunTick(new TickInput(1));

            Assert.That(GetEntity(solidWorld, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetChargeState(solidWorld).phase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(blockedTick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == EnemyId), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ActivatedDestroyTileDoesNotHardStopActive_CurrentPolicy()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyTile = CreateTileFeature(102, destination, TileFeatureKind.Destroy);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { destroyTile });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile(), CreateActiveDefinitions(destroyTile));

            var result = RunUntilEnemyMovesOrIsRemoved(pipeline, worldState, destination);

            Assert.That(result.Trace.Text, Does.Not.Contain("ChargeBlocked"));
            Assert.That(result.PresentationData.TileEvents.Any(tileEvent => tileEvent.TargetEntityId == EnemyId), Is.True);
            Assert.That(
                !worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy) || enemy.position == destination,
                Is.True,
                "CurrentPolicy: DestroyTile effect resolution is separate from the charge active hard-stop rule.");
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ActivatedBarricadeStopsActiveChargeAndThenRecovers()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var barricade = CreateTileFeature(103, destination, TileFeatureKind.Barricade);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { barricade });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile(), CreateActiveDefinitions(barricade));

            var tick = pipeline.RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == EnemyId), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Recover));
            Assert.That(GetChargeState(worldState).recoverRemainingTicks, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_InactiveBarricadeDoesNotStopActiveCharge()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var barricade = CreateTileFeature(104, destination, TileFeatureKind.Barricade);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { barricade });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile(), CreateInactiveDefinitions(barricade));
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, destination);

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(destination));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(destination, out _), Is.False);
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Active));
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_SamePlanarOtherFaceActivatedBarricadeDoesNotStopCharge()
        {
            var destination = new SurfaceCell(FaceId.Floor, 1, 0);
            var otherFaceBarricade = CreateTileFeature(105, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Barricade);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                    CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
                },
                initialTileFeatures: new[] { otherFaceBarricade });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile(), CreateActiveDefinitions(otherFaceBarricade));
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, destination);

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(destination));
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Active));
        }

        [TestCase("HpZero")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_DeathOrDetachPreventsNextActiveStep(string invalidationKind)
        {
            var enemy = CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge);
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
                CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                enemy,
            });
            SeedActiveCharge(worldState, remainingActiveSteps: 2);

            var tick = CreatePipeline(worldState, LoadRocketFaceProfile()).RunTick(new TickInput(1));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == EnemyId), Is.False);
            if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var remainingEnemy))
            {
                Assert.That(remainingEnemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            }

            if (worldState.CreateSnapshot().TryGetEnemyChargeState(EnemyId, out var chargeState))
            {
                Assert.That(chargeState.phase, Is.EqualTo(EnemyChargePhase.None));
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_RecoverBeginsOnlyAfterActiveStops()
        {
            var worldState = CreateOpenActiveChargeWorld(remainingActiveSteps: 2);
            var pipeline = CreatePipeline(worldState, LoadRocketFaceProfile());
            var tick = 1;

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 1, 0));
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Active));

            RunUntilChargeStepSettles(pipeline, worldState, ref tick, new SurfaceCell(FaceId.Floor, 2, 0));
            Assert.That(GetChargeState(worldState).remainingActiveSteps, Is.Zero);

            var recoverTick = pipeline.RunTick(new TickInput(tick++));
            Assert.That(GetChargeState(worldState).phase, Is.EqualTo(EnemyChargePhase.Recover), recoverTick.Trace.Text);
        }

        [Test]
        [Category("Extended")]
        public void EnemyCharge_RocketFaceProfile_ChargeStateParticipatesInDeterminismHash()
        {
            var baselineWorld = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            });
            var chargeWorld = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0)),
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            });
            SeedActiveCharge(chargeWorld, remainingActiveSteps: 2);

            var profile = LoadRocketFaceProfile();
            var baselineHash = CreatePipeline(baselineWorld, profile)
                .RunTick(new TickInput(1))
                .DeterminismHash;
            var chargeHash = CreatePipeline(chargeWorld, profile)
                .RunTick(new TickInput(1))
                .DeterminismHash;

            Assert.That(profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond).Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(baselineHash, Is.Not.Empty);
            Assert.That(chargeHash, Is.Not.Empty);
            Assert.That(chargeHash, Is.Not.EqualTo(baselineHash));
        }

        private static EnemyAiProfile LoadRocketFaceProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(RocketFaceProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing RocketFace Charge profile at '{RocketFaceProfilePath}'.");
            return profile;
        }

        private static WorldState CreateOpenActiveChargeWorld(int remainingActiveSteps)
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 5, 0), hp: 5),
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            });
            SeedActiveCharge(worldState, remainingActiveSteps);
            return worldState;
        }

        private static WorldState CreateBlockedActiveChargeWorld(string blockerKind)
        {
            var entities = new List<EntityState>
            {
                CreateEnemy(new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right, EnemyAiMode.Charge),
            };
            var bounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0));
            if (blockerKind == "BoardEdge")
            {
                bounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
            }
            else
            {
                entities.Insert(0, CreatePlayer(new SurfaceCell(FaceId.Floor, 4, 0), hp: 5));
                if (blockerKind == "Solid")
                {
                    entities.Add(CreateBox(50, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal));
                }
            }

            var worldState = CreateWorldState(entities, boardBounds: bounds);
            SeedActiveCharge(worldState, remainingActiveSteps: 2);
            return worldState;
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
                unitKinematicLocomotionTiming: CreateOneTickKinematicTiming(timingProfile),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming(GameplayTimingProfile timingProfile)
        {
            return new UnitKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static TickResult RunUntilChargeStepSettles(
            TickPipeline pipeline,
            WorldState worldState,
            int startTick,
            SurfaceCell destination)
        {
            var tick = startTick;
            return RunUntilChargeStepSettles(pipeline, worldState, ref tick, destination);
        }

        private static TickResult RunUntilChargeStepSettles(
            TickPipeline pipeline,
            WorldState worldState,
            ref int tick,
            SurfaceCell destination)
        {
            TickResult result = null;
            for (var i = 0; i < 32; i++)
            {
                result = pipeline.RunTick(new TickInput(tick++));
                if (worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy) &&
                    enemy.position == destination &&
                    !worldState.CreateSnapshot().TryGetUnitKinematicState(EnemyId, out _))
                {
                    return result;
                }
            }

            Assert.Fail($"RocketFace charge step did not settle at {destination}. Last trace: {result?.Trace.Text}");
            return result;
        }

        private static TickResult RunUntilEnemyMovesOrIsRemoved(
            TickPipeline pipeline,
            WorldState worldState,
            SurfaceCell destination)
        {
            TickResult result = null;
            for (var tick = 1; tick <= 32; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick));
                if (!worldState.CreateSnapshot().TryGetEntity(EnemyId, out var enemy) ||
                    enemy.position == destination)
                {
                    return result;
                }
            }

            Assert.Fail($"RocketFace charge did not enter or resolve destination {destination}. Last trace: {result?.Trace.Text}");
            return result;
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds? boardBounds = null,
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                boardBounds ?? new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 6)),
                topology ?? new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
        }

        private static EntityState CreatePlayer(SurfaceCell position, int hp = 3)
        {
            return new EntityState
            {
                entityId = PlayerId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemy(SurfaceCell position, Direction facing, EnemyAiMode mode)
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
                aiMode = mode,
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
                    boundEntityId: tileFeature.Charges))
                .ToArray();
        }

        private static void SeedActiveCharge(WorldState worldState, int remainingActiveSteps)
        {
            worldState.CreateWriteContext().SetEnemyChargeState(
                EnemyId,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 1,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = remainingActiveSteps,
                });
        }

        private static EnemyChargeRuntimeState GetChargeState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(EnemyId, out var state), Is.True);
            return state;
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static void AssertNoUnitSolidOverlap(WorldState worldState, SurfaceCell cell)
        {
            var snapshot = worldState.CreateSnapshot();
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            Assert.That(
                units.Any(unit => unit.boardPresence == EntityBoardPresence.Occupying) &&
                snapshot.TryGetSolidSemanticAt(cell, out _),
                Is.False,
                $"Unexpected Unit/Solid overlap at {cell}.");
        }
    }
}
