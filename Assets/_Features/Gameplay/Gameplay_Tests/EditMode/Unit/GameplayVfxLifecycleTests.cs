using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxLifecycleTests
    {
        [Test]
        [Category("Extended")]
        public void TransientRequest_UsesPoolAndDoesNotRegisterPersistentHandle()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);

            controller.Refresh(new GameplayVfxRequestPlan(new[]
            {
                CreateRequest(isPersistent: false),
            }));

            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(1));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(0));
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(0));
            Assert.That(registry.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PersistentRequest_StartsOnceForSameKey()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());
            var plan = new GameplayVfxRequestPlan(new[] { request });

            controller.Refresh(plan);
            controller.Refresh(plan);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(0));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void PersistentRequest_StyleChangeForSameKey_StopsExistingAndStartsReplacement()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var key = CreatePersistentKey();
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var greenRequest = CreateRequest(isPersistent: true, persistentKey: key, styleKey: VfxStyleKey.Green);
            var yellowRequest = CreateRequest(isPersistent: true, persistentKey: key, styleKey: VfxStyleKey.Yellow);
            var resolver = new StyleSwitchingBindingResolver(cueId);
            var controller = new GameplayVfxPresentationController(
                pool,
                new FakeVfxAnchorResolver(),
                resolver,
                registry,
                runner);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { greenRequest }));
            var firstHandle = pool.CreatedHandles[0];
            controller.Refresh(new GameplayVfxRequestPlan(new[] { yellowRequest }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(firstHandle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(firstHandle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.CreatedHandles[1].PersistentKey, Is.EqualTo(key));
        }

        [Test]
        [Category("Extended")]
        public void MissingPersistentDesiredKey_StopsTailsAndReleasesAfterTailCompletion()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var key = CreatePersistentKey();
            var request = CreateRequest(
                isPersistent: true,
                persistentKey: key);
            var policy = CreatePolicy(request.CueId, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease);
            controller = CreateController(pool, registry, runner, policy);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            Assert.That(registry.TryGet(key, out var handle), Is.True);

            controller.Refresh(GameplayVfxRequestPlan.Empty);

            var fakeHandle = (FakeVfxPlaybackHandle)handle;
            Assert.That(fakeHandle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(fakeHandle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(fakeHandle.ReleaseCount, Is.EqualTo(0));

            runner.AdvanceTail(handle, tailComplete: true, pool);
            registry.ReleaseCompleted();

            Assert.That(fakeHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PersistentVfx_FrontFaceInactiveWhileRequestContinues_StopsOrDespawnsExistingInstance()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var key = CreatePersistentKey();
            var request = CreateRequest(isPersistent: true, persistentKey: key);
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(
                    request.CueId,
                    VfxPlaybackMode.Loop,
                    VfxStopPolicy.StopEmittingThenRelease,
                    visibilityMode: GameplayVfxVisibilityMode.DefaultGameplay));

            controller.SetVisibilityContext(new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        7,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: true,
                            hasSemanticState: true)
                    },
                }));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            Assert.That(registry.TryGet(key, out var handle), Is.True);

            controller.SetVisibilityContext(new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        7,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: true,
                            hasSemanticState: true,
                            isFrontFaceInactive: true)
                    },
                }));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            var fakeHandle = (FakeVfxPlaybackHandle)handle;
            Assert.That(fakeHandle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(fakeHandle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(controller.VisibilityBlockedCount, Is.EqualTo(1));
            Assert.That(controller.LastVisibilityBlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.FrontFaceInactive));
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_SemanticInactiveThenActive_RecreatesAfterAllowedAgain()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var key = CreatePersistentKey();
            var request = CreateRequest(isPersistent: true, persistentKey: key);
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(
                    request.CueId,
                    VfxPlaybackMode.Loop,
                    VfxStopPolicy.StopEmittingThenRelease,
                    visibilityMode: GameplayVfxVisibilityMode.DefaultGameplay));
            var activeContext = new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        7,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: true,
                            hasSemanticState: true)
                    },
                });
            var inactiveContext = new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        7,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: true,
                            hasSemanticState: true,
                            isFrontFaceInactive: true)
                    },
                });

            controller.SetVisibilityContext(activeContext);
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var firstHandle = pool.CreatedHandles[0];

            controller.SetVisibilityContext(inactiveContext);
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            Assert.That(firstHandle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));

            controller.SetVisibilityContext(activeContext);
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(firstHandle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(registry.TryGet(key, out var currentHandle), Is.True);
            Assert.That(currentHandle, Is.Not.SameAs(firstHandle));
            Assert.That(currentHandle.State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_StopIfActive_IsIdempotent()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var key = CreatePersistentKey();
            var request = CreateRequest(isPersistent: true, persistentKey: key);
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(request.CueId, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease));

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            Assert.That(registry.StopIfActive(key, VfxStopPolicy.StopEmittingThenRelease, runner), Is.True);
            Assert.That(registry.StopIfActive(key, VfxStopPolicy.StopEmittingThenRelease, runner), Is.False);

            Assert.That(handle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_TailPlayingHandle_IsNotReusableForNewDesired()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var key = CreatePersistentKey();
            var request = CreateRequest(isPersistent: true, persistentKey: key);
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(request.CueId, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease));

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var firstHandle = pool.CreatedHandles[0];
            firstHandle.MarkTailPlaying();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(registry.TryGet(key, out var currentHandle), Is.True);
            Assert.That(currentHandle, Is.Not.SameAs(firstHandle));
            Assert.That(currentHandle.State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_ActiveHandle_SameBinding_IsReusable()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());
            var plan = new GameplayVfxRequestPlan(new[] { request });

            controller.Refresh(plan);
            var firstHandle = pool.CreatedHandles[0];
            controller.Refresh(plan);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.TryGet(request.PersistentKey, out var currentHandle), Is.True);
            Assert.That(currentHandle, Is.SameAs(firstHandle));
            Assert.That(currentHandle.State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionRunning_SuppressesNewGameplayVfxStarts()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionStart());

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionStart_ClearsPersistentGameplayVfxImmediatelyWithoutTail()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.ClearForTopologyTransitionStart(epoch: 1);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(handle.LastStopMode, Is.EqualTo(GameplayVfxStopMode.TopologyTransitionHardClear));
            Assert.That(handle.StopEmittingCount, Is.Zero);
            Assert.That(handle.TailPlayingCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionCompletion_SpawnsDestinationPersistentVfxWithSoftStart()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var request = CreateTileFeatureLoopRequest().WithSoftSpawnDelay(0.12f);

            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);

            controller.Update(0.119f);
            Assert.That(pool.StartPersistentCallCount, Is.Zero);

            controller.Update(0.002f);
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Flip_ButtonActiveLoop_Runtime_Does_Not_Start_Before_Barrier()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ButtonActiveLoop,
                delaySeconds: 0.936f);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.Update(0.62f);

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void Flip_ButtonActiveLoop_Ignores_Raw_IsActive_Until_GateRelease()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var gatedRequest = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ButtonActiveLoop,
                delaySeconds: 0.936f);
            var rawSemanticRequest = CreateTileFeatureLoopRequest(TileFeatureVfxCue.ButtonActiveLoop);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { gatedRequest }));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { rawSemanticRequest }));
            controller.Update(0.62f);

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void Flip_ButtonActiveLoop_Starts_On_GateRelease()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ButtonActiveLoop,
                delaySeconds: 0.936f);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.Update(0.935f);
            Assert.That(pool.StartPersistentCallCount, Is.Zero);

            controller.Update(0.002f);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.CreatedHandles[0].CueId, Is.EqualTo(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
        }

        [Test]
        [Category("Extended")]
        public void Flip_ButtonActiveLoop_No_Immediate_Reconcile_Path()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var rawSemanticRequest = CreateTileFeatureLoopRequest(TileFeatureVfxCue.ButtonActiveLoop);
            var gatedRequest = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ButtonActiveLoop,
                delaySeconds: 0.936f);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { rawSemanticRequest }));
            var existing = pool.CreatedHandles[0];
            controller.Refresh(new GameplayVfxRequestPlan(new[] { gatedRequest }));

            Assert.That(existing.StopEmittingCount, Is.EqualTo(1));
            Assert.That(existing.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(registry.ActiveCount, Is.Zero);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { rawSemanticRequest }));
            controller.Update(0.62f);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ButtonActive_And_ExitOpen_VFX_ActualPlayTime_Not_Just_RequestDelay()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var buttonLoop = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ButtonActiveLoop,
                delaySeconds: 0.936f);
            var exitLoop = CreateTileFeatureLoopRequest(
                TileFeatureVfxCue.ExitOpenLoop,
                tileId: 20,
                delaySeconds: 0.936f);
            var exitOpened = CreateDelayedExitOpenedRequest(0.936f);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { buttonLoop, exitLoop, exitOpened }));
            controller.Update(0.62f);

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(pool.PlayTransientCallCount, Is.Zero);

            controller.Update(0.317f);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(1));
            Assert.That(
                pool.CreatedHandles.Select(handle => handle.CueId).ToArray(),
                Has.Member(GameplayVfxCueId.From(TileFeatureVfxCue.ButtonActiveLoop)));
            Assert.That(
                pool.CreatedHandles.Select(handle => handle.CueId).ToArray(),
                Has.Member(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpenLoop)));
            Assert.That(
                pool.CreatedHandles.Select(handle => handle.CueId).ToArray(),
                Has.Member(GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpened)));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionStart_DropsPendingSoftSpawn()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var request = CreateTileFeatureLoopRequest().WithSoftSpawnDelay(0.12f);

            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            controller.ClearForTopologyTransitionStart(epoch: 2);
            controller.SetTopologyTransitionStartSuppression(false, 0);
            controller.Update(1f);

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void ActiveBothVfx_IsClearedAtStartAndSoftRespawnedAtCompletion()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var sourceActive = CreateTileFeatureLoopRequest();
            var destinationActive = new GameplayVfxRequest(
                tickIndex: 2,
                sequenceId: 2,
                presentationSeed: 32,
                cueId: sourceActive.CueId,
                anchor: VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Front, 1, 1),
                    new CubeTopologyState(FaceId.Front)),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    sourceActive.CueId,
                    VfxAnchorKind.Cell,
                    tileId: 11,
                    cell: new SurfaceCell(FaceId.Front, 1, 1),
                    hasCell: true,
                    effectIndex: 1));

            controller.Refresh(new GameplayVfxRequestPlan(new[] { sourceActive }));
            var sourceHandle = pool.CreatedHandles[0];
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);

            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { destinationActive.WithSoftSpawnDelay(0.12f) }),
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            controller.Update(0.12f);

            Assert.That(sourceHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(pool.CreatedHandles[1], Is.Not.SameAs(sourceHandle));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionRunning_DoesNotMarkDesiredOrReanchorClearedHandles()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            controller.ClearForTopologyTransitionStart(epoch: 1);

            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionStart());

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TileFeatureVfx_TopologyTransition_CompletionReconcile_DoesNotReviveDestinationInactiveVfx()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);
            controller.Refresh(
                GameplayVfxRequestPlan.Empty,
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void PresentationOnlyTopologyHelper_ClearPolicy_IsExplicitlyExempt()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);
            var request = new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 1,
                presentationSeed: 31,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor)),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: 0,
                    cell: new SurfaceCell(FaceId.Floor, 1, 1),
                    hasCell: true),
                topologyStopMode: GameplayVfxTopologyStopMode.TopologyHelperExempt,
                topologySpawnMode: GameplayVfxTopologySpawnMode.TopologyHelperExempt);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                visibilityMode: GameplayVfxVisibilityMode.PresentationOnly);
            var controller = CreateController(pool, registry, policy: policy);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);
            controller.Refresh(
                GameplayVfxRequestPlan.Empty,
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.CreatedHandles[0].State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void VisibilityPolicy_DoesNotOverrideTopologyTransitionHardClear()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.SetTopologyTransitionStartSuppression(true, epoch: 1);
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void VisibilityPolicy_DoesNotOverrideTopologyTransitionHardClear_DirectControllerPath()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var policy = CreatePolicy(
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                visibilityMode: GameplayVfxVisibilityMode.PresentationOnly);
            var controller = CreateController(pool, registry, policy: policy);
            var request = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());

            controller.SetTopologyTransitionStartSuppression(true, epoch: 1);
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionCompletion_FilterPersistentOnly_RejectsActionImpactProjectile()
        {
            var action = CreatePersistentRequest(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup), VfxAnchorKind.Entity);
            var impact = CreatePersistentRequest(GameplayVfxCueId.From(BoxVfxCue.ImpactTransientBreak), VfxAnchorKind.Cell);
            var projectile = CreatePersistentRequest(GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellProjectileFlight), VfxAnchorKind.Cell);

            var filtered = FilterPersistentOnly(new GameplayVfxRequestPlan(new[] { action, impact, projectile }));

            Assert.That(filtered.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionCompletion_AllowsSteadyStatePersistentLoopsOnly()
        {
            var steadyLoop = CreateTileFeatureLoopRequest();
            var action = CreatePersistentRequest(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup), VfxAnchorKind.Entity);

            var filtered = FilterPersistentOnly(new GameplayVfxRequestPlan(new[] { steadyLoop, action }));

            Assert.That(filtered.Requests.Count, Is.EqualTo(1));
            Assert.That(filtered.Requests[0].CueId, Is.EqualTo(steadyLoop.CueId));
            Assert.That(filtered.Requests[0].CompletionReplayPolicy, Is.EqualTo(GameplayVfxCompletionReplayPolicy.SteadyStatePersistentLoop));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionCompletion_DoesNotReplayPersistentActionCue()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var action = CreatePersistentRequest(GameplayVfxCueId.From(EnemyVfxCue.UtilityWindup), VfxAnchorKind.Entity);
            var filtered = FilterPersistentOnly(new GameplayVfxRequestPlan(new[] { action }));

            controller.Refresh(filtered, GameplayVfxRefreshOptions.TopologyTransitionCompletion());

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionCompletion_DoesNotReplayTransientOneShots()
        {
            var transient = CreateRequest(isPersistent: false);

            var filtered = FilterPersistentOnly(new GameplayVfxRequestPlan(new[] { transient }));

            Assert.That(filtered.Requests, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionHardClear_RegistryAndPoolStayConsistent()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            controller.ClearForTopologyTransitionStart(epoch: 1);
            pool.HardClearActiveForTopologyTransition();

            Assert.That(registry.ActiveCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(pool.ReleasedHandles, Does.Contain(handle));
            Assert.That(handle.LastStopMode, Is.EqualTo(GameplayVfxStopMode.TopologyTransitionHardClear));
            Assert.That(handle.TailPlayingCount, Is.Zero);
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionHardClear_DoesNotReuseClearedHandles()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var clearedHandle = pool.CreatedHandles[0];
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionStart());
            controller.SetTopologyTransitionStartSuppression(false, 0);
            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request.WithSoftSpawnDelay(0.12f) }),
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            controller.Update(0.12f);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(pool.CreatedHandles[1], Is.Not.SameAs(clearedHandle));
            Assert.That(clearedHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionHardClear_ExemptHandlesRemainConsistentAcrossRegistryAndPool()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var gameplay = CreateTileFeatureLoopRequest();
            var helper = CreateTopologyHelperExemptRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { gameplay, helper }));
            var gameplayHandle = FindCreatedHandle(pool, gameplay.CueId);
            var helperHandle = FindCreatedHandle(pool, helper.CueId);
            controller.ClearForTopologyTransitionStart(epoch: 1);
            pool.HardClearActiveForTopologyTransition();

            Assert.That(gameplayHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(helperHandle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(registry.TryGet(helper.PersistentKey, out var current), Is.True);
            Assert.That(current, Is.SameAs(helperHandle));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionHardClear_RepeatedStartIsIdempotent()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var request = CreateTileFeatureLoopRequest();

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.ClearForTopologyTransitionStart(epoch: 2);
            pool.HardClearActiveForTopologyTransition();
            pool.HardClearActiveForTopologyTransition();

            Assert.That(registry.ActiveCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(pool.CreatedHandles[0].LastStopMode, Is.EqualTo(GameplayVfxStopMode.TopologyTransitionHardClear));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionHardClear_ZeroDurationTransition_OrderingIsSafe()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var source = CreateTileFeatureLoopRequest();
            var destination = CreateTileFeatureLoopRequest(tileId: 11, effectIndex: 2);
            var transient = CreateRequest(sequenceId: 9, isPersistent: false);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { source }));
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);
            var completionPlan = FilterPersistentOnly(new GameplayVfxRequestPlan(new[]
            {
                destination.WithSoftSpawnDelay(0.12f),
                transient,
            }));
            controller.Refresh(completionPlan, GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            controller.Update(0.12f);
            controller.Refresh(completionPlan, GameplayVfxRefreshOptions.TopologyTransitionCompletion());
            controller.Update(0.12f);

            Assert.That(pool.PlayTransientCallCount, Is.Zero);
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void DetachThenStopEmittingPolicy_PreservesTailUntilCompletion()
        {
            var pool = new FakeVfxPool();
            var runner = new VfxLifetimeRunner();
            var handle = new FakeVfxPlaybackHandle(1, CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey()));
            handle.MarkSpawned();
            handle.MarkActive();

            runner.Stop(handle, VfxStopPolicy.DetachThenStopEmittingThenRelease);

            Assert.That(handle.DetachCount, Is.EqualTo(1));
            Assert.That(handle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(handle.ReleaseCount, Is.EqualTo(0));

            runner.AdvanceTail(handle, tailComplete: false, pool);
            Assert.That(handle.ReleaseCount, Is.EqualTo(0));

            runner.AdvanceTail(handle, tailComplete: true, pool);
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(handle.ReleaseCount, Is.EqualTo(1));
            Assert.That(pool.ReleaseCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void HardCleanupAll_ReleasesPersistentAndDelegatesPoolCleanup()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var persistentRequest = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());
            var transientRequest = CreateRequest(sequenceId: 2, isPersistent: false);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { persistentRequest, transientRequest }));

            controller.HardCleanupAll();

            Assert.That(registry.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.ReleaseCallCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(pool.HardCleanupCallCount, Is.EqualTo(1));
            Assert.That(pool.CreatedHandles, Has.All.Matches<FakeVfxPlaybackHandle>(
                handle => handle.State == VfxLifetimeState.HardCleanup));
        }

        private static GameplayVfxPresentationController CreateController(
            FakeVfxPool pool,
            VfxPersistentHandleRegistry registry,
            VfxLifetimeRunner runner = null,
            VfxBindingRuntimePolicy? policy = null)
        {
            return new GameplayVfxPresentationController(
                pool,
                new FakeVfxAnchorResolver(),
                new FakeVfxBindingResolver(policy),
                registry,
                runner ?? new VfxLifetimeRunner());
        }

        private static GameplayVfxRequest CreateRequest(
            int tickIndex = 1,
            int sequenceId = 1,
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default,
            VfxStyleKey styleKey = default)
        {
            return new GameplayVfxRequest(
                tickIndex,
                sequenceId,
                presentationSeed: sequenceId * 31,
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchor.ForEntity(7),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent,
                persistentKey,
                styleKey);
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            VfxStyleKey styleKey = default,
            GameplayVfxVisibilityMode visibilityMode = GameplayVfxVisibilityMode.PresentationOnly)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                playbackMode,
                stopPolicy,
                styleKey: styleKey,
                visibilityMode: visibilityMode);
        }

        private static VfxPersistentKey CreatePersistentKey()
        {
            return new VfxPersistentKey(
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchorKind.Entity,
                entityId: 7);
        }

        private static GameplayVfxRequest CreatePersistentRequest(
            GameplayVfxCueId cueId,
            VfxAnchorKind anchorKind,
            GameplayVfxCompletionReplayPolicy replayPolicy = GameplayVfxCompletionReplayPolicy.None)
        {
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            var key = anchorKind == VfxAnchorKind.Entity
                ? new VfxPersistentKey(cueId, VfxAnchorKind.Entity, entityId: 7)
                : new VfxPersistentKey(cueId, VfxAnchorKind.Cell, tileId: 10, cell: cell, hasCell: true);
            var anchor = anchorKind == VfxAnchorKind.Entity
                ? VfxAnchor.ForEntity(7)
                : VfxAnchor.ForCell(cell, new CubeTopologyState(FaceId.Floor));
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: cueId.Code,
                presentationSeed: cueId.Code * 31,
                cueId,
                anchor,
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: key,
                completionReplayPolicy: replayPolicy);
        }

        private static GameplayVfxRequest CreateTopologyHelperExemptRequest()
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipImpactStayTrail);
            var cell = new SurfaceCell(FaceId.Floor, 2, 2);
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 20,
                presentationSeed: 20,
                cueId,
                VfxAnchor.ForCell(cell, new CubeTopologyState(FaceId.Floor)),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(cueId, VfxAnchorKind.Cell, tileId: 20, cell: cell, hasCell: true),
                topologyStopMode: GameplayVfxTopologyStopMode.TopologyHelperExempt,
                topologySpawnMode: GameplayVfxTopologySpawnMode.TopologyHelperExempt);
        }

        private static GameplayVfxRequestPlan FilterPersistentOnly(GameplayVfxRequestPlan plan)
        {
            var method = typeof(GameplayVfxProductionRuntime).GetMethod(
                "FilterPersistentOnly",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (GameplayVfxRequestPlan)method.Invoke(null, new object[] { plan });
        }

        private static FakeVfxPlaybackHandle FindCreatedHandle(FakeVfxPool pool, GameplayVfxCueId cueId)
        {
            for (var i = 0; i < pool.CreatedHandles.Count; i++)
            {
                var handle = pool.CreatedHandles[i];
                if (handle.CueId.Equals(cueId))
                {
                    return handle;
                }
            }

            Assert.Fail($"Expected a created handle for cue '{cueId}'.");
            return null;
        }

        private static GameplayVfxRequest CreateTileFeatureLoopRequest(
            TileFeatureVfxCue cue = TileFeatureVfxCue.BarricadeActiveLoop,
            int tileId = 10,
            int effectIndex = 1,
            float delaySeconds = 0f)
        {
            var cueId = GameplayVfxCueId.From(cue);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 1,
                presentationSeed: 31,
                sourceEntityId: 0,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(cell, new CubeTopologyState(FaceId.Floor)),
                timing: delaySeconds > 0f
                    ? VfxTimingKind.Delayed
                    : VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    tileId: tileId,
                    cell: cell,
                    hasCell: true,
                    effectIndex: effectIndex),
                delaySeconds: delaySeconds,
                topologyStopMode: GameplayVfxTopologyStopMode.HardClearAtTransitionStart,
                topologySpawnMode: GameplayVfxTopologySpawnMode.SuppressDuringTransition,
                completionReplayPolicy: GameplayVfxCompletionReplayPolicy.SteadyStatePersistentLoop);
        }

        private static GameplayVfxRequest CreateDelayedExitOpenedRequest(float delaySeconds)
        {
            var cueId = GameplayVfxCueId.From(TileFeatureVfxCue.ExitOpened);
            var cell = new SurfaceCell(FaceId.Floor, 1, 1);
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 2,
                presentationSeed: 62,
                sourceEntityId: 0,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(cell, new CubeTopologyState(FaceId.Floor)),
                timing: delaySeconds > 0f
                    ? VfxTimingKind.Delayed
                    : VfxTimingKind.ImmediateOnTickPresentation,
                delaySeconds: delaySeconds);
        }

        private sealed class FakeVfxPool : IVfxPool
        {
            private int nextHandleId;

            public int PlayTransientCallCount { get; private set; }

            public int StartPersistentCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

            public int ActiveCount
            {
                get
                {
                    var count = 0;
                    for (var i = 0; i < CreatedHandles.Count; i++)
                    {
                        var state = CreatedHandles[i].State;
                        if (state != VfxLifetimeState.ReleasedToPool &&
                            state != VfxLifetimeState.HardCleanup)
                        {
                            count++;
                        }
                    }

                    return count;
                }
            }

            public List<FakeVfxPlaybackHandle> CreatedHandles { get; } = new();

            public List<IVfxPlaybackHandle> ReleasedHandles { get; } = new();

            public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
            {
                PlayTransientCallCount++;
                return CreateHandle(command.Request);
            }

            public IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command)
            {
                StartPersistentCallCount++;
                return CreateHandle(command.Request);
            }

            public void Release(IVfxPlaybackHandle handle)
            {
                ReleaseCallCount++;
                ReleasedHandles.Add(handle);
            }

            public void HardClearActiveForTopologyTransition()
            {
                foreach (var handle in CreatedHandles)
                {
                    if (handle.State == VfxLifetimeState.Active ||
                        handle.State == VfxLifetimeState.Spawned ||
                        handle.State == VfxLifetimeState.StopEmitting ||
                        handle.State == VfxLifetimeState.TailPlaying)
                    {
                        if (GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                                handle.CueId,
                                handle.TopologyStopMode))
                        {
                            continue;
                        }

                        handle.Stop(GameplayVfxStopMode.TopologyTransitionHardClear);
                        Release(handle);
                    }
                }
            }

            public void HardCleanupAll()
            {
                HardCleanupCallCount++;
                foreach (var handle in CreatedHandles)
                {
                    if (handle.State != VfxLifetimeState.HardCleanup)
                    {
                        handle.HardCleanup();
                    }
                }
            }

            private FakeVfxPlaybackHandle CreateHandle(in GameplayVfxRequest request)
            {
                var handle = new FakeVfxPlaybackHandle(++nextHandleId, request);
                handle.MarkSpawned();
                handle.MarkActive();
                CreatedHandles.Add(handle);
                return handle;
            }
        }

        private sealed class FakeVfxPlaybackHandle : IVfxPlaybackHandle
        {
            public FakeVfxPlaybackHandle(int handleId, in GameplayVfxRequest request)
            {
                HandleId = handleId;
                CueId = request.CueId;
                PersistentKey = request.PersistentKey;
                IsPersistent = request.IsPersistent;
                TopologyStopMode = request.TopologyStopMode;
                TopologySpawnMode = request.TopologySpawnMode;
            }

            public int HandleId { get; }

            public GameplayVfxCueId CueId { get; }

            public VfxPersistentKey PersistentKey { get; }

            public bool IsPersistent { get; }

            public VfxLifetimeState State { get; private set; }

            public GameplayVfxTopologyStopMode TopologyStopMode { get; }

            public GameplayVfxTopologySpawnMode TopologySpawnMode { get; }

            public int StopEmittingCount { get; private set; }

            public int DetachCount { get; private set; }

            public int TailPlayingCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public int HardCleanupCount { get; private set; }

            public int ReanchorCount { get; private set; }

            public GameplayVfxStopMode LastStopMode { get; private set; }

            public void MarkSpawned()
            {
                State = VfxLifetimeState.Spawned;
            }

            public void MarkActive()
            {
                State = VfxLifetimeState.Active;
            }

            public void Stop(GameplayVfxStopMode mode)
            {
                LastStopMode = mode;
                switch (mode)
                {
                    case GameplayVfxStopMode.Default:
                    case GameplayVfxStopMode.StopWithTail:
                        StopEmitting();
                        MarkTailPlaying();
                        break;
                    case GameplayVfxStopMode.StopEmittingAndClear:
                    case GameplayVfxStopMode.ReleaseImmediately:
                    case GameplayVfxStopMode.TopologyTransitionHardClear:
                        State = VfxLifetimeState.ReleasedToPool;
                        break;
                    default:
                        throw new System.ArgumentOutOfRangeException(nameof(mode), mode, null);
                }
            }

            public void StopEmitting()
            {
                StopEmittingCount++;
                State = VfxLifetimeState.StopEmitting;
            }

            public void Detach()
            {
                DetachCount++;
                State = VfxLifetimeState.Detached;
            }

            public void MarkTailPlaying()
            {
                TailPlayingCount++;
                State = VfxLifetimeState.TailPlaying;
            }

            public void Reanchor(in VfxResolvedAnchor anchor)
            {
                ReanchorCount++;
            }

            public void ReleaseToPool()
            {
                ReleaseCount++;
                State = VfxLifetimeState.ReleasedToPool;
            }

            public void HardCleanup()
            {
                HardCleanupCount++;
                State = VfxLifetimeState.HardCleanup;
            }
        }

        private sealed class FakeVfxAnchorResolver : IVfxAnchorResolver
        {
            public bool TryResolve(
                in GameplayVfxRequest request,
                VfxBindingRuntimePolicy policy,
                out VfxResolvedAnchor resolvedAnchor)
            {
                resolvedAnchor = VfxResolvedAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter);
                return true;
            }
        }

        private sealed class FakeVfxBindingResolver : IVfxBindingResolver
        {
            private readonly VfxBindingRuntimePolicy? policy;

            public FakeVfxBindingResolver(VfxBindingRuntimePolicy? policy)
            {
                this.policy = policy;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = policy ?? CreatePolicy(
                    request.CueId,
                    request.IsPersistent ? VfxPlaybackMode.Loop : VfxPlaybackMode.OneShot,
                    VfxStopPolicy.StopEmittingThenRelease,
                    visibilityMode: GameplayVfxVisibilityMode.PresentationOnly);
                return true;
            }
        }

        private sealed class StyleSwitchingBindingResolver : IVfxBindingResolver
        {
            private readonly GameplayVfxCueId cueId;

            public StyleSwitchingBindingResolver(GameplayVfxCueId cueId)
            {
                this.cueId = cueId;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = CreatePolicy(
                    cueId,
                    VfxPlaybackMode.Loop,
                    VfxStopPolicy.StopEmittingThenRelease,
                    request.StyleKey);
                return true;
            }
        }
    }
}
