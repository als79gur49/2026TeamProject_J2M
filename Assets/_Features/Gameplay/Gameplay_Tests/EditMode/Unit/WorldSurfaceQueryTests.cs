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
        [Category("Extended")]
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
            var activeUnits = new List<EntityState>();
            var inactiveUnits = new List<EntityState>();

            Assert.That(snapshot.TryGetEntity(20, out var inactiveEntity), Is.True);
            Assert.That(inactiveEntity.position.face, Is.EqualTo(FaceId.Ceiling));

            snapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 1), activeUnits);
            snapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Ceiling, 1, 1), inactiveUnits);

            CollectionAssert.AreEqual(new[] { 10 }, activeUnits.ConvertAll(entity => entity.entityId));
            Assert.That(inactiveUnits, Is.Empty);
        }

        [Test]
        [Category("Extended")]
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
            Assert.That(snapshot.TryGetSolidSemanticAt(boxCell, out var solidSemantic), Is.True);
            Assert.That(solidSemantic.Kind, Is.EqualTo(SolidKind.Box));
            Assert.That(snapshot.IsBoxAt(boxCell), Is.True);
            Assert.That(snapshot.IsWallAt(boxCell), Is.False);
            Assert.That(snapshot.TryGetPrimaryUnitAt(boxCell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_SolidSemanticQueries_DistinguishWallsFromBoxes()
        {
            var wallCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var boxCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 40, position: boxCell),
                    CreateWall(entityId: 50, position: wallCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                GameplayTerrainData.Empty);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetSolidSemanticAt(boxCell, out var boxSemantic), Is.True);
            Assert.That(boxSemantic.Kind, Is.EqualTo(SolidKind.Box));
            Assert.That(boxSemantic.Entity.entityId, Is.EqualTo(40));

            Assert.That(snapshot.TryGetSolidSemanticAt(wallCell, out var wallSemantic), Is.True);
            Assert.That(wallSemantic.Kind, Is.EqualTo(SolidKind.Wall));
            Assert.That(wallSemantic.Entity.entityId, Is.EqualTo(50));
            Assert.That(snapshot.IsWallAt(wallCell), Is.True);
            Assert.That(snapshot.IsBoxAt(wallCell), Is.False);
            Assert.That(snapshot.TryGetBoxAt(wallCell, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TerrainQueries_AreFaceAwareWhileLegacyPlanarTerrainRemainsExpanded()
        {
            var floorCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var frontCell = new SurfaceCell(FaceId.Front, 1, 0);
            var faceAwareTerrain = new GameplayTerrainData(new[]
            {
                new TerrainCellState(floorCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
            });
            var faceAwareSnapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    System.Array.Empty<EntityState>(),
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    faceAwareTerrain));

            Assert.That(faceAwareSnapshot.TryGetTerrain(floorCell, out var floorTerrain), Is.True);
            Assert.That(floorTerrain.Cell, Is.EqualTo(floorCell));
            Assert.That(faceAwareSnapshot.TryGetTerrain(frontCell, out _), Is.False);
            Assert.That(faceAwareSnapshot.IsTerrainBlockedForUnit(floorCell), Is.True);
            Assert.That(faceAwareSnapshot.IsTerrainBlockedForUnit(frontCell), Is.False);

            var legacySnapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    System.Array.Empty<EntityState>(),
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    new GameplayTerrainData(new[] { floorCell.PlanarPosition })));

            Assert.That(legacySnapshot.IsTerrainBlockedForUnit(floorCell), Is.True);
            Assert.That(legacySnapshot.IsTerrainBlockedForUnit(frontCell), Is.True);
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
        public void WorldSnapshot_TryPickHostileUnitImpactTargetAt_SelectsOnlyHostileUnitsDeterministically()
        {
            var contestedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var friendlyOnlyCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var boxCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(entityId: 40, position: boxCell),
                    CreateUnit(entityId: 10, position: contestedCell, teamId: 1),
                    CreateUnit(entityId: 20, position: contestedCell, teamId: 2),
                    CreateUnit(entityId: 30, position: contestedCell, teamId: 2),
                    CreateUnit(entityId: 50, position: friendlyOnlyCell, teamId: 1),
                    CreateUnit(entityId: 60, position: friendlyOnlyCell, teamId: 1),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 0)),
                GameplayTerrainData.Empty);
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(contestedCell, sourceTeamId: 1, out var hostileTarget), Is.True);
            Assert.That(hostileTarget.entityId, Is.EqualTo(20));

            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(friendlyOnlyCell, sourceTeamId: 1, out _), Is.False);
            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(boxCell, sourceTeamId: 1, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_MovesOntoBottomTopEdgeCell()
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
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_RotatesForwardFromBottomTopEdge()
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

            Assert.That(resolved, Is.True);
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(updatedTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
            Assert.That(updatedTopology.FrontFace, Is.EqualTo(FaceId.Ceiling));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_RotatesBackwardFromBottomBottomEdge()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

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
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_AlwaysResolvesBottomTopTraversalGeometry()
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

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(rotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(updatedTopology, Is.EqualTo(new CubeTopologyState(FaceId.Front)));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_SeamDestinationUnitDoesNotBlockButSolidDoes()
        {
            var unitOnlySnapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                        CreateUnit(entityId: 20, position: new SurfaceCell(FaceId.Front, 1, 0), teamId: 2),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));
            var wallSnapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                        CreateWall(entityId: 30, position: new SurfaceCell(FaceId.Front, 1, 0)),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)),
                    GameplayTerrainData.Empty));

            Assert.That(
                unitOnlySnapshot.TryResolvePlayerStep(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    Direction.Up,
                    out var unitDestination,
                    out _,
                    out var unitTopology),
                Is.True);
            Assert.That(unitDestination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(
                unitOnlySnapshot.TryGetPlacementBlocker(unitTopology, EntityType.Unit, unitDestination, ignoredEntityId: 10, out _),
                Is.False);

            Assert.That(
                wallSnapshot.TryResolvePlayerStep(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    Direction.Up,
                    out var wallDestination,
                    out _,
                    out var wallTopology),
                Is.True);
            Assert.That(wallDestination, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)));
            Assert.That(
                wallSnapshot.TryGetPlacementBlocker(wallTopology, EntityType.Unit, wallDestination, ignoredEntityId: 10, out var blocker),
                Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(blocker.EntityType, Is.EqualTo(EntityType.None));
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_SlidesToEdgeCell()
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
        [Category("Full")]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_CrossesBottomFrontSharedEdge()
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

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        [Category("Full")]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_CrossesFrontBottomSharedEdgeBackToBottom()
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

            Assert.That(resolved, Is.True);
            Assert.That(destination, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.None));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsAtSolidOnBottomFrontSeamDestination()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateWall(entityId: 20, position: new SurfaceCell(FaceId.Front, 0, 0)),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(stopper.EntityType, Is.EqualTo(EntityType.None));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolveNextSurfaceBoxSlideStep_StopsAtSolidOnFrontBottomSeamDestination()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateBox(entityId: 20, position: new SurfaceCell(FaceId.Floor, 0, 1)),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                    GameplayTerrainData.Empty));

            var resolved = snapshot.TryResolveNextSurfaceBoxSlideStep(
                new SurfaceCell(FaceId.Front, 0, 0),
                Vector2Int.down,
                out var destination,
                out var stopper);

            Assert.That(resolved, Is.False);
            Assert.That(destination, Is.EqualTo(default(SurfaceCell)));
            Assert.That(stopper.Kind, Is.EqualTo(SlideStopperKind.Entity));
            Assert.That(stopper.Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 1)));
            Assert.That(stopper.EntityType, Is.EqualTo(EntityType.Box));
        }

        [Test]
        [Category("Extended")]
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
        [Category("Extended")]
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
                    GameplayTerrainData.Empty));

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
        [Category("Extended")]
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
        [Category("Extended")]
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
            var units = new List<EntityState>();

            Assert.That(snapshot.TryGetEntity(20, out var detachedEntity), Is.True);
            Assert.That(detachedEntity.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            snapshot.EnumerateUnitsAt(new SurfaceCell(FaceId.Floor, 1, 0), units);
            Assert.That(units, Is.Empty);
            Assert.That(snapshot.TryGetUnitTraversalBlocker(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(new SurfaceCell(FaceId.Floor, 1, 0), sourceTeamId: 1, out _), Is.False);
            Assert.That(snapshot.CanBeTargetedForNewSelection(20), Is.False);
        }

        [Test]
        [Category("Extended")]
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
                    new CubeTopologyState(FaceId.Floor)));

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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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
        [Category("Extended")]
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

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusZeroForward_KeepsCenterCrossingOvershootFormula()
        {
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                new KinematicOffset2(KinematicFixed.FromRaw(384), KinematicFixed.FromRaw(KinematicFixed.MaxPositiveLocalOffset)),
                new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(remap.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(384));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset + 1024 - KinematicFixed.UnitsPerCell));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusZeroBackward_KeepsCenterCrossingOvershootFormula()
        {
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.down,
                new KinematicOffset2(KinematicFixed.FromRaw(-384), KinematicFixed.FromRaw(KinematicFixed.MinLocalOffset)),
                new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(-1024)),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.RotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(remap.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Back, 0, 1)));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(-384));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset - 1024 + KinematicFixed.UnitsPerCell));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusForward_TriggersAtContactThresholdAndPreservesLocalX()
        {
            const int radius = 768;
            var sourceThresholdY = KinematicFixed.HalfCellUnits - radius;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.FromRaw(sourceThresholdY - 1024)),
                new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(1024)),
                radius,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(512));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MinLocalOffset + radius));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusBackward_TriggersAtContactThresholdAndPreservesLocalX()
        {
            const int radius = 768;
            var sourceThresholdY = KinematicFixed.MinLocalOffset + radius;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.down,
                new KinematicOffset2(KinematicFixed.FromRaw(-512), KinematicFixed.FromRaw(sourceThresholdY + 1024)),
                new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(-1024)),
                radius,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(-512));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(KinematicFixed.MaxPositiveLocalOffset - radius));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_SupportedSeamBeforeThreshold_ReturnsCrossingAxisDidNotReachSeam()
        {
            const int radius = 768;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                new KinematicOffset2(KinematicFixed.Zero, KinematicFixed.FromRaw(KinematicFixed.HalfCellUnits - radius - 1025)),
                new KinematicVelocity2(KinematicFixed.Zero, KinematicFixed.FromRaw(1024)),
                radius,
                out var rejectReason,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_UnsupportedSideOrFace_ReturnsUnsupportedSeam()
        {
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Front, 0, 1),
                Vector2Int.right,
                KinematicOffset2.Zero,
                new KinematicVelocity2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.UnsupportedSeam));
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
                stateTimer = 0,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
            };
        }
    }
}
