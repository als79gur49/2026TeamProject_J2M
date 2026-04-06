using System;
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
using Game.Feature.Gameplay.Model.Sorting;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Commit;
using Game.Feature.Gameplay.Movement.Expansion;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.Movement.Resolution;
using Game.Feature.Gameplay.Movement.Sorting;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
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
        public void Movement_ScriptedMoveIntoUnit_SucceedsAndStacks()
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

            var occupancyBefore = DumpUnitOccupancy(CreateSnapshot(worldState));
            var result = pipeline.RunTick(new TickInput(1));
            var occupancyAfter = DumpUnitOccupancy(CreateSnapshot(worldState));

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
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(occupancyBefore, Is.EqualTo("10@(0,0),20@(1,0)"));
            Assert.That(occupancyAfter, Is.EqualTo("10@(1,0),20@(1,0)"));
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        public void Movement_TwoScriptedUnitsEnteringSameDestinationInSameTick_BothSucceedAndStack()
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
                    new StubMovementLogic(new RawMovementIntent(10, 10, new Vector2Int(1, 0))),
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1));

            CollectionAssert.AreEqual(
                new[]
                {
                    (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Move, Destination: new Vector2Int(1, 0)),
                    (GroupId: 2, SourceId: 20, Kind: ActionGroupKind.Move, Destination: new Vector2Int(1, 0)),
                },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, group.GroupKind, group.Moves.Single().Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[] { 1, 2 },
                result.MovementPhaseResult.SelectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "MoveCommitted|G=2|I=2|E=20|To=(1,0)|Facing=Left",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        public void Movement_MoveIntoPushBox_StartsHoldContactWithoutMoveOrRejection()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.SortedIntents, Is.Empty);
            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_MoveIntoUnit_SucceedsWithoutStartingPushContact()
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

            CollectionAssert.AreEqual(
                new[] { "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right" },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
            CollectionAssert.AreEqual(
                new[] { 10, 20 },
                GetUnitIdsAt(worldState, new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        public void Movement_PushInputPushBox_StopsBeforeEntityBlocker_AndEntityTypeNoneWallRemainsValid()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push, facing: Direction.Left),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(1, 0), Command: MovementCommandKind.Push),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            Assert.That(slideGroup.GroupId, Is.EqualTo(1));
            Assert.That(slideGroup.SourceId, Is.EqualTo(10));
            Assert.That(slideGroup.GroupKind, Is.EqualTo(ActionGroupKind.Push));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0), Facing: Direction.Right),
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
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(GetEntityFacing(worldState, 10), Is.EqualTo(Direction.Up));
            Assert.That(GetEntityFacing(worldState, 30), Is.EqualTo(Direction.Right));
            var snapshotAfter = CreateSnapshot(worldState);
            Assert.That(snapshotAfter.TryGetEntity(30, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.BoxSlide, Source: new SurfaceCell(FaceId.Floor, 1, 0), Destination: new SurfaceCell(FaceId.Floor, 2, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            Assert.That(result.Trace.Text, Does.Contain("Kind=Push"));
        }

        [Test]
        public void Movement_SlidingPushBox_ContinuesOnLaterTicksUntilBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(4, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var laterIdleTicks = RunTicks(pipeline, startTickIndex: 14, endTickIndex: 24);
            var thirdTick = pipeline.RunTick(new TickInput(25));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 30, IntentId: 1, Destination: new Vector2Int(3, 0), Command: MovementCommandKind.Move),
                },
                secondTick.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(laterIdleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Idle|Timer=0",
                },
                thirdTick.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(3, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(0));
        }

        [Test]
        public void Movement_BoxSlideInterval_At60Tps_PreservesRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 60,
                boxSlideStepIntervalSeconds: 0.1f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 6);
            var slideTick = pipeline.RunTick(new TickInput(7));

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=6",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=6",
                    "MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right",
                },
                slideTick.MovementPhaseResult.CommitEvents);
        }

        [Test]
        public void Movement_BoxSlideInterval_At120Tps_PreservesRealTimeCadence()
        {
            var timingProfile = CreateTimingProfile(
                simulationTicksPerSecond: 120,
                boxSlideStepIntervalSeconds: 0.1f);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(6, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                },
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile));

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var slideTick = pipeline.RunTick(new TickInput(13));

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(3,0)|Facing=Right",
                },
                slideTick.MovementPhaseResult.CommitEvents);
        }

        [Test]
        public void Movement_PushInputPushBox_StartsSlidingBeforeTerrainBlocker()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0)),
                },
                slideGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void Movement_PushInputPushBox_StartsSlidingBeforeBoardEdge()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            var slideGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(1, 0), Destination: new Vector2Int(2, 0)),
                },
                slideGroup.Moves
                    .Select(move => (move.EntityId, move.Source, move.Destination))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=30|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=30|To=(2,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void Movement_PushInputPushBox_IgnoresProjectileAsSlideStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                    CreateProjectile(entityId: 40, position: new Vector2Int(2, 0), hp: 1),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(4, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.RejectedReasons, Is.Empty);
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(finalSnapshot.TryGetProjectileAt(new Vector2Int(2, 0), out var projectile), Is.True);
            Assert.That(projectile.entityId, Is.EqualTo(40));
        }

        [Test]
        public void Movement_PushInputPushBox_StartsSlidingWhenBoundedLaneHasNoStopper()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(2,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[] { 1 },
                result.MovementPhaseResult.SelectedGroups.Select(group => group.GroupId).ToArray());
            CollectionAssert.AreEqual(
                System.Array.Empty<string>(),
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
        }

        [Test]
        public void Movement_ImmediatePushHoldMoveIntoPushBox_FallsBackToBlockedDestinationWhenEntityStopperIsAdjacent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                CreateBox(entityId: 30, position: new Vector2Int(2, 0), capabilities: BoxCapabilities.Push),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

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
        public void Movement_ImmediatePushHoldMoveIntoPushBox_FallsBackToBlockedDestinationWhenTerrainStopperIsAdjacent()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                new GameplayTerrainData(new[] { new Vector2Int(2, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

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
        public void Movement_ItemBox_PlayerEntersCellInSameTick_AndCleanupRemovesBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            Assert.That(result.MovementPhaseResult.SortedIntents.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Item) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "BoardPresenceCommitted|G=1|I=1|E=20|Presence=Detached",
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "DestroyMarked|G=1|I=1|Target=20|Condition=AlwaysMark",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(new[] { 20 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        public void Movement_ItemBox_LosesBoardPresenceBeforeCleanupRemoval()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 1, 0), capabilities: BoxCapabilities.Item),
            });

            var movementOnly = RunMovementPhaseOnly(
                worldState,
                new TickInput(1, PlayerTickCommand.Move(Direction.Right)),
                new PlayerLogic(10));

            CollectionAssert.AreEqual(
                new[]
                {
                    "BoardPresenceCommitted|G=1|I=1|E=20|Presence=Detached",
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "DestroyMarked|G=1|I=1|Target=20|Condition=AlwaysMark",
                },
                movementOnly.Result.CommitEvents);
            Assert.That(movementOnly.SnapshotAfterMovement.TryGetEntity(20, out var itemBox), Is.True);
            Assert.That(itemBox.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(movementOnly.SnapshotAfterMovement.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out var occupyingUnit), Is.True);
            Assert.That(occupyingUnit.entityId, Is.EqualTo(10));
            Assert.That(movementOnly.SnapshotAfterMovement.BlocksMovement(20), Is.False);
        }

        [Test]
        public void Movement_PushInputOnItemPushFlipBox_ResolvesAsItemBeforePushOrFlip()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Item) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "BoardPresenceCommitted|G=1|I=1|E=20|Presence=Detached",
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "DestroyMarked|G=1|I=1|Target=20|Condition=AlwaysMark",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(new[] { 20 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Kind=Item"));
            Assert.That(result.Trace.Text, Does.Contain("Command=Push"));
        }

        [Test]
        public void Movement_PushInputOnItemPushFlipDestroyBox_ResolvesAsItemBeforePushFlipOrDestroy_AndPresentationUsesEntityExitOwnership()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(
                    entityId: 20,
                    position: new SurfaceCell(FaceId.Floor, 1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var finalSnapshot = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Item) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "BoardPresenceCommitted|G=1|I=1|E=20|Presence=Detached",
                    "MoveCommitted|G=1|I=1|E=10|To=(1,0)|Facing=Right",
                    "DestroyMarked|G=1|I=1|Target=20|Condition=AlwaysMark",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 10, Kind: TickEntityMotionKind.Move, Source: new SurfaceCell(FaceId.Floor, 0, 0), Destination: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            CollectionAssert.AreEqual(
                Array.Empty<(int EntityId, TickVisibilityChangeKind Kind)>(),
                result.PresentationData
                    .VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(result.PresentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.ItemConsume));
            CollectionAssert.AreEqual(new[] { 20 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(finalSnapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(result.Trace.Text, Does.Contain("Kind=Item"));
            Assert.That(result.Trace.Text, Does.Contain("Command=Push"));
        }

        [Test]
        public void Movement_PushInputPushBox_FailsWhenBoxLacksCapability()
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
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=PushTargetNotPushBox|Cell=(1,0)|Target=20|Capabilities=None",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_PushInputIntoUnit_FailsBecausePushTargetsOnlyBoxes()
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
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=PushTargetNotBox|Cell=(1,0)|Target=20|Type=Unit",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_Flip_SucceedsWhenOppositeCellIsFree()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip, facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, IntentId: 1, Destination: new Vector2Int(-1, 0), Command: MovementCommandKind.Flip),
                },
                result.MovementPhaseResult
                    .SortedIntents
                    .Select(intent => (intent.SourceId, intent.IntentId, intent.Destination, intent.CommandKind))
                    .ToArray());
            var flipGroup = result.MovementPhaseResult.ExpandedCandidates.Single();
            Assert.That(flipGroup.GroupId, Is.EqualTo(1));
            Assert.That(flipGroup.SourceId, Is.EqualTo(10));
            Assert.That(flipGroup.GroupKind, Is.EqualTo(ActionGroupKind.Flip));
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Source: new Vector2Int(-1, 0), Destination: new Vector2Int(1, 0), Facing: Direction.Right),
                },
                flipGroup.Moves
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
            CollectionAssert.AreEqual(
                new[]
                {
                    (EntityId: 30, Kind: TickEntityMotionKind.Flip, Source: new SurfaceCell(FaceId.Floor, -1, 0), Destination: new SurfaceCell(FaceId.Floor, 1, 0)),
                },
                result.PresentationData
                    .EntityMotions
                    .Select(motion => (motion.EntityId, motion.MotionKind, motion.SourceCell, motion.DestinationCell))
                    .ToArray());
            Assert.That(result.Trace.Text, Does.Contain("Kind=Flip"));
            Assert.That(result.Trace.Text, Does.Contain("Moves=[E=30:(-1,0)->(1,0):Right]"));
        }

        [Test]
        public void Movement_Flip_FailsWhenTargetIsNotFlippableBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.None),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=FlipTargetNotFlippableBox|Cell=(-1,0)|Target=20|Type=Box|Capabilities=None",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Movement_FlipInputIntoUnit_FailsBecauseFlipTargetsOnlyBoxes()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateUnit(entityId: 20, position: new Vector2Int(-1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=FlipTargetNotFlippableBox|Cell=(-1,0)|Target=20|Type=Unit|Capabilities=None",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Movement_FlipInputOnItemFlipDestroyBox_UsesFlipBranch_WithoutConsumeOrDestroy()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), facing: Direction.Up),
                CreateBox(
                    entityId: 30,
                    position: new Vector2Int(-1, 0),
                    capabilities: BoxCapabilities.Item | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    facing: Direction.Left),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Flip) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "FacingCommitted|G=1|I=1|E=10|Facing=Left",
                    "MoveCommitted|G=1|I=1|E=30|To=(1,0)|Facing=Right",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("BoardPresenceCommitted")), Is.False);
            Assert.That(result.MovementPhaseResult.CommitEvents.Any(evt => evt.Contains("DestroyMarked")), Is.False);
            Assert.That(result.CleanupPhaseResult.RemovedEntityIds, Is.Empty);
            Assert.That(result.PresentationData.VisibilityChanges, Is.Empty);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(GetEntityCell(worldState, 30), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshotAfter.TryGetEntity(30, out var flippedBox), Is.True);
            Assert.That(flippedBox.boxCapabilities, Is.EqualTo(BoxCapabilities.Item | BoxCapabilities.Flip | BoxCapabilities.Destroy));
            Assert.That(result.Trace.Text, Does.Contain("Kind=Flip"));
        }

        [Test]
        public void Movement_Flip_FailsWhenLandingCellIsBlocked()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(-1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Left)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=FlipLandingBlocked|StopperKind=Entity|Stopper=20|StopperType=Unit|Cell=(1,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(1, 0)));
        }

        [Test]
        public void Movement_PlayerInput_FlipBeatsPushWhenBothButtonsArePressed()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Flip),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(3, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(
                new TickInput(
                    1,
                    PlayerTickCommand.Flip(Direction.Right)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Flip) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(GetEntityPosition(worldState, 30), Is.EqualTo(new Vector2Int(-1, 0)));
        }

        [Test]
        public void Movement_Flip_RejectsFrontBoundaryCrossing()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1), facing: Direction.Up),
                    CreateBox(entityId: 30, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Flip),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Up)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=FlipCrossesBoundary|Origin=(0,1)|Direction=Up",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 30), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        public void Movement_MoveAcrossBottomTopEdge_CommitsForwardTopologyChange()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Forward|Bottom=Front|Front=Ceiling",
                    "MoveCommitted|G=1|I=1|E=10|To=Front(0,0)|Facing=Up",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        public void Movement_TopologyChangingTick_RejectsOrdinaryCandidateInSameTick()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                    new StubMovementLogic(new RawMovementIntent(20, 50, new Vector2Int(1, 0))),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Move) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=20|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=True",
                },
                result.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Forward|Bottom=Front|Front=Ceiling",
                    "MoveCommitted|G=1|I=1|E=10|To=Front(0,0)|Facing=Up",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        [Test]
        public void Movement_OrdinarySelection_RejectsLaterTopologyChangingCandidate()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2, facing: Direction.Left),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(new RawMovementIntent(20, 150, new Vector2Int(1, 0))),
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 20, Kind: ActionGroupKind.Move) },
                result.MovementPhaseResult.SelectedGroups.Select(group => (group.GroupId, group.SourceId, group.GroupKind)).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Resolve|G=2|I=2|Source=10|Reason=TopologyExclusive|BlockedBy=1|BlockingKind=Move|BlockingTopologyChange=False",
                },
                result.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MoveCommitted|G=1|I=1|E=20|To=(1,0)|Facing=Left",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        public void Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationHasWallBlocker()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateNonUnitBlocker(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=Front(0,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        public void Movement_MoveAcrossBottomTopEdge_FailsWhenRotatedDestinationTerrainBlocked()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new GameplayTerrainData(new[] { new Vector2Int(0, 0) }));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=Front(0,0)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
        }

        [Test]
        public void Movement_MoveAcrossBottomBottomEdge_CommitsBackwardTopologyChange()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "TopologyCommitted|G=1|I=1|Rotation=Backward|Bottom=Back|Front=Floor",
                    "MoveCommitted|G=1|I=1|E=10|To=Back(0,1)|Facing=Down",
                },
                result.MovementPhaseResult.CommitEvents);
            Assert.That(snapshotAfter.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Back)));
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Back, 0, 1)));
        }

        [Test]
        public void Movement_MoveFromFrontBottomEdge_FailsWithoutRotation()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.ExpandedCandidates, Is.Empty);
            Assert.That(result.MovementPhaseResult.SelectedGroups, Is.Empty);
            Assert.That(result.MovementPhaseResult.CommitEvents, Is.Empty);
            CollectionAssert.AreEqual(
                new[]
                {
                    "MovementRejected|Stage=Expand|Source=10|I=1|Reason=BlockedDestination|Cell=Front(0,-1)",
                },
                result.MovementPhaseResult.RejectedReasons);
            Assert.That(GetEntityCell(worldState, 10), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
        }

        [Test]
        public void Movement_PushInputPushBox_ContinuesAcrossBottomFrontSharedEdge()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=Front(0,0)|Facing=Up",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=Front(0,1)|Facing=Up",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 1)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            Assert.That(firstTick.Trace.Text, Does.Contain("Kind=Push"));
        }

        [Test]
        public void Movement_PushInputPushBox_ContinuesAcrossFrontBottomSharedEdgeBackToBottom()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Front, 0, 1)),
                    CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0), capabilities: BoxCapabilities.Push),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(0,1)|Facing=Down",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            Assert.That(idleTicks.All(result => result.MovementPhaseResult.CommitEvents.Count == 0), Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(0,0)|Facing=Down",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            Assert.That(GetEntityCell(worldState, 20), Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Sliding));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(11));
            Assert.That(firstTick.Trace.Text, Does.Contain("Kind=Push"));
        }

        [Test]
        public void Movement_PushDestroyBox_WhenSlideStopperIsAdjacent_DetachesAndRemovesBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(2, 0)),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Push, MoveCount: 0) },
                result.MovementPhaseResult
                    .SelectedGroups
                    .Select(group => (group.GroupId, group.SourceId, group.GroupKind, MoveCount: group.Moves.Count))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "BoardPresenceCommitted|G=1|I=1|E=20|Presence=Detached",
                    "DestroyMarked|G=1|I=1|Target=20|Condition=AlwaysMark",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                Array.Empty<(int EntityId, TickVisibilityChangeKind Kind)>(),
                result.PresentationData
                    .VisibilityChanges
                    .Select(change => (change.EntityId, change.ChangeKind))
                    .ToArray());
            Assert.That(result.PresentationData.EntityExitSignals.Count, Is.EqualTo(1));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitedEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.EntityExitSignals[0].ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            CollectionAssert.AreEqual(new[] { 20 }, result.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(GetEntityPosition(worldState, 10), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        public void Movement_SlidingPushDestroyBox_WhenLaterSlideStops_RemainsOnBoardAndBecomesIdle()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                    CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Push | BoxCapabilities.Destroy),
                    CreateNonUnitBlocker(entityId: 90, position: new Vector2Int(3, 0)),
                },
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(5, 0)),
                GameplayTerrainData.Empty);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediatePushPlayerLogic(10),
                });

            var firstTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var idleTicks = RunTicks(pipeline, startTickIndex: 2, endTickIndex: 12);
            var secondTick = pipeline.RunTick(new TickInput(13));
            var snapshotAfter = CreateSnapshot(worldState);

            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Sliding|Timer=12",
                    "MoveCommitted|G=1|I=1|E=20|To=(2,0)|Facing=Right",
                },
                firstTick.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                Array.Empty<string>(),
                idleTicks.SelectMany(result => result.MovementPhaseResult.CommitEvents).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "StateChanged|G=1|I=1|E=20|State=Idle|Timer=0",
                },
                secondTick.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(Array.Empty<string>(), secondTick.MovementPhaseResult.RejectedReasons);
            CollectionAssert.AreEqual(Array.Empty<int>(), secondTick.CleanupPhaseResult.RemovedEntityIds);
            Assert.That(GetEntityPosition(worldState, 20), Is.EqualTo(new Vector2Int(2, 0)));
            Assert.That(snapshotAfter.TryGetEntity(20, out var pushedBox), Is.True);
            Assert.That(pushedBox.state, Is.EqualTo(EntityPhaseState.Idle));
            Assert.That(pushedBox.stateTimer, Is.EqualTo(0));
        }

        [Test]
        public void Movement_Flip_RejectsLaterCandidateThatMovesSameBox()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0)),
                CreateBox(entityId: 30, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 20, position: new Vector2Int(2, 0), teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreateImmediateFlipPlayerLogic(10),
                    new StubMovementLogic(new RawMovementIntent(20, 5, new Vector2Int(1, 0), MovementCommandKind.Flip)),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));

            CollectionAssert.AreEqual(
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.Flip) },
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
                new[] { (GroupId: 1, SourceId: 10, Kind: ActionGroupKind.ProjectileImpact, MoveCount: 0, TargetId: 20) },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, group.GroupKind, MoveCount: group.Moves.Count, TargetId: group.ProjectileImpactTargetId))
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
        public void Movement_ProjectileImpact_PrefersHostileTargetWithinStackedUnits()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 1),
                CreateUnit(entityId: 30, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
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
                new[]
                {
                    (GroupId: 1, SourceId: 10, TargetId: 30),
                },
                result.MovementPhaseResult
                    .ExpandedCandidates
                    .Select(group => (group.GroupId, group.SourceId, TargetId: group.ProjectileImpactTargetId))
                    .ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=10|Target=30|At=(1,0)|Damage=1|Sequence=1",
                },
                result.MovementPhaseResult.CommitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 30, Damage: 1, Tick: 1, GroupId: 1, Sequence: 1),
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

            Assert.That(finalSnapshot.TryGetEntity(20, out var friendlyUnit), Is.True);
            Assert.That(friendlyUnit.hp, Is.EqualTo(3));
            Assert.That(finalSnapshot.TryGetEntity(30, out var hostileUnit), Is.True);
            Assert.That(hostileUnit.hp, Is.EqualTo(2));
        }

        [Test]
        public void MovementCommitter_ProjectileImpact_UsesResolvedGroupTargetWithoutIntentLookup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateProjectile(entityId: 10, position: new Vector2Int(0, 0), hp: 1),
                CreateUnit(entityId: 20, position: new Vector2Int(1, 0), hp: 3, teamId: 2),
            });
            var snapshot = CreateSnapshot(worldState);
            var projectileImpactGroup = new ActionGroup(intentId: 1, sourceId: 10, priority: 5, ActionGroupKind.ProjectileImpact);
            projectileImpactGroup.AssignGroupId(1);
            projectileImpactGroup.AssignProjectileImpactTarget(20);

            var transientBuffer = new PhaseTransientBuffer();
            var commitEvents = new List<string>();

            new MovementCommitter(CreateDefaultPlayerControlTimingSnapshot()).Commit(
                snapshot,
                Array.Empty<MoveIntent>(),
                tickIndex: 1,
                worldState.CreateWriteContext(),
                transientBuffer,
                new[] { projectileImpactGroup },
                commitEvents);

            var reservations = transientBuffer.DrainImpacts();

            CollectionAssert.AreEqual(
                new[]
                {
                    "ImpactReservationCreated|G=1|I=1|Source=10|Target=20|At=(1,0)|Damage=1|Sequence=1",
                },
                commitEvents);
            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 10, TargetId: 20, Position: new Vector2Int(1, 0), Damage: 1, Tick: 1),
                },
                reservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.Position,
                        reservation.Damage,
                        Tick: reservation.TickGenerated))
                    .ToArray());
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

        private static List<TickResult> RunTicks(TickPipeline pipeline, int startTickIndex, int endTickIndex)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (endTickIndex < startTickIndex)
            {
                throw new ArgumentOutOfRangeException(nameof(endTickIndex));
            }

            var results = new List<TickResult>(endTickIndex - startTickIndex + 1);
            for (var tickIndex = startTickIndex; tickIndex <= endTickIndex; tickIndex++)
            {
                results.Add(pipeline.RunTick(new TickInput(tickIndex)));
            }

            return results;
        }

        private static GameplayTimingProfile CreateTimingProfile(
            int simulationTicksPerSecond = 60,
            float initialMoveDelaySeconds = 0f,
            float repeatedMoveIntervalSeconds = 0.4f,
            float boxSlideStepIntervalSeconds = 0.2f,
            float projectileStepIntervalSeconds = 0.2f,
            float pushMotionDurationSeconds = 0.2f,
            float flipMotionDurationSeconds = 0.2f,
            float flipArcHeightInCells = 0.65f,
            int maxTicksPerFrame = 8)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond,
                initialMoveDelaySeconds,
                repeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                projectileStepIntervalSeconds,
                pushMotionDurationSeconds,
                flipMotionDurationSeconds,
                flipArcHeightInCells,
                maxTicksPerFrame);
        }

        private static IMovementEntityLogic CreateImmediatePushPlayerLogic(int entityId)
        {
            return new ImmediatePlayerInteractionLogic(entityId, MovementCommandKind.Push);
        }

        private static IMovementEntityLogic CreateImmediateFlipPlayerLogic(int entityId)
        {
            return new ImmediatePlayerInteractionLogic(entityId, MovementCommandKind.Flip);
        }

        private static (MovementPhaseResult Result, WorldSnapshot SnapshotAfterMovement) RunMovementPhaseOnly(
            WorldState worldState,
            TickInput input,
            params IMovementEntityLogic[] entityLogics)
        {
            var snapshot = CreateSnapshot(worldState);
            var rawMovementIntents = new List<RawMovementIntent>();
            new MovementIntentCollector().Collect(snapshot, in input, entityLogics, rawMovementIntents);

            var idAllocator = new IdAllocator();
            idAllocator.ResetForTick(input.TickIndex);
            rawMovementIntents.Sort(RawMovementIntentComparer.Instance);

            var sortedIntents = new List<MoveIntent>(rawMovementIntents.Count);
            for (var i = 0; i < rawMovementIntents.Count; i++)
            {
                var rawIntent = rawMovementIntents[i];
                MoveIntent moveIntent = rawIntent.CommandKind switch
                {
                    MovementCommandKind.Push => new PushIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    MovementCommandKind.Flip => new FlipIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                    _ => new MoveIntent(
                        rawIntent.SourceId,
                        rawIntent.Priority,
                        rawIntent.Destination,
                        rawIntent.LocalSequence),
                };
                moveIntent.AssignIntentId(idAllocator.AllocateIntentId());
                sortedIntents.Add(moveIntent);
            }

            var expandedCandidates = new List<ActionGroup>();
            var rejectedReasons = new List<string>();
            new MovementExpander().Expand(snapshot, sortedIntents, expandedCandidates, rejectedReasons);
            expandedCandidates.Sort(ActionGroupComparer.Instance);
            for (var i = 0; i < expandedCandidates.Count; i++)
            {
                expandedCandidates[i].AssignGroupId(idAllocator.AllocateGroupId());
            }

            var selectedGroups = new List<ActionGroup>();
            new MovementResolver().Resolve(snapshot, expandedCandidates, selectedGroups, rejectedReasons);

            var commitEvents = new List<string>();
            new MovementCommitter(CreateDefaultPlayerControlTimingSnapshot()).Commit(
                snapshot,
                sortedIntents,
                input.TickIndex,
                worldState.CreateWriteContext(),
                new PhaseTransientBuffer(),
                selectedGroups,
                commitEvents);

            return (
                new MovementPhaseResult(
                    rawMovementIntents,
                    sortedIntents,
                    expandedCandidates,
                    selectedGroups,
                    commitEvents,
                    rejectedReasons),
                CreateSnapshot(worldState));
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile = null)
        {
            var generalTimingProfile = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                generalTimingProfile.SimulationTicksPerSecond,
                generalTimingProfile.RepeatedMoveIntervalSeconds);
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position, int hp = 3, int teamId = 1, Direction facing = Direction.Right)
        {
            return CreateUnit(entityId, SurfaceCell.FromPlanar(position), hp, teamId, facing);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position, int hp = 3, int teamId = 1, Direction facing = Direction.Right)
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
            return CreateNonUnitBlocker(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateNonUnitBlocker(int entityId, SurfaceCell position)
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
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            Direction facing = Direction.Right)
        {
            return CreateBox(entityId, SurfaceCell.FromPlanar(position), capabilities, facing);
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
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
            return CreateProjectile(entityId, SurfaceCell.FromPlanar(position), hp);
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position, int hp)
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

        private static SurfaceCell GetEntityCell(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.position;
        }

        private static int[] GetUnitIdsAt(WorldState worldState, SurfaceCell cell)
        {
            var snapshot = CreateSnapshot(worldState);
            var entities = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, entities);
            return entities.Select(entity => entity.entityId).ToArray();
        }

        private static Direction GetEntityFacing(WorldState worldState, int entityId)
        {
            var snapshot = CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(entityId, out var entity), Is.True);
            return entity.facing;
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities, boardBounds, terrainData);
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            var createSnapshotMethod = typeof(WorldState).GetMethod(
                "CreateSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(createSnapshotMethod, Is.Not.Null);

            return (WorldSnapshot)createSnapshotMethod.Invoke(worldState, null);
        }

        private sealed class StubMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent? _movementIntent;

            public StubMovementLogic(RawMovementIntent? movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent?.SourceId ?? 0;

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
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _controlledEntityId;
            private readonly IReadOnlyDictionary<int, RawMovementIntent> _movementIntentsByTick;

            public ScriptedMovementLogic(IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                _movementIntentsByTick = movementIntentsByTick;
                _controlledEntityId = ResolveControlledEntityId(movementIntentsByTick);
            }

            public int ControlledEntityId => _controlledEntityId;

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

            private static int ResolveControlledEntityId(IReadOnlyDictionary<int, RawMovementIntent> movementIntentsByTick)
            {
                foreach (var pair in movementIntentsByTick)
                {
                    return pair.Value.SourceId;
                }

                return 0;
            }
        }

        private sealed class ImmediatePlayerInteractionLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly MovementCommandKind _commandKind;
            private readonly int _entityId;

            public ImmediatePlayerInteractionLogic(int entityId, MovementCommandKind commandKind)
            {
                _entityId = entityId;
                _commandKind = commandKind;
            }

            public int ControlledEntityId => _entityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (snapshot == null)
                {
                    throw new ArgumentNullException(nameof(snapshot));
                }

                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (!snapshot.TryGetEntity(_entityId, out var entity) ||
                    entity.hp <= 0 ||
                    entity.markedForDeath ||
                    input.PlayerCommand.MoveDirection == Direction.None ||
                    (_commandKind == MovementCommandKind.Flip && !input.PlayerCommand.FlipPressed) ||
                    !TryResolveDelta(input.PlayerCommand.MoveDirection, out var delta))
                {
                    return;
                }

                buffer.Add(
                    new RawMovementIntent(
                        _entityId,
                        priority: 100,
                        entity.position.PlanarPosition + delta,
                        _commandKind,
                        localSequence: 0));
            }

            private static bool TryResolveDelta(Direction direction, out Vector2Int delta)
            {
                switch (direction)
                {
                    case Direction.Up:
                        delta = Vector2Int.up;
                        return true;

                    case Direction.Right:
                        delta = Vector2Int.right;
                        return true;

                    case Direction.Down:
                        delta = Vector2Int.down;
                        return true;

                    case Direction.Left:
                        delta = Vector2Int.left;
                        return true;

                    default:
                        delta = Vector2Int.zero;
                        return false;
                }
            }
        }
    }
}
