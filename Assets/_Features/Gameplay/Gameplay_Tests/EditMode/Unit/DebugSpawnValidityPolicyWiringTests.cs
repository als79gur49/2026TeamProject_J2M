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
        [Category("Extended")]
        public void DebugSpawnValidityPolicy_IsDirectlyWired_AtExactlyTwoEntrypoints()
        {
            var gameplayRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "_Features/Gameplay"));
            var callSiteCount = Directory
                .GetFiles(gameplayRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("DebugSpawnValidityPolicyWiringTests.cs", StringComparison.Ordinal))
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
    }
}
