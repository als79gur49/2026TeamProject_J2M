using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class JPeterUtilitySummonRuntimeContractTests
    {
        private const int EnemyId = 40;
        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_UtilitySummoner/EnemyAi_ArchetypeSummoner.asset";
        private const string CombinedArchetypeCatalogPath =
            StageContentPaths.SharedEnemyAiRoot + "/Catalogs/EnemyUnitArchetypeCatalog_CombinedGameplayShowcase.asset";

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_WindupArmsSummonEffectButDoesNotCreateSummoned()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedReadyUtilityState(worldState);

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.effectKind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
            Assert.That(state.windupStartTick, Is.EqualTo(1));
            Assert.That(state.windupEndTick, Is.GreaterThan(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            Assert.That(tick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_RecoverSuppressesImmediateReenter()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            var pipeline = CreatePipeline(worldState);

            var executeTick = pipeline.RunTick(new TickInput(1));
            var spawnedAfterExecute = GetSummonedChildren(worldState);
            var recoverState = GetUtilityEffectState(worldState);
            var nextTick = pipeline.RunTick(new TickInput(2));

            Assert.That(spawnedAfterExecute, Has.Count.EqualTo(1));
            Assert.That(recoverState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
            Assert.That(recoverState.recoverEndTickExclusive, Is.GreaterThan(2));
            Assert.That(GetSummonedChildren(worldState), Has.Count.EqualTo(1));
            Assert.That(nextTick.EventLog, Has.None.Contains("SummonCommitted|Source=40|Effect=0|SpawnIndex=0"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_WindupDetachedCancelsWithoutSummon()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedReadyUtilityState(worldState);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetBoardPresence(EnemyId, EntityBoardPresence.Detached);
            var canceledTick = pipeline.RunTick(new TickInput(2));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
            Assert.That(state.cooldownTicksRemaining, Is.GreaterThan(0));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(canceledTick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            Assert.That(canceledTick.PresentationData.EnemyUtilitySignals.Single().Phase, Is.EqualTo(EnemyUtilityPresentationPhase.Canceled));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_SummonPlacementUsesSurfaceCellFaceAfterTopologyRotation()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreateJpeter(sourceCell) },
                topology: new CubeTopologyState(FaceId.Floor));
            SeedWindupUtilityState(worldState, windupEndTick: 2);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(GetUtilityEffectState(worldState).windupEndTick, Is.EqualTo(3));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(2));
            var commitTick = pipeline.RunTick(new TickInput(3));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)), commitTick.Trace.Text);
            Assert.That(child.position.face, Is.EqualTo(FaceId.Front));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_MarkedForDeathDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("MarkedForDeath");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_HpZeroDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("HpZero");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_DeadDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("Dead");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_SourceDeathAfterSummonLeavesExistingChildLifecycleDocumented()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);
            worldState.CreateWriteContext().ApplyDamage(EnemyId, 99);
            pipeline.RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetEntity(EnemyId, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(child.entityId, out var existingChild), Is.True);
            Assert.That(existingChild.position, Is.EqualTo(child.position));
            Assert.That(worldState.CreateSnapshot().TryGetSummonedEntityState(child.entityId, out var summonedState), Is.True);
            Assert.That(summonedState.SourceEntityId, Is.EqualTo(EnemyId));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_DestroyTilePlacementBlockerBehaviorIsObserved()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10) },
                initialTileFeatures: new[] { CreateTileFeature(100, forwardCell, TileFeatureKind.Destroy) });
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            Assert.That(worldState.CreateSnapshot().TryGetPlacementBlocker(EntityType.Unit, forwardCell, ignoredEntityId: 0, out _), Is.False);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell), "CurrentContract: Destroy tile features alone do not block Jpeter summon placement.");
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), forwardCell), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_BarricadePlacementBlockerBehaviorIsObserved()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10) },
                initialTileFeatures: new[] { CreateTileFeature(101, forwardCell, TileFeatureKind.Barricade) });
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            Assert.That(worldState.CreateSnapshot().TryGetPlacementBlocker(EntityType.Unit, forwardCell, ignoredEntityId: 0, out _), Is.False);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell), "CurrentContract: Barricade tile features alone do not block Jpeter summon placement.");
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), forwardCell), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_MoonBlockGeneratorPlacementBlockerBehaviorIsObserved()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var rightCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(),
                    CreateBox(50, forwardCell, BoxArchetype.Moon),
                },
                initialTileFeatures: new[] { CreateTileFeature(102, forwardCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 50) });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(rightCell), "CurrentContract: the moon box blocks via Solid occupancy; the generator tile is not itself the Unit placement blocker.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out var solid), Is.True);
            Assert.That(solid.Kind, Is.EqualTo(SolidKind.Box));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_BlockerOnSamePlanarDifferentFaceDoesNotAffectSummonCandidate()
        {
            var floorForward = new SurfaceCell(FaceId.Floor, 1, 0);
            var frontSamePlanar = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                CreateWall(90, frontSamePlanar),
            });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(floorForward));
            Assert.That(child.position, Is.Not.EqualTo(frontSamePlanar));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(frontSamePlanar, out _), Is.True);
        }

        private static void AssertInvalidatedWindupDoesNotSummon(string invalidationKind)
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 2);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var writeContext = worldState.CreateWriteContext();
            switch (invalidationKind)
            {
                case "MarkedForDeath":
                    ((IAttackCommitContext)writeContext).MarkDestroy(EnemyId);
                    break;
                case "HpZero":
                    writeContext.ApplyDamage(EnemyId, 99);
                    break;
                case "Dead":
                    writeContext.ApplyEnemyAiState(EnemyId, EnemyAiMode.Dead, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidationKind), invalidationKind, null);
            }

            var tick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            if (worldState.CreateSnapshot().TryGetEnemyUtilityState(EnemyId, out var utilityState))
            {
                Assert.That(utilityState.EffectStates[0].phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
            }
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            var profile = LoadJpeterProfile();
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyUnitArchetypeCatalog>(CombinedArchetypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing enemy archetype catalog at '{CombinedArchetypeCatalogPath}'.");

            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = profile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = EnemyId,
                        Profile = profile,
                    },
                },
                EnemyUnitArchetypeCatalog = catalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId)
                .CreateTickPipeline(
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                    playerKinematicLocomotionTiming: CreateOneTickKinematicTiming());
        }

        private static EnemyAiProfile LoadJpeterProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ArchetypeSummonerProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing Jpeter ArchetypeSummoner profile at '{ArchetypeSummonerProfilePath}'.");
            return profile;
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static void SeedReadyUtilityState(WorldState worldState)
        {
            worldState.CreateWriteContext().SetEnemyUtilityState(
                EnemyId,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            effectKind = EnemyUtilityEffectKind.SummonMinion,
                            cooldownTicksRemaining = 0,
                        },
                    }));
        }

        private static void SeedWindupUtilityState(WorldState worldState, int windupEndTick)
        {
            worldState.CreateWriteContext().SetEnemyUtilityState(
                EnemyId,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            effectKind = EnemyUtilityEffectKind.SummonMinion,
                            phase = EnemyUtilityEffectPhase.Windup,
                            windupStartTick = 0,
                            windupEndTick = windupEndTick,
                            activationSequence = 1,
                        },
                    }));
        }

        private static EnemyUtilityEffectState GetUtilityEffectState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyUtilityState(EnemyId, out var state), Is.True);
            Assert.That(state.EffectStates, Has.Count.EqualTo(1));
            return state.EffectStates[0];
        }

        private static IReadOnlyList<EntityState> GetSummonedChildren(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var result = new List<EntityState>();
            for (var entityId = 1; entityId <= 200; entityId++)
            {
                if (snapshot.TryGetSummonedEntityState(entityId, out var summonedState) &&
                    summonedState.SourceEntityId == EnemyId &&
                    snapshot.TryGetEntity(entityId, out var entity))
                {
                    result.Add(entity);
                }
            }

            result.Sort((left, right) => left.entityId.CompareTo(right.entityId));
            return result;
        }

        private static EntityState GetSingleSummonedChild(WorldState worldState)
        {
            var children = GetSummonedChildren(worldState);
            Assert.That(children, Has.Count.EqualTo(1));
            return children[0];
        }

        private static int CountUnitsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Count;
        }

        private static WorldState CreateWorldState(
            params EntityState[] initialEntities)
        {
            return CreateWorldState(initialEntities, null, null, null);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState? topology = null,
            GameplayTerrainData terrainData = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static EntityState CreateJpeter()
        {
            return CreateJpeter(new SurfaceCell(FaceId.Floor, 0, 0));
        }

        private static EntityState CreateJpeter(EnemyAiMode aiMode, int aiStateTimer = 0)
        {
            return CreateJpeter(new SurfaceCell(FaceId.Floor, 0, 0), aiMode, aiStateTimer);
        }

        private static EntityState CreateJpeter(
            SurfaceCell position,
            EnemyAiMode aiMode = EnemyAiMode.Patrol,
            int aiStateTimer = 0)
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
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype = boxArchetype,
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
                facing = Direction.None,
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
    }
}
