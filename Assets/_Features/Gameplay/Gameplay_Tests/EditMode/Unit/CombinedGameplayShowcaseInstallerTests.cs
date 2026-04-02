using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class CombinedGameplayShowcaseInstallerTests
    {
        [Test]
        public void CombinedGameplayShowcaseInstaller_PopulatesSharedEdgeOpeningsAcrossAllFourFaces()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstallerTests");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardBounds = InvokeNonPublic<BoardBounds>(installer, "CreateBoardBounds");
                var entities = new List<EntityState>();

                InvokePopulateInitialEntities(installer, entities, boardBounds);

                var sharedEdgeOpeningColumns = new[] { 1, 3, 10, 14 };
                foreach (var face in new[] { FaceId.Floor, FaceId.Front, FaceId.Ceiling, FaceId.Back })
                {
                    for (var i = 0; i < sharedEdgeOpeningColumns.Length; i++)
                    {
                        var column = sharedEdgeOpeningColumns[i];
                        Assert.That(
                            HasWallAt(entities, new SurfaceCell(face, column, boardBounds.MinInclusive.y)),
                            Is.False,
                            $"{face} bottom shared-edge opening at x={column} should remain open.");
                        Assert.That(
                            HasWallAt(entities, new SurfaceCell(face, column, boardBounds.MaxInclusive.y)),
                            Is.False,
                            $"{face} top shared-edge opening at x={column} should remain open.");
                    }
                }

                Assert.That(
                    HasWallAt(entities, new SurfaceCell(FaceId.Floor, 2, boardBounds.MaxInclusive.y)),
                    Is.True,
                    "Columns between the traversal lane and left box lane should stay walled.");
                Assert.That(
                    HasWallAt(entities, new SurfaceCell(FaceId.Floor, 4, boardBounds.MaxInclusive.y)),
                    Is.True,
                    "Columns outside the designated shared-edge cutouts should stay walled.");
                Assert.That(
                    HasWallAt(entities, new SurfaceCell(FaceId.Floor, 11, boardBounds.MinInclusive.y)),
                    Is.True,
                    "Only the configured box lanes and traversal lanes should open shared edges.");
            }
            finally
            {
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_PlacesPushableBoxesOnEveryRotatingFace()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstallerTests");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardBounds = InvokeNonPublic<BoardBounds>(installer, "CreateBoardBounds");
                var entities = new List<EntityState>();

                InvokePopulateInitialEntities(installer, entities, boardBounds);

                Assert.That(
                    HasPushableBoxAt(entities, new SurfaceCell(FaceId.Floor, 14, boardBounds.MaxInclusive.y)),
                    Is.True,
                    "Floor should retain the dedicated top-edge push box.");
                Assert.That(
                    HasPushableBoxAt(entities, new SurfaceCell(FaceId.Front, 3, 1)),
                    Is.True,
                    "Front should start with a pushable box near a shared edge opening.");
                Assert.That(
                    HasPushableBoxAt(entities, new SurfaceCell(FaceId.Ceiling, 10, 1)),
                    Is.True,
                    "Ceiling should start with a pushable box near a shared edge opening.");
                Assert.That(
                    HasPushableBoxAt(entities, new SurfaceCell(FaceId.Back, 3, 5)),
                    Is.True,
                    "Back should start with a pushable box near a shared edge opening.");
            }
            finally
            {
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_PlacesTwoPatrolEnemiesForFsmValidation()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstallerTests");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardBounds = InvokeNonPublic<BoardBounds>(installer, "CreateBoardBounds");
                var entities = new List<EntityState>();

                InvokePopulateInitialEntities(installer, entities, boardBounds);

                Assert.That(
                    TryGetUnitAt(entities, new SurfaceCell(FaceId.Floor, 2, 4), out var floorEnemy),
                    Is.True,
                    "Floor face should include an immediately reachable FSM test enemy.");
                Assert.That(floorEnemy.teamId, Is.EqualTo(2));
                Assert.That(floorEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

                Assert.That(
                    TryGetUnitAt(entities, new SurfaceCell(FaceId.Front, 2, 2), out var frontEnemy),
                    Is.True,
                    "Front face should include a second FSM test enemy after a surface transition.");
                Assert.That(frontEnemy.teamId, Is.EqualTo(2));
                Assert.That(frontEnemy.aiMode, Is.EqualTo(EnemyAiMode.Patrol));

                Assert.That(
                    GetPlanarDistance(new SurfaceCell(FaceId.Floor, 1, 1), floorEnemy.position),
                    Is.EqualTo(4),
                    "The floor enemy should start inside sense range but outside attack range so Chase is visible first.");
            }
            finally
            {
                Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void GameplayBoxCapabilityLabelViewFactory_AddsCapabilityTextOnlyToBoxes()
        {
            var parentObject = new GameObject("GameplayBoxCapabilityLabelViewFactoryTests");

            try
            {
                var factory = new GameplayBoxCapabilityLabelViewFactory(parentObject.transform, 1f, playerEntityId: 10);

                var boxView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 30,
                        position = new SurfaceCell(FaceId.Floor, 0, 0),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.Box,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                    });

                var label = boxView.transform.Find("CapabilityLabel");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.GetComponent<GameplayFloatingTextBillboard>(), Is.Not.Null);

                var textMesh = label.GetComponent<TextMesh>();
                Assert.That(textMesh, Is.Not.Null);
                Assert.That(textMesh.text, Is.EqualTo("Push\nFlip\nDestroy"));

                var unitView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 31,
                        position = new SurfaceCell(FaceId.Floor, 1, 0),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Up,
                    });

                Assert.That(unitView.transform.Find("CapabilityLabel"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory_UsesPrefabOnlyForPlayerAndKeepsBoxLabels()
        {
            var installerObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory");
            var boardRootObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerViewPrefabFactory_BoardRoot");
            var playerPrefabObject = new GameObject("CombinedGameplayShowcaseInstaller_PlayerPrefab");

            try
            {
                var installer = installerObject.AddComponent<CombinedGameplayShowcaseInstaller>();
                var boardRoot = boardRootObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                var playerPrefabView = playerPrefabObject.AddComponent<GameplayEntityView>();
                playerPrefabObject.AddComponent<PlayerAnimatorDriver>();
                new GameObject("PrefabMarker").transform.SetParent(playerPrefabObject.transform, worldPositionStays: false);

                var prefabField = typeof(CombinedGameplayShowcaseInstaller).GetField(
                    "playerViewPrefab",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prefabField, Is.Not.Null);
                prefabField.SetValue(installer, playerPrefabView);

                var factoryMethod = installer.GetType().GetMethod(
                    "CreateViewFactory",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(factoryMethod, Is.Not.Null);
                var factory = (IGameplayEntityViewFactory)factoryMethod.Invoke(installer, new object[] { boardRoot });
                Assert.That(factory, Is.Not.Null);

                var playerView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 10,
                        position = new SurfaceCell(FaceId.Floor, 1, 1),
                        hp = 3,
                        maxHp = 3,
                        teamId = 1,
                        type = EntityType.Unit,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                    });

                Assert.That(playerView, Is.Not.SameAs(playerPrefabView));
                Assert.That(playerView.transform.parent, Is.EqualTo(boardRoot.EntityRoot));
                Assert.That(playerView.GetComponent<PlayerAnimatorDriver>(), Is.Not.Null);
                Assert.That(playerView.transform.Find("PrefabMarker"), Is.Not.Null);
                Assert.That(playerView.transform.Find("CapabilityLabel"), Is.Null);

                var boxView = factory.CreateView(
                    new EntityState
                    {
                        entityId = 30,
                        position = new SurfaceCell(FaceId.Floor, 3, 1),
                        hp = 1,
                        maxHp = 1,
                        teamId = 0,
                        type = EntityType.Box,
                        state = EntityPhaseState.Idle,
                        facing = Direction.Right,
                        boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                    });

                Assert.That(boxView.transform.Find("CapabilityLabel"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(playerPrefabObject);
                Object.DestroyImmediate(boardRootObject);
                Object.DestroyImmediate(installerObject);
            }
        }

        private static bool HasWallAt(IReadOnlyList<EntityState> entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.None && entity.position.Equals(cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPushableBoxAt(IReadOnlyList<EntityState> entities, SurfaceCell cell)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.Box &&
                    entity.position.Equals(cell) &&
                    (entity.boxCapabilities & BoxCapabilities.Push) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetUnitAt(IReadOnlyList<EntityState> entities, SurfaceCell cell, out EntityState unit)
        {
            for (var i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.type == EntityType.Unit &&
                    entity.position.Equals(cell))
                {
                    unit = entity;
                    return true;
                }
            }

            unit = default;
            return false;
        }

        private static int? GetPlanarDistance(SurfaceCell source, SurfaceCell target)
        {
            if (source.face != target.face)
            {
                return null;
            }

            var delta = target - source;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        private static T InvokeNonPublic<T>(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Expected method '{methodName}' to exist.");
            return (T)method.Invoke(target, null);
        }

        private static void InvokePopulateInitialEntities(
            CombinedGameplayShowcaseInstaller installer,
            List<EntityState> entities,
            BoardBounds boardBounds)
        {
            var method = installer.GetType().GetMethod("PopulateInitialEntities", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(installer, new object[] { entities, boardBounds });
        }
    }
}
