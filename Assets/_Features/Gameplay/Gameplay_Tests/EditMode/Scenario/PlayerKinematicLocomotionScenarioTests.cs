using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerKinematicLocomotionScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOff_UsesLegacyDiscreteMove()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState, GameplayRuntimeFeatureFlags.None);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_AdvancesOneCellOverFourTicks()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var tickOne = worldState.CreateSnapshot();
            AssertPose(tickOne, expectedAnchorX: 0, expectedLocalX: 1024, expectedRemainingTicks: 3);

            pipeline.RunTick(new TickInput(2));
            var tickTwo = worldState.CreateSnapshot();
            AssertPose(tickTwo, expectedAnchorX: 1, expectedLocalX: -2048, expectedRemainingTicks: 2);

            pipeline.RunTick(new TickInput(3));
            var tickThree = worldState.CreateSnapshot();
            AssertPose(tickThree, expectedAnchorX: 1, expectedLocalX: -1024, expectedRemainingTicks: 1);

            pipeline.RunTick(new TickInput(4));
            var tickFour = worldState.CreateSnapshot();
            Assert.That(tickFour.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(tickFour.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_BoxBlocksOrdinaryMove()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Reason=KinematicTraversalBlocked")),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_PushInputWhileMovingIsDropped()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 2, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Reason=UnitKinematicNotSettled")),
                Is.True);
        }

        private static void AssertPose(
            WorldSnapshot snapshot,
            int expectedAnchorX,
            int expectedLocalX,
            int expectedRemainingTicks)
        {
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, expectedAnchorX, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(expectedLocalX));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.velocity.X.RawValue, Is.EqualTo(KinematicFixed.DefaultPlayerUnitsPerTick));
            Assert.That(state.remainingTicks, Is.EqualTo(expectedRemainingTicks));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new PlayerControlStateLogic(10),
                },
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: runtimeFeatureFlags);
        }

        private static WorldState CreateWorldState(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static EntityState CreatePlayer(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxCapabilities = capabilities,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
