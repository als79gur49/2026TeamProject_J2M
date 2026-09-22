using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    [Category("Extended")]
    public sealed class GameplayPlayerActionCountRuntimeIntegrationTests
    {
        private const int PlayerEntityId = 10;
        private const string PrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Prefabs/PlayerActionCountView.prefab";

        public enum ActualActionCase
        {
            PushSlide,
            PushImpact,
            PushDestroyFallback,
            FlipSuccess,
            FlipImpact,
        }

        [TestCase(ActualActionCase.PushSlide, TickPlayerActionResolutionKind.Success)]
        [TestCase(ActualActionCase.PushImpact, TickPlayerActionResolutionKind.Impact)]
        [TestCase(ActualActionCase.PushDestroyFallback, TickPlayerActionResolutionKind.Success)]
        [TestCase(ActualActionCase.FlipSuccess, TickPlayerActionResolutionKind.Success)]
        [TestCase(ActualActionCase.FlipImpact, TickPlayerActionResolutionKind.Impact)]
        public void ActualPushFlipResolution_PresenterCoordinatorExtension_CountsOnceAndRevealsAfterDelay(
            ActualActionCase actionCase,
            TickPlayerActionResolutionKind expectedResolution)
        {
            using var harness = CreateHarness(actionCase);
            var pipeline = CreatePipeline(harness.WorldState);
            var command = actionCase == ActualActionCase.FlipSuccess || actionCase == ActualActionCase.FlipImpact
                ? PlayerTickCommand.Flip(Direction.Right)
                : PlayerTickCommand.Push(Direction.Right);

            var startResult = pipeline.RunTick(new TickInput(1, command));
            var executeResult = pipeline.RunTick(new TickInput(2));
            Assert.That(
                executeResult.PresentationData.PlayerActionSignals.Single().ResolutionKind,
                Is.EqualTo(expectedResolution));

            harness.Presenter.Present(startResult);
            harness.Presenter.Present(executeResult);
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));
            Assert.That(harness.Runtime.IsVisible, Is.False);

            for (var i = 0; i < 100 && !harness.Runtime.IsVisible; i++)
            {
                harness.Presenter.UpdatePresentation(0.02f);
            }

            Assert.That(harness.Runtime.IsVisible, Is.True);
            Assert.That(harness.Runtime.VisibleCount, Is.EqualTo(1));
        }

        [Test]
        public void TickPipelinePushSlide_PresenterCoordinatorExtension_CountsOnceAndUsesBoundedMount()
        {
            using var harness = CreateHarness();
            var pipeline = CreatePipeline(harness.WorldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            var executeResult = pipeline.RunTick(new TickInput(2));
            Assert.That(
                executeResult.PresentationData.PlayerActionSignals.Single().ResolutionKind,
                Is.EqualTo(TickPlayerActionResolutionKind.Success));

            harness.Presenter.Present(executeResult);
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));
            Assert.That(harness.Runtime.VisibleCount, Is.Zero);
            Assert.That(harness.Runtime.IsVisible, Is.False);
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.CounterView, Is.Null);
            Assert.That(harness.Runtime.Mount, Is.Null);

            harness.Presenter.Present(executeResult);
            Assert.That(harness.Runtime.Count, Is.EqualTo(1), "Repeated result presentation must dedupe.");
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));

            harness.Presenter.SetPresentationPaused(true);
            harness.Presenter.UpdatePresentation(2f);
            Assert.That(harness.Runtime.IsVisible, Is.False, "Host presentation pause freezes reveal timing.");
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));
            harness.Presenter.SetPresentationPaused(false);

            harness.Presenter.UpdatePresentation(harness.Runtime.PushRevealDelaySeconds - 0.01f);
            Assert.That(harness.Runtime.IsVisible, Is.False);
            harness.Presenter.UpdatePresentation(0.02f);

            Assert.That(harness.Runtime.IsVisible, Is.True);
            Assert.That(harness.Runtime.VisibleCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.PendingRevealCount, Is.Zero);
            Assert.That(harness.Runtime.CounterView, Is.Not.Null);
            Assert.That(harness.Runtime.Mount.parent, Is.SameAs(harness.BoardRoot.transform));
            Assert.That(harness.Runtime.Mount, Is.Not.SameAs(harness.BoardRoot.EntityRoot));
            AssertEntityRootDirectChildrenAreViews(harness.BoardRoot.EntityRoot);
            var effectDriver = harness.Runtime.CounterView.GetComponent<GameplayPlayerActionCountEffectDriver>();
            Assert.That(effectDriver, Is.Not.Null);
            Assert.That(effectDriver.DebugPlayCount, Is.EqualTo(1));
            Assert.That(effectDriver.IsPlaying, Is.True);

            var elapsedBeforePause = effectDriver.DebugElapsedSeconds;
            harness.Presenter.SetPresentationPaused(true);
            harness.Presenter.UpdatePresentation(2f);
            Assert.That(harness.Runtime.IsVisible, Is.True, "Host presentation pause freezes fade timing.");
            Assert.That(effectDriver.DebugElapsedSeconds, Is.EqualTo(elapsedBeforePause));
            harness.Presenter.SetPresentationPaused(false);
            harness.Presenter.UpdatePresentation(0.12f);
            Assert.That(effectDriver.DebugElapsedSeconds, Is.GreaterThan(elapsedBeforePause));
            harness.Presenter.UpdatePresentation(1.38f);
            Assert.That(harness.Runtime.IsVisible, Is.False);
        }

        [TestCase(
            TickPlayerFlipOutcomeKind.FollowThrough,
            GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime)]
        [TestCase(
            TickPlayerFlipOutcomeKind.Stay,
            GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime)]
        [TestCase(
            TickPlayerFlipOutcomeKind.DestroySelf,
            GameplayPresentationTimingConstants.FlipImpactInteractionOnsetNormalizedTime)]
        public void FlipReveal_WaitsForMatchingMotionContactThreshold(
            TickPlayerFlipOutcomeKind outcome,
            float contactNormalizedTime)
        {
            using var harness = CreateHarness(ActualActionCase.FlipSuccess);
            const int sequence = 7;
            harness.Presenter.Present(CreateResult(
                tickIndex: 1,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[]
                {
                    CreateActionSignal(
                        PlayerEntityId,
                        sequence,
                        PlayerActionKind.Flip,
                        outcome == TickPlayerFlipOutcomeKind.FollowThrough
                            ? TickPlayerActionResolutionKind.Success
                            : TickPlayerActionResolutionKind.Impact,
                        outcome),
                })));

            var revealTime = contactNormalizedTime - harness.Runtime.FlipPreContactLeadNormalized;
            var progressSource = outcome switch
            {
                TickPlayerFlipOutcomeKind.Stay => MotionTrackProgressSourceKind.OriginalViewMotion,
                TickPlayerFlipOutcomeKind.DestroySelf => MotionTrackProgressSourceKind.FlipInteraction,
                _ => MotionTrackProgressSourceKind.LocalMotion,
            };
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));
            Assert.That(harness.Runtime.IsVisible, Is.False);

            harness.Runtime.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    tickIndex: 1,
                    entityId: 20,
                    motionKind: TickEntityMotionKind.Flip,
                    previousNormalizedTime: 0f,
                    currentNormalizedTime: revealTime - 0.001f,
                    sequenceOrActionPlanId: sequence,
                    sourceKind: progressSource),
            });
            Assert.That(harness.Runtime.IsVisible, Is.False);

            harness.Runtime.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    tickIndex: 1,
                    entityId: 20,
                    motionKind: TickEntityMotionKind.Flip,
                    previousNormalizedTime: revealTime - 0.001f,
                    currentNormalizedTime: revealTime + 0.001f,
                    sequenceOrActionPlanId: sequence,
                    sourceKind: progressSource),
            });

            Assert.That(harness.Runtime.IsVisible, Is.True);
            Assert.That(harness.Runtime.VisibleCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.PendingRevealCount, Is.Zero);
        }

        [Test]
        public void FlipReveal_ReusedActionPlanAcrossTicks_ConsumesOnlyMatchingTickProgress()
        {
            using var harness = CreateHarness(ActualActionCase.FlipSuccess);
            const int actionPlanId = 1;
            harness.Presenter.Present(CreateResult(
                tickIndex: 1,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[]
                {
                    CreateActionSignal(
                        PlayerEntityId,
                        sequence: 7,
                        PlayerActionKind.Flip,
                        TickPlayerActionResolutionKind.Success,
                        TickPlayerFlipOutcomeKind.FollowThrough,
                        actionPlanId),
                })));
            harness.Presenter.Present(CreateResult(
                tickIndex: 2,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[]
                {
                    CreateActionSignal(
                        PlayerEntityId,
                        sequence: 8,
                        PlayerActionKind.Flip,
                        TickPlayerActionResolutionKind.Success,
                        TickPlayerFlipOutcomeKind.FollowThrough,
                        actionPlanId),
                })));
            var revealTime = GameplayPresentationTimingConstants.FlipVisualSlamContactNormalizedTime -
                             harness.Runtime.FlipPreContactLeadNormalized;

            harness.Runtime.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    tickIndex: 1,
                    entityId: 20,
                    motionKind: TickEntityMotionKind.Flip,
                    previousNormalizedTime: revealTime - 0.001f,
                    currentNormalizedTime: revealTime + 0.001f,
                    sequenceOrActionPlanId: actionPlanId),
            });
            Assert.That(harness.Runtime.VisibleCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));

            harness.Runtime.ObserveMotionProgress(new[]
            {
                new MotionTrackProgressSample(
                    tickIndex: 2,
                    entityId: 20,
                    motionKind: TickEntityMotionKind.Flip,
                    previousNormalizedTime: revealTime - 0.001f,
                    currentNormalizedTime: revealTime + 0.001f,
                    sequenceOrActionPlanId: actionPlanId),
            });
            Assert.That(harness.Runtime.VisibleCount, Is.EqualTo(2));
            Assert.That(harness.Runtime.PendingRevealCount, Is.Zero);
        }

        [Test]
        public void BlockedAndCanceledActualActions_DoNotCreateOrIncrementCounter()
        {
            using (var blockedHarness = CreateHarness())
            {
                blockedHarness.WorldState.CreateWriteContext().SpawnEntity(
                    CreateEntity(30, 4, UnitRole.Player, EntityType.Unit, BoxCapabilities.None));
                var blockedResult = CreatePipeline(blockedHarness.WorldState).RunTick(
                    new TickInput(1, PlayerTickCommand.Push(Direction.Right)));

                Assert.That(blockedResult.PresentationData.PlayerActionSignals, Is.Empty);
                blockedHarness.Presenter.Present(blockedResult);
                Assert.That(blockedHarness.Runtime.Count, Is.Zero);
                Assert.That(blockedHarness.Runtime.Mount, Is.Null);
            }

            using var canceledHarness = CreateHarness();
            var pipeline = CreatePipeline(canceledHarness.WorldState);
            var startResult = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));
            canceledHarness.Presenter.Present(startResult);
            Assert.That(canceledHarness.Runtime.Count, Is.Zero);

            canceledHarness.WorldState.CreateWriteContext().SpawnEntity(
                CreateEntity(30, 4, UnitRole.Player, EntityType.Unit, BoxCapabilities.None));
            var canceledResult = pipeline.RunTick(new TickInput(2));
            Assert.That(canceledResult.PresentationData.PlayerActionSignals.Single().CanceledThisTick, Is.True);
            canceledHarness.Presenter.Present(canceledResult);
            Assert.That(canceledHarness.Runtime.Count, Is.Zero);
            Assert.That(canceledHarness.Runtime.Mount, Is.Null);
        }

        [Test]
        public void CanonicalPlayerScope_OtherEntitySignalsAndResetsAreIgnored_CanonicalRespawnRebindsReplacementView()
        {
            using var harness = CreateHarness();

            harness.Presenter.Present(CreateResult(
                tickIndex: 1,
                GetEntities(harness.WorldState),
                CreatePresentationData(
                    playerActionSignals: new[]
                    {
                        CreateActionSignal(99, 1),
                        CreateActionSignal(PlayerEntityId, 1),
                    })));
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));

            harness.Presenter.Present(CreateResult(
                tickIndex: 2,
                GetEntities(harness.WorldState),
                CreatePresentationData(
                    playerDeathSignals: new[] { CreateDeathSignal(99) },
                    entitySpawnSignals: new[] { CreateRespawnSignal(99) })));
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));

            Assert.That(harness.Registry.TryGetView(PlayerEntityId, out var oldPlayerView), Is.True);
            Assert.That(harness.Registry.Unregister(PlayerEntityId), Is.True);
            Object.DestroyImmediate(oldPlayerView.gameObject);

            harness.Presenter.Present(CreateResult(
                tickIndex: 3,
                GetEntities(harness.WorldState),
                CreatePresentationData(entitySpawnSignals: new[] { CreateRespawnSignal(PlayerEntityId) })));
            Assert.That(harness.Runtime.Count, Is.Zero);
            Assert.That(harness.Runtime.Mount, Is.Null, "Respawn reset removes the transient mount until a new action.");

            harness.Presenter.Present(CreateResult(
                tickIndex: 4,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[] { CreateActionSignal(PlayerEntityId, 2) })));
            Assert.That(harness.Registry.TryGetView(PlayerEntityId, out var replacementPlayerView), Is.True);
            Assert.That(replacementPlayerView, Is.Not.SameAs(oldPlayerView));
            Assert.That(harness.Runtime.Count, Is.EqualTo(1));
            Assert.That(harness.Runtime.CounterView, Is.Null);
            harness.Presenter.UpdatePresentation(harness.Runtime.PushRevealDelaySeconds);
            Assert.That(harness.Runtime.CounterView, Is.Not.Null);
            AssertEntityRootDirectChildrenAreViews(harness.BoardRoot.EntityRoot);
        }

        [Test]
        public void ResetSessionAndHardCleanup_ClearStateAndOwnedMount()
        {
            using var harness = CreateHarness();
            harness.Presenter.Present(CreateResult(
                tickIndex: 1,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[] { CreateActionSignal(PlayerEntityId, 1) })));
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.Mount, Is.Null);

            harness.Presenter.PresentInitial(
                GetEntities(harness.WorldState),
                new CubeTopologyState(FaceId.Floor));
            Assert.That(harness.Runtime.Count, Is.Zero);
            Assert.That(harness.Runtime.PendingRevealCount, Is.Zero);
            Assert.That(harness.Runtime.Mount, Is.Null);

            harness.Presenter.Present(CreateResult(
                tickIndex: 2,
                GetEntities(harness.WorldState),
                CreatePresentationData(playerActionSignals: new[] { CreateActionSignal(PlayerEntityId, 1) })));
            Assert.That(harness.Runtime.PendingRevealCount, Is.EqualTo(1));
            Assert.That(harness.Runtime.Mount, Is.Null);

            harness.Runtime.HardCleanup();
            Assert.That(harness.Runtime.Count, Is.Zero);
            Assert.That(harness.Runtime.PendingRevealCount, Is.Zero);
            Assert.That(harness.Runtime.Mount, Is.Null);
        }

        [Test]
        public void PlayerAnchoredExtension_AttachedBeforeContext_WaitsForValidConfiguration()
        {
            var root = new GameObject(nameof(PlayerAnchoredExtension_AttachedBeforeContext_WaitsForValidConfiguration));
            try
            {
                var runtime = root.AddComponent<GameplayPlayerActionCountPresentationRuntime>();
                var presenter = root.AddComponent<GameplayTickViewPresenter>();
                GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);

                Assert.DoesNotThrow(() => presenter.AttachPresentationExtension(runtime));
                Assert.That(runtime.PlayerEntityId, Is.Zero);

                var boardObject = new GameObject("GameplayBoardRoot");
                boardObject.transform.SetParent(root.transform, false);
                var boardRoot = boardObject.AddComponent<GameplayBoardRoot>();
                boardRoot.EnsureHierarchy();

                Assert.DoesNotThrow(() =>
                    presenter.ConfigurePlayerAnchorContext(PlayerEntityId, boardRoot.transform));
                Assert.That(runtime.PlayerEntityId, Is.EqualTo(PlayerEntityId));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Harness CreateHarness(ActualActionCase actionCase = ActualActionCase.PushSlide)
        {
            var root = new GameObject(nameof(GameplayPlayerActionCountRuntimeIntegrationTests));
            var boardObject = new GameObject("GameplayBoardRoot");
            boardObject.transform.SetParent(root.transform, false);
            var boardRoot = boardObject.AddComponent<GameplayBoardRoot>();
            boardRoot.EnsureHierarchy();

            var runtime = root.AddComponent<GameplayPlayerActionCountPresentationRuntime>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var prefabView = prefab.GetComponent<GameplayPlayerActionCountView>();
            Assert.That(prefabView, Is.Not.Null);
            var serializedRuntime = new SerializedObject(runtime);
            serializedRuntime.FindProperty("counterViewPrefab").objectReferenceValue = prefabView;
            serializedRuntime.ApplyModifiedPropertiesWithoutUndo();

            var presenter = root.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = root.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(registry, new TestViewFactory(boardRoot.EntityRoot));
            var worldState = CreateWorldState(actionCase);
            var snapshot = worldState.CreateSnapshot();

            presenter.Initialize(
                binder,
                snapshot.BoardBounds,
                snapshot.Topology,
                1f,
                GameplayTimingProfile.CreateDefault(),
                boardRoot);
            presenter.ConfigurePlayerAnchorContext(PlayerEntityId, boardRoot.transform);
            presenter.AttachPresentationExtension(runtime);
            presenter.PresentInitial(GetEntities(worldState), snapshot.Topology);

            return new Harness(root, boardRoot, presenter, runtime, registry, worldState);
        }

        private static WorldState CreateWorldState(ActualActionCase actionCase)
        {
            var boxCapabilities = actionCase == ActualActionCase.FlipSuccess || actionCase == ActualActionCase.FlipImpact
                ? BoxCapabilities.Flip
                : BoxCapabilities.Push;
            if (actionCase == ActualActionCase.PushDestroyFallback)
            {
                boxCapabilities |= BoxCapabilities.Destroy;
            }

            var entities = new List<EntityState>
            {
                CreateEntity(PlayerEntityId, 2, UnitRole.Player, EntityType.Unit, BoxCapabilities.None),
                CreateEntity(20, 3, UnitRole.None, EntityType.Box, boxCapabilities),
            };
            if (actionCase == ActualActionCase.PushImpact)
            {
                entities.Add(CreateEntity(30, 4, UnitRole.Enemy, EntityType.Unit, BoxCapabilities.None));
            }
            else if (actionCase == ActualActionCase.PushDestroyFallback)
            {
                entities.Add(CreateEntity(90, 4, UnitRole.None, EntityType.Wall, BoxCapabilities.None));
            }
            else if (actionCase == ActualActionCase.FlipImpact)
            {
                entities.Add(CreateEntity(30, 1, UnitRole.Enemy, EntityType.Unit, BoxCapabilities.None));
            }

            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 2)),
                new CubeTopologyState(FaceId.Floor));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[] { new PlayerLogic(PlayerEntityId) });
        }

        private static EntityState CreateEntity(
            int entityId,
            int x,
            UnitRole unitRole,
            EntityType type,
            BoxCapabilities boxCapabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 3,
                maxHp = 3,
                teamId = unitRole == UnitRole.Player ? 1 : unitRole == UnitRole.Enemy ? 2 : 0,
                type = type,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = boxCapabilities,
            };
        }

        private static TickPlayerActionPresentationSignal CreateActionSignal(
            int entityId,
            int sequence,
            PlayerActionKind actionKind = PlayerActionKind.Push,
            TickPlayerActionResolutionKind resolutionKind = TickPlayerActionResolutionKind.Success,
            TickPlayerFlipOutcomeKind flipOutcome = TickPlayerFlipOutcomeKind.None,
            int actionPlanId = 0)
        {
            return new TickPlayerActionPresentationSignal(
                entityId,
                actionKind,
                sequence,
                startedThisTick: false,
                completedThisTick: true,
                canceledThisTick: false,
                executedThisTick: true,
                isRecoveryPhase: false,
                resolutionKind: resolutionKind,
                targetEntityId: 20,
                direction: Direction.Right,
                actionPlanId: actionPlanId > 0 ? actionPlanId : sequence,
                flipOutcome: flipOutcome,
                hasFlipImpactContactTiming: flipOutcome == TickPlayerFlipOutcomeKind.Stay ||
                                            flipOutcome == TickPlayerFlipOutcomeKind.DestroySelf,
                flipTargetBoxEntityId: actionKind == PlayerActionKind.Flip ? 20 : 0);
        }

        private static TickPlayerDeathPresentationSignal CreateDeathSignal(int entityId)
        {
            return new TickPlayerDeathPresentationSignal(
                entityId,
                didDieThisTick: true,
                sourceEntityId: 20,
                fallbackFacing: Direction.Right,
                resolvedDamageSourceAvailable: true,
                damageAmountAtFatalHit: 1,
                deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse);
        }

        private static EntitySpawnPresentationSignal CreateRespawnSignal(int entityId)
        {
            return new EntitySpawnPresentationSignal(
                entityId,
                EntityPresentationKind.Player,
                EntitySpawnPresentationReason.PlayerRespawn,
                new SurfaceCell(FaceId.Floor, 0, 0),
                new CubeTopologyState(FaceId.Floor),
                Direction.Right,
                sourceTileFeature: null);
        }

        private static TickPresentationData CreatePresentationData(
            TickPlayerActionPresentationSignal[] playerActionSignals = null,
            TickPlayerDeathPresentationSignal[] playerDeathSignals = null,
            EntitySpawnPresentationSignal[] entitySpawnSignals = null)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                playerActionSignals ?? Array.Empty<TickPlayerActionPresentationSignal>(),
                playerLocomotionSignals: Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                playerDamageSignals: Array.Empty<TickPlayerDamagePresentationSignal>(),
                playerDeathSignals: playerDeathSignals ?? Array.Empty<TickPlayerDeathPresentationSignal>(),
                enemyDamageSignals: Array.Empty<TickEnemyDamagePresentationSignal>(),
                enemyActionSignals: Array.Empty<TickEnemyActionPresentationSignal>(),
                enemyJumpSignals: Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals: Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals: Array.Empty<FlipImpactPresentationSignal>(),
                entitySpawnSignals: entitySpawnSignals ?? Array.Empty<EntitySpawnPresentationSignal>());
        }

        private static TickResult CreateResult(
            int tickIndex,
            EntityState[] finalEntities,
            TickPresentationData presentationData)
        {
            return new TickResult(
                tickIndex,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static EntityState[] GetEntities(WorldState worldState)
        {
            var entities = new List<EntityState>();
            worldState.CreateSnapshot().EnumerateEntitiesOrdered(entities);
            return entities.ToArray();
        }

        private static void AssertEntityRootDirectChildrenAreViews(Transform entityRoot)
        {
            for (var i = 0; i < entityRoot.childCount; i++)
            {
                Assert.That(entityRoot.GetChild(i).GetComponent<GameplayEntityView>(), Is.Not.Null);
            }
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
                viewObject.transform.SetParent(_parent, false);
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(entity.entityId);
                return view;
            }
        }

        private sealed class Harness : IDisposable
        {
            public Harness(
                GameObject root,
                GameplayBoardRoot boardRoot,
                GameplayTickViewPresenter presenter,
                GameplayPlayerActionCountPresentationRuntime runtime,
                GameplayEntityViewRegistry registry,
                WorldState worldState)
            {
                Root = root;
                BoardRoot = boardRoot;
                Presenter = presenter;
                Runtime = runtime;
                Registry = registry;
                WorldState = worldState;
            }

            private GameObject Root { get; }

            public GameplayBoardRoot BoardRoot { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameplayPlayerActionCountPresentationRuntime Runtime { get; }

            public GameplayEntityViewRegistry Registry { get; }

            public WorldState WorldState { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
            }
        }
    }
}
