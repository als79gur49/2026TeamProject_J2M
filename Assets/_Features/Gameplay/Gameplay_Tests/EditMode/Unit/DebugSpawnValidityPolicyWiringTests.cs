using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class DebugSpawnValidityPolicyWiringTests
    {
        [Test]
        [Category("Core")]
        public void DebugSpawnValidityPolicy_ProposedWall_PreservesLegacyWallSolidRules()
        {
            var proposedWall = CreateWall(
                entityId: 40,
                position: new SurfaceCell(FaceId.Floor, 1, 0));
            proposedWall.type = (EntityType)4;
            var accepted = true;

            try
            {
                DebugSpawnValidityPolicy.EnsureRepresentable(
                    new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                    new CubeTopologyState(FaceId.Floor),
                    new[] { proposedWall });
            }
            catch (ArgumentOutOfRangeException)
            {
                accepted = false;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }

            Assert.That(
                accepted,
                Is.True,
                "Proposed Wall must be admitted as a known representable Solid debug-spawn type.");
        }

        [Test]
        [Category("Extended")]
        public void DebugSpawnValidityPolicy_IsDirectlyWired_AtExactlyTwoNonTestEntrypoints()
        {
            var gameplayRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Gameplay"));
            var callSiteCount = Directory
                .GetFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("Tests.cs", StringComparison.Ordinal))
                .Select(File.ReadAllText)
                .Count(source => source.Contains("DebugSpawnValidityPolicy.EnsureRepresentable("));

            Assert.That(callSiteCount, Is.EqualTo(2));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayHostRuntimeFactory.cs"),
                Does.Contain("DebugSpawnValidityPolicy.EnsureRepresentable("));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/TestSupport/GameplayWorldStateTestFactory.cs"),
                Does.Contain("DebugSpawnValidityPolicy.EnsureRepresentable("));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayShowcaseSceneInstallerBase.cs"),
                Does.Not.Contain("DebugSpawnValidityPolicy.EnsureRepresentable("));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("DebugSpawnValidityPolicy.EnsureRepresentable("));
        }

        [Test]
        [Category("Extended")]
        public void DebugSpawnValidityPolicy_UsesResolvedSpatialOccupancyClaim_WithoutRawBoardPresenceBranch()
        {
            var source = ReadRepoFile(
                "Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/DebugSpawnValidityPolicy.cs");

            Assert.That(source, Does.Contain("SpatialStateResolver.Resolve("));
            Assert.That(source, Does.Contain(".ClaimsAuthoritativeOccupancy"));
            Assert.That(source, Does.Not.Contain("entity.boardPresence"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_InvalidDebugSpawnId_ThrowsViaPolicy()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateUnit(entityId: 0, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                }));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("positive entity id", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_DuplicateDebugSpawnIds_ThrowsViaPolicy()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateUnit(entityId: 10, position: new SurfaceCell(FaceId.Floor, 1, 0)),
                }));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("unique entity ids", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_OccupyingBoxSharingUnitCell_ThrowsViaPolicy()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateUnit(entityId: 10, position: sharedCell),
                    CreateBox(entityId: 20, position: sharedCell),
                }));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("BlockerEntity=10", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_DetachedBoxOutsideBounds_ThrowsViaPolicy()
        {
            var detachedBox = CreateBox(
                entityId: 20,
                position: new SurfaceCell(FaceId.Floor, 2, 0),
                boardPresence: EntityBoardPresence.Detached);

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(
                    new[] { detachedBox },
                    new BoardBounds(Vector2Int.zero, Vector2Int.one)));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("outside the configured board bounds", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_UnsupportedBoardPresence_ThrowsViaResolver()
        {
            var box = CreateBox(
                entityId: 20,
                position: new SurfaceCell(FaceId.Floor, 0, 0),
                boardPresence: (EntityBoardPresence)999);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[] { box }));
        }

        [TestCase(true, EntityType.Unit)]
        [TestCase(false, EntityType.Unit)]
        [TestCase(true, EntityType.Box)]
        [TestCase(false, EntityType.Box)]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_OccupyingWallConflict_ThrowsViaPolicyRegardlessOfOrder(
            bool wallFirst,
            EntityType conflictingType)
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var wall = CreateWall(entityId: 10, position: sharedCell);
            var conflictingEntity = conflictingType == EntityType.Unit
                ? CreateUnit(entityId: 20, position: sharedCell)
                : CreateBox(entityId: 20, position: sharedCell);
            var entities = wallFirst
                ? new[] { wall, conflictingEntity }
                : new[] { conflictingEntity, wall };

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(entities));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("Debug spawn entity", exception.Message);
            StringAssert.Contains($"BlockerEntity={(wallFirst ? 10 : 20)}", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_TwoOccupyingWalls_ThrowsViaPolicy()
        {
            var sharedCell = new SurfaceCell(FaceId.Floor, 0, 0);

            var exception = Assert.Throws<InvalidOperationException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[]
                {
                    CreateWall(entityId: 10, position: sharedCell),
                    CreateWall(entityId: 20, position: sharedCell),
                }));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("Debug spawn entity", exception.Message);
            StringAssert.Contains("BlockerEntity=10", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplayWorldStateTestFactory_CreateBounded_DetachedUnsupportedEntityType_ThrowsViaPolicy()
        {
            var entity = CreateBox(
                entityId: 20,
                position: new SurfaceCell(FaceId.Floor, 0, 0),
                boardPresence: EntityBoardPresence.Detached);
            entity.type = (EntityType)2;

            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => GameplayWorldStateTestFactory.CreateBounded(new[] { entity }));

            Assert.That(exception, Is.Not.Null);
            StringAssert.Contains("Unsupported debug spawn entity type", exception.Message);
        }

        [Test]
        [Category("Extended")]
        public void GameplaySceneHost_Initialize_InvalidDebugSpawnId_ThrowsViaPolicy()
        {
            var hostObject = new GameObject("DebugSpawnValidityPolicyWiringTests");

            try
            {
                var host = hostObject.AddComponent<GameplaySceneHost>();

                var exception = Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(
                        new GameplaySceneHostConfiguration
                        {
                            AutoCreateViews = false,
                            InitialBoardBounds = new BoardBounds(Vector2Int.zero, new Vector2Int(2, 2)),
                            InitialEntities = new[]
                            {
                                CreateUnit(entityId: 0, position: new SurfaceCell(FaceId.Floor, 0, 0)),
                            },
                            InitialTopology = new CubeTopologyState(FaceId.Floor),
                            PlayerEntityId = 10,
                            StaticEntityLogics = Array.Empty<IEntityLogic>(),
                        }));

                Assert.That(exception, Is.Not.Null);
                StringAssert.Contains("positive entity id", exception.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hostObject);
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
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
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
                boardPresence = boardPresence,
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
                type = EntityType.Wall,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }
    }
}
