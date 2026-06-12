using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class SecbotStationaryRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int SecbotId = 165;
        private const string TutorialPassiveContactProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_Common/EnemyAi_TutorialPassiveContact.asset";

        [Test]
        [Category("Extended")]
        public void SecbotProfile_UsesStationaryNoDetectionPassiveContactLane()
        {
            var profile = LoadSecbotProfile();
            var runtime = profile.CreateRuntimeDefinition(GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            Assert.That(runtime.Brain.StateResolver.Kind, Is.EqualTo(EnemyAiStateResolverKind.Default));
            Assert.That(runtime.Brain.StateResolver.Kind, Is.Not.EqualTo(EnemyAiStateResolverKind.Charge));
            Assert.That(runtime.Brain.Patrol.Kind, Is.EqualTo(PatrolStrategyKind.Stationary));
            Assert.That(runtime.Brain.Detection.Kind, Is.EqualTo(DetectionStrategyKind.None));
            Assert.That(runtime.Capabilities.TryGetMovementSkill(out var movementSkill), Is.False, $"Secbot must not compile MovementSkill={movementSkill?.Kind.ToString() ?? "<null>"}.");
            Assert.That(runtime.Capabilities.TryGetCombat(out var combat), Is.False, $"Secbot must not compile Combat={combat?.Kind.ToString() ?? "<null>"}.");
            Assert.That(runtime.Capabilities.TryGetPassiveContact(out var passiveContact), Is.True);
            Assert.That(passiveContact.Kind, Is.EqualTo(AttackDecisionStrategyKind.ContactSameCell));
        }

        [Test]
        [Category("Extended")]
        public void SecbotStationary_DoesNotProduceMovementIntent_InPatrolMode()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateSecbot(source),
            });
            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            AssertNoSecbotMovement(worldState, tick, source);
            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(SecbotId, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SecbotStationary_DoesNotProduceMovementIntent_WhenPlayerIsVisible()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateSecbot(source),
            });
            var pipeline = CreatePipeline(worldState);

            var firstTick = pipeline.RunTick(new TickInput(1));
            var secondTick = pipeline.RunTick(new TickInput(2));

            AssertNoSecbotMovement(worldState, firstTick, source);
            AssertNoSecbotMovement(worldState, secondTick, source);
            Assert.That(GetEntity(worldState, SecbotId).aiMode, Is.EqualTo(EnemyAiMode.Patrol));
            Assert.That(worldState.CreateSnapshot().TryGetEnemyChargeState(SecbotId, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SecbotStationary_DoesNotCreateKinematicMotionTrack()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(new SurfaceCell(FaceId.Floor, 3, 0)),
                CreateSecbot(source),
            });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == SecbotId), Is.False);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == SecbotId), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(SecbotId, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetUnitContinuousLocomotionState(SecbotId, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SecbotStationary_TileFeatureAdjacentCellsDoNotMatterBecauseNoCandidateIsGenerated()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var activeBarricade = CreateTileFeature(100, new SurfaceCell(FaceId.Floor, 1, 0), TileFeatureKind.Barricade);
            var inactiveBarricade = CreateTileFeature(101, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Barricade);
            var destroyTile = CreateTileFeature(102, new SurfaceCell(FaceId.Floor, -1, 0), TileFeatureKind.Destroy);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(new SurfaceCell(FaceId.Floor, 3, 0)),
                    CreateSecbot(source),
                },
                initialTileFeatures: new[] { activeBarricade, inactiveBarricade, destroyTile });
            var definitions = CreateDefinitions(
                (activeBarricade, TileFeatureActivationRule.Always),
                (inactiveBarricade, TileFeatureActivationRule.InactiveFaceOnly),
                (destroyTile, TileFeatureActivationRule.Always));

            var tick = CreatePipeline(worldState, definitions).RunTick(new TickInput(1));

            AssertNoSecbotMovement(worldState, tick, source);
            Assert.That(tick.MovementPhaseResult.RejectedReasons, Is.Empty, "Stationary Secbot should not generate a movement candidate that tile-feature traversal could reject.");
        }

        [Test]
        [Category("Extended")]
        public void SecbotStationary_CurrentCellDestroyTilePolicy_UsesUnitMobilityKind()
        {
            var source = new SurfaceCell(FaceId.Floor, 0, 0);
            var destroyTile = CreateTileFeature(100, source, TileFeatureKind.Destroy);
            var definitions = CreateActiveDefinitions(destroyTile);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateSecbot(source),
                },
                initialTileFeatures: new[] { destroyTile });

            var tick = CreatePipeline(worldState, definitions).RunTick(new TickInput(1));

            var secbot = GetEntity(worldState, SecbotId);
            Assert.That(secbot.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Ground));
            Assert.That(
                TileFeatureHazardQueries.IsDestroyTileLethalForUnit(secbot),
                Is.True,
                "The current Secbot stage binding is Ground; this assertion should change only if the binding changes.");
            Assert.That(secbot.position, Is.EqualTo(source));
            Assert.That(secbot.hp, Is.EqualTo(1));
            Assert.That(secbot.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(secbot.markedForDeath, Is.False);
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == SecbotId), Is.Empty);
            Assert.That(tick.PresentationData.TileEvents.Any(tileEvent =>
                tileEvent.EventKind == TilePresentationEventKind.DestroyTileTriggered &&
                tileEvent.TargetEntityId == SecbotId), Is.False);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var profile = LoadSecbotProfile();
            var timingProfile = GameplayTimingProfile.CreateDefault();

            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static EnemyAiProfile LoadSecbotProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(TutorialPassiveContactProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing Secbot profile at '{TutorialPassiveContactProfilePath}'.");
            return profile;
        }

        private static void AssertNoSecbotMovement(WorldState worldState, TickResult tick, SurfaceCell expectedCell)
        {
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == SecbotId), Is.Empty);
            Assert.That(GetEntity(worldState, SecbotId).position, Is.EqualTo(expectedCell));
            Assert.That(worldState.CreateSnapshot().TryGetUnitKinematicState(SecbotId, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetUnitContinuousLocomotionState(SecbotId, out _), Is.False);
            Assert.That(tick.PresentationData.KinematicMotionTracks.Any(track => track.EntityId == SecbotId), Is.False);
            Assert.That(tick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == SecbotId), Is.False);
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static WorldState CreateWorldState(
            EntityState[] initialEntities,
            GameplayTerrainData terrainData = null,
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)),
                terrainData ?? GameplayTerrainData.Empty,
                topology ?? new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
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
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateSecbot(SurfaceCell position)
        {
            return new EntityState
            {
                entityId = SecbotId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
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
                .Select(tileFeature => CreateDefinition(tileFeature, TileFeatureActivationRule.Always))
                .ToArray();
        }

        private static TileFeatureRuntimeDefinition[] CreateDefinitions(
            params (TileFeatureState TileFeature, TileFeatureActivationRule ActivationRule)[] entries)
        {
            return entries
                .Select(entry => CreateDefinition(entry.TileFeature, entry.ActivationRule))
                .ToArray();
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
            TileFeatureState tileFeature,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileFeature.TileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: tileFeature.Charges,
                presentationKey: string.Empty);
        }
    }
}
