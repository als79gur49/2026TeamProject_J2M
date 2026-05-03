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
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

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
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(result, 10);
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
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback),
                Is.False);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
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
            Assert.That(idle.velocity, Is.EqualTo(KinematicVelocity2.Zero));
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
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(result, 10);
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
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
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
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(
                result.MovementPhaseResult.RejectedReasons.Any(reason =>
                    reason.Contains("Free2DContinuousBlocked") &&
                    reason.Contains("TraversalBlocked")),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TerrainClamp()
        {
            var terrain = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        TerrainKind.Generic,
                        TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                terrain);
            var pipeline = CreatePipeline(worldState);

            for (var tick = 1; tick <= 10; tick++)
            {
                pipeline.RunTick(new TickInput(tick, PlayerTickCommand.Move(Direction.Right)));
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Idle));
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_LocalZero_HandsOffToTopologyGridTransaction()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.Not.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(snapshot.Topology.BottomFace, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.False);
            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.False);
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                result,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology &&
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.TopologyMaterialization),
                Is.True);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyEdge_ApproachFromInterior_HandsOffToTopologyGridTransaction()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            TickResult result = null;
            for (var tick = 1; tick <= 20; tick++)
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
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                result,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Player_Free2D_TopologyEdge_CenterCrossing_SameTickHandoff()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, -DefaultFree2DSpeedUnitsPerTick());
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.Not.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(snapshot.Topology.BottomFace, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                result,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_SeamClampedNonZero_SettlesThenNextTickHandoff()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, KinematicFixed.MaxPositiveLocalOffset);
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var settleTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));
            var settledSnapshot = worldState.CreateSnapshot();
            Assert.That(settledSnapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(settledSnapshot.TryGetEntity(10, out var settledPlayer), Is.True);
            Assert.That(settledPlayer.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(settledSnapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(
                settleTick.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology),
                Is.False);

            var handoffTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.Move(Direction.Up)));
            var handoffSnapshot = worldState.CreateSnapshot();
            Assert.That(handoffSnapshot.Topology, Is.Not.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(handoffSnapshot.TryGetEntity(10, out var handoffPlayer), Is.True);
            Assert.That(handoffPlayer.position, Is.EqualTo(new SurfaceCell(handoffSnapshot.Topology.BottomFace, 0, 0)));
            LegacyMovementBoundaryAssert.GridTransactionBranchesRemainAllowed(
                handoffTick,
                10,
                MovementExecutionBoundaryKind.TopologyMaterialization);
            Assert.That(handoffTick.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void Player_Free2D_TopologyEdge_LocalNonZero_ClampsOrRejects()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 256, KinematicFixed.MaxPositiveLocalOffset);
            var pipeline = CreateDefaultGameplayPipeline(worldState);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(256));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology),
                Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.BoundaryReason == "PlayerFree2DTopologyApproachSettle"),
                Is.False);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_LateralProgressThenForward_CrossesFace()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            var speed = DefaultFree2DSpeedUnitsPerTick();
            SetPlayerContinuousLocalOffset(worldState, 256, KinematicFixed.MaxPositiveLocalOffset, speed);
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
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset + speed - KinematicFixed.UnitsPerCell));
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
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.LegacyFallback),
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
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(
                worldState,
                -384,
                KinematicFixed.MaxPositiveLocalOffset,
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
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(
                worldState,
                384,
                KinematicFixed.MaxPositiveLocalOffset,
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
            const int radius = KinematicFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = KinematicFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 512, sourceThresholdY - speed, speed);
            var pipeline = CreateNativeTopologyPipelineWithCollisionRadius(worldState, radiusCells);
            var preSnapshot = worldState.CreateSnapshot();
            Assert.That(preSnapshot.TryGetUnitContinuousLocomotionState(10, out var preState), Is.True);
            Assert.That(preState.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(preState.localOffset.Y.RawValue + speed, Is.EqualTo(sourceThresholdY));
            Assert.That(preState.localOffset.Y.RawValue + speed, Is.LessThan(KinematicFixed.HalfCellUnits));

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out var state), Is.True);
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(state.localOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset + radius));
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.MovementExecutionBoundaryKind == MovementExecutionBoundaryKind.Free2DTopologyTransition),
                Is.True);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.BoundaryReason == "PlayerFree2DTopologyApproachSettle"),
                Is.False);
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
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 256, KinematicFixed.MaxPositiveLocalOffset, DefaultFree2DSpeedUnitsPerTick());
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
            Assert.That(result.PresentationData.ContinuousLocomotionTracks.Any(track => track.EntityId == 10), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopology_TargetFaceFootprintBlocked_DoesNotApproachSettle()
        {
            const int radius = KinematicFixed.UnitsPerCell * 3 / 16;
            const float radiusCells = 0.1875f;
            var speed = DefaultFree2DSpeedUnitsPerTick();
            var sourceThresholdY = KinematicFixed.HalfCellUnits - radius;
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, KinematicFixed.HalfCellUnits - radius, sourceThresholdY - speed, speed);
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
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Metadata.BoundaryReason == "PlayerFree2DTopologyApproachSettle"),
                Is.False);
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
        public void Player_Free2D_RadiusApproachTerrain_ClampsBeforeBoundary()
        {
            var terrain = new GameplayTerrainData(
                new[]
                {
                    new TerrainCellState(
                        new SurfaceCell(FaceId.Floor, 1, 0),
                        TerrainKind.Generic,
                        TerrainFlags.BlocksGroundTraversal),
                });
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10) },
                terrain);
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
                GameplayTerrainData.Empty,
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
        public void Player_Free2D_TopologyHandoff_RadiusClampedNonZero_DoesNotHandoff()
        {
            var boardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1));
            var worldState = CreateWorldState(
                new[] { CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                boardBounds,
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
            SetPlayerContinuousLocalOffset(worldState, 0, 1280);
            var pipeline = CreatePipelineWithCollisionRadius(worldState, collisionRadiusCells: 0.1875f);

            var result = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Up)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.Topology, Is.EqualTo(new CubeTopologyState(FaceId.Floor)));
            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
            Assert.That(
                result.MovementPhaseResult.ResolvedOperations.Any(operation =>
                    operation.Kind == FinalizationOperationKind.SetTopology),
                Is.False);
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
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
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
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
        public void Free2DActionAssist_NoCandidatePushWithHeldMove_ContinuesFree2DMovement()
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
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Moving));
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(512));
            Assert.That(result.MovementPhaseResult.RejectedReasons.Any(reason =>
                reason.Contains("Free2DActionAssistRejected") &&
                reason.Contains("Reason=NoActionCandidate")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void Free2DActionAssist_NoCandidateFlipWithHeldMove_ContinuesFree2DMovement()
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
            Assert.That(state.mode, Is.EqualTo(ContinuousLocomotionMode.Moving));
            Assert.That(state.localOffset.X.RawValue, Is.GreaterThan(512));
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
            LegacyMovementBoundaryAssert.NoLegacyOrdinaryUnitMove(executeResult, 10);
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
            pipeline.RunTick(new TickInput(settledTick + 1, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(controlState.activeAction.kind, Is.EqualTo(PlayerActionKind.Flip));
            Assert.That(controlState.activeAction.targetEntityId, Is.EqualTo(20));
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

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.kind, Is.EqualTo(PlayerQueuedFree2DActionKind.Push));
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
                GameplayTerrainData.Empty,
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
            LegacyMovementBoundaryAssert.NoUnexpectedLegacyOrdinaryDiagnostics(result);
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
        public void Free2DActionAssist_DoesNotAffectKinematicFallback()
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
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.Push(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetPlayerControlState(10, out var controlState), Is.True);
            Assert.That(controlState.queuedFree2DAction.IsQueued, Is.False);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
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
            Assert.That(state.localOffset.X.RawValue, Is.LessThan(KinematicFixed.HalfCellUnits));
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
            Assert.That(state.localOffset.X.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset));
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
        public void Player_Free2D_FlagOff_ExistingKinematicBaseline()
        {
            var worldState = CreateWorldState(CreatePlayer(10));
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerStoppableKinematicLocomotionEnabled);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Move(Direction.Right)));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetUnitKinematicState(10, out _), Is.True);
            Assert.That(snapshot.TryGetUnitContinuousLocomotionState(10, out _), Is.False);
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
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled);
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

        private static TickPipeline CreateNativeTopologyPipeline(WorldState worldState, params IEntityLogic[] extraLogics)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                CreatePlayerLogics(extraLogics),
                GameplayTimingProfile.CreateDefault(),
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                    GameplayTimingProfile.CreateDefault().RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled);
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
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DLocalLocomotionEnabled,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond));
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
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.PlayerFree2DNativeTopologyTransitionEnabled,
                playerContinuousLocomotion: new PlayerContinuousLocomotionSettings
                {
                    CollisionRadiusCells = collisionRadiusCells,
                }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond));
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
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(localX),
                        KinematicFixed.FromRaw(localY)),
                    velocity = KinematicVelocity2.Zero,
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

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities, GameplayTerrainData terrainData)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, terrainData);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> entities,
            BoardBounds boardBounds,
            GameplayTerrainData terrainData,
            CubeTopologyState topology)
        {
            return GameplayWorldStateTestFactory.CreateBounded(entities, boardBounds, terrainData, topology);
        }

        private static bool HasAcceptedPassiveContact(TickResult result, int sourceId, int targetId)
        {
            return result.AttackPhaseResult.DamageResolutions.Any(
                record => record.Accepted &&
                          record.SourceId == sourceId &&
                          record.TargetId == targetId &&
                          record.SourceKind == AttackSourceKind.PassiveContact);
        }

        private static EntityState CreatePlayer(int entityId, int hp = 3)
        {
            return CreatePlayer(entityId, new SurfaceCell(FaceId.Floor, 0, 0), hp);
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position, int hp = 3)
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
