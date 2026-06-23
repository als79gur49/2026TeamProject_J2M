using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)));
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)));
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
            Assert.That(snapshot.TryGetBoxArchetypeAt(boxCell, out var boxArchetype), Is.True);
            Assert.That(boxArchetype, Is.EqualTo(BoxArchetype.Normal));
            Assert.That(snapshot.TryGetSolidSemanticAt(boxCell, out var solidSemantic), Is.True);
            Assert.That(solidSemantic.Kind, Is.EqualTo(SolidKind.Box));
            Assert.That(snapshot.IsBoxAt(boxCell), Is.True);
            Assert.That(snapshot.IsWallAt(boxCell), Is.False);
            Assert.That(snapshot.TryGetPrimaryUnitAt(boxCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void WorldSnapshot_TryGetBoxArchetypeAt_ExposesOnlyBoxIdentity()
        {
            var normalBoxCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var moonBoxCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var unitCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var emptyCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var wallCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateBox(20, normalBoxCell),
                    CreateBox(21, moonBoxCell, BoxArchetype.Moon),
                    CreateUnit(30, unitCell),
                    CreateWall(50, wallCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 1)));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetBoxArchetypeAt(normalBoxCell, out var normalArchetype), Is.True);
            Assert.That(normalArchetype, Is.EqualTo(BoxArchetype.Normal));
            Assert.That(snapshot.TryGetBoxArchetypeAt(moonBoxCell, out var moonArchetype), Is.True);
            Assert.That(moonArchetype, Is.EqualTo(BoxArchetype.Moon));
            Assert.That(snapshot.TryGetBoxArchetypeAt(unitCell, out _), Is.False);
            Assert.That(snapshot.TryGetBoxArchetypeAt(emptyCell, out _), Is.False);
            Assert.That(snapshot.TryGetBoxArchetypeAt(wallCell, out _), Is.False);
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1)));
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 0)));
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 0)));
            var snapshot = CreateSnapshot(worldState);

            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(contestedCell, sourceTeamId: 1, out var hostileTarget), Is.True);
            Assert.That(hostileTarget.entityId, Is.EqualTo(20));

            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(friendlyOnlyCell, sourceTeamId: 1, out _), Is.False);
            Assert.That(snapshot.TryPickHostileUnitImpactTargetAt(boxCell, sourceTeamId: 1, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_EnumerateUnitImpactTargetsAt_ReturnsAllTargetableUnitsDeterministically()
        {
            var contestedCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(entityId: 30, position: contestedCell, teamId: 2),
                    CreateUnit(entityId: 10, position: contestedCell, teamId: 1),
                    CreateUnit(entityId: 20, position: contestedCell, teamId: 2),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 0)));
            var snapshot = CreateSnapshot(worldState);
            var targets = new List<EntityState>();

            snapshot.EnumerateUnitImpactTargetsAt(contestedCell, targets);

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, targets.Select(target => target.entityId).ToArray());
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_TryResolvePlayerStep_MovesOntoBottomTopEdgeCell()
        {
            var snapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new EntityState[0],
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

            Assert.That(snapshot.Topology.BottomFace, Is.EqualTo(FaceId.Floor));
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
                new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

            Assert.That(snapshot.Topology.BottomFace, Is.EqualTo(FaceId.Floor));
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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));
            var wallSnapshot = CreateSnapshot(
                GameplayWorldStateTestFactory.CreateBounded(
                    new[]
                    {
                        CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 1)),
                        CreateWall(entityId: 30, position: new SurfaceCell(FaceId.Front, 1, 0)),
                    },
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));
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
                    BoardBounds.Unbounded));

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
                    new BoardBounds(new Vector2Int(-1, 0), new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                    new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1))));

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
                new SimulationOffset2(SimulationFixed.FromRaw(384), SimulationFixed.FromRaw(SimulationFixed.MaxPositiveLocalOffset)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.RotationKind, Is.EqualTo(CubeRotationKind.Forward));
            Assert.That(remap.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(384));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MaxPositiveLocalOffset + 1024 - SimulationFixed.UnitsPerCell));
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
                new SimulationOffset2(SimulationFixed.FromRaw(-384), SimulationFixed.FromRaw(SimulationFixed.MinLocalOffset)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(-1024)),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out var remap);

            Assert.That(resolved, Is.True);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
            Assert.That(remap.RotationKind, Is.EqualTo(CubeRotationKind.Backward));
            Assert.That(remap.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Back, 0, 1)));
            Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(-384));
            Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset - 1024 + SimulationFixed.UnitsPerCell));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusForward_ExactContactThresholdDoesNotCross()
        {
            const int radius = 768;
            var sourceThresholdY = SimulationFixed.HalfCellUnits - radius;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 1),
                Vector2Int.up,
                new SimulationOffset2(SimulationFixed.FromRaw(512), SimulationFixed.FromRaw(sourceThresholdY - 1024)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                radius,
                out var rejectReason,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasis_RadiusBackward_ExactContactThresholdDoesNotCross()
        {
            const int radius = 768;
            var sourceThresholdY = SimulationFixed.MinLocalOffset + radius;
            var resolved = SurfaceTopologyBasisQueries.TryRemapBottomFaceYEdgeCrossing(
                new CubeTopologyState(FaceId.Floor),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new SurfaceCell(FaceId.Floor, 0, 0),
                Vector2Int.down,
                new SimulationOffset2(SimulationFixed.FromRaw(-512), SimulationFixed.FromRaw(sourceThresholdY + 1024)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(-1024)),
                radius,
                out var rejectReason,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.CrossingAxisDidNotReachSeam));
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
                new SimulationOffset2(SimulationFixed.Zero, SimulationFixed.FromRaw(SimulationFixed.HalfCellUnits - radius - 1025)),
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
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
                SimulationOffset2.Zero,
                new SimulationVelocity2(SimulationFixed.FromRaw(1024), SimulationFixed.Zero),
                collisionRadiusUnits: 0,
                out var rejectReason,
                out _);

            Assert.That(resolved, Is.False);
            Assert.That(rejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.UnsupportedSeam));
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopologyTransition_TargetAnchorWithUnit_AllowsTransitionOrIntent()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateUnit(20, new SurfaceCell(FaceId.Front, 0, 0), teamId: 2),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)));
            SetContinuousPoseAtForwardSeam(worldState, 10);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var result);

            Assert.That(resolved, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopologyTransition_TargetAnchorWithSolid_StillBlocks()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, new SurfaceCell(FaceId.Front, 0, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)));
            SetContinuousPoseAtForwardSeam(worldState, 10);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var result);

            Assert.That(resolved, Is.False);
            Assert.That(result.Success, Is.False);
            Assert.That(result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.TargetFaceBlockedBySolid));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureMovementBlockerQuery_TopologyTransition_TargetAnchorDestroyPresenceBlocksRegardlessOfActivation()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, targetCell, TileFeatureKind.Destroy) });

            var blocked = TileFeatureMovementBlockerQuery.TryGetTopologyTransitionTileFeatureBlocker(
                worldState.CreateSnapshot(),
                targetCell,
                out var blocker);

            Assert.That(blocked, Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(TileFeatureKind.Destroy));
            Assert.That(blocker.Cell, Is.EqualTo(targetCell));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureMovementBlockerQuery_TopologyTransition_TargetAnchorBarricadePresenceBlocksRegardlessOfActivation()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, targetCell, TileFeatureKind.Barricade) });

            var blocked = TileFeatureMovementBlockerQuery.TryGetTopologyTransitionTileFeatureBlocker(
                worldState.CreateSnapshot(),
                targetCell,
                out var blocker);

            Assert.That(blocked, Is.True);
            Assert.That(blocker.Kind, Is.EqualTo(TileFeatureKind.Barricade));
            Assert.That(blocker.Cell, Is.EqualTo(targetCell));
        }

        [Test]
        [Category("Core")]
        public void TileFeatureMovementBlockerQuery_TopologyTransition_IgnoreNonTargetCellFeature()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[]
                {
                    CreateTileFeature(100, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Destroy),
                    CreateTileFeature(101, new SurfaceCell(FaceId.Back, 0, 0), TileFeatureKind.Barricade),
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(
                TileFeatureMovementBlockerQuery.TryGetTopologyTransitionTileFeatureBlocker(
                    snapshot,
                    targetCell,
                    out _),
                Is.False);
            Assert.That(
                TileFeatureMovementBlockerQuery.HasTopologyTransitionTileFeatureBlocker(
                    snapshot,
                    targetCell),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldSurfaceQuery_Free2DTopologyTransition_TargetInactiveBarricadePresence_BlocksWithTileFeatureReason()
        {
            AssertFree2DTopologyTransitionTargetTileFeaturePresenceBlocks(
                TileFeatureKind.Barricade,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void WorldSurfaceQuery_Free2DTopologyTransition_TargetActiveBarricadePresence_BlocksWithTileFeatureReason()
        {
            AssertFree2DTopologyTransitionTargetTileFeaturePresenceBlocks(
                TileFeatureKind.Barricade,
                TileFeatureActivationRule.BottomFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void WorldSurfaceQuery_Free2DTopologyTransition_TargetInactiveDestroyTilePresence_BlocksWithTileFeatureReason()
        {
            AssertFree2DTopologyTransitionTargetTileFeaturePresenceBlocks(
                TileFeatureKind.Destroy,
                TileFeatureActivationRule.FrontFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void WorldSurfaceQuery_Free2DTopologyTransition_TargetActiveDestroyTilePresence_BlocksWithTileFeatureReason()
        {
            AssertFree2DTopologyTransitionTargetTileFeaturePresenceBlocks(
                TileFeatureKind.Destroy,
                TileFeatureActivationRule.BottomFaceOnly);
        }

        [Test]
        [Category("Extended")]
        public void Free2DTopologyTransition_UnrelatedBarricade_AllowsTransition()
        {
            var unrelatedWorldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(20, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(101, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Barricade) });
            SetContinuousPoseAtForwardSeam(unrelatedWorldState, 20);

            var unrelatedResolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                unrelatedWorldState.CreateSnapshot(),
                20,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var unrelatedResult,
                new[] { CreateDefinition(101, TileFeatureActivationRule.FrontFaceOnly) });

            Assert.That(unrelatedResolved, Is.True);
            Assert.That(unrelatedResult.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceFree2DTopologyTransitionQueries_BottomToFront_FootprintNeighborCheckedOnlyOnOverflow()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var speed = BoundaryRadiusSpeedUnitsPerTick();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneInsideX = exactMaxContactX - 1;
            var oneBeyondX = exactMaxContactX + 1;
            var footprintNeighbor = new SurfaceCell(FaceId.Front, 1, 0);

            var exact = ResolveBottomToFrontWithNeighborWall(exactMaxContactX, radius, speed, footprintNeighbor);
            Assert.That(exact.Resolved, Is.True);
            Assert.That(exact.Result.Success, Is.True);
            Assert.That(exact.Result.TargetLocalOffset.X.RawValue + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            Assert.That(exact.Result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));

            var inside = ResolveBottomToFrontWithNeighborWall(oneInsideX, radius, speed, footprintNeighbor);
            Assert.That(inside.Resolved, Is.True);
            Assert.That(inside.Result.Success, Is.True);
            Assert.That(inside.Result.TargetLocalOffset.X.RawValue + radius, Is.LessThan(SimulationFixed.HalfCellUnits));
            Assert.That(inside.Result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));

            var beyond = ResolveBottomToFrontWithNeighborWall(oneBeyondX, radius, speed, footprintNeighbor);
            Assert.That(beyond.Resolved, Is.False);
            Assert.That(beyond.Result.Success, Is.False);
            Assert.That(beyond.Result.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(beyond.Result.TargetLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(beyond.Result.TargetLocalOffset.X.RawValue + radius, Is.GreaterThan(SimulationFixed.HalfCellUnits));
            Assert.That(beyond.Result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.TargetFaceFootprintBlocked));
            Assert.That(beyond.Result.TargetLegality.Cell, Is.EqualTo(footprintNeighbor));
            Assert.That(beyond.Result.TargetLegality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Solid));
            Assert.That(beyond.Result.TargetLegality.Blockers[0].EntityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceContinuousContactProjectionQueries_PositiveX_BlockingNeighborProjectsToMaxContact()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var maxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneBeyondX = maxContactX + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var contactCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, sourceCell),
                    CreateWall(20, contactCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var actor), Is.True);

            var projection = SurfaceContinuousContactProjectionQueries.ProjectLocalOffsetAgainstSourceFaceBlockers(
                snapshot,
                StateQuery.BuildActorRef(snapshot, actor),
                sourceCell,
                new SimulationOffset2(SimulationFixed.FromRaw(oneBeyondX), SimulationFixed.Zero),
                radius,
                snapshot.Topology,
                null,
                SurfaceContactProjectionAxes.X);

            Assert.That(projection.OriginalLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(projection.ProjectedLocalOffset.X.RawValue, Is.EqualTo(maxContactX));
            Assert.That(projection.ClampedPositiveX, Is.True);
            Assert.That(projection.Contacts.Count, Is.EqualTo(1));
            Assert.That(projection.Contacts[0].Cell, Is.EqualTo(contactCell));
            Assert.That(projection.Contacts[0].Legality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.Solid));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceContinuousContactProjectionQueries_PositiveX_NoBlockingNeighborDoesNotProject()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneBeyondX = SimulationFixed.HalfCellUnits - radius + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var actor), Is.True);

            var projection = SurfaceContinuousContactProjectionQueries.ProjectLocalOffsetAgainstSourceFaceBlockers(
                snapshot,
                StateQuery.BuildActorRef(snapshot, actor),
                sourceCell,
                new SimulationOffset2(SimulationFixed.FromRaw(oneBeyondX), SimulationFixed.Zero),
                radius,
                snapshot.Topology,
                null,
                SurfaceContactProjectionAxes.X);

            Assert.That(projection.ProjectedLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(projection.ClampedPositiveX, Is.False);
            Assert.That(projection.Contacts, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void SurfaceContinuousContactProjectionQueries_PositiveX_InactiveBarricadeAndDestroyTileDoNotProject()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var oneBeyondX = SimulationFixed.HalfCellUnits - radius + 1;
            AssertPositiveXSourceFeatureDoesNotProject(
                TileFeatureKind.Barricade,
                TileFeatureActivationRule.FrontFaceOnly,
                oneBeyondX,
                radius);
            AssertPositiveXSourceFeatureDoesNotProject(
                TileFeatureKind.Destroy,
                TileFeatureActivationRule.BottomFaceOnly,
                oneBeyondX,
                radius);
        }

        [Test]
        [Category("Extended")]
        public void SurfaceFree2DTopologyTransitionQueries_BottomToFront_UsesProjectedSourceOffsetForRemap()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var speed = BoundaryRadiusSpeedUnitsPerTick();
            var maxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneBeyondX = maxContactX + 1;
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var sourceContactCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, sourceCell),
                    CreateWall(20, sourceContactCell),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetContinuousPoseAtForwardSeam(
                worldState,
                10,
                oneBeyondX,
                SimulationFixed.HalfCellUnits - radius - speed,
                speed);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(speed)),
                radius,
                out var result);

            Assert.That(resolved, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.SourceLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(result.SourceContactProjection.OriginalLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(result.SourceContactProjection.ProjectedLocalOffset.X.RawValue, Is.EqualTo(maxContactX));
            Assert.That(result.SourceContactProjection.Contacts[0].Cell, Is.EqualTo(sourceContactCell));
            Assert.That(result.TargetLocalOffset.X.RawValue, Is.EqualTo(maxContactX));
            Assert.That(result.TargetLocalOffset.X.RawValue + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            Assert.That(result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
        }

        [Test]
        [Category("Extended")]
        public void SurfaceTopologyBasisQueries_BottomToFront_RemapsLocalOffsetWithoutChangingLateralClamp()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var speed = BoundaryRadiusSpeedUnitsPerTick();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            var oneInsideX = exactMaxContactX - 1;

            foreach (var localX in new[] { exactMaxContactX, oneInsideX })
            {
                var remap = AssertBottomToFrontBoundaryRemap(localX, radius, speed);

                Assert.That(remap.TargetLocalOffset.X.RawValue, Is.EqualTo(localX));
                Assert.That(remap.TargetLocalOffset.Y.RawValue, Is.EqualTo(SimulationFixed.MinLocalOffset + radius));
                Assert.That(remap.TargetLocalOffset.X.RawValue + radius, Is.LessThanOrEqualTo(SimulationFixed.HalfCellUnits));
            }
        }

        [Test]
        [Category("Extended")]
        public void SurfaceFree2DTopologyTransitionQueries_BottomToFront_SamePlanarOtherFaceNeighborIgnored()
        {
            var radius = BoundaryFootprintRadiusUnits();
            var speed = BoundaryRadiusSpeedUnitsPerTick();
            var exactMaxContactX = SimulationFixed.HalfCellUnits - radius;
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, new SurfaceCell(FaceId.Back, 1, 0)),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetContinuousPoseAtForwardSeam(
                worldState,
                10,
                exactMaxContactX,
                SimulationFixed.HalfCellUnits - radius - speed,
                speed);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(speed)),
                radius,
                out var result);

            Assert.That(resolved, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.TargetAnchor, Is.EqualTo(new SurfaceCell(FaceId.Front, 0, 0)));
            Assert.That(result.TargetLocalOffset.X.RawValue + radius, Is.EqualTo(SimulationFixed.HalfCellUnits));
            Assert.That(result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.None));
        }

        private static void AssertFree2DTopologyTransitionTargetTileFeaturePresenceBlocks(
            TileFeatureKind tileFeatureKind,
            TileFeatureActivationRule activationRule)
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, targetCell, tileFeatureKind) });
            SetContinuousPoseAtForwardSeam(worldState, 10);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(1024)),
                collisionRadiusUnits: 0,
                out var result,
                new[] { CreateDefinition(100, activationRule) });

            Assert.That(resolved, Is.False);
            Assert.That(result.Success, Is.False);
            Assert.That(result.TargetAnchor, Is.EqualTo(targetCell));
            Assert.That(result.RejectReason, Is.EqualTo(Free2DTopologyTransitionRejectReason.TargetFaceBlockedByTileFeature));
            Assert.That(result.TargetLegality.Blockers[0].Kind, Is.EqualTo(LegalityBlockerKind.TileFeature));
            Assert.That(result.TargetLegality.Blockers[0].TileFeatureKind, Is.EqualTo(tileFeatureKind));
        }

        private static WorldSnapshot CreateSnapshot(WorldState worldState)
        {
            return worldState.CreateSnapshot();
        }

        private static void SetContinuousPoseAtForwardSeam(WorldState worldState, int entityId)
        {
            SetContinuousPoseAtForwardSeam(
                worldState,
                entityId,
                localX: 0,
                localY: SimulationFixed.MaxPositiveLocalOffset,
                speedUnitsPerTick: 0);
        }

        private static void SetContinuousPoseAtForwardSeam(
            WorldState worldState,
            int entityId,
            int localX,
            int localY,
            int speedUnitsPerTick)
        {
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                entityId,
                new UnitContinuousLocomotionState
                {
                    localOffset = new SimulationOffset2(
                        SimulationFixed.FromRaw(localX),
                        SimulationFixed.FromRaw(localY)),
                    velocity = SimulationVelocity2.Zero,
                    facing = Direction.Up,
                    lastMoveDirection = Direction.Up,
                    speedUnitsPerTick = speedUnitsPerTick,
                    mode = ContinuousLocomotionMode.Idle,
                    sequenceId = 1,
                }.NormalizedForStorage());
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
            return PlayerContinuousLocomotionSettings.CreateDefault()
                .CreateAuthoritativeSnapshot(GameplayTimingProfile.DefaultSimulationTicksPerSecond)
                .SpeedUnitsPerTick;
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
            return remap;
        }

        private static (bool Resolved, Free2DTopologyTransitionResult Result) ResolveBottomToFrontWithNeighborWall(
            int localX,
            int radiusUnits,
            int speedUnitsPerTick,
            SurfaceCell footprintNeighbor)
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(20, footprintNeighbor),
                },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor));
            SetContinuousPoseAtForwardSeam(
                worldState,
                10,
                localX,
                SimulationFixed.HalfCellUnits - radiusUnits - speedUnitsPerTick,
                speedUnitsPerTick);

            var resolved = SurfaceFree2DTopologyTransitionQueries.TryResolveFree2DTopologyTransition(
                worldState.CreateSnapshot(),
                10,
                Vector2Int.up,
                new SimulationVelocity2(SimulationFixed.Zero, SimulationFixed.FromRaw(speedUnitsPerTick)),
                radiusUnits,
                out var result);
            return (resolved, result);
        }

        private static void AssertPositiveXSourceFeatureDoesNotProject(
            TileFeatureKind featureKind,
            TileFeatureActivationRule activationRule,
            int oneBeyondX,
            int radius)
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var featureCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[] { CreateUnit(10, sourceCell) },
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)),
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { CreateTileFeature(100, featureCell, featureKind) });
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out var actor), Is.True);

            var projection = SurfaceContinuousContactProjectionQueries.ProjectLocalOffsetAgainstSourceFaceBlockers(
                snapshot,
                StateQuery.BuildActorRef(snapshot, actor),
                sourceCell,
                new SimulationOffset2(SimulationFixed.FromRaw(oneBeyondX), SimulationFixed.Zero),
                radius,
                snapshot.Topology,
                new[] { CreateDefinition(100, activationRule) },
                SurfaceContactProjectionAxes.X);

            Assert.That(projection.ProjectedLocalOffset.X.RawValue, Is.EqualTo(oneBeyondX));
            Assert.That(projection.ClampedPositiveX, Is.False);
            Assert.That(projection.Contacts, Is.Empty);
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

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
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
                boxArchetype = boxArchetype,
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

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(
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
    }
}
