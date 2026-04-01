using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerControlScenarioTests
    {
        [Test]
        public void PlayerControl_MoveCooldown_CannotBeBypassedByTapSpam()
        {
            var timingProfile = CreateTimingProfile(playerMoveCooldownTicks: 3);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                },
                timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                },
                timingProfile);

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(2,0)|Facing=Right" },
                fourthTick.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.EqualTo(3));
            Assert.That(controlState.interactionLockTicks, Is.Zero);
        }

        [Test]
        public void PlayerControl_HoldAgainstSameBox_TriggersPushAtThreshold()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var secondTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstTick.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Command: MovementCommandKind.Push),
                },
                secondTick.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId, intent.CommandKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(2,0)|Facing=Right",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.Zero);
            Assert.That(controlState.interactionLockTicks, Is.EqualTo(12));
        }

        [Test]
        public void PlayerControl_InputRelease_ResetsPushContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var releaseTick = pipeline.RunTick(new TickInput(2));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(releaseTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        public void PlayerControl_DirectionChange_ResetsPushContact()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(-1, 0)),
                CreateWall(entityId: 91, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var directionChangeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Left)));
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var fourthTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=(-1,0)",
                },
                directionChangeTick.MovementPhaseResult.RejectedReasons);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        public void PlayerControl_FlipRetainsPriorityOverPush()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Flip(Direction.Right)));

            Assert.That(result.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Flip));
            Assert.That(result.MovementPhaseResult.SelectedGroups.Single().GroupKind, Is.EqualTo(ActionGroupKind.Flip));
        }

        [Test]
        public void PlayerControl_PushInteractionLock_BlocksPlayerButWorldStateStillAdvances()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10, pushContactThresholdTicks: 1),
                });

            var pushTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var lockedTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(pushTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(lockedTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(box.stateTimer, Is.EqualTo(10));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.interactionLockTicks, Is.EqualTo(11));
        }

        [Test]
        public void PlayerControl_FlipInteractionLock_BlocksFollowUpMovementUntilExpiry()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var flipTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var lockedTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            for (var tick = 3; tick <= 12; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Up)));
            }

            var unlockTick = pipeline.RunTick(new TickInput(13, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(flipTick.MovementPhaseResult.SelectedGroups.Single().GroupKind, Is.EqualTo(ActionGroupKind.Flip));
            Assert.That(lockedTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(unlockTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Move));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.interactionLockTicks, Is.EqualTo(0));
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            GameplayTimingProfile timingProfile)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, timingProfile);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            return worldState.CreateSnapshot();
        }

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            int playerMoveCooldownTicks = 2,
            int playerPushContactThresholdTicks = GameplayTimingProfile.DefaultPlayerPushContactThresholdTicks)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.4f,
                boxSlideStepIntervalSeconds: 0.2f,
                projectileStepIntervalSeconds: 0.2f,
                pushMotionDurationSeconds: 0.2f,
                topologyMotionDurationSeconds: 0.2f,
                flipMotionDurationSeconds: 0.2f,
                flipArcHeightInCells: 0.65f,
                maxTicksPerFrame: 8,
                playerMoveCooldownSeconds: playerMoveCooldownTicks / (float)simulationTicksPerSecond,
                playerPushContactThresholdSeconds: playerPushContactThresholdTicks / (float)simulationTicksPerSecond);
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position, Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateWall(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }
    }
}
