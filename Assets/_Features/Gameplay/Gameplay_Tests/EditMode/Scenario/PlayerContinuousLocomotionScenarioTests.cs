using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class PlayerContinuousLocomotionScenarioTests
    {
        [Test]
        [Category("Extended")]
        public void Player_Free2D_StartRight_FromCenter()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Moving));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, 0, 0) &&
                track.DestinationLocalOffset.X.RawValue == state.localOffset.X.RawValue), Is.True);
            MovementExecutionOwnershipAssert.NoGenericExpansionOrdinaryUnitMove(result, 10);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_SameFaceMove_StillUsesContinuousTrack()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(
                result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.DestinationLocalOffset.X.RawValue == state.localOffset.X.RawValue),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 10 &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned),
                Is.False);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ReleaseInput_HoldsCurrentLocalPoint()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var before = worldState.CreateSnapshot();
            Assert.That(before.TryGetUnitContinuousLocomotionState(10, out var moving), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var after = worldState.CreateSnapshot();

            Assert.That(after.TryGetUnitContinuousLocomotionState(10, out var idle), Is.True);
            Assert.That(idle.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(idle.velocity, Is.EqualTo(SimulationVelocity2.Zero));
            Assert.That(idle.localOffset, Is.EqualTo(moving.localOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenUpInput_MovesImmediatelyUp()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var upSnapshot = worldState.CreateSnapshot();

            Assert.That(upSnapshot.TryGetUnitContinuousLocomotionState(10, out var upState), Is.True);
            Assert.That(upState.localOffset.X.RawValue, Is.EqualTo(rightState.localOffset.X.RawValue));
            Assert.That(upState.localOffset.Y.RawValue, Is.GreaterThan(0));
            Assert.That(upState.lastMoveDirection, Is.EqualTo(Direction.Up));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RightOffset_ThenLeftInput_MovesBackImmediately()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var rightSnapshot = worldState.CreateSnapshot();
            Assert.That(rightSnapshot.TryGetUnitContinuousLocomotionState(10, out var rightState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Left)));
            var leftSnapshot = worldState.CreateSnapshot();

            Assert.That(leftSnapshot.TryGetUnitContinuousLocomotionState(10, out var leftState), Is.True);
            Assert.That(leftState.localOffset.X.RawValue, Is.LessThan(rightState.localOffset.X.RawValue));
            Assert.That(leftState.lastMoveDirection, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_CrossHalfBoundary_NormalizesAnchor()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipeline(worldState);

            TickResult result = null;
            for (var tick = 1; tick <= 10; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            MovementExecutionOwnershipAssert.NoGenericExpansionOrdinaryUnitMove(result, 10);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_ApproachBox_ClampsAtBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_UnitOverlap_DoesNotBlock()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalNonZero_PushFlipRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_WallClamp()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            TickResult result = null;
            for (var tick = 1; tick <= 10; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Free2DContinuousBlocked") &&
                    reason.Contains("TraversalBlocked")),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_SolidClamp()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateWall(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_LocalZero_OutwardInputStartsFree2DWithoutTopologyTransition()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(DefaultFree2DSpeedUnitsPerTick()));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyEdge_ApproachFromInterior_CrossesNativelyOnOvershoot()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            TickResult result = null;
            for (var tick = 1; tick <= 60; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Up)));
                if (!worldState.CreateSnapshot().Topology.Equals(new CubeTopologyState(FaceId.Floor)))
                {
                    break;
                }
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.Not.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(snapshot.Topology.BottomFace, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            MovementExecutionOwnershipAssert.GridTransactionBranchesRemainAllowed(
                result,
                10,
                MovementExecutionBoundaryKind.Free2DTopologyTransition);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyEdge_ExactSeamArrivalWithoutOvershoot_DoesNotTransition()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, -DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology ||
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization ||
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_SeamClampedNonZero_CrossesNativelyUnderDefaultGameplay()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            MovementExecutionOwnershipAssert.GridTransactionBranchesRemainAllowed(
                result,
                10,
                MovementExecutionBoundaryKind.Free2DTopologyTransition);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_LocalNonZero_CrossesNativelyUnderDefaultGameplay()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 256, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(256));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 10 &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_LateralProgressThenForward_CrossesFace()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            SetPlayerContinuousLocalOffset(worldState, 256, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateNativeTopologyPipeline(worldState);
            var preSnapshot = worldState.CreateSnapshot();
            Assert.That(preSnapshot.TryGetUnitContinuousLocomotionState(10, out var preState), Is.True);
            Assert.That(Mathf.Abs(preState.localOffset.X.RawValue), Is.GreaterThan(0));

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(256));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(state.subUnitRemainderX, Is.EqualTo(0));
            Assert.That(state.subUnitRemainderY, Is.EqualTo(0));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 10 &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.GenericExpansionOwned),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.False);
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion => motion.EntityId == 10),
                Is.False,
                string.Join(";", result.PresentationData.EntityMotions.Select(motion => $"{motion.EntityId}:{motion.MotionKind}:{motion.SourceCell}->{motion.DestinationCell}")));
            Assert.That(result.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(result.PresentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(result.PresentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(snapshot.Topology));
            Assert.That(
                result.PresentationData.TopologyMotion.Value.SourceTopology,
                Is.EqualTo(snapshot.Topology.Rotate(CubeRotationKind.Backward)));
            if (!result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.SourceTopology.HasValue &&
                    track.SourceTopology.Value.Equals(new CubeTopologyState(FaceId.Floor)) &&
                    track.DestinationTopology.HasValue &&
                    track.DestinationTopology.Value.Equals(new CubeTopologyState(FaceId.Front)) &&
                    track.TopologyRotationKind == CubeRotationKind.Forward))
            {
                Assert.Fail(string.Join(";", result.PresentationData.ContinuousLocomotionTracks.Select(track => $"{track.EntityId}:{track.SourceTopology}->{track.DestinationTopology}:{track.TopologyRotationKind}:{track.TopologyTransitionReason}")));
            }
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_WithLeftOffset_CrossesAndPreservesLateralOffset()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(
                worldState,
                -384,
                SimulationFixed.MaxPositiveLocalOffset,
                DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);
            var preSnapshot = worldState.CreateSnapshot();
            Assert.That(preSnapshot.TryGetUnitContinuousLocomotionState(10, out var preState), Is.True);
            Assert.That(preState.localOffset.X.RawValue, Is.LessThan(0));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(player.position.face, Is.EqualTo(FaceId.Front));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(-384));
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_WithRightOffset_CrossesAndPreservesLateralOffset()
        {
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(
                worldState,
                384,
                SimulationFixed.MaxPositiveLocalOffset,
                DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);
            var preSnapshot = worldState.CreateSnapshot();
            Assert.That(preSnapshot.TryGetUnitContinuousLocomotionState(10, out var preState), Is.True);
            Assert.That(preState.localOffset.X.RawValue, Is.GreaterThan(0));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(player.position.face, Is.EqualTo(FaceId.Front));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(384));
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_RadiusContactBeforeCenter_CrossesNatively()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 512, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, radiusCells);
            var preSnapshot = worldState.CreateSnapshot();
            Assert.That(preSnapshot.TryGetUnitContinuousLocomotionState(10, out var preState), Is.True);
            Assert.That(preState.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(preState.localOffset.Y.RawValue + speed, Is.EqualTo(sourceThresholdY + 1));
            Assert.That(preState.localOffset.Y.RawValue + speed, Is.LessThan(SimulationFixed.HalfCellUnits));

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset + radius + 1));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.True);
            Assert.That(result.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(result.PresentationData.TopologyMotion.Value.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(result.PresentationData.TopologyMotion.Value.DestinationTopology, Is.EqualTo(snapshot.Topology));
            Assert.That(
                result.PresentationData.TopologyMotion.Value.SourceTopology,
                Is.EqualTo(snapshot.Topology.Rotate(CubeRotationKind.Backward)));
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_TargetFaceBox_Blocks()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 0, 0)),
                },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 256, SimulationFixed.MaxPositiveLocalOffset, DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            if (!result.MovementPhaseResult.RejectedReasons.Any(entry =>
                    entry.Contains("Free2DTopologyNativeRejected") &&
                    entry.Contains("TargetFaceBlockedBySolid")))
            {
                Assert.Fail(string.Join(";", result.MovementPhaseResult.RejectedReasons));
            }
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Free2DTopology_BottomToBackTargetFaceBox_EmitsTopologyBlockedSignal()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Back, 0, 1)),
                },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MinLocalOffset, DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(entry =>
                    entry.Contains("Free2DTopologyNativeRejected") &&
                    entry.Contains("TargetFaceBlockedBySolid")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            AssertBottomToBackBlockedSignal(
                result,
                FaceId.Floor,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new SurfaceCell(FaceId.Back, 0, 1),
                TickTraversalBlockerKind.Solid);
        }

        [Test]
        [Category("Core")]
        public void Free2DTopology_VisualBottomToBackTargetFaceBox_WhenBottomFaceIsFront_EmitsTopologyBlockedSignal()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Front, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 1)),
                },
                boardBounds,
                new CubeTopologyState(FaceId.Front));
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MinLocalOffset, DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(entry =>
                    entry.Contains("Free2DTopologyNativeRejected") &&
                    entry.Contains("TargetFaceBlockedBySolid")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            AssertBottomToBackBlockedSignal(
                result,
                FaceId.Front,
                new SurfaceCell(FaceId.Front, 0, 0),
                new SurfaceCell(FaceId.Floor, 0, 1),
                TickTraversalBlockerKind.Solid);
        }

        [Test]
        [Category("Core")]
        public void Free2DTopology_BottomToBackTargetFaceSolid_EmitsTopologyBlockedSignal()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var targetCell = new SurfaceCell(FaceId.Back, 0, 1);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)), CreateWall(20, targetCell) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MinLocalOffset, DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateNativeTopologyPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(entry =>
                    entry.Contains("Free2DTopologyNativeRejected") &&
                    entry.Contains("TargetFaceBlockedBySolid")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            AssertBottomToBackBlockedSignal(
                result,
                FaceId.Floor,
                new SurfaceCell(FaceId.Floor, 0, 0),
                targetCell,
                TickTraversalBlockerKind.Solid);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_TargetFaceFootprintBlocked_DoesNotApproachSettle()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.HalfCellUnits - radius + 1, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, radiusCells);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(entry =>
                    entry.Contains("Free2DTopologyNativeRejected") &&
                    entry.Contains("TargetFaceFootprintBlocked")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_TargetFaceFootprintTangentToBox_CrossesNatively()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.HalfCellUnits - radius, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, radiusCells);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.HalfCellUnits - radius));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_TargetFaceFootprintTangentToBoardEdge_CrossesNatively()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(0, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.HalfCellUnits - radius, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, radiusCells);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.HalfCellUnits - radius));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_AdjacentWallExactMaxContact_AllowsTransition()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            AssertBottomToFrontBoundaryRemap(exactMaxContactX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var footprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, sourceCell),
                    CreateWall(20, footprintNeighbor),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, exactMaxContactX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(exactMaxContactX + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, exactMaxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
            Assert.That(snapshot.TryGetSolidOccupantAt(snapshot.Topology, footprintNeighbor, out var wall), Is.True);
            Assert.That(wall.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_AdjacentWallOneInsideMaxContact_AllowsTransition()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneInsideX = SimulationFixed.HalfCellUnits - radius - 1;
            AssertBottomToFrontBoundaryRemap(oneInsideX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, oneInsideX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneInsideX + radius, Is.LessThan(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, oneInsideX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_AdjacentWallOneBeyondMaxContact_BlocksWithFootprintReason()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneBeyondX = SimulationFixed.HalfCellUnits - radius + 1;
            AssertBottomToFrontBoundaryRemap(oneBeyondX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, sourceCell),
                    CreateWall(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, oneBeyondX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneBeyondX + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionBlockedByFootprint(snapshot, result, sourceCell);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_SourceSideWallProjectsOneBeyondBeforeRemap_AllowsTransition()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var maxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneBeyondX = maxContactX + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var sourceContactCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetFootprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, sourceCell),
                    CreateWall(20, sourceContactCell),
                    CreateBox(21, targetFootprintNeighbor),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, oneBeyondX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneBeyondX + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            Assert.That(maxContactX + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, maxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
            Assert.That(snapshot.TryGetSolidOccupantAt(snapshot.Topology, targetFootprintNeighbor, out var targetBox), Is.True);
            Assert.That(targetBox.entityId, Is.EqualTo(21));
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_SourceSideInactiveBarricadeDoesNotProjectOneBeyond()
        {
            AssertBottomToFrontSourceFeatureDoesNotProjectOneBeyond(
                CreateBarricade,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_SourceSideDestroyTileDoesNotProjectOneBeyond()
        {
            AssertBottomToFrontSourceFeatureDoesNotProjectOneBeyond(
                CreateDestroyTile,
                TileFeatureActivationRule.BottomFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_SourceSideActiveBarricadeProjectsOneBeyondBeforeRemap()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var maxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneBeyondX = maxContactX + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var sourceContactCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetFootprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, sourceCell),
                    CreateBox(20, targetFootprintNeighbor),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, sourceContactCell) });
            SetBoundaryFootprintPose(worldState, oneBeyondX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                BoundaryFootprintRadiusCells(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneBeyondX + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            Assert.That(maxContactX + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, maxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_SourceSideWallsDoNotCauseFalseTargetFootprintBlock()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            AssertBottomToFrontBoundaryRemap(exactMaxContactX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, new SurfaceCell(FaceId.Floor, -1, 1)),
                    CreateWall(21, new SurfaceCell(FaceId.Floor, 1, 1)),
                },
                new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, exactMaxContactX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, exactMaxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_WrongFaceNeighborWallIgnored()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            AssertBottomToFrontBoundaryRemap(exactMaxContactX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, new SurfaceCell(FaceId.Back, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetBoundaryFootprintPose(worldState, exactMaxContactX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, BoundaryFootprintRadiusCells());

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, exactMaxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_AdjacentActiveBarricadeExactMaxContact_AllowsTransition()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            AssertBottomToFrontBoundaryRemap(exactMaxContactX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, new SurfaceCell(FaceId.Front, 1, 0)) });
            SetBoundaryFootprintPose(worldState, exactMaxContactX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                BoundaryFootprintRadiusCells(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(exactMaxContactX + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionSucceeded(snapshot, result, targetCell, exactMaxContactX);
            AssertNoTargetFootprintOrTileFeatureReject(result);
        }

        [Test]
        [Category("Extended")]
        public void PlayerFree2D_NativeTopologyTransition_BottomToFront_AdjacentActiveBarricadeOneBeyond_BlocksWithFootprintReason()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneBeyondX = SimulationFixed.HalfCellUnits - radius + 1;
            AssertBottomToFrontBoundaryRemap(oneBeyondX, radius, BoundaryRadiusSpeedUnitsPerTick());

            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, new SurfaceCell(FaceId.Front, 1, 0)) });
            SetBoundaryFootprintPose(worldState, oneBeyondX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                BoundaryFootprintRadiusCells(),
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneBeyondX + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionBlockedByFootprint(snapshot, result, sourceCell);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusApproachBox_ClampsBeforeBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_GroundPlayer_ActivatedBarricadeBlocksMovement()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var expectedClampX = SimulationFixed.HalfCellUnits - radius;
            var barricadeCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, barricadeCell) });
            SetPlayerContinuousLocalOffset(worldState, expectedClampX - speed, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(expectedClampX));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(snapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            Assert.That(snapshot.TryGetSolidSemanticAt(player.position, out _), Is.False);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DContinuousBlocked") &&
                reason.Contains("TraversalBlocked")), Is.True);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, 0, 0) &&
                track.DestinationLocalOffset.X.RawValue == expectedClampX),
                Is.True);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_InactiveBarricadeDoesNotBlockMovement()
        {
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var barricadeCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, barricadeCell) });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.MaxPositiveLocalOffset, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(barricadeCell));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(snapshot.TryGetSolidSemanticAt(barricadeCell, out _), Is.False);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DContinuousBlocked") ||
                reason.Contains("TraversalBlocked") ||
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == barricadeCell),
                Is.True);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_SamePlanarOtherFaceTileFeaturesIgnored()
        {
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var floorDestination = new SurfaceCell(FaceId.Floor, 1, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[]
                {
                    CreateBarricade(100, otherFaceCell),
                    CreateDestroyTile(200, otherFaceCell),
                });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.MaxPositiveLocalOffset, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly),
                    CreateTileFeatureDefinition(200, TileFeatureActivationRule.FrontFaceOnly),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(floorDestination));
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(player.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(player.markedForDeath, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(snapshot.TryGetSolidSemanticAt(otherFaceCell, out _), Is.False);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TraversalBlocked") ||
                reason.Contains("PlayerVoluntaryDestroyTileEntryBlocked") ||
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.FinalEntities.Single(entity => entity.entityId == 10).markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_TargetInactiveBarricadePresence_BlocksTransition()
        {
            AssertPlayerFree2DNativeTopologyTransitionTargetTileFeaturePresenceBlocks(
                CreateBarricade,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_TargetActiveBarricadePresence_BlocksTransition()
        {
            AssertPlayerFree2DNativeTopologyTransitionTargetTileFeaturePresenceBlocks(
                CreateBarricade,
                TileFeatureActivationRule.BottomFaceOnly);
        }

        private static void AssertPlayerFree2DNativeTopologyTransitionTargetTileFeaturePresenceBlocks(
            Func<int, SurfaceCell, TileFeatureState> createTileFeature,
            TileFeatureActivationRule activationRule)
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                new[] { createTileFeature(100, targetCell) });
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(
                worldState,
                new[] { CreateTileFeatureDefinition(100, activationRule) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(sourceCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DTopologyNativeRejected") &&
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.SetTopology ||
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
            Assert.That(result.PresentationData.TopologyMotion.HasValue, Is.False);
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals[0].PrimaryBlockerKind, Is.EqualTo(TickTraversalBlockerKind.TileFeature));
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_GroundPlayer_ActivatedDestroyTile_CurrentPolicyGuard()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var expectedClampX = SimulationFixed.HalfCellUnits - radius;
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(worldState, expectedClampX - speed, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(expectedClampX));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(0));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Stage=Free2D") &&
                reason.Contains("Reason=PlayerVoluntaryDestroyTileEntryBlocked")), Is.True);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.EntityId == 10 &&
                operation.Kind == FinalizationOperationKind.MoveEntity),
                Is.False);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                track.EntityId == 10 &&
                track.DestinationAnchorCell == new SurfaceCell(FaceId.Floor, 0, 0) &&
                track.DestinationLocalOffset.X.RawValue == expectedClampX),
                Is.True);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.FinalEntities.Single(entity => entity.entityId == 10).markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_SameFaceApproachActiveDestroyTile_ClampFollowsCollisionRadius()
        {
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var largeRadiusClampX = SimulationFixed.HalfCellUnits - SimulationFixed.UnitsPerCell * 3 / 16;
            var smallRadiusClampX = SimulationFixed.HalfCellUnits - SimulationFixed.UnitsPerCell / 8;

            var largeRadiusWorldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(largeRadiusWorldState, largeRadiusClampX - speed, 0, speed);
            var largeRadiusPipeline = CreatePipelineWithCollisionRadius(
                largeRadiusWorldState,
                collisionRadiusCells: 0.1875f,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var largeRadiusResult = largeRadiusPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var largeRadiusSnapshot = largeRadiusWorldState.CreateSnapshot();

            Assert.That(largeRadiusSnapshot.TryGetUnitContinuousLocomotionState(10, out var largeRadiusState), Is.True);
            Assert.That(largeRadiusState.localOffset.X.RawValue, Is.EqualTo(largeRadiusClampX));
            Assert.That(largeRadiusResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Reason=PlayerVoluntaryDestroyTileEntryBlocked")), Is.True);

            var smallRadiusWorldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(smallRadiusWorldState, smallRadiusClampX - speed, 0, speed);
            var smallRadiusPipeline = CreatePipelineWithCollisionRadius(
                smallRadiusWorldState,
                collisionRadiusCells: 0.125f,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var smallRadiusResult = smallRadiusPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var smallRadiusSnapshot = smallRadiusWorldState.CreateSnapshot();

            Assert.That(smallRadiusSnapshot.TryGetUnitContinuousLocomotionState(10, out var smallRadiusState), Is.True);
            Assert.That(smallRadiusState.localOffset.X.RawValue, Is.EqualTo(smallRadiusClampX));
            Assert.That(smallRadiusResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Reason=PlayerVoluntaryDestroyTileEntryBlocked")), Is.True);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_AirPlayer_ActivatedDestroyTile_IsAllowedAndNoEffect()
        {
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell, unitMobilityKind: UnitMobilityKind.Air) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.MaxPositiveLocalOffset, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(destroyCell));
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(player.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(player.markedForDeath, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Stage=Free2D") &&
                reason.Contains("Reason=PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.FinalEntities.Single(entity => entity.entityId == 10).markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_SameFaceInactiveDestroyTile_AllowsApproachAndEntry()
        {
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 0)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.MaxPositiveLocalOffset, 0, speed);
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.FrontFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(destroyCell));
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + speed - SimulationFixed.UnitsPerCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_AdjacentActiveDestroyTile_MoveAwayOrParallel_IsAllowed()
        {
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var destroyCell = new SurfaceCell(FaceId.Floor, 1, 0);

            var moveAwayWorldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(moveAwayWorldState, speed, 0, speed);
            var moveAwayPipeline = CreatePipelineWithCollisionRadius(
                moveAwayWorldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var moveAwayResult = moveAwayPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Left)));
            var moveAwaySnapshot = moveAwayWorldState.CreateSnapshot();

            Assert.That(moveAwaySnapshot.TryGetEntity(10, out var moveAwayPlayer), Is.True);
            Assert.That(moveAwayPlayer.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(moveAwaySnapshot.TryGetUnitContinuousLocomotionState(10, out var moveAwayState), Is.True);
            Assert.That(moveAwayState.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(moveAwayResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(moveAwayResult.PresentationData.TileEvents, Is.Empty);

            var parallelWorldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, destroyCell) });
            SetPlayerContinuousLocalOffset(parallelWorldState, 0, 0, speed);
            var parallelPipeline = CreatePipelineWithCollisionRadius(
                parallelWorldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var parallelResult = parallelPipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var parallelSnapshot = parallelWorldState.CreateSnapshot();

            Assert.That(parallelSnapshot.TryGetEntity(10, out var parallelPlayer), Is.True);
            Assert.That(parallelPlayer.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(parallelSnapshot.TryGetUnitContinuousLocomotionState(10, out var parallelState), Is.True);
            Assert.That(parallelState.localOffset.X.RawValue, Is.EqualTo(0));
            Assert.That(parallelState.localOffset.Y.RawValue, Is.EqualTo(speed));
            Assert.That(parallelResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(parallelResult.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_TargetInactiveDestroyTilePresence_BlocksTransition()
        {
            AssertPlayerFree2DNativeTopologyTransitionTargetTileFeaturePresenceBlocks(
                CreateDestroyTile,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_TargetActiveDestroyTilePresence_BlocksTransition()
        {
            AssertPlayerFree2DNativeTopologyTransitionTargetTileFeaturePresenceBlocks(
                CreateDestroyTile,
                TileFeatureActivationRule.ActiveFaceOnly);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_BottomToBackNativeTopologyTransitionTargetActiveDestroyTile_EmitsTopologyBlockedSignal()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Back, 0, 1);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, targetCell) });
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MinLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(
                worldState,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.ActiveFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Down)));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Free2DTopologyNativeRejected") &&
                    reason.Contains("TargetFaceBlockedByTileFeature")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            AssertBottomToBackBlockedSignal(
                result,
                FaceId.Floor,
                sourceCell,
                targetCell,
                TickTraversalBlockerKind.TileFeature);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_AirDestroyTileTargetPresence_BlocksTransition()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell, unitMobilityKind: UnitMobilityKind.Air) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateDestroyTile(100, targetCell) });
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(
                worldState,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.ActiveFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            Assert.That(player.position, Is.EqualTo(sourceCell));
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(player.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(player.markedForDeath, Is.False);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DTopologyNativeRejected") &&
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Reason=PlayerVoluntaryDestroyTileEntryBlocked")), Is.False);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Kind == FinalizationOperationKind.SetTopology ||
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_UnrelatedDestroyOrBarricadePresence_DoesNotBlock()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor),
                new[]
                {
                    CreateDestroyTile(100, new SurfaceCell(FaceId.Front, 1, 0)),
                    CreateBarricade(101, new SurfaceCell(FaceId.Back, 0, 0)),
                });
            SetPlayerContinuousLocalOffset(worldState, 0, SimulationFixed.MaxPositiveLocalOffset, speed);
            var pipeline = CreateDefaultGameplayPipeline(
                worldState,
                new[]
                {
                    CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly),
                    CreateTileFeatureDefinition(101, TileFeatureActivationRule.BottomFaceOnly),
                });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(targetCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_FootprintInactiveBarricadePresence_DoesNotBlockByTopologyPresenceRule()
        {
            AssertPlayerFree2DNativeTopologyTransitionFootprintTileFeatureDoesNotBlockByPresenceRule(
                CreateBarricade,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_FootprintInactiveDestroyTilePresence_DoesNotBlockByTopologyPresenceRule()
        {
            AssertPlayerFree2DNativeTopologyTransitionFootprintTileFeatureDoesNotBlockByPresenceRule(
                CreateDestroyTile,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Core")]
        public void PlayerFree2D_NativeTopologyTransition_FootprintActiveBarricadeMayBlockByExistingLegality_NotPresenceRule()
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { CreateBarricade(100, new SurfaceCell(FaceId.Front, 1, 0)) });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.HalfCellUnits - radius + 1, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, TileFeatureActivationRule.BottomFaceOnly) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(sourceCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DTopologyNativeRejected") &&
                reason.Contains("TargetFaceFootprintBlocked")), Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TargetFaceBlockedByTileFeature")), Is.False);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
        }

        private static void AssertPlayerFree2DNativeTopologyTransitionFootprintTileFeatureDoesNotBlockByPresenceRule(
            Func<int, SurfaceCell, TileFeatureState> createTileFeature,
            TileFeatureActivationRule activationRule)
        {
            const int radius = SimulationFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var footprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { createTileFeature(100, footprintNeighbor) });
            SetPlayerContinuousLocalOffset(worldState, SimulationFixed.HalfCellUnits - radius + 1, sourceThresholdY - speed + 1, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                radiusCells,
                new[] { CreateTileFeatureDefinition(100, activationRule) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(targetCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("TargetFaceBlockedByTileFeature") ||
                reason.Contains("TargetFaceFootprintBlocked")), Is.False);
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusApproachBoxNegative_ClampsBeforeBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, -1, 0)));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Left)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(-1280));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusApproachWall_ClampsBeforeBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusApproachSolid_ClampsBeforeBoundary()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateWall(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusApproachTopologyEdge_ClampsBeforeBoundary()
        {
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(0, 0));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_RadiusClampedNonZero_CrossesNatively()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, 1280);
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_Radius_FreeNeighborStillNormalizesAtHalfBoundary()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_Radius_UnitOverlapStillNormalizesAnchor()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(20, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_RadiusClampedLocalNonZero_PushFlipRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            pipeline.RunTick(new TickInput(11, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_PushQueuedAtLocalNonZero()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var pushResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.kind, Is.EqualTo(PlayerQueuedFree2DActionKind.Push));
            Assert.That(controlState.queuedFree2DAction.direction, Is.EqualTo(Direction.Right));
            Assert.That(controlState.queuedFree2DAction.requestedTick, Is.EqualTo(1));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.AlignToAnchor));
            Assert.That(pushResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistQueued") &&
                reason.Contains("Kind=Push")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_EmptyFloorWithinSettleWindow_PushDoesNotQueueOrAlign()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_EmptyFloorWithinSettleWindow_FlipDoesNotQueueOrAlign()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_NoCandidatePushWithHeldMove_EmitsFakeAttemptAndConsumesMovement()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(
                1,
                PlayerTickCommand.Push(Direction.Right, heldMoveDirection: Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].ActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.NoTarget));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_NoCandidateFlipWithHeldMove_EmitsFakeAttemptAndConsumesMovement()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(
                1,
                PlayerTickCommand.Flip(Direction.Right, heldMoveDirection: Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].ActionKind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.NoTarget));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_NoActionCandidate_EmitsDeterministicRejectTrace()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));

            Assert.That(result.MovementPhaseResult.RejectedReasons.Count(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate") &&
                reason.Contains("Source=10") &&
                reason.Contains("Kind=Push") &&
                reason.Contains("Direction=Right") &&
                reason.Contains("Anchor=(0,0)") &&
                reason.Contains("Offset=(512,0)")), Is.EqualTo(1));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistQueued")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void GridPush_NoTargetWithHeldMove_EmitsFakeAttemptAndDoesNotMove()
        {
            var startCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(CreatePlayer(10, startCell));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(
                1,
                PlayerTickCommand.Push(Direction.Right, heldMoveDirection: Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(startCell));
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(result.MovementPhaseResult.RawIntents, Is.Empty);
            Assert.That(result.PresentationData.EntityMotions, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].ActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].Direction, Is.EqualTo(Direction.Right));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].FeedbackKind, Is.EqualTo(PlayerActionAttemptFeedbackKind.NoTarget));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_BoxWithoutPushCapability_DoesNotQueueOrAlign()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0)));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_AlignsToAnchorWithoutSnap()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var before = worldState.CreateSnapshot();
            Assert.That(before.TryGetUnitContinuousLocomotionState(10, out var clampedState), Is.True);
            Assert.That(clampedState.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(
                clampedState.localOffset.X.RawValue,
                Is.LessThanOrEqualTo(PlayerContinuousLocomotionSettings.CreateDefault()
                    .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                    .ActionAssistSettleWindowUnits));

            pipeline.RunTick(new TickInput(3, PlayerTickCommand.Push(Direction.Right)));
            var after = worldState.CreateSnapshot();

            Assert.That(after.TryGetUnitContinuousLocomotionState(10, out var aligningState), Is.True);
            Assert.That(aligningState.localOffset.X.RawValue, Is.GreaterThan(0));
            Assert.That(aligningState.localOffset.X.RawValue, Is.LessThan(clampedState.localOffset.X.RawValue));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_PushExecutesAfterAlign()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var settledTick = RunUntilSettledWithoutAction(pipeline, worldState, firstTick: 2);
            var executeResult = pipeline.RunTick(new TickInput(settledTick + 1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(executeResult.PresentationData.PlayerActionSignals.Any(signal =>
                signal.EntityId == 10 &&
                signal.ActiveActionKind == PlayerActionKind.Push &&
                signal.StartedThisTick), Is.True);
            Assert.That(executeResult.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            MovementExecutionOwnershipAssert.NoGenericExpansionOrdinaryUnitMove(executeResult, 10);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_FlipExecutesAfterAlign()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var settledTick = RunUntilSettledWithoutAction(pipeline, worldState, firstTick: 2);
            var executeResult = pipeline.RunTick(new TickInput(settledTick + 1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(executeResult.PresentationData.PlayerActionAttemptSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_NormalMoveDirectionChange_CommitsFacingImmediately()
        {
            var worldState = CreateWorldState(CreatePlayer(10, facing: Direction.Left));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.facing, Is.EqualTo(Direction.Up));
            Assert.That(result.PresentationData.PlayerFlipResultTurnSignals, Is.Empty);
            Assert.That(
                result.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.DestinationFacing == Direction.Up),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_FlipLeft_StartsFacingLeftAndEndsFacingRight()
        {
            AssertImmediateFlipResultFacing(
                Direction.Left,
                Direction.Right,
                new SurfaceCell(FaceId.Floor, -1, 0));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_FlipRight_StartsFacingRightAndEndsFacingLeft()
        {
            AssertImmediateFlipResultFacing(
                Direction.Right,
                Direction.Left,
                new SurfaceCell(FaceId.Floor, 1, 0));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_FlipUp_StartsFacingUpAndEndsFacingDown()
        {
            AssertImmediateFlipResultFacing(
                Direction.Up,
                Direction.Down,
                new SurfaceCell(FaceId.Floor, 0, 1));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_FlipDown_StartsFacingDownAndEndsFacingUp()
        {
            AssertImmediateFlipResultFacing(
                Direction.Down,
                Direction.Up,
                new SurfaceCell(FaceId.Floor, 0, -1));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_QueuedFlip_StartsFacingActionDirectionAndEndsFacingOpposite()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, facing: Direction.Left),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var queuedResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var settledTick = RunUntilSettledWithoutAction(pipeline, worldState, firstTick: 2);
            var startTick = settledTick + 1;
            var startResult = pipeline.RunTick(new TickInput(startTick, PlayerTickCommand.None));
            var startSnapshot = worldState.CreateSnapshot();

            Assert.That(queuedResult.PresentationData.PlayerFlipResultTurnSignals, Is.Empty);
            Assert.That(queuedResult.Trace.Text, Does.Contain("Free2DActionAssistQueued"));
            Assert.That(startResult.Trace.Text, Does.Contain("Free2DActionAssistExecute"));
            Assert.That(startSnapshot.TryGetEntity(10, out var startPlayer), Is.True);
            Assert.That(startPlayer.facing, Is.EqualTo(Direction.Right));
            AssertFlipResultTurn(
                startResult,
                Direction.Right,
                Direction.Right,
                Direction.Left,
                PlayerFlipResultTurnStartReason.QueuedFlip);

            var executeResult = pipeline.RunTick(new TickInput(startTick + 1, PlayerTickCommand.None));
            Assert.That(executeResult.Trace.Text, Does.Contain("FlipResultFacingCommitted"));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out var executedPlayer), Is.True);
            Assert.That(executedPlayer.facing, Is.EqualTo(Direction.Left));

            pipeline.RunTick(new TickInput(startTick + 2, PlayerTickCommand.None));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out var completedPlayer), Is.True);
            Assert.That(completedPlayer.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_FlipResultTurn_WithKpo_UsesKpoPositionAndResultTurnRotation()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, facing: Direction.Left),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Flip));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var queuedResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var settledTick = RunUntilSettledWithoutAction(pipeline, worldState, firstTick: 2);
            var executeResult = pipeline.RunTick(new TickInput(settledTick + 1, PlayerTickCommand.None));

            Assert.That(queuedResult.PresentationData.ContinuousLocomotionTracks, Is.Not.Empty);
            Assert.That(executeResult.PresentationData.ContinuousLocomotionTracks, Is.Empty);
            AssertFlipResultTurn(
                executeResult,
                Direction.Right,
                Direction.Right,
                Direction.Left,
                PlayerFlipResultTurnStartReason.QueuedFlip);
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_StationaryFlipWithoutDirection_DropsOrUsesConfiguredFallback()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, facing: Direction.Left),
                CreateBox(20, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Flip));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerFlipResultTurnSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_PlayerS1Prefab_DefaultGameplayLocomotion_PushLeft_DoesNotUseFlipOppositeFacingPolicy()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, facing: Direction.Up),
                CreateBox(20, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Push));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Left)));
            var startSnapshot = worldState.CreateSnapshot();

            Assert.That(startSnapshot.TryGetEntity(10, out var startPlayer), Is.True);
            Assert.That(startPlayer.facing, Is.EqualTo(Direction.Left));
            Assert.That(startResult.PresentationData.PlayerFlipResultTurnSignals, Is.Empty);
            Assert.That(startResult.PresentationData.PlayerActionSignals.Single().ActiveActionKind, Is.EqualTo(PlayerActionKind.Push));

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            pipeline.RunTick(new TickInput(3, PlayerTickCommand.None));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(10, out var finalPlayer), Is.True);
            Assert.That(finalPlayer.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_BoxRadiusClampThenPush()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreateActionAssistPipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            MoveRightToRadiusClamp(pipeline);
            var clampedSnapshot = worldState.CreateSnapshot();
            Assert.That(clampedSnapshot.TryGetUnitContinuousLocomotionState(10, out var clampedState), Is.True);
            Assert.That(clampedState.localOffset.X.RawValue, Is.EqualTo(1280));

            var pushResult = pipeline.RunTick(new TickInput(11, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.False);
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(1280));
            Assert.That(pushResult.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=OutsideSettleWindow")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_WithinSettleWindow_QueuesAndAligns()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 512);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.kind, Is.EqualTo(PlayerQueuedFree2DActionKind.Push));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Is.Empty);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.AlignToAnchor));
            Assert.That(state.localOffset.X.RawValue, Is.LessThan(512));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(512));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_OutsideSettleWindow_DoesNotQueueOrAlign()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 513, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(513));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=OutsideSettleWindow")), Is.True);
            Assert.That(result.PresentationData.PlayerActionSignals, Is.Empty);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].ActionKind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(
                result.PresentationData.PlayerActionAttemptSignals[0].FeedbackKind,
                Is.EqualTo(PlayerActionAttemptFeedbackKind.AssistOutOfRange));
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].HasTarget, Is.True);
            Assert.That(result.PresentationData.PlayerActionAttemptSignals[0].TargetEntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_WindowBoundaryInclusive()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.AlignToAnchor));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_WindowBoundaryExclusiveAbove()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 513, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(513));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_ExistingQueue_IgnoresWindowAndContinuesAlign()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 1024, localY: 0);
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                PlayerControlQueries.QueueFree2DAction(
                    default,
                    PlayerQueuedFree2DActionKind.Push,
                    Direction.Right,
                    requestedTick: 7));
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.AlignToAnchor));
            Assert.That(state.localOffset.X.RawValue, Is.LessThan(1024));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_ExistingQueue_NoCandidateClearsWithoutAlign()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                PlayerControlQueries.QueueFree2DAction(
                    default,
                    PlayerQueuedFree2DActionKind.Push,
                    Direction.Right,
                    requestedTick: 7));
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistCleared") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistAlign")), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyHandoff_ActionAssistQueued_DoesNotHandoff()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, 512);
            worldState.CreateWriteContext().SetPlayerControlState(
                10,
                PlayerControlQueries.QueueFree2DAction(
                    default,
                    PlayerQueuedFree2DActionKind.Push,
                    Direction.Up,
                    requestedTick: 1));
            var pipeline = CreateActionAssistPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology),
                Is.False);
            MovementExecutionOwnershipAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_InvalidAfterAlign_ClearsQueue()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            worldState.CreateWriteContext().RemoveEntity(20);
            var rejectedResult = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(rejectedResult.Trace.Text, Does.Contain("Free2DActionAssistRejected"));
            Assert.That(rejectedResult.Trace.Text, Does.Contain("Free2DActionAssistCleared"));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_ActionTargetRevalidatedAtExecute()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            worldState.CreateWriteContext().RemoveEntity(20);
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_MovementInputDoesNotCancelQueue()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var queuedSnapshot = worldState.CreateSnapshot();
            Assert.That(queuedSnapshot.TryGetUnitContinuousLocomotionState(10, out var queuedState), Is.True);

            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.kind, Is.EqualTo(PlayerQueuedFree2DActionKind.Push));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var aligningState), Is.True);
            Assert.That(aligningState.localOffset.X.RawValue, Is.LessThan(queuedState.localOffset.X.RawValue));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_HitClearsQueue()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipelineWithCollisionRadius(
                worldState,
                0.1875f,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var interruptedState), Is.True);
            Assert.That(interruptedState.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_DeathClearsQueue()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, hp: 1),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipelineWithCollisionRadius(
                worldState,
                0.1875f,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            pipeline.RunTick(new TickInput(3, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.IsActive, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_LocalZero_PushStillImmediate()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_LocalNonZero_ActionNotExecutedBeforeSettled()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            SetPlayerContinuousLocalOffset(worldState, localX: 512, localY: 0);
            var pipeline = CreateActionAssistPipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            for (var tick = 2; tick <= 6; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
                Assert.That(controlState.activeAction.IsActive, Is.False);
                if (UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10))
                {
                    return;
                }
            }

            Assert.Fail("Action assist did not settle within the expected tick window.");
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_FlagOff_Baseline()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            MoveRightToRadiusClamp(pipeline);
            pipeline.RunTick(new TickInput(11, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.IsActive, Is.False);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_Disabled_DoesNotQueueActionDuringFree2DMovement()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_Radius_PassiveContactRemainsAnchorBased()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipelineWithCollisionRadius(
                worldState,
                0.1875f,
                new TickGatedPassiveContactProbeLogic(40, 10, firstTick: 9));

            TickResult result = null;
            for (var tick = 1; tick <= 9; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.False);

            result = pipeline.RunTick(new TickInput(10, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_BeforeAnchorBoundary_NoEnemyContact()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new SameCellPassiveContactProbeLogic(40, 10));

            TickResult result = null;
            for (var tick = 1; tick <= 9; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.LessThan(SimulationFixed.HalfCellUnits));
            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_AfterAnchorBoundary_EnemyContactPossible()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 1, 0), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickGatedPassiveContactProbeLogic(40, 10, firstTick: 10));

            TickResult result = null;
            for (var tick = 1; tick <= 10; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(HasAcceptedPassiveContact(result, 40, 10), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalZero_PushFlipAllowed()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10), Is.True);
            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Push));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
            Assert.That(result.PresentationData.PlayerActionSignals.Any(signal =>
                signal.EntityId == 10 &&
                signal.ActiveActionKind == PlayerActionKind.Push &&
                signal.StartedThisTick), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_LocalNonZero_ActionPreviewRejected()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxCapabilities.Push));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                UnitSpatialQuery.TryResolveSettledProbeCell(snapshot, 10, Direction.Right, out var result),
                Is.False);
            Assert.That(result.RejectedBy, Is.EqualTo(UnitProbeRejectionReason.NotSettledAtAnchor));
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_HitNonlethal_PreservesPose()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var preHitSnapshot = worldState.CreateSnapshot();
            Assert.That(preHitSnapshot.TryGetUnitContinuousLocomotionState(10, out var preHitState), Is.True);
            var hitResult = pipeline.RunTick(new TickInput(2));

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.hp, Is.EqualTo(2));
            Assert.That(hitSnapshot.TryGetUnitContinuousLocomotionState(10, out var interrupted), Is.True);
            Assert.That(interrupted.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(interrupted.velocity.IsZero, Is.True);
            Assert.That(interrupted.localOffset, Is.EqualTo(preHitState.localOffset));
            Assert.That(
                hitResult.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Interrupted &&
                    track.DestinationLocalOffset.Equals(preHitState.localOffset)),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_HitLethal_RemovedTerminalPreservesPose()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, hp: 1),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 0, 1), teamId: 2));
            var pipeline = CreatePipeline(
                worldState,
                new TickScriptedAttackLogic(40, 10, attackTick: 2));

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var preHitSnapshot = worldState.CreateSnapshot();
            Assert.That(preHitSnapshot.TryGetUnitContinuousLocomotionState(10, out var preHitState), Is.True);
            var hitResult = pipeline.RunTick(new TickInput(2));

            var hitSnapshot = worldState.CreateSnapshot();
            Assert.That(hitSnapshot.TryGetEntity(10, out _), Is.False);
            Assert.That(hitSnapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(
                hitResult.EventLog.Any(entry =>
                    entry.Contains("ContinuousLocomotionPoseRemoved|E=10") &&
                    entry.Contains($"Offset=({preHitState.localOffset.X.RawValue},{preHitState.localOffset.Y.RawValue})")),
                Is.True);
            Assert.That(
                hitResult.PresentationData.ContinuousLocomotionTracks.Any(track =>
                    track.EntityId == 10 &&
                    track.TerminalKind == TickKinematicMotionTerminalKind.Removed &&
                    track.DestinationLocalOffset.Equals(preHitState.localOffset)),
                Is.True);
            Assert.That(
                hitResult.PresentationData.PlayerDeathHoldSignals.Any(signal =>
                    signal.EntityId == 10 &&
                    signal.StartedThisTick),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_Unconditional_DoesNotFallbackToPlayerKinematic()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            Assert.That(
                result.PresentationData.EntityMotions.Any(motion =>
                    motion.EntityId == 10 &&
                    motion.MotionKind == TickEntityMotionKind.Move),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 10),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_DoesNotAffectEnemyOrCharge()
        {
            var worldState = CreateWorldState(
                CreatePlayer(10),
                CreateUnit(40, new SurfaceCell(FaceId.Floor, 2, 0), teamId: 2));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(40, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(40, out var enemy), Is.True);
            Assert.That(enemy.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return CreatePipeline(worldState, extraLogics: null);
        }

        private static TickPipeline CreatePipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static TickPipeline CreateDefaultGameplayPipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
        }

        private static TickPipeline CreateDefaultGameplayPipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static TickPipeline CreateNativeTopologyPipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None);
        }

        private static TickPipeline CreatePipelineWithCollisionRadius(
            WorldState worldState,
            float collisionRadiusCells,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond));
        }

        private static TickPipeline CreatePipelineWithCollisionRadius(
            WorldState worldState,
            float collisionRadiusCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static TickPipeline CreateNativeTopologyPipelineWithCollisionRadius(
            WorldState worldState,
            float collisionRadiusCells,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond));
        }

        private static TickPipeline CreateNativeTopologyPipelineWithCollisionRadius(
            WorldState worldState,
            float collisionRadiusCells,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateDefaultBootstrapper().CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.None,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static TickPipeline CreateActionAssistPipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled);
        }

        private static TickPipeline CreateActionAssistPipelineWithCollisionRadius(
            WorldState worldState,
            float collisionRadiusCells,
            params IEntityLogic[] extraLogics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DActionAssistEnabled,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond));
        }

        private static void SetPlayerContinuousLocalOffset(
            WorldState worldState,
            int localX,
            int localY,
            int speedUnitsPerTick = 0)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                10,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(localX),
                        SimulationFixed.FromRaw(localY)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Right,
                    lastMoveDirection = Direction.Right,
                    speedUnitsPerTick = speedUnitsPerTick,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
        }

        private static int DefaultFree2DSpeedUnitsPerTick()
        {
            return PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
        }

        private static int BoundaryFootprintRadiusUnits()
        {
            return SimulationFixed.UnitsPerCell * 3 / 16;
        }

        private static float BoundaryFootprintRadiusCells()
        {
            return BoundaryFootprintRadiusUnits() / (float)SimulationFixed.UnitsPerCell;
        }

        private static int BoundaryRadiusSpeedUnitsPerTick()
        {
            return DefaultFree2DSpeedUnitsPerTick();
        }

        private static void SetBoundaryFootprintPose(WorldState worldState, int localX, int radiusUnits)
        {
            var speed = BoundaryRadiusSpeedUnitsPerTick();
            SetPlayerContinuousLocalOffset(
                worldState,
                localX,
                SimulationFixed.HalfCellUnits - radiusUnits - speed + 1,
                speed);
        }

        private static Free2DTopologyRemapResult AssertBottomToFrontBoundaryRemap(
            int localX,
            int radiusUnits,
            int speedUnitsPerTick)
        {
            var configuredRadius = new PlayerContinuousLocomotionSettings
            {
                CollisionRadiusCells = BoundaryFootprintRadiusCells(),
            }.CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond).CollisionRadiusUnits;
            Assert.That(configuredRadius, Is.EqualTo(radiusUnits));

            var sourceThresholdY = SimulationFixed.HalfCellUnits - radiusUnits;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                new SimulationOffset2(
                    SimulationFixed.FromRaw(localX),
                    SimulationFixed.FromRaw(sourceThresholdY - speedUnitsPerTick + 1)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(speedUnitsPerTick)),
                radiusUnits,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(remap.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(localX));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset + radiusUnits + 1));
            Assert.That(remap.TargetVelocity.X.RawValue, Is.Zero);
            Assert.That(remap.TargetVelocity.Y.RawValue, Is.EqualTo(speedUnitsPerTick));
            return remap;
        }

        private static void AssertBottomToFrontNativeTransitionSucceeded(
            WorldSnapshot snapshot,
            TickResult result,
            SurfaceCell targetCell,
            int expectedLocalX)
        {
            var radius = BoundaryFootprintRadiusUnits();

            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(targetCell));
            Assert.That(snapshot.TryGetSolidOccupantAt(snapshot.Topology, targetCell, out _), Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(expectedLocalX));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset + radius + 1));
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.MoveEntity &&
                    operation.EntityId == 10 &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition &&
                    operation.Metadata.BoundaryReason == "Free2DTopologyNativeTransition"),
                Is.True);
            Assert.That(result.PresentationData.TopologyMotion.HasValue, Is.True);
            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Is.Empty);
        }

        private static void AssertBottomToFrontNativeTransitionBlockedByFootprint(
            WorldSnapshot snapshot,
            TickResult result,
            SurfaceCell sourceCell)
        {
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(sourceCell));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Free2DTopologyNativeRejected") &&
                    reason.Contains("TargetFaceFootprintBlocked")),
                Is.True,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
            Assert.That(result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology ||
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.False);
            Assert.That(result.PresentationData.TopologyMotion.HasValue, Is.False);
        }

        private static void AssertNoTargetFootprintOrTileFeatureReject(TickResult result)
        {
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("TargetFaceFootprintBlocked") ||
                    reason.Contains("TargetFaceBlockedByTileFeature")),
                Is.False,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
        }

        private static void AssertBottomToFrontSourceFeatureDoesNotProjectOneBeyond(
            Func<int, SurfaceCell, TileFeatureState> createTileFeature,
            TileFeatureActivationRule activationRule)
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneBeyondX = SimulationFixed.HalfCellUnits - radius + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceContactCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var targetFootprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, sourceCell),
                    CreateBox(20, targetFootprintNeighbor),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                new[] { createTileFeature(100, sourceContactCell) });
            SetBoundaryFootprintPose(worldState, oneBeyondX, radius);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(
                worldState,
                BoundaryFootprintRadiusCells(),
                new[] { CreateTileFeatureDefinition(100, activationRule) });

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(oneBeyondX + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            AssertBottomToFrontNativeTransitionBlockedByFootprint(snapshot, result, sourceCell);
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("TargetFaceBlockedByTileFeature")),
                Is.False,
                string.Join(";", result.MovementPhaseResult.RejectedReasons));
        }

        private static void AssertBottomToBackBlockedSignal(
            TickResult result,
            FaceId sourceBottomFace,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            TickTraversalBlockerKind blockerKind)
        {
            var visualBackFace = FaceIdUtility.GetPrevious(sourceBottomFace);

            Assert.That(result.PresentationData.PlayerTopologyTransitionBlockedSignals, Has.Count.EqualTo(1));
            var signal = result.PresentationData.PlayerTopologyTransitionBlockedSignals[0];
            Assert.That(signal.EntityId, Is.EqualTo(10));
            Assert.That(signal.Direction, Is.EqualTo(Direction.Down));
            Assert.That(signal.OriginCell, Is.EqualTo(sourceCell));
            Assert.That(signal.CandidateCell, Is.EqualTo(targetCell));
            Assert.That(signal.SourceTopology, Is.EqualTo(new CubeTopologyState(sourceBottomFace)));
            Assert.That(signal.RequiredTopology, Is.EqualTo(new CubeTopologyState(visualBackFace)));
            Assert.That(signal.RotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(signal.PrimaryBlockerKind, Is.EqualTo(blockerKind));
        }

        private static void MoveRightToRadiusClamp(TickPipeline pipeline)
        {
            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }
        }

        private static int RunUntilSettledWithoutAction(
            TickPipeline pipeline,
            WorldState worldState,
            int firstTick)
        {
            for (var tick = firstTick; tick < firstTick + 30; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
                var snapshot = worldState.CreateSnapshot();
                Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
                Assert.That(controlState.activeAction.IsActive, Is.False);
                if (UnitSpatialQuery.IsSettledAtAnchor(snapshot, 10))
                {
                    return tick;
                }
            }

            Assert.Fail("Action assist align did not reach local-zero settled pose.");
            return -1;
        }

        private static void AssertImmediateFlipResultFacing(
            Direction actionDirection,
            Direction resultFacing,
            SurfaceCell boxCell)
        {
            var worldState = CreateWorldState(
                CreatePlayer(10, facing: Direction.Up),
                CreateBox(20, boxCell, BoxCapabilities.Flip));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(actionDirection)));
            var startSnapshot = worldState.CreateSnapshot();

            Assert.That(startSnapshot.TryGetEntity(10, out var startPlayer), Is.True);
            Assert.That(startPlayer.facing, Is.EqualTo(actionDirection));
            Assert.That(startResult.PresentationData.PlayerActionSignals.Single().StartedThisTick, Is.True);
            AssertFlipResultTurn(
                startResult,
                actionDirection,
                actionDirection,
                resultFacing,
                PlayerFlipResultTurnStartReason.ImmediateFlip);

            var executeResult = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var executeSnapshot = worldState.CreateSnapshot();

            Assert.That(executeResult.Trace.Text, Does.Contain("FlipResultFacingCommitted"));
            Assert.That(executeSnapshot.TryGetEntity(10, out var executedPlayer), Is.True);
            Assert.That(executedPlayer.facing, Is.EqualTo(resultFacing));

            pipeline.RunTick(new TickInput(3, PlayerTickCommand.None));
            var completedSnapshot = worldState.CreateSnapshot();

            Assert.That(completedSnapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.None));
            Assert.That(completedSnapshot.TryGetEntity(10, out var completedPlayer), Is.True);
            Assert.That(completedPlayer.facing, Is.EqualTo(resultFacing));
        }

        private static void AssertFlipResultTurn(
            TickResult result,
            Direction actionDirection,
            Direction contactFacing,
            Direction resultFacing,
            PlayerFlipResultTurnStartReason reason)
        {
            Assert.That(result.PresentationData.PlayerFlipResultTurnSignals, Has.Count.EqualTo(1));
            var signal = result.PresentationData.PlayerFlipResultTurnSignals[0];
            Assert.That(signal.EntityId, Is.EqualTo(10));
            Assert.That(signal.ActionDirection, Is.EqualTo(actionDirection));
            Assert.That(signal.ContactFacing, Is.EqualTo(contactFacing));
            Assert.That(signal.ResultFacing, Is.EqualTo(resultFacing));
            Assert.That(signal.StartTick, Is.EqualTo(result.TickIndex));
            Assert.That(signal.Reason, Is.EqualTo(reason));
        }

        private static IEntityLogic[] CreatePlayerLogics(params IEntityLogic[] extraLogics)
        {
            return new IEntityLogic[]
            {
                new PlayerLogic(10),
                new PlayerControlStateLogic(10),
            }.Concat(extraLogics ?? Enumerable.Empty<IEntityLogic>()).ToArray();
        }

        private static WorldState CreateWorldState(params EntityState[] entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, topology);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            CubeTopologyState topology,
            IEnumerable<TileFeatureState> tileFeatures)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities,
                boardBounds,
                topology,
                GameplayTimingProfile.CreateDefault(),
                tileFeatures);
        }

        private static bool HasAcceptedPassiveContact(TickResult result, int sourceId, int targetId)
        {
            return result.AttackPhaseResult.DamageResolutions.Any(
                record => record.Accepted &&
                          record.SourceId == sourceId &&
                          record.TargetId == targetId &&
                          record.SourceKind == AttackSourceKind.PassiveContact);
        }

        private static EntityState CreatePlayer(
            int entityId,
            int hp = 3,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return CreatePlayer(entityId, new SurfaceCell(FaceId.Floor, 0, 0), hp, facing, unitMobilityKind);
        }

        private static EntityState CreatePlayer(
            int entityId,
            SurfaceCell position,
            int hp = 3,
            Direction facing = Direction.Right,
            UnitMobilityKind unitMobilityKind = UnitMobilityKind.Ground)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                unitMobilityKind = unitMobilityKind,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static TileFeatureState CreateDestroyTile(int tileId, SurfaceCell cell)
        {
            return new TileFeatureState(
                tileId,
                cell,
                TileFeatureKind.Destroy,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureState CreateBarricade(int tileId, SurfaceCell cell)
        {
            return new TileFeatureState(
                tileId,
                cell,
                TileFeatureKind.Barricade,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateTileFeatureDefinition(
            int tileId,
            TileFeatureActivationRule activationRule)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                activationRule,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: string.Empty);
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

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return CreateBox(entityId, position, BoxCapabilities.None);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxCapabilities capabilities)
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
                List<RawAttackIntent> buffer)
            {
                if (input.TickIndex == _attackTick)
                {
                    buffer.Add(new RawAttackIntent(_sourceId, priority: 50, _targetId));
                }
            }
        }

        private sealed class SameCellPassiveContactProbeLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _sourceId;
            private readonly int _targetId;

            public SameCellPassiveContactProbeLogic(int sourceId, int targetId)
            {
                _sourceId = sourceId;
                _targetId = targetId;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                buffer.Add(new RawAttackIntent(
                    _sourceId,
                    priority: 50,
                    _targetId,
                    AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }

        private sealed class TickGatedPassiveContactProbeLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly int _firstTick;
            private readonly int _sourceId;
            private readonly int _targetId;

            public TickGatedPassiveContactProbeLogic(int sourceId, int targetId, int firstTick)
            {
                _sourceId = sourceId;
                _targetId = targetId;
                _firstTick = firstTick;
            }

            public int ControlledEntityId => _sourceId;

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                if (input.TickIndex < _firstTick)
                {
                    return;
                }

                buffer.Add(new RawAttackIntent(
                    _sourceId,
                    priority: 50,
                    _targetId,
                    AttackSourceKind.PassiveContact,
                    localSequence: 1));
            }
        }
    }
}
