using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Tests;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WorldSurfaceQueryTests
    {
        [Test]
        public void WorldSnapshot_TryGetUnitAt_IgnoresInactiveFaceOccupant()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                    CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Ceiling, 1, 1)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                GameplayTerrainData.Empty);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out var inactiveEntity), Is.True);
            Assert.That(inactiveEntity.position.face, Is.EqualTo(FaceId.Ceiling));

            Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 1), out var activeOccupant), Is.True);
            Assert.That(activeOccupant.entityId, Is.EqualTo(10));
            Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Ceiling, 1, 1), out _), Is.False);
        }

        [Test]
        public void WorldSnapshot_ExplicitOccupancyQueries_SeparateUnitsAndSolids()
        {
            var unitCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var boxCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var inactiveUnitCell = new SurfaceCell(FaceId.Ceiling, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 10, position: unitCell),
                    CreateUnit(entityId: 20, position: unitCell, teamId: 2),
                    CreateUnit(entityId: 30, position: inactiveUnitCell),
                    CreateBox(entityId: 40, position: boxCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var snapshot = CreateSnapshot(worldState);
            var units = new List<EntityState>();

            Assert.That(snapshot.HasAnyUnitAt(unitCell), Is.True);
            Assert.That(snapshot.TryGetPrimaryUnitAt(unitCell, out var primaryUnit), Is.True);
            Assert.That(primaryUnit.entityId, Is.EqualTo(10));

            snapshot.EnumerateUnitsAt(unitCell, units);
            CollectionAssert.AreEqual(new[] { 10, 20 }, units.ConvertAll(entity => entity.entityId));

            Assert.That(snapshot.HasAnyUnitAt(inactiveUnitCell), Is.False);
            Assert.That(snapshot.TryGetPrimaryUnitAt(inactiveUnitCell, out _), Is.False);

            Assert.That(snapshot.TryGetSolidOccupantAt(unitCell, out _), Is.False);
            Assert.That(snapshot.TryGetBoxAt(unitCell, out _), Is.False);

            Assert.That(snapshot.TryGetSolidOccupantAt(boxCell, out var solidOccupant), Is.True);
            Assert.That(solidOccupant.entityId, Is.EqualTo(40));
            Assert.That(snapshot.TryGetBoxAt(boxCell, out var box), Is.True);
            Assert.That(box.entityId, Is.EqualTo(40));
            Assert.That(snapshot.TryGetPrimaryUnitAt(boxCell, out _), Is.False);
        }

        [Test]
        public void WorldSnapshot_TryPickImpactTargetAt_PrefersHostileThenFallsBackDeterministically()
        {
            var hostileCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var friendlyOnlyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var boxCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 40, position: boxCell),
                    CreateUnit(entityId: 10, position: hostileCell, teamId: 1),
                    CreateUnit(entityId: 20, position: hostileCell, teamId: 2),
                    CreateUnit(entityId: 30, position: hostileCell, teamId: 2),
                    CreateUnit(entityId: 50, position: friendlyOnlyCell, teamId: 1),
                    CreateUnit(entityId: 60, position: friendlyOnlyCell, teamId: 1),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 0)),
                GameplayTerrainData.Empty);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryPickImpactTargetAt(hostileCell, sourceTeamId: 1, out var hostileTarget), Is.True);
            Assert.That(hostileTarget.entityId, Is.EqualTo(20));

            Assert.That(snapshot.TryPickImpactTargetAt(friendlyOnlyCell, sourceTeamId: 1, out var fallbackTarget), Is.True);
            Assert.That(fallbackTarget.entityId, Is.EqualTo(50));

            Assert.That(snapshot.TryPickImpactTargetAt(boxCell, sourceTeamId: 1, out var boxTarget), Is.True);
            Assert.That(boxTarget.entityId, Is.EqualTo(40));
            Assert.That(boxTarget.type, Is.EqualTo(EntityType.Box));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_MovesOntoBottomTopEdgeCellBeforeTraversalGateApplies()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Up,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_RotatesForwardFromBottomTopEdgeWhenTraversalColumnAllowed()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 1 })));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 1, 1),
                Direction.Up,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.True);
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(updatedTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(updatedTopology.FrontFace, Is.EqualTo(FaceId.Ceiling));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_RotatesBackwardFromBottomBottomEdgeWhenTraversalColumnAllowed()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 1 })));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 1, 0),
                Direction.Down,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.True);
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Back, 1, 1)));
            Assert.That(updatedTopology, Is.EqualTo(new CubeTopologyState(FaceId.Back)));
            Assert.That(updatedTopology.FrontFace, Is.EqualTo(FaceId.Floor));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_DisallowedBottomTopTraversalFromEdgeCellFails()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 1, 1),
                Direction.Up,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_DoesNotRotateFromFrontFaceOrSideEdge()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var frontMoveResolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Front, 1, 0),
                Direction.Up,
                out var frontDestination,
                out var frontRotationKind,
                out var frontTopology);

            Assert.That(frontMoveResolved, Is.True);
            Assert.That(frontDestination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 1)));
            Assert.That(frontRotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(frontTopology, Is.EqualTo(snapshot.Topology));

            var sideEdgeResolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 2, 0),
                Direction.Right,
                out var sideDestination,
                out var sideRotationKind,
                out var sideTopology);

            Assert.That(sideEdgeResolved, Is.False);
            Assert.That(sideDestination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(sideRotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(sideTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_VectorDeltaOutsideSideEdge_LeavesOutParametersAtDefault()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 2, 0),
                Vector2Int.right,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_LeftSideEdge_LeavesOutParametersAtDefault()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Floor, 0, 0),
                Direction.Left,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolvePlayerStep_FrontBottomEdge_DoesNotRotateOrLeakDestination()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolvePlayerStep(
                new SurfaceCell(FaceId.Front, 1, 0),
                Direction.Down,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolveUnitStep_BottomFaceTopEdge_DoesNotRotateOrLeakDestination()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveUnitStep(
                new SurfaceCell(FaceId.Floor, 1, 1),
                Vector2Int.up,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolveUnitStep_InteriorMove_StaysOnActiveFaceWithoutRotation()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveUnitStep(
                new SurfaceCell(FaceId.Floor, 1, 0),
                Vector2Int.up,
                out var destination,
                out var rotationKind,
                out var updatedTopology);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.None));
            Assert.That(updatedTopology, Is.EqualTo(snapshot.Topology));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_SlidesToEdgeCellBeforeTraversalGateApplies()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_CrossesBottomFrontSharedEdgeWhenTraversalColumnAllowed()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 0 })));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_CrossesFrontBottomSharedEdgeBackToBottomWhenTraversalColumnAllowed()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 0 })));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 0, 0),
                Vector2Int.down,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsAtDisallowedBottomFrontSharedEdge()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.BoardEdge));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 2)));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsAtDisallowedFrontBottomSharedEdge()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 0, 0),
                Vector2Int.down,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.BoardEdge));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, -1)));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsAtOtherBoardEdges()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 0, 1),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.BoardEdge));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 2)));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_IgnoresDetachedOccupantOnNextCell()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(
                            entityId: 20,
                            position: new SurfaceCell(FaceId.Front, 1, 0),
                            boardPresence: EntityBoardPresence.Detached),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 1 })));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 1, 1),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsOnStackedUnitsOnNextCell()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(
                            entityId: 20,
                            position: new SurfaceCell(FaceId.Front, 1, 1)),
                        CreateUnit(
                            entityId: 30,
                            position: new SurfaceCell(FaceId.Front, 1, 1),
                            teamId: 2),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 1, 0),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(stopper.EntityId, Is.EqualTo(20));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 1)));
        }

        [Test]
        public void WorldSnapshot_GameplayQueries_HideDetachedEntities()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(
                            entityId: 20,
                            position: new SurfaceCell(FaceId.Floor, 1, 0),
                            boardPresence: EntityBoardPresence.Detached),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            Assert.That(snapshot.TryGetEntity(20, out var detachedEntity), Is.True);
            Assert.That(detachedEntity.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(snapshot.TryGetUnitAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(snapshot.IsBlockedForUnit(new SurfaceCell(FaceId.Floor, 1, 0)), Is.False);
            Assert.That(snapshot.BlocksMovement(20), Is.False);
            Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.False);
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsOnMarkedForDeathOccupantOnNextCell()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(
                            entityId: 20,
                            position: new SurfaceCell(FaceId.Front, 1, 0),
                            boardPresence: EntityBoardPresence.Detached),
                        CreateUnit(
                            entityId: 30,
                            position: new SurfaceCell(FaceId.Front, 1, 1),
                            markedForDeath: true),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty,
                    new BoardTraversalRules(new[] { 1 })));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 1, 0),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(stopper.EntityId, Is.EqualTo(30));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 1)));
        }

        [Test]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_SucceedsWhenUnboundedBoardHasNoStopper()
        {
            var snapshot = CreateSnapshot(
                new WorldState(
                    new EntityState[0],
                    BoardBounds.Unbounded,
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 1, 0),
                Vector2Int.right,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 2, 0)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        public void WorldSnapshot_TryResolveLocalFlipCells_StaysOnSameFace()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveLocalFlipCells(
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.left,
                out var target,
                out var landing);

            Assert.That(resolved, Is.True);
            Assert.That(target, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(landing, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
        }

        [Test]
        public void WorldSnapshot_TryResolveLocalFlipCells_RejectsBottomFrontBoundaryCrossing()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveLocalFlipCells(
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                out var target,
                out var landing);

            Assert.That(resolved, Is.False);
            Assert.That(target, Is.EqualTo(default(SurfaceCell)));
            Assert.That(landing, Is.EqualTo(default(SurfaceCell)));
        }

        [Test]
        public void WorldSnapshot_TryResolveLocalFlipCells_RejectsSideBoundaryCrossing()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveLocalFlipCells(
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.left,
                out var target,
                out var landing);

            Assert.That(resolved, Is.False);
            Assert.That(target, Is.EqualTo(default(SurfaceCell)));
            Assert.That(landing, Is.EqualTo(default(SurfaceCell)));
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            return worldState.CreateSnapshot();
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            bool markedForDeath = false,
            int teamId = 1,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = Direction.Right,
                boardPresence = boardPresence,
                markedForDeath = markedForDeath,
                spawnTick = 0,
            };
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
                stateTimer = 0,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                boxCapabilities = BoxCapabilities.None,
            };
        }
    }
}
