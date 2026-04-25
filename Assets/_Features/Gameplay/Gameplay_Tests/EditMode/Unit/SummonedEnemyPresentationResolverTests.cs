using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class SummonedEnemyPresentationResolverTests
    {
        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_SummonedEnemyBinding_InstantiatesArchetypeMappedPrefab()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_Presenter");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                new GameObject("SummonedMarker").transform.SetParent(enemyPrefabObject.transform, worldPositionStays: false);
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);
                var initialEntities = new[]
                {
                    CreatePlayerEntity(10, new Vector2Int(0, 0)),
                };

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(initialEntities, topology);

                presenter.Present(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[]
                        {
                            CreatePlayerEntity(10, new Vector2Int(0, 0)),
                            CreateEnemyEntity(41, new Vector2Int(1, 0)),
                        },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        }));

                Assert.That(registry.TryGetView(41, out var summonedView), Is.True);
                Assert.That(summonedView.transform.Find("SummonedMarker"), Is.Not.Null);
                Assert.That(summonedView.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null);
                Assert.That(summonedView.GetComponent<EnemyInactiveVisualController>(), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_SummonedEnemyWithoutBinding_Throws()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_MissingBinding");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);

                Assert.Throws<InvalidOperationException>(
                    () => presenter.Present(
                        CreateResult(
                            tickIndex: 1,
                            finalEntities: new[]
                            {
                                CreatePlayerEntity(10, new Vector2Int(0, 0)),
                                CreateEnemyEntity(41, new Vector2Int(1, 0)),
                            },
                            summonedBindings: new[]
                            {
                                new TickSummonedEnemyPresentationBinding(
                                    entityId: 41,
                                    hasEnemyDefinitionBinding: false,
                                    archetypeId: EnemyUnitArchetypeId.None),
                            })));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_Present_RemovedSummonedEnemy_DestroysOwnedView()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_Removal");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);

                presenter.Present(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[]
                        {
                            CreatePlayerEntity(10, new Vector2Int(0, 0)),
                            CreateEnemyEntity(41, new Vector2Int(1, 0)),
                        },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        }));
                Assert.That(registry.TryGetView(41, out _), Is.True);

                presenter.Present(
                    CreateResult(
                        tickIndex: 2,
                        finalEntities: new[]
                        {
                            CreatePlayerEntity(10, new Vector2Int(0, 0)),
                        },
                        summonedBindings: Array.Empty<TickSummonedEnemyPresentationBinding>()));

                Assert.That(registry.TryGetView(41, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static TickResult CreateResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            IReadOnlyList<TickSummonedEnemyPresentationBinding> summonedBindings)
        {
            return new TickResult(
                tickIndex,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                CreatePresentationData(summonedBindings),
                determinismHash: string.Empty,
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            IReadOnlyList<TickSummonedEnemyPresentationBinding> summonedBindings)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                summonedBindings);
        }

        private static EnemyPresentationArchetypeRegistry CreatePresentationRegistry(
            EnemyUnitArchetypeId archetypeId,
            GameplayEntityView prefab)
        {
            return new EnemyPresentationArchetypeRegistry(
                new Dictionary<EnemyUnitArchetypeId, EnemyPresentationArchetypeRuntime>(EnemyUnitArchetypeId.EqualityComparer)
                {
                    { archetypeId, new EnemyPresentationArchetypeRuntime(archetypeId, prefab) },
                });
        }

        private static EntityState CreatePlayerEntity(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemyEntity(int entityId, Vector2Int position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 2,
                maxHp = 2,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Patrol,
            };
        }

        private sealed class TestViewFactory : IGameplayEntityViewFactory
        {
            private readonly Transform _parent;

            public TestViewFactory(Transform parent)
            {
                _parent = parent;
            }

            public GameplayEntityView CreateView(in EntityState entity)
            {
                var viewObject = new GameObject($"EntityView_{entity.entityId}");
                viewObject.transform.SetParent(_parent, worldPositionStays: false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);

                if (EntityRolePolicy.IsPlayerUnit(entity))
                {
                    viewObject.AddComponent<PlayerAnimatorDriver>();
                    viewObject.AddComponent<PlayerAnimationTimingAuthoring>();
                }

                if (EntityRolePolicy.IsEnemyUnit(entity))
                {
                    viewObject.AddComponent<EnemyAnimatorDriver>();
                }

                return view;
            }
        }
    }
}
