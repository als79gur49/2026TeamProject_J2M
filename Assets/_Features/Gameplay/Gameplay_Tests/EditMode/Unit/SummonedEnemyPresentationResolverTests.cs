using System;
using System.Collections.Generic;
using System.Reflection;
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
            var settings = CreateEnemyInactiveVisualSettings(
                new Color(0.12f, 0.34f, 0.56f, 1f),
                desaturateStrength: 0.44f,
                emissionSuppression: 0.55f);

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(enemyPrefabObject.transform, worldPositionStays: false);
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
                    enemyPresentationArchetypeRegistry: presentationRegistry,
                    enemyInactiveVisualSettings: settings);
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
                Assert.That(summonedView.TryGetComponent<EnemyInactiveVisualController>(out var controller), Is.True);
                var renderer = summonedView.GetComponentInChildren<Renderer>();
                Assert.That(renderer, Is.Not.Null);
                controller.Apply(new EnemyVisualSemanticState(EnemyVisualActivityState.FrontFaceInactive));
                AssertColorApproximately(
                    new Color(0.12f, 0.34f, 0.56f, 1f),
                    GetRendererColor(renderer, "_InactiveTint"));
                Assert.That(GetRendererFloat(renderer, "_DesaturateStrength"), Is.EqualTo(0.44f).Within(0.0001f));
                Assert.That(GetRendererFloat(renderer, "_EmissionSuppression"), Is.EqualTo(0.55f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
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
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
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
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
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

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_AtContactRemovedSummonedEnemy_RetainsSameViewUntilContactThenReleasesOnce()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_AtContact");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                new GameObject("SummonedMarker").transform.SetParent(enemyPrefabObject.transform, worldPositionStays: false);
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var enemyCell = SurfaceCell.FromPlanar(new Vector2Int(1, 0));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile,
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);
                PresentSummonedEnemy(presenter, enemyCell);

                Assert.That(registry.TryGetView(41, out var summonedView), Is.True);
                var summonedViewInstanceId = summonedView.GetInstanceID();

                presenter.Present(
                    CreateResult(
                        tickIndex: 2,
                        finalEntities: new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) },
                        summonedBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                41,
                                TickEntityExitCause.EnemyDeath,
                                enemyCell,
                                topology,
                                Direction.Right,
                                EntityType.Unit,
                                timing: EntityExitPresentationTiming.AtContactTime,
                                visualContactNormalizedTime: 0.5f),
                        }));

                Assert.That(registry.TryGetView(41, out var retainedView), Is.True);
                Assert.That(retainedView.GetInstanceID(), Is.EqualTo(summonedViewInstanceId));
                Assert.That(retainedView.transform.Find("SummonedMarker"), Is.Not.Null);

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.49f);

                Assert.That(registry.TryGetView(41, out retainedView), Is.True);
                Assert.That(retainedView.GetInstanceID(), Is.EqualTo(summonedViewInstanceId));

                presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds * 0.02f);

                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(summonedView == null, Is.True);
                Assert.DoesNotThrow(() => presenter.UpdatePresentation(timingProfile.FlipMotionDurationSeconds));
                Assert.That(registry.TryGetView(41, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickViewPresenter_AfterEntityMotionRemovedSummonedEnemy_RetainsUntilMotionCompletes()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_AfterEntityMotion");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);
                var timingProfile = GameplayTimingProfile.CreateDefault();
                var sourceCell = SurfaceCell.FromPlanar(new Vector2Int(1, 0));
                var destinationCell = SurfaceCell.FromPlanar(new Vector2Int(2, 0));

                presenter.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    timingProfile,
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                presenter.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);
                PresentSummonedEnemy(presenter, sourceCell);

                Assert.That(registry.TryGetView(41, out var summonedView), Is.True);
                var summonedViewInstanceId = summonedView.GetInstanceID();

                presenter.Present(
                    CreateResult(
                        tickIndex: 2,
                        finalEntities: new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) },
                        summonedBindings: Array.Empty<TickSummonedEnemyPresentationBinding>(),
                        entityMotions: new[]
                        {
                            new TickEntityMotion(41, TickEntityMotionKind.Move, sourceCell, destinationCell),
                        },
                        entityExitSignals: new[]
                        {
                            new TickEntityExitPresentationSignal(
                                41,
                                TickEntityExitCause.OutOfBounds,
                                sourceCell,
                                topology,
                                Direction.Right,
                                EntityType.Unit,
                                timing: EntityExitPresentationTiming.AfterEntityMotion),
                        }));

                Assert.That(registry.TryGetView(41, out var retainedView), Is.True);
                Assert.That(retainedView.GetInstanceID(), Is.EqualTo(summonedViewInstanceId));

                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds * 0.5f);

                Assert.That(registry.TryGetView(41, out retainedView), Is.True);
                Assert.That(retainedView.GetInstanceID(), Is.EqualTo(summonedViewInstanceId));

                presenter.UpdatePresentation(timingProfile.MoveMotionDurationSeconds * 0.5f + 0.01f);

                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(summonedView == null, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_ResetSession_NullStateViewUsesRegistryFallbackAndReleasesExactlyOnce()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ResetSession");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var animationSync = new GameplayAnimationSyncCoordinator();
                var resolver = new SummonedEnemyPresentationResolver();
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);

                resolver.Initialize(
                    rootObject.transform,
                    registry,
                    stateStore,
                    animationSync,
                    presentationRegistry);
                resolver.Reconcile(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[] { CreateEnemyEntity(41, new Vector2Int(1, 0)) },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        }));

                Assert.That(registry.TryGetView(41, out var summonedView), Is.True);
                stateStore.ViewsByEntityId[41] = null;

                resolver.ResetSession();

                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(stateStore.ViewsByEntityId.ContainsKey(41), Is.False);
                Assert.That(summonedView == null, Is.True);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_ReleaseOwnedView_StateReplacementIsPreserved()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_StateReplacement");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");
            var replacementObject = new GameObject("ReplacementStateView");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = CreateResolverWithOwnedView(
                    rootObject,
                    enemyPrefabObject,
                    registry,
                    stateStore,
                    out var ownedView,
                    out var animationSync);
                var replacementView = replacementObject.AddComponent<GameplayEntityView>();
                var replacementDriver = replacementObject.AddComponent<EnemyAnimatorDriver>();
                replacementView.Initialize(41);
                stateStore.ViewsByEntityId[41] = replacementView;

                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.True);

                Assert.That(stateStore.ViewsByEntityId[41], Is.SameAs(replacementView));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(replacementView, Is.Not.Null);
                Assert.That(ownedView == null, Is.True);
                Assert.That(GetCachedEnemyAnimatorDriver(animationSync, 41), Is.SameAs(replacementDriver));
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementObject);
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_ReleaseOwnedView_RegistryReplacementIsPreserved()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_RegistryReplacement");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");
            var replacementObject = new GameObject("ReplacementRegistryView");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = CreateResolverWithOwnedView(
                    rootObject,
                    enemyPrefabObject,
                    registry,
                    stateStore,
                    out var ownedView,
                    out var animationSync);
                stateStore.ViewsByEntityId[41] = ownedView;
                var replacementView = replacementObject.AddComponent<GameplayEntityView>();
                var replacementDriver = replacementObject.AddComponent<EnemyAnimatorDriver>();
                replacementView.Initialize(41);
                registry.Register(replacementView);

                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.True);

                Assert.That(stateStore.ViewsByEntityId.ContainsKey(41), Is.False);
                Assert.That(registry.TryGetView(41, out var registeredView), Is.True);
                Assert.That(registeredView, Is.SameAs(replacementView));
                Assert.That(replacementView, Is.Not.Null);
                Assert.That(ownedView == null, Is.True);
                Assert.That(GetCachedEnemyAnimatorDriver(animationSync, 41), Is.SameAs(replacementDriver));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementObject);
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_RegisterSubscriberThrows_RollsBackOwnedView()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ThrowingRegister");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = new SummonedEnemyPresentationResolver();
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                resolver.Initialize(
                    rootObject.transform,
                    registry,
                    stateStore,
                    new GameplayAnimationSyncCoordinator(),
                    CreatePresentationRegistry(new EnemyUnitArchetypeId("BasicMinion"), enemyPrefabView));
                registry.ViewRegistered += _ => throw new InvalidOperationException("test register failure");

                var exception = Assert.Throws<InvalidOperationException>(() => resolver.Reconcile(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[] { CreateEnemyEntity(41, new Vector2Int(1, 0)) },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        })));

                Assert.That(exception.Message, Is.EqualTo("test register failure"));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(stateStore.ViewsByEntityId.ContainsKey(41), Is.False);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
                var remainingViews = rootObject.GetComponentsInChildren<GameplayEntityView>(includeInactive: true);
                Assert.That(
                    Array.Exists(remainingViews, view => view != null && view.EntityId == 41),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_RegisterSubscriberInstallsReplacementThenThrows_PreservesReplacement()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_RegisterReplacement");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");
            var replacementObject = new GameObject("ReplacementView");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = new SummonedEnemyPresentationResolver();
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var replacementView = replacementObject.AddComponent<GameplayEntityView>();
                replacementView.Initialize(41);
                resolver.Initialize(
                    rootObject.transform,
                    registry,
                    stateStore,
                    new GameplayAnimationSyncCoordinator(),
                    CreatePresentationRegistry(new EnemyUnitArchetypeId("BasicMinion"), enemyPrefabView));
                var isInstallingReplacement = false;
                registry.ViewRegistered += registeredView =>
                {
                    if (registeredView.EntityId != 41 || isInstallingReplacement)
                    {
                        return;
                    }

                    isInstallingReplacement = true;
                    registry.Register(replacementView);
                    throw new InvalidOperationException("test register replacement failure");
                };

                Assert.Throws<InvalidOperationException>(() => resolver.Reconcile(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[] { CreateEnemyEntity(41, new Vector2Int(1, 0)) },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        })));

                Assert.That(registry.TryGetView(41, out var registeredReplacement), Is.True);
                Assert.That(registeredReplacement, Is.SameAs(replacementView));
                Assert.That(replacementView, Is.Not.Null);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
                var rootViews = rootObject.GetComponentsInChildren<GameplayEntityView>(includeInactive: true);
                Assert.That(Array.Exists(rootViews, view => view != null && view.EntityId == 41), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(replacementObject);
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_RegisterAndRollbackSubscribersThrow_AggregatesAndDestroysOwnedView()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_RegisterRollbackFailure");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = new SummonedEnemyPresentationResolver();
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                resolver.Initialize(
                    rootObject.transform,
                    registry,
                    stateStore,
                    new GameplayAnimationSyncCoordinator(),
                    CreatePresentationRegistry(new EnemyUnitArchetypeId("BasicMinion"), enemyPrefabView));
                registry.ViewRegistered += _ => throw new InvalidOperationException("test register failure");
                registry.ViewUnregistered += (_, _) => throw new InvalidOperationException("test rollback failure");

                var exception = Assert.Throws<AggregateException>(() => resolver.Reconcile(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[] { CreateEnemyEntity(41, new Vector2Int(1, 0)) },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        })));

                Assert.That(exception.InnerExceptions, Has.Count.EqualTo(2));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
                var remainingViews = rootObject.GetComponentsInChildren<GameplayEntityView>(includeInactive: true);
                Assert.That(Array.Exists(remainingViews, view => view != null && view.EntityId == 41), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_CleanupOwnedViews_UnregisterFailuresReleaseEntireBatch()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ThrowingCleanupBatch");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = new SummonedEnemyPresentationResolver();
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                resolver.Initialize(
                    rootObject.transform,
                    registry,
                    stateStore,
                    new GameplayAnimationSyncCoordinator(),
                    CreatePresentationRegistry(new EnemyUnitArchetypeId("BasicMinion"), enemyPrefabView));
                resolver.Reconcile(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[]
                        {
                            CreateEnemyEntity(41, new Vector2Int(1, 0)),
                            CreateEnemyEntity(42, new Vector2Int(2, 0)),
                        },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 42,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        }));
                Assert.That(registry.TryGetView(41, out var firstOwnedView), Is.True);
                Assert.That(registry.TryGetView(42, out var secondOwnedView), Is.True);
                var unregisterFailureCount = 0;
                registry.ViewUnregistered += (_, _) =>
                {
                    unregisterFailureCount++;
                    throw new InvalidOperationException("test cleanup unregister failure");
                };

                Assert.Throws<AggregateException>(
                    () => resolver.CleanupOwnedViews(Array.Empty<EntityState>()));

                Assert.That(unregisterFailureCount, Is.EqualTo(2));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(registry.TryGetView(42, out _), Is.False);
                Assert.That(firstOwnedView == null, Is.True);
                Assert.That(secondOwnedView == null, Is.True);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(42), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_ResetSession_RejectsReentrantViewCreationAndRestoresGuardAfterCleanupFailure()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ReentrantReset");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = CreateResolverWithOwnedView(
                    rootObject,
                    enemyPrefabObject,
                    registry,
                    stateStore,
                    out var ownedView);
                Exception reentrantException = null;
                registry.ViewUnregistered += (_, _) =>
                {
                    reentrantException = Assert.Throws<InvalidOperationException>(() => resolver.Reconcile(
                        CreateResult(
                            tickIndex: 2,
                            finalEntities: new[] { CreateEnemyEntity(42, new Vector2Int(2, 0)) },
                            summonedBindings: new[]
                            {
                                new TickSummonedEnemyPresentationBinding(
                                    entityId: 42,
                                    hasEnemyDefinitionBinding: true,
                                    archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                            })));
                    throw new InvalidOperationException("test reset cleanup failure");
                };

                var resetException = Assert.Throws<InvalidOperationException>(resolver.ResetSession);

                Assert.That(resetException.Message, Is.EqualTo("test reset cleanup failure"));
                Assert.That(reentrantException, Is.Not.Null);
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(ownedView == null, Is.True);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);

                Assert.DoesNotThrow(() => resolver.Reconcile(
                    CreateResult(
                        tickIndex: 3,
                        finalEntities: new[] { CreateEnemyEntity(42, new Vector2Int(2, 0)) },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 42,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        })));
                Assert.That(registry.TryGetView(42, out var recoveredView), Is.True);
                Assert.That(recoveredView, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void SummonedEnemyPresentationResolver_UnregisterSubscriberThrows_OwnedViewStillDestroyed()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ThrowingUnregister");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var stateStore = new GameplayPresentationStateStore();
                var resolver = CreateResolverWithOwnedView(
                    rootObject,
                    enemyPrefabObject,
                    registry,
                    stateStore,
                    out var ownedView);
                stateStore.ViewsByEntityId[41] = ownedView;
                registry.ViewUnregistered += (_, _) => throw new InvalidOperationException("test unregister failure");

                var exception = Assert.Throws<InvalidOperationException>(
                    () => resolver.ReleaseOwnedViewIfPresent(41));

                Assert.That(exception.Message, Is.EqualTo("test unregister failure"));
                Assert.That(stateStore.ViewsByEntityId.ContainsKey(41), Is.False);
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(ownedView == null, Is.True);
                Assert.That(resolver.ReleaseOwnedViewIfPresent(41), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void GameplayTickPresentationCoordinator_Teardown_ReleasesSummonedViewAndPreservesInitialView()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_Teardown");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);

                coordinator.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                coordinator.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);
                coordinator.Present(
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
                Assert.That(registry.TryGetView(10, out var initialView), Is.True);
                Assert.That(registry.TryGetView(41, out var summonedView), Is.True);

                coordinator.TeardownPresentationRuntime();

                Assert.That(registry.TryGetView(10, out var retainedInitialView), Is.True);
                Assert.That(retainedInitialView, Is.SameAs(initialView));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(summonedView == null, Is.True);
                Assert.DoesNotThrow(coordinator.TeardownPresentationRuntime);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        [Category("Full")]
        public void GameplayTickPresentationCoordinator_Teardown_UnregisterFailuresStillReleaseAllOwnedViewsAndAllowRetry()
        {
            var rootObject = new GameObject("SummonedEnemyPresentationResolverTests_ThrowingTeardown");
            var enemyPrefabObject = new GameObject("SummonedEnemyPrefab");

            try
            {
                var coordinator = GameplayPresentationTestCompositionBuilder.CreateCoordinator();
                var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
                var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(registry.transform));
                var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
                enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
                var presentationRegistry = CreatePresentationRegistry(
                    new EnemyUnitArchetypeId("BasicMinion"),
                    enemyPrefabView);
                var topology = new CubeTopologyState(FaceId.Floor);

                coordinator.Initialize(
                    binder,
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                    topology,
                    1f,
                    GameplayTimingProfile.CreateDefault(),
                    enemyPresentationArchetypeRegistry: presentationRegistry);
                coordinator.PresentInitial(new[] { CreatePlayerEntity(10, new Vector2Int(0, 0)) }, topology);
                coordinator.Present(
                    CreateResult(
                        tickIndex: 1,
                        finalEntities: new[]
                        {
                            CreatePlayerEntity(10, new Vector2Int(0, 0)),
                            CreateEnemyEntity(41, new Vector2Int(1, 0)),
                            CreateEnemyEntity(42, new Vector2Int(2, 0)),
                        },
                        summonedBindings: new[]
                        {
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 41,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                            new TickSummonedEnemyPresentationBinding(
                                entityId: 42,
                                hasEnemyDefinitionBinding: true,
                                archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                        }));
                Assert.That(registry.TryGetView(10, out var initialView), Is.True);
                Assert.That(registry.TryGetView(41, out var firstSummonedView), Is.True);
                Assert.That(registry.TryGetView(42, out var secondSummonedView), Is.True);
                var unregisterFailureCount = 0;
                Action<int, GameplayEntityView> throwingSubscriber = (_, _) =>
                {
                    unregisterFailureCount++;
                    throw new InvalidOperationException("test teardown unregister failure");
                };
                registry.ViewUnregistered += throwingSubscriber;

                Assert.Throws<AggregateException>(coordinator.TeardownPresentationRuntime);

                Assert.That(unregisterFailureCount, Is.EqualTo(2));
                Assert.That(registry.TryGetView(10, out var retainedInitialView), Is.True);
                Assert.That(retainedInitialView, Is.SameAs(initialView));
                Assert.That(registry.TryGetView(41, out _), Is.False);
                Assert.That(registry.TryGetView(42, out _), Is.False);
                Assert.That(firstSummonedView == null, Is.True);
                Assert.That(secondSummonedView == null, Is.True);

                registry.ViewUnregistered -= throwingSubscriber;
                Assert.DoesNotThrow(coordinator.TeardownPresentationRuntime);
                Assert.DoesNotThrow(coordinator.TeardownPresentationRuntime);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyPrefabObject);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static SummonedEnemyPresentationResolver CreateResolverWithOwnedView(
            GameObject rootObject,
            GameObject enemyPrefabObject,
            GameplayEntityViewRegistry registry,
            GameplayPresentationStateStore stateStore,
            out GameplayEntityView ownedView)
        {
            return CreateResolverWithOwnedView(
                rootObject,
                enemyPrefabObject,
                registry,
                stateStore,
                out ownedView,
                out _);
        }

        private static SummonedEnemyPresentationResolver CreateResolverWithOwnedView(
            GameObject rootObject,
            GameObject enemyPrefabObject,
            GameplayEntityViewRegistry registry,
            GameplayPresentationStateStore stateStore,
            out GameplayEntityView ownedView,
            out GameplayAnimationSyncCoordinator animationSync)
        {
            var resolver = new SummonedEnemyPresentationResolver();
            var enemyPrefabView = enemyPrefabObject.AddComponent<GameplayEntityView>();
            enemyPrefabObject.AddComponent<EnemyAnimatorDriver>();
            var presentationRegistry = CreatePresentationRegistry(
                new EnemyUnitArchetypeId("BasicMinion"),
                enemyPrefabView);
            animationSync = new GameplayAnimationSyncCoordinator();
            resolver.Initialize(
                rootObject.transform,
                registry,
                stateStore,
                animationSync,
                presentationRegistry);
            resolver.Reconcile(
                CreateResult(
                    tickIndex: 1,
                    finalEntities: new[] { CreateEnemyEntity(41, new Vector2Int(1, 0)) },
                    summonedBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(
                            entityId: 41,
                            hasEnemyDefinitionBinding: true,
                            archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                    }));
            Assert.That(registry.TryGetView(41, out ownedView), Is.True);
            animationSync.CacheDrivers(41, ownedView);
            return resolver;
        }

        private static EnemyAnimatorDriver GetCachedEnemyAnimatorDriver(
            GameplayAnimationSyncCoordinator animationSync,
            int entityId)
        {
            var field = typeof(GameplayAnimationSyncCoordinator).GetField(
                "_enemyAnimatorDriversByEntityId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var drivers = field.GetValue(animationSync) as Dictionary<int, EnemyAnimatorDriver>;
            Assert.That(drivers, Is.Not.Null);
            Assert.That(drivers.TryGetValue(entityId, out var driver), Is.True);
            return driver;
        }

        private static void PresentSummonedEnemy(
            GameplayTickViewPresenter presenter,
            SurfaceCell enemyCell)
        {
            presenter.Present(
                CreateResult(
                    tickIndex: 1,
                    finalEntities: new[]
                    {
                        CreatePlayerEntity(10, new Vector2Int(0, 0)),
                        CreateEnemyEntity(41, enemyCell),
                    },
                    summonedBindings: new[]
                    {
                        new TickSummonedEnemyPresentationBinding(
                            entityId: 41,
                            hasEnemyDefinitionBinding: true,
                            archetypeId: new EnemyUnitArchetypeId("BasicMinion")),
                    }));
        }

        private static TickResult CreateResult(
            int tickIndex,
            IReadOnlyList<EntityState> finalEntities,
            IReadOnlyList<TickSummonedEnemyPresentationBinding> summonedBindings,
            IReadOnlyList<TickEntityMotion> entityMotions = null,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals = null)
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
                CreatePresentationData(
                    summonedBindings,
                    entityMotions ?? Array.Empty<TickEntityMotion>(),
                    entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>()),
                determinismHash: string.Empty,
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            IReadOnlyList<TickSummonedEnemyPresentationBinding> summonedBindings,
            IReadOnlyList<TickEntityMotion> entityMotions,
            IReadOnlyList<TickEntityExitPresentationSignal> entityExitSignals)
        {
            return new TickPresentationData(
                entityMotions,
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
                entityExitSignals,
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

        private static EnemyInactiveVisualSettings CreateEnemyInactiveVisualSettings(
            Color inactiveTint,
            float desaturateStrength,
            float emissionSuppression)
        {
            var settings = ScriptableObject.CreateInstance<EnemyInactiveVisualSettings>();
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "inactiveTint", inactiveTint);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "desaturateStrength", desaturateStrength);
            PlayerViewPrefabTestUtility.SetSerializedField(settings, "emissionSuppression", emissionSuppression);
            return settings;
        }

        private static float GetRendererFloat(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetFloat(propertyName);
        }

        private static Color GetRendererColor(Renderer targetRenderer, string propertyName)
        {
            var propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            return propertyBlock.GetColor(propertyName);
        }

        private static void AssertColorApproximately(Color expected, Color actual, float tolerance = 0.0001f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(tolerance));
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
            return CreateEnemyEntity(entityId, SurfaceCell.FromPlanar(position));
        }

        private static EntityState CreateEnemyEntity(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
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
