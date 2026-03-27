using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class RuntimeBoardBoundsGuardTests
    {
        [Test]
        public void GameplaySceneHost_Initialize_UnboundedBoard_Throws()
        {
            var gameObject = new GameObject("RuntimeBoardBoundsGuardTests");

            try
            {
                var host = gameObject.AddComponent<GameplaySceneHost>();

                Assert.Throws<InvalidOperationException>(
                    () => host.Initialize(new GameplaySceneHostConfiguration()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void GameplayCompositionRoot_CreateWorldState_RejectsUnboundedBoard()
        {
            Assert.Throws<InvalidOperationException>(
                () => GameplayCompositionRoot.CreateWorldState(
                    Array.Empty<EntityState>(),
                    BoardBounds.Unbounded,
                    GameplayTerrainData.Empty));
        }

        [Test]
        public void GameplayCompositionRoot_PublicApi_DoesNotExposeUnboundedWorldFactory()
        {
            var publicDefaultFactory = typeof(GameplayCompositionRoot).GetMethod(
                nameof(GameplayCompositionRoot.CreateWorldState),
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(IEnumerable<EntityState>) },
                modifiers: null);
            var publicLegacyFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateLegacyUnboundedWorldState",
                BindingFlags.Static | BindingFlags.Public);

            Assert.That(publicDefaultFactory, Is.Null);
            Assert.That(publicLegacyFactory, Is.Null);
        }

        [Test]
        public void GameplayCompositionRoot_LegacyUnboundedHelper_IsInternalOnly_AndStillAvailableToTests()
        {
            var internalLegacyFactory = typeof(GameplayCompositionRoot).GetMethod(
                "CreateLegacyUnboundedWorldState",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(IEnumerable<EntityState>) },
                modifiers: null);

            Assert.That(internalLegacyFactory, Is.Not.Null);
            Assert.That(internalLegacyFactory.IsAssembly, Is.True);

            var worldState = GameplayCompositionRoot.CreateLegacyUnboundedWorldState(
                new[]
                {
                    CreateUnit(entityId: 10, position: Vector2Int.zero),
                });

            Assert.That(worldState.CreateSnapshot().BoardBounds.IsBounded, Is.False);
        }

        private static EntityState CreateUnit(int entityId, Vector2Int position)
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
                stateTimer = 0,
                facing = Direction.Right,
                markedForDeath = false,
                spawnTick = 0,
            };
        }
    }
}
