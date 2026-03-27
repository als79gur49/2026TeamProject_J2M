using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Groups;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class MovementPhaseScenarioTests
    {
        [Test]
        public void Movement_EmptyCellMove_Succeeds()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));

            var result = pipeline.RunTick(new TickInput(1));

            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

            CollectionAssert.AreEqual(
                new[] { (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0)) },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Move, Destination: new Vector2Int(1, 0)) },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, group.GroupKind, group.Moves.Single().Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 1 },
                result.MovementPhaseResult.SelectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_ScriptedMoveIntoUnit_FailsWithoutPushing()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_MoveIntoPushableBox_FailsWithoutExplicitInteract()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_MoveIntoUnit_FailsWithoutPushing()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_InteractPushableBox_StopsBeforeEntityBlocker_AndEntityTypeNoneWallRemainsValid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable, facing: Direction.Left),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Interact),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            Assert.That(slideGroup.GroupId, Is.EqualTo(1));
            Assert.That(slideGroup.SourceId, Is.EqualTo(10));
            Assert.That(slideGroup.GroupKind, Is.EqualTo(ActionGroupKind.BoxSlide));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0), Facing: Direction.Right),
                    (EntityId: 30, Source: new Vector2Int(2, 0), Destination: new Vector2Int(3, 0), Facing: Direction.Right),
                },
                slideGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination, move.Facing))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 1 },
                result.MovementPhaseResult.SelectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                    "MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(GetEntityFacing(worldState, 10), Is.EqualTo(Direction.Up));
            Assert.That(GetEntityFacing(worldState, 30), Is.EqualTo(Direction.Right));
            Assert.That(result.Trace.Text, Does.Contain("Kind=BoxSlide"));
        }

        [Test]
        public void Movement_InteractPushableBox_StopsBeforeTerrainBlocker()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                },
                BoardBounds.Unbounded,
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0)),
                    (EntityId: 30, Source: new Vector2Int(2, 0), Destination: new Vector2Int(3, 0)),
                },
                slideGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination))
                    .ToArray());
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(result.Trace.Text, Does.Contain("S0.Terrain"));
        }

        [Test]
        public void Movement_InteractPushableBox_StopsBeforeBoardEdge()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0)),
                    (EntityId: 30, Source: new Vector2Int(2, 0), Destination: new Vector2Int(3, 0)),
                },
                slideGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination))
                    .ToArray());
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(result.Trace.Text, Does.Contain("S0.BoardBounds"));
        }

        [Test]
        public void Movement_InteractPushableBox_IgnoresProjectileAsSlideStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                    CreateProjectile(entityId: 40, position: new Vector2Int(2, 0), hp: 1),
                },
                BoardBounds.Unbounded,
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(finalSnapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(40));
        }

        [Test]
        public void Movement_InteractPushableBox_FailsWhenUnboundedBoardHasNoStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                },
                BoardBounds.Unbounded,
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=SlideRayHasNoStopper|Cell=(1,0)|Direction=Right",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_InteractPushableBox_FailsWhenEntityStopperIsAdjacent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                CreateBox(entityId: 30, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Pushable),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=SlideStopperAdjacent|Target=20|StopperKind=Entity|Stopper=30|StopperType=Box|Cell=(2,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void Movement_InteractPushableBox_FailsWhenTerrainStopperIsAdjacent()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable),
                },
                BoardBounds.Unbounded,
                new GameplayTerrainData(new[] { new Vector2Int(2, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=SlideStopperAdjacent|Target=20|StopperKind=Terrain|Cell=(2,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_InteractLootOnInteractDestroyBox_DoesNotMoveDuringMovementPhase()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.LootOnInteractDestroy),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            Assert.That(result.MovementPhaseResult.SortedIntents.Count, Is.EqualTo(1));
            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        public void Movement_InteractPushableBox_FailsWhenBoxLacksCapability()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.None),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Interact(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=InteractTargetNotPushableBox|Cell=(1,0)|Target=20|Capabilities=None",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_Throw_SucceedsWhenOppositeCellIsFree()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Throwable, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Throw(Direction.Left)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(-1, 0), Command: MovementCommandKind.Throw),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            var throwGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            Assert.That(throwGroup.GroupId, Is.EqualTo(1));
            Assert.That(throwGroup.SourceId, Is.EqualTo(10));
            Assert.That(throwGroup.GroupKind, Is.EqualTo(ActionGroupKind.Throw));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(-1, 0), Destination: new Vector2Int(1, 0), Facing: Direction.Right),
                },
                throwGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination, move.Facing))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "FacingCommitted|G=1|I=1|E=10|Facing=Left",
                    "MoveCommitted|G=1|I=1|E=30|To=(1,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityFacing(worldState, 10), Is.EqualTo(Direction.Left));
            Assert.That(GetEntityFacing(worldState, 30), Is.EqualTo(Direction.Right));
            Assert.That(result.Trace.Text, Does.Contain("Kind=Throw"));
            Assert.That(result.Trace.Text, Does.Contain("Moves=[E=30:(-1,0)->(1,0):Right]"));
        }

        [Test]
        public void Movement_Throw_FailsWhenTargetIsNotThrowableBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Pushable),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Throw(Direction.Left)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=ThrowTargetNotThrowableBox|Cell=(-1,0)|Target=20|Type=Box|Capabilities=Pushable",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Movement_Throw_FailsWhenLandingCellIsBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Throwable),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Throw(Direction.Left)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=ThrowLandingBlocked|StopperKind=Entity|Stopper=20|StopperType=Unit|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_PlayerInput_ThrowBeatsMoveWhenBothArePresentInSameTickPayload()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Pushable | BoxCapabilities.Throwable),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(
                new TickInput(
                    1,
                    PlayerTickCommand.Create(
                        Direction.Right,
                        interactPressed: false,
                        throwPressed: true)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Throw) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Movement_Throw_RejectsLaterCandidateThatMovesSameBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Throwable),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(1, 0), MovementCommandKind.Throw)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Throw(Direction.Right)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Throw) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=SharedMovedEntity|Entity=30",
                },
                result.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    "FacingCommitted|G=1|I=1|E=10|Facing=Right",
                    "MoveCommitted|G=1|I=1|E=30|To=(-1,0)|Facing=Left",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void Movement_SameDestination_OnlyHigherPriorityWins()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 10, new Vector2Int(1, 0))),
                });

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));

            var result = pipeline.RunTick(new TickInput(1));

            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, IntentId: 1),
                    (SourceId: 10, IntentId: 2),
                },
                result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, SourceId: 20, Priority: 10, Destination: new Vector2Int(1, 0)),
                    (GroupId: 2, SourceId: 10, Priority: 5, Destination: new Vector2Int(1, 0)),
                },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, group.Priority, group.Moves.Single().Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 20) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId)).ToArray());
            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=20|To=(1,0)|Facing=Left" },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=10|Reason=DestinationReserved|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0),20@(2,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(0,0),20@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_EdgeReservation_DoesNotPersistAcrossTicks()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new ScriptedMovementLogic(
                        new Dictionary<int, RawMovementIntent>
                        {
                            { 1, new RawMovementIntent(10, 5, new Vector2Int(1, 0)) },
                            { 2, new RawMovementIntent(10, 5, new Vector2Int(0, 0)) },
                        }),
                });

            var firstTick = pipeline.RunTick(new TickInput(1));
            var secondTick = pipeline.RunTick(new TickInput(2));

            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                firstTick.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(0,0)|Facing=Left" },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(firstTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(secondTick.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(secondTick.Trace.Text, Does.Not.Contain("Reason=EdgeReserved"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
        }

        [Test]
        public void Movement_SameInput_AssignsDeterministicIntentIds()
        {
            var firstRun = RunDeterministicMovementTick();
            var secondRun = RunDeterministicMovementTick();

            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray(),
                secondRun.Result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.IntentId, group.SourceId, group.Moves.Single().Destination))
                    .ToArray(),
                secondRun.Result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.IntentId, group.SourceId, group.Moves.Single().Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult
                    .SelectedGroups
                    .Select(group => (group.GroupId, group.SourceId))
                    .ToArray(),
                secondRun.Result.MovementPhaseResult
                    .SelectedGroups
                    .Select(group => (group.GroupId, group.SourceId))
                    .ToArray());
            CollectionAssert.AreEqual(
                firstRun.Result.MovementPhaseResult.CommitEvents,
                secondRun.Result.MovementPhaseResult.CommitEvents);
            Assert.That(firstRun.OccupancyAfter, Is.EqualTo(secondRun.OccupancyAfter));
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1),
                    (SourceId: 20, IntentId: 2),
                },
                firstRun.Result.MovementPhaseResult.SortedIntents.Select(intent => (intent.SourceId, intent.IntentId)).ToArray());
        }

        [Test]
        public void Movement_ProjectileImpact_CreatesReservation_AndAttackConsumesIt()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));
            var finalSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.ProjectileImpact, MoveCount: 0) },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, group.GroupKind, MoveCount: group.Moves.Count))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=10|Target=20|At=(1,0)|Damage=1|Sequence=1",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.CommitEvents.All(evt => !evt.Contains("DamageCommitted")), Is.True);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, Damage: 1, Tick: 1, GroupId: 1, Sequence: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.Damage,
                        Tick: reservation.TickGenerated,
                        GroupId: reservation.SourceActionGroupId,
                        Sequence: reservation.ReservationSequence))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    (IntentId: 2, SourceId: 10, Kind: AttackInputKind.ImpactReservation, LocalSequence: 1, TargetId: 20),
                },
                result.AttackPhaseResult
                    .SortedInputs
                    .Select(intent => (intent.IntentId, intent.SourceId, intent.InputKind, intent.LocalSequence, intent.TargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "DamageCommitted|G=2|I=2|Target=20|Amount=1",
                    "DamageCommitted|G=2|I=2|Target=10|Amount=1",
                    "DestroyMarked|G=2|I=2|Target=10|FinalHp=0|Condition=WhenHpDepleted",
                },
                result.AttackPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(new[] { 10 }, result.CleanupPhaseResult.RemovedEntityIds);

            Assert.That(finalSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEntity(20, out var targetAfterTick), Is.True);
            Assert.That(targetAfterTick.hp, Is.EqualTo(2));
            Assert.That(targetAfterTick.markedForDeath, Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Attack.DrainedImpacts"));
            Assert.That(result.Trace.Text, Does.Contain("ImpactReservationCreated|G=1|I=1|Source=10|Target=20|At=(1,0)|Damage=1|Sequence=1"));
        }

        [Test]
        public void Movement_ProjectileReservations_AssignSequenceByCommitOrder()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateProjectile(entityId: 20, position: new Vector2Int(4, 0), hp: 1),
                CreateUnit(entityId: 30, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
                CreateUnit(entityId: 40, position: new Vector2Int(3, 0), hp: 3, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(10, 5, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 10, new Vector2Int(3, 0))),
                });

            var result = pipeline.RunTick(new TickInput(7));

            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=20|Target=40|At=(3,0)|Damage=1|Sequence=1",
                    "ImpactReservationCreated|G=2|I=2|Source=10|Target=30|At=(1,0)|Damage=1|Sequence=2",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, Sequence: 2),
                    (SourceId: 20, Sequence: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (reservation.SourceId, reservation.ReservationSequence))
                    .ToArray());
        }

        private static (TickResult Result, string OccupancyAfter) RunDeterministicMovementTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(20, 1, new Vector2Int(3, 0))),
                    new StubMovementLogic(new RawMovementIntent(10, 1, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(7));
            return (result, DumpUnitOccupancy(CreateSnapshot(worldState)));
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position, int hp = 3, int teamId = 1, Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                facing = facing,
            };
        }

        private static EntityState CreateNonUnitBlocker(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                facing = Direction.None,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            Vector2Int position,
            BoxCapabilities capabilities = BoxCapabilities.Pushable | BoxCapabilities.Throwable,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                facing = facing,
                boxCapabilities = capabilities,
            };
        }

        private static EntityState CreateProjectile(int entityId, Vector2Int position, int hp)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 1,
                type = EntityType.Projectile,
            };
        }

        private static string DumpUnitOccupancy(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);

            return string.Join(
                ",",
                entities
                    .Where(entity => entity.type == EntityType.Unit)
                    .Select(entity => $"{entity.entityId}@({entity.position.x},{entity.position.y})"));
        }

        private static Vector2Int GetEntityPosition(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.position;
        }

        private static Direction GetEntityFacing(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.facing;
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return CreateWorldState(initialEntities, BoardBounds.Unbounded, GameplayTerrainData.Empty);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return new WorldState(initialEntities, boardBounds, terrainData);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private sealed class StubMovementLogic : IEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent? _movementIntent;

            public StubMovementLogic(RawMovementIntent? movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntent.HasValue)
                {
                    buffer.Add(_movementIntent.Value);
                }
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
            }

            public bool ControlsEntity(int entityId, TickPhase phase)
            {
                return phase == TickPhase.Movement &&
                    _movementIntent.HasValue &&
                    _movementIntent.Value.SourceId == entityId;
            }
        }

        private sealed class ScriptedMovementLogic : IEntityLogic, IEntityLogicSourceBinding
        {
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public ScriptedMovementLogic(IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _movementIntentsByTick = movementIntentsByTick;
            }

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (_movementIntentsByTick.TryGetValue(input.TickIndex, out var movementIntent))
                {
                    buffer.Add(movementIntent);
                }
            }

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
            }

            public bool ControlsEntity(int entityId, TickPhase phase)
            {
                if (phase != TickPhase.Movement)
                {
                    return false;
                }

                foreach (var pair in _movementIntentsByTick)
                {
                    if (pair.Value.SourceId == entityId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
