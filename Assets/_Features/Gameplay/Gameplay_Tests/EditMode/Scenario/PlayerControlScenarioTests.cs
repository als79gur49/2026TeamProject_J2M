using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.PlayerControl;
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
            var thirdTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(firstTick.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(secondTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(secondTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(thirdTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Command: MovementCommandKind.Push),
                },
                thirdTick.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId, intent.CommandKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(2,0)|Facing=Right",
                },
                thirdTick.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.moveCooldownTicks, Is.Zero);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
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
            var fifthTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(releaseTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fifthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
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
            var fifthTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(firstTick.MovementPhaseResult.SortedIntents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=(-1,0)",
                },
                directionChangeTick.MovementPhaseResult.RejectedReasons);
            Assert.That(thirdTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fourthTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(fifthTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
        }

        [Test]
        public void PlayerControl_FlipStartsActionAndRetainsPriorityOverPush()
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
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(result.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
        }

        [Test]
        public void PlayerControl_PushAction_ExecutesAfterWindupInsteadOfThresholdTick()
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

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(box.stateTimer, Is.EqualTo(10));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        public void PlayerControl_FlipAction_ExecutesAfterWindupInsteadOfSameTick()
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

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            Assert.That(startTick.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(executeTick.MovementPhaseResult.SelectedGroups.Single().GroupKind, Is.EqualTo(ActionGroupKind.Flip));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.False);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(snapshotAfter.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.executionAttempted, Is.True);
        }

        [Test]
        public void PlayerControl_CustomPushInputLock_IgnoresNewInputsUntilActionCompletes()
        {
            var timingProfile = CreateTimingProfile(
                playerPushContactThresholdTicks: 1);
            var actionTiming = CreateActionTiming(
                playerPushExecuteDelayTicks: 2,
                playerPushInputLockDurationTicks: 4);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Right),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                CreateWall(entityId: 90, position: new Vector2Int(4, 0)),
                CreateWall(entityId: 91, position: new Vector2Int(-4, 0)),
            }, timingProfile);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(
                        10,
                        timingProfile.PlayerPushContactThresholdTicks,
                        actionTiming.PushWindupTicks,
                        actionTiming.PushRecoveryTicks,
                        actionTiming.FlipWindupTicks,
                        actionTiming.FlipRecoveryTicks),
                },
                timingProfile);

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var lockedWindupTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Flip(Direction.Left)));
            var executeTick = pipeline.RunTick(new TickInput(3, PlayerTickCommand.Move(Direction.Left)));
            var lockedRecoveryTick = pipeline.RunTick(new TickInput(4, PlayerTickCommand.Move(Direction.Left)));
            var lastLockedTick = pipeline.RunTick(new TickInput(5, PlayerTickCommand.Flip(Direction.Left)));
            var postLockTick = pipeline.RunTick(new TickInput(6, PlayerTickCommand.Move(Direction.Left)));

            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(startTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(lockedWindupTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lockedWindupTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(lockedWindupTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.False);
            Assert.That(executeTick.MovementPhaseResult.SortedIntents.Single().CommandKind, Is.EqualTo(MovementCommandKind.Push));
            Assert.That(executeTick.PresentationData.PlayerActionSignals.Single().ExecutedThisTick, Is.True);
            Assert.That(lockedRecoveryTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lockedRecoveryTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(lastLockedTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(lastLockedTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(postLockTick.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(postLockTick.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            Assert.That(postLockTick.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));
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

        private static (int PushWindupTicks, int PushRecoveryTicks, int FlipWindupTicks, int FlipRecoveryTicks) CreateActionTiming(
            int playerPushExecuteDelayTicks = 1,
            int playerPushInputLockDurationTicks = 1,
            int playerFlipExecuteDelayTicks = 1,
            int playerFlipInputLockDurationTicks = 1)
        {
            if (playerPushInputLockDurationTicks < playerPushExecuteDelayTicks)
            {
                throw new System.ArgumentOutOfRangeException(nameof(playerPushInputLockDurationTicks));
            }

            if (playerFlipInputLockDurationTicks < playerFlipExecuteDelayTicks)
            {
                throw new System.ArgumentOutOfRangeException(nameof(playerFlipInputLockDurationTicks));
            }

            return (
                playerPushExecuteDelayTicks,
                playerPushInputLockDurationTicks - playerPushExecuteDelayTicks,
                playerFlipExecuteDelayTicks,
                playerFlipInputLockDurationTicks - playerFlipExecuteDelayTicks);
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
