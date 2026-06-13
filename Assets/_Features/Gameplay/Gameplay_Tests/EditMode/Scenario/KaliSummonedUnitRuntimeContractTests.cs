using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class KaliSummonedUnitRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int JPeterId = 59;
        private const string PassiveContactMinionArchetypeId = "PassiveContactMinion";
        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_UtilitySummoner/EnemyAi_ArchetypeSummoner.asset";
        private const string CombinedArchetypeCatalogPath =
            StageContentPaths.SharedEnemyAiRoot + "/Catalogs/EnemyUnitArchetypeCatalog_CampaignMainEnemy.asset";

        [Test]
        [Category("Extended")]
        public void KaliSummonedUnit_ArchetypeBinding_UsesExpectedSummonedDefaults()
        {
            var worldState = CreateWorldState(CreateJpeter());
            var commitTick = CommitKaliSummon(worldState);
            var child = GetSingleSummonedChild(worldState);
            var snapshot = worldState.CreateSnapshot();
            var catalogEntry = LoadPassiveContactMinionArchetype();

            Assert.That(snapshot.TryGetEnemyDefinitionBindingState(child.entityId, out var binding), Is.True);
            Assert.That(binding.ArchetypeId, Is.EqualTo(new EnemyUnitArchetypeId(PassiveContactMinionArchetypeId)));
            Assert.That(AssetDatabase.GetAssetPath(catalogEntry.AiProfile), Does.EndWith("/EnemyAi_PassiveContactMinion.asset"));
            Assert.That(catalogEntry.SpawnDefaults.Hp, Is.EqualTo(2), "The archetype default remains 2 HP; JPeter's summon effect overrides actual spawned HP to 1.");
            Assert.That(catalogEntry.SpawnDefaults.InitialAiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(catalogEntry.SpawnDefaults.UnitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            Assert.That(child.hp, Is.EqualTo(1));
            Assert.That(child.maxHp, Is.EqualTo(1));
            Assert.That(child.aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(child.unitRole, Is.EqualTo(UnitRole.Enemy));
            Assert.That(child.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            Assert.That(child.teamId, Is.EqualTo(GetEntity(worldState, JPeterId).teamId));
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
            Assert.That(commitTick.EventLog, Has.Some.Contains("SummonCommitted|Source=59").And.Contains("Archetype=PassiveContactMinion"));
        }

        [Test]
        [Category("Extended")]
        public void KaliSummonedUnit_AppliesPassiveContact_OnSameSurfaceCell()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateJpeter(),
            });
            var pipeline = CreatePipeline(worldState);
            CommitKaliSummon(pipeline, worldState);
            var child = GetSingleSummonedChild(worldState);
            worldState.CreateWriteContext().MoveEntity(PlayerId, child.position);

            var tick = pipeline.RunTick(new TickInput(2));

            var damage = tick.AttackPhaseResult.DamageResolutions.Single(record =>
                record.SourceId == child.entityId &&
                record.TargetId == PlayerId);
            Assert.That(damage.Accepted, Is.True);
            Assert.That(damage.SourceKind, Is.EqualTo(AttackSourceKind.PassiveContact));
            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
            AssertActionInactiveOrMissing(worldState, child.entityId);
        }

        [Test]
        [Category("Extended")]
        public void KaliSummonedUnit_PassiveContactDoesNotRequireEnemyActionState()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateJpeter(),
            });
            var pipeline = CreatePipeline(worldState);
            CommitKaliSummon(pipeline, worldState);
            var child = GetSingleSummonedChild(worldState);
            worldState.CreateWriteContext().MoveEntity(PlayerId, child.position);

            var tick = pipeline.RunTick(new TickInput(2));

            AssertActionInactiveOrMissing(worldState, child.entityId);
            Assert.That(tick.AttackPhaseResult.DamageResolutions.Any(record =>
                    record.SourceId == child.entityId &&
                    record.TargetId == PlayerId &&
                    record.Accepted &&
                    record.SourceKind == AttackSourceKind.PassiveContact),
                Is.True);
            Assert.That(GetEntity(worldState, child.entityId).aiMode, Is.Not.EqualTo(EnemyAiMode.Attack));
        }

        [Test]
        [Category("Extended")]
        public void KaliSummonedUnit_DoesNotApplyPassiveContactAcrossFaces_WithSamePlanarCell()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Front, 4, 0)),
                CreateJpeter(),
            });
            var pipeline = CreatePipeline(worldState);
            CommitKaliSummon(pipeline, worldState);
            var child = GetSingleSummonedChild(worldState);
            var samePlanarOtherFace = new SurfaceCell(FaceId.Front, child.position.x, child.position.y);
            worldState.CreateWriteContext().MoveEntity(PlayerId, samePlanarOtherFace);

            Assert.That(child.position.PlanarPosition, Is.EqualTo(GetEntity(worldState, PlayerId).position.PlanarPosition));
            Assert.That(child.position.face, Is.Not.EqualTo(GetEntity(worldState, PlayerId).position.face));

            var tick = pipeline.RunTick(new TickInput(2));

            Assert.That(tick.AttackPhaseResult.DamageResolutions.Any(record =>
                record.SourceId == child.entityId &&
                record.TargetId == PlayerId &&
                record.SourceKind == AttackSourceKind.PassiveContact), Is.False);
            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void KaliSummonedUnit_DestroyTilePolicy_FollowsSummonedUnitMobility()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyTile = CreateTileFeature(100, forwardCell, TileFeatureKind.Destroy);
            var definitions = CreateActiveDefinitions(destroyTile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 0, -1)),
                    CreateWall(91, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(92, new SurfaceCell(FaceId.Floor, -1, 0)),
                },
                initialTileFeatures: new[] { destroyTile });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            Assert.That(worldState.CreateSnapshot().TryGetPlacementBlocker(EntityType.Unit, forwardCell, ignoredEntityId: 0, out _), Is.False);

            var tick = CreatePipeline(worldState, definitions).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(child.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            Assert.That(
                TileFeatureHazardQueries.EvaluateTileApproachRisk(worldState.CreateSnapshot(), definitions, child, forwardCell),
                Is.EqualTo(TileApproachRisk.Neutral));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(child.entityId, out var remainingChild), Is.True);
            Assert.That(remainingChild.hp, Is.EqualTo(1));
            Assert.That(remainingChild.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(remainingChild.markedForDeath, Is.False);
            Assert.That(tick.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.DestroyTileTriggered &&
                tileEvent.TargetEntityId == child.entityId), Is.False);
        }

        private static TickResult CommitKaliSummon(WorldState worldState)
        {
            return CommitKaliSummon(CreatePipeline(worldState), worldState);
        }

        private static TickResult CommitKaliSummon(TickPipeline pipeline, WorldState worldState)
        {
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            var tick = pipeline.RunTick(new TickInput(1));
            Assert.That(GetSummonedChildren(worldState), Has.Count.EqualTo(1));
            return tick;
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var profile = LoadJpeterProfile();
            var catalog = LoadArchetypeCatalog();
            var timingProfile = GameplayTimingProfile.CreateDefault();

            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = timingProfile.SimulationTicksPerSecond,
                DefaultEnemyAiProfile = profile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = JPeterId,
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
                    timingProfile,
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        timingProfile.SimulationTicksPerSecond,
                        timingProfile.RepeatedMoveIntervalSeconds),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                    playerKinematicLocomotionTiming: CreateOneTickKinematicTiming(),
                    tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static EnemyAiProfile LoadJpeterProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ArchetypeSummonerProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing JPeter ArchetypeSummoner profile at '{ArchetypeSummonerProfilePath}'.");
            return profile;
        }

        private static EnemyUnitArchetypeCatalog LoadArchetypeCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyUnitArchetypeCatalog>(CombinedArchetypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing enemy archetype catalog at '{CombinedArchetypeCatalogPath}'.");
            return catalog;
        }

        private static EnemyUnitArchetypeAsset LoadPassiveContactMinionArchetype()
        {
            var entry = LoadArchetypeCatalog().Entries.Single(candidate =>
                candidate.ArchetypeId.Equals(new EnemyUnitArchetypeId(PassiveContactMinionArchetypeId)));
            Assert.That(entry.AiProfile, Is.Not.Null);
            return entry;
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static void SeedWindupUtilityState(WorldState worldState, int windupEndTick)
        {
            worldState.CreateWriteContext().SetEnemyUtilityState(
                JPeterId,
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

        private static IReadOnlyList<EntityState> GetSummonedChildren(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var result = new List<EntityState>();
            for (var entityId = 1; entityId <= 200; entityId++)
            {
                if (snapshot.TryGetSummonedEntityState(entityId, out var summonedState) &&
                    summonedState.SourceEntityId == JPeterId &&
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

        private static void AssertSummonedMetadata(WorldState worldState, int childEntityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetSummonedEntityState(childEntityId, out var summonedState), Is.True);
            Assert.That(summonedState.SourceEntityId, Is.EqualTo(JPeterId));
            Assert.That(summonedState.SourceEffectIndex, Is.EqualTo(0));
        }

        private static void AssertNoGhostSummonState(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var entries = new List<SummonedEntitySnapshotEntry>();
            snapshot.EnumerateSummonedEntityStatesOrdered(entries);
            foreach (var entry in entries)
            {
                Assert.That(snapshot.TryGetEntity(entry.EntityId, out _), Is.True, $"Summoned metadata for {entry.EntityId} must not outlive its entity.");
            }
        }

        private static void AssertActionInactiveOrMissing(WorldState worldState, int entityId)
        {
            if (!worldState.CreateSnapshot().TryGetEnemyActionState(entityId, out var action))
            {
                return;
            }

            Assert.That(action.IsActive, Is.False, "PassiveContact must not require an active EnemyActionRuntimeState.");
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static WorldState CreateWorldState(params EntityState[] initialEntities)
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
                entityId = JPeterId,
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

        private static EntityState CreatePlayer(SurfaceCell position)
        {
            return new EntityState
            {
                entityId = PlayerId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
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

        private static TileFeatureState CreateTileFeature(int tileId, SurfaceCell cell, TileFeatureKind kind)
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
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition[] CreateActiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return tileFeatures
                .Select(tileFeature => new TileFeatureRuntimeDefinition(
                    tileFeature.TileId,
                    TileFeatureActivationRule.Always,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: tileFeature.Charges,
                    presentationKey: string.Empty))
                .ToArray();
        }
    }
}
