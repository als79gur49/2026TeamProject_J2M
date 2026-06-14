using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class BarricadeActiveSolidInvariantCoreTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void ActiveBarricade_BoxOnlyStableOccupancy_IsCrushedAndLeavesNoSolidOccupancy()
        {
            var barricadeCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreateBox(20, barricadeCell) },
                new[] { CreateTileFeature(100, barricadeCell, TileFeatureKind.Barricade) });
            var pipeline = CreatePipeline(
                worldState,
                new[] { CreateDefinition(100, TileFeatureActivationRule.FrontFaceOnly, selector: TileFeatureBoxSelector.None) });

            var result = pipeline.RunTick(new TickInput(7));
            var finalSnapshot = worldState.CreateSnapshot();

            var crushedEvent = result.PresentationData.TileEvents.Single(
                tileEvent => tileEvent.EventKind == TilePresentationEventKind.BarricadeCrushed);
            Assert.That(crushedEvent.TileId, Is.EqualTo(100));
            Assert.That(crushedEvent.Cell, Is.EqualTo(barricadeCell));
            Assert.That(crushedEvent.TargetEntityId, Is.EqualTo(20));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(finalSnapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                playerRespawnDelayTicks: 1,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TileFeatureState> initialTileFeatures)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                TestBounds,
                new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
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

        private static TileFeatureRuntimeDefinition CreateDefinition(
            int tileId,
            TileFeatureActivationRule activationRule,
            Direction2D direction = Direction2D.None,
            TileFeatureBoxSelector selector = TileFeatureBoxSelector.AnyPushableBox)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                direction,
                selector,
                boundEntityId: 0,
                presentationKey: string.Empty);
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
                boxCapabilities = BoxCapabilities.Push,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
