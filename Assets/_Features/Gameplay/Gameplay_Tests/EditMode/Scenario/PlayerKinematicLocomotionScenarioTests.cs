using System.Linq;
using Game.Feature.Gameplay.Attack.Collection;
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
        public void PlayerSameFaceContinuousLocomotion_FlagOn_DefaultDurationAdvancesOneCellOverTwentyTicks()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var tickOne = worldState.CreateSnapshot();
            AssertPose(tickOne, expectedAnchorX: 0, expectedLocalX: 205, expectedRemainingTicks: 19, expectedElapsedTicks: 1, expectedTotalTicks: 20);

            for (var tick = 2; tick <= 9; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
            }

            var tickNine = worldState.CreateSnapshot();
            AssertPose(tickNine, expectedAnchorX: 0, expectedLocalX: 1843, expectedRemainingTicks: 11, expectedElapsedTicks: 9, expectedTotalTicks: 20);

            pipeline.RunTick(new TickInput(10));
            var tickTen = worldState.CreateSnapshot();
            AssertPose(tickTen, expectedAnchorX: 1, expectedLocalX: -2048, expectedRemainingTicks: 10, expectedElapsedTicks: 10, expectedTotalTicks: 20);

            for (var tick = 11; tick <= 19; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
            }

            var tickNineteen = worldState.CreateSnapshot();
            AssertPose(tickNineteen, expectedAnchorX: 1, expectedLocalX: -205, expectedRemainingTicks: 1, expectedElapsedTicks: 19, expectedTotalTicks: 20);

            pipeline.RunTick(new TickInput(20));
            var tickTwenty = worldState.CreateSnapshot();
            Assert.That(tickTwenty.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(tickTwenty.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_FourTickCompatibilityDurationReproducesLegacyCadence()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                kinematicMoveDurationSeconds: 4f / GameplayTimingProfile.DefaultSimulationTicksPerSecond);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 0, expectedLocalX: 1024, expectedRemainingTicks: 3, expectedElapsedTicks: 1, expectedTotalTicks: 4);

            pipeline.RunTick(new TickInput(2));
            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 1, expectedLocalX: -2048, expectedRemainingTicks: 2, expectedElapsedTicks: 2, expectedTotalTicks: 4);

            pipeline.RunTick(new TickInput(3));
            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 1, expectedLocalX: -1024, expectedRemainingTicks: 1, expectedElapsedTicks: 3, expectedTotalTicks: 4);

            pipeline.RunTick(new TickInput(4));
            var finalSnapshot = worldState.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_PointThirtyFiveDurationUsesTwentyTwoTicksAtSixtyTps()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                kinematicMoveDurationSeconds: 0.35f);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 0, expectedLocalX: 186, expectedRemainingTicks: 21, expectedElapsedTicks: 1, expectedTotalTicks: 22);

            for (var tick = 2; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
            }

            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 0, expectedLocalX: 1862, expectedRemainingTicks: 12, expectedElapsedTicks: 10, expectedTotalTicks: 22);

            pipeline.RunTick(new TickInput(11));
            AssertPose(worldState.CreateSnapshot(), expectedAnchorX: 1, expectedLocalX: -2048, expectedRemainingTicks: 11, expectedElapsedTicks: 11, expectedTotalTicks: 22);

            for (var tick = 12; tick <= 22; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
            }

            var finalSnapshot = worldState.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
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

        [TestCase(5, 0, 1024)]
        [TestCase(10, 1, -2048)]
        [TestCase(15, 1, -1024)]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_NonlethalHitInterruptsAndNextTickClears(
            int hitTick,
            int expectedAnchorX,
            int expectedLocalX)
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, expectedAnchorX, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                new TickScriptedAttackLogic(40, 10, hitTick));

            TickResult hitResult = null;
            for (var tick = 1; tick <= hitTick; tick++)
            {
                hitResult = pipeline.RunTick(
                    tick == 1
                        ? new TickInput(tick, PlayerTickCommand.Move(Direction.Right))
                        : new TickInput(tick));
            }

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, expectedAnchorX, 0)));
            Assert.That(hitSnapshot.TryGetUnitKinematicState(10, out var interruptedState), Is.True);
            Assert.That(interruptedState.mode, Is.EqualTo(MotionMode.Interrupted));
            Assert.That(interruptedState.localOffset.X.RawValue, Is.EqualTo(expectedLocalX));
            Assert.That(interruptedState.velocity.IsZero, Is.True);
            Assert.That(hitResult.AttackPhaseResult.MotionInterruptRecords.Count, Is.EqualTo(1));
            Assert.That(hitResult.AttackPhaseResult.MotionInterruptRecords[0].EntityId, Is.EqualTo(10));
            Assert.That(
                hitResult.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.EntityType == EntityType.Unit &&
                    track.SourceTopology.HasValue &&
                    track.DestinationTopology.HasValue &&
                    track.SourceFacing == Direction.Right &&
                    track.DestinationFacing == Direction.Right &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Interrupted),
                Is.True);

            pipeline.RunTick(new TickInput(hitTick + 1));
            var nextSnapshot = worldState.CreateSnapshot();
            Assert.That(nextSnapshot.TryGetEntity(10, out var settledPlayer), Is.True);
            Assert.That(settledPlayer.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, expectedAnchorX, 0)));
            Assert.That(nextSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [TestCase(5, 0, 1024)]
        [TestCase(10, 1, -2048)]
        [TestCase(15, 1, -1024)]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_LethalHitRemovesAndPurgesKinematicState(
            int hitTick,
            int expectedAnchorX,
            int expectedLocalX)
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, hp: 1),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, expectedAnchorX, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled,
                new TickScriptedAttackLogic(40, 10, hitTick));

            TickResult hitResult = null;
            for (var tick = 1; tick <= hitTick; tick++)
            {
                hitResult = pipeline.RunTick(
                    tick == 1
                        ? new TickInput(tick, PlayerTickCommand.Move(Direction.Right))
                        : new TickInput(tick));
            }

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(hitSnapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(
                hitResult.EventLog.Any(entry => entry.Contains("CleanupRemoved|E=10")),
                Is.True);
            Assert.That(
                hitResult.EventLog.Any(entry =>
                    entry.Contains("PlayerRespawnDelayStarted|E=10") &&
                    entry.Contains("DelayTicks=1")),
                Is.True);
            Assert.That(
                hitResult.EventLog.Any(entry => entry.Contains("RespawnCommitted|E=10")),
                Is.False);
            Assert.That(
                hitResult.EventLog.Any(entry =>
                    entry.Contains("KinematicPoseRemoved|E=10") &&
                    entry.Contains($"Offset=({expectedLocalX},0)")),
                Is.True);
            Assert.That(
                hitResult.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.EntityType == EntityType.Unit &&
                    track.SourceTopology.HasValue &&
                    track.DestinationTopology.HasValue &&
                    track.SourceFacing == Direction.Right &&
                    track.DestinationFacing == Direction.Right &&
                    track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, expectedAnchorX, 0) &&
                    track.DestinationLocalOffset.X.RawValue == expectedLocalX &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Removed),
                Is.True);
            Assert.That(
                hitResult.PresentationData.PlayerDeathHoldSignals.Any(signal =>
                    signal.EntityId == 10 &&
                    signal.StartedThisTick &&
                    signal.RemainingTicks == 1),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerSameFaceContinuousLocomotion_FlagOn_MarkedForDeathWhileMoving_RemovesAndPurgesKinematicState()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(
                worldState,
            GameplayRuntimeFeatureFlags.PlayerSameFaceContinuousLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            ((IAttackCommitContext)worldState.CreateWriteContext()).MarkDestroy(10);
            var result = pipeline.RunTick(new TickInput(2));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(
                result.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.EntityType == EntityType.Unit &&
                    track.SourceTopology.HasValue &&
                    track.DestinationTopology.HasValue &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Removed),
                Is.True);
            Assert.That(
                result.PresentationData.PlayerDeathHoldSignals.Any(signal =>
                    signal.EntityId == 10 &&
                    signal.StartedThisTick),
                Is.True);
        }

        private static void AssertPose(
            WorldSnapshot snapshot,
            int expectedAnchorX,
            int expectedLocalX,
            int expectedRemainingTicks,
            int expectedElapsedTicks,
            int expectedTotalTicks)
        {
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, expectedAnchorX, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(MotionMode.Voluntary));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(expectedLocalX));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.remainingTicks, Is.EqualTo(expectedRemainingTicks));
            Assert.That(state.elapsedTicks, Is.EqualTo(expectedElapsedTicks));
            Assert.That(state.totalTicks, Is.EqualTo(expectedTotalTicks));
            Assert.That(state.commitTick, Is.EqualTo(expectedTotalTicks / 2));
            Assert.That(state.stepDirectionX, Is.EqualTo(1));
            Assert.That(state.stepDirectionY, Is.EqualTo(0));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            params IEntityLogic[] extraLogics)
        {
            return CreatePipeline(
                worldState,
                runtimeFeatureFlags,
                kinematicMoveDurationSeconds: null,
                extraLogics);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            GameplayRuntimeFeatureFlags runtimeFeatureFlags,
            float? kinematicMoveDurationSeconds,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            var playerTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
            var kinematicTimingSettings = PlayerKinematicLocomotionTimingSettings.CreateDefault();
            if (kinematicMoveDurationSeconds.HasValue)
            {
                kinematicTimingSettings.KinematicMoveDurationSeconds = kinematicMoveDurationSeconds.Value;
            }

            var playerKinematicTiming = kinematicTimingSettings.CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond);
            var entityLogics = new IEntityLogic[]
            {
                new PlayerLogic(10),
                new PlayerControlStateLogic(10),
            }.Concat(extraLogics ?? Enumerable.Empty<IEntityLogic>()).ToArray();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogics,
                timingProfile,
                playerTiming,
                runtimeFeatureFlags: runtimeFeatureFlags,
                playerKinematicLocomotionTiming: playerKinematicTiming);
        }

        private static WorldState CreateWorldState(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static EntityState CreatePlayer(int entityId, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, 0, 0),
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int teamId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                facing = Direction.Left,
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

        private sealed class TickScriptedAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _attackTick;
            private readonly int _sourceId;
            private readonly int _targetId;

            public TickScriptedAttackLogic(int sourceId, int targetId, int attackTick)
            {
                _sourceId = sourceId;
                _targetId = targetId;
                _attackTick = attackTick;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                System.Collections.Generic.List<RawAttackIntent> buffer)
            {
                if (input.TickIndex == _attackTick)
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 50, _targetId));
                }
            }
        }
    }
}
