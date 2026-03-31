using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class EnemyViewIsolationTests
    {
        [Test]
        public void GameplayTickViewPresenter_PresentingEnemyFrames_DoesNotChangeLaterTickAuthoritativeResults()
        {
            var initialEntities = new[]
            {
                CreateUnit(entityId: 10, teamId: 1, position: new Vector2Int(3, 0), hp: 3),
                CreateUnit(entityId: 40, teamId: 2, position: new Vector2Int(0, 0), hp: 3, aiMode: EnemyAiMode.Patrol, facing: Direction.Right),
            };
            var baselineWorld = CreateWorldState(initialEntities);
            var presentedWorld = CreateWorldState(initialEntities);
            var baselinePipeline = GameplayCompositionRoot.CreateTickPipeline(baselineWorld);
            var presentedPipeline = GameplayCompositionRoot.CreateTickPipeline(presentedWorld);
            var rootObject = new GameObject("EnemyViewIsolationTests_Presenter");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform, attachEnemyAnimatorDriver: true));
                var initialSnapshot = presentedWorld.CreateSnapshot();

                presenter.Initialize(
                    binder,
                    initialSnapshot.BoardBounds,
                    initialSnapshot.Topology,
                    1f,
                    GameplayTimingProfile.CreateDefault());
                presenter.PresentInitial(initialEntities, initialSnapshot.Topology);

                var baselineFirstTick = baselinePipeline.RunTick(new TickInput(1));
                var presentedFirstTick = presentedPipeline.RunTick(new TickInput(1));

                presenter.Present(presentedFirstTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedFirstTick.DeterminismHash, Is.EqualTo(baselineFirstTick.DeterminismHash));

                var baselineSecondTick = baselinePipeline.RunTick(new TickInput(2));
                var presentedSecondTick = presentedPipeline.RunTick(new TickInput(2));

                presenter.Present(presentedSecondTick);
                presenter.UpdatePresentation(0f);

                Assert.That(presentedSecondTick.DeterminismHash, Is.EqualTo(baselineSecondTick.DeterminismHash));
                CollectionAssert.AreEqual(
                    SummarizeEntities(baselineSecondTick.FinalEntities),
                    SummarizeEntities(presentedSecondTick.FinalEntities));
                CollectionAssert.AreEqual(
                    baselineSecondTick.EventLog.ToArray(),
                    presentedSecondTick.EventLog.ToArray());

                var presentedSnapshotAfter = presentedWorld.CreateSnapshot();
                Assert.That(presentedSnapshotAfter.TryGetEntity(40, out var presentedEnemy), Is.True);
                Assert.That(presentedEnemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(presentedEnemy.aiStateTimer, Is.EqualTo(1));
                Assert.That(registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static (int EntityId, SurfaceCell Position, int Hp, EnemyAiMode AiMode, int AiTimer)[] SummarizeEntities(IReadOnlyList<EntityState> entities)
        {
            return entities
                .Select(entity => (entity.entityId, entity.position, entity.hp, entity.aiMode, entity.aiStateTimer))
                .ToArray();
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            Vector2Int position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = 0,
            };
        }

        private sealed class TestViewFactory : IGameplayEntityViewFactory
        {
            private readonly bool _attachEnemyAnimatorDriver;
            private readonly Transform _parent;

            public TestViewFactory(Transform parent, bool attachEnemyAnimatorDriver)
            {
                _parent = parent;
                _attachEnemyAnimatorDriver = attachEnemyAnimatorDriver;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);

                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (_attachEnemyAnimatorDriver &&
                    entity.aiMode != EnemyAiMode.None)
                {
                    viewObject.AddComponent<EnemyAnimatorDriver>();
                }

                return view;
            }
        }
    }
}
