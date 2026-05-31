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
        public void PlayerDeath_StopsOrReleasesPlayerAttachedPersistentVfx()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var request = CreatePlayerAttachedPersistentRequest();
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(
                    request.CueId,
                    VfxPlaybackMode.Follow,
                    VfxStopPolicy.DetachThenStopEmittingThenRelease,
                    visibilityMode: GameplayVfxVisibilityMode.DefaultGameplay));

            controller.SetVisibilityContext(CreatePlayerVisibilityContext(hasView: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            Assert.That(registry.TryGet(request.PersistentKey, out var handle), Is.True);

            controller.SetVisibilityContext(CreatePlayerVisibilityContext(hasView: false));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            var fakeHandle = (FakeVfxPlaybackHandle)handle;
            Assert.That(fakeHandle.DetachCount, Is.EqualTo(1));
            Assert.That(fakeHandle.StopEmittingCount, Is.EqualTo(1));
            Assert.That(fakeHandle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(registry.ActiveCount, Is.Zero);

            controller.SetVisibilityContext(CreatePlayerVisibilityContext(hasView: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(pool.CreatedHandles[1], Is.Not.SameAs(fakeHandle));
        }

        [Test]
        [Category("Extended")]
        public void PlayerDeath_AllowsDeathOneShotTail_ButBlocksPersistentLoopsUntilRestart()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var controller = CreateController(pool, registry, runner);
            var oneShot = CreateRequest(isPersistent: false);
            var persistent = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());

            controller.Refresh(new GameplayVfxRequestPlan(new[] { oneShot, persistent }));
            var transientHandle = pool.CreatedHandles[0];
            var persistentHandle = pool.CreatedHandles[1];

            controller.BeginStageTerminalVfxSuppression();
            controller.Refresh(new GameplayVfxRequestPlan(new[] { persistent }));

            Assert.That(transientHandle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(persistentHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(registry.ActiveCount, Is.Zero);
            Assert.That(controller.PendingDelayedRequestCount, Is.Zero);
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void StageTerminal_DoesNotRefreshGameplayVfxAsIfGameplayContinues()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var removedPlayerRequest = CreatePlayerAttachedPersistentRequest();

            controller.BeginStageTerminalVfxSuppression();
            controller.SetVisibilityContext(CreatePlayerVisibilityContext(hasView: false));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { removedPlayerRequest }));

            Assert.That(pool.StartPersistentCallCount, Is.Zero);
            Assert.That(pool.PlayTransientCallCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.Zero);
            Assert.That(controller.IsStageTerminalSuppressed, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void RetryAfterPlayerDeath_ClearsVfxPoolAndPersistentRegistry()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(pool, registry);
            var persistent = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());

            controller.Refresh(new GameplayVfxRequestPlan(new[] { persistent }));
            controller.BeginStageTerminalVfxSuppression();
            controller.HardCleanupAll();
            controller.ClearStageTerminalVfxSuppression();
            controller.Refresh(new GameplayVfxRequestPlan(new[] { persistent }));

            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.HardCleanupCallCount, Is.EqualTo(1));
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(2));
            Assert.That(pool.CreatedHandles[0].State, Is.EqualTo(VfxLifetimeState.HardCleanup));
            Assert.That(pool.CreatedHandles[1].State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Extended")]
        public void PlayerDeath_ResultScreen_DoesNotLeaveTargetMarkersOrActionVfxVisible()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var runner = new VfxLifetimeRunner();
            var targetMarker = CreatePersistentRequest(
                GameplayVfxCueId.From(ProjectileVfxCue.ForwardCellDangerMarker),
                VfxAnchorKind.Cell);
            var actionMarker = CreatePersistentRequest(
                GameplayVfxCueId.From(PlayerVfxCue.PushWindup),
                VfxAnchorKind.Entity);
            var controller = CreateController(
                pool,
                registry,
                runner,
                CreatePolicy(
                    targetMarker.CueId,
                    VfxPlaybackMode.Loop,
                    VfxStopPolicy.StopEmittingThenRelease));
            var resolver = new MultiCueBindingResolver(new[]
            {
                CreatePolicy(targetMarker.CueId, VfxPlaybackMode.Loop, VfxStopPolicy.StopEmittingThenRelease),
                CreatePolicy(actionMarker.CueId, VfxPlaybackMode.Follow, VfxStopPolicy.DetachThenStopEmittingThenRelease),
            });
            controller = new GameplayVfxPresentationController(
                pool,
                new FakeVfxAnchorResolver(),
                resolver,
                registry,
                runner);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { targetMarker, actionMarker }));
            controller.BeginStageTerminalVfxSuppression();

            Assert.That(pool.CreatedHandles, Has.All.Matches<FakeVfxPlaybackHandle>(
                handle => handle.State == VfxLifetimeState.ReleasedToPool));
            Assert.That(registry.ActiveCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void StageTerminalVfxSuppression_DoesNotBreakTopologyTransitionPersistentValidation()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.ClearForTopologyTransitionStart(epoch: 3);
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));

            controller.BeginStageTerminalVfxSuppression();
            controller.ValidatePendingTopologyTransitionVisibility(new GameplayVfxRequestPlan(new[] { request }));
            controller.Refresh(
                new GameplayVfxRequestPlan(new[] { request }),
                GameplayVfxRefreshOptions.TopologyTransitionCompletion());

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(handle.ResumePresentationCount, Is.Zero);
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.ActiveCount, Is.Zero);
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
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxHidesWhenOwnerTopologySuspended()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(handle.SuspendPresentationCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.StopEmittingCount, Is.Zero);
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxDoesNotAdvanceWhileSuspended()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            handle.AdvancePresentationProgress(0.25f);

            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            handle.AdvancePresentationProgress(1f);

            Assert.That(handle.PresentationProgressSeconds, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxRestoresOnResumeWithoutDuplicate()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(handle.ResumePresentationCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(registry.TryGet(request.PersistentKey, out var currentHandle), Is.True);
            Assert.That(currentHandle, Is.SameAs(handle));
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_GameplayPauseSuspend_ComposesWithVisibilitySuspend()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.Visibility), Is.True);
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.GameplayPause), Is.True);

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(registry.TryGet(request.PersistentKey, out var currentHandle), Is.True);
            Assert.That(currentHandle, Is.SameAs(handle));

            controller.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void VfxSuspend_VisibilityThenGameplayPause_ReleaseGameplayPause_RemainsSuspendedByVisibility()
        {
            var controller = CreateControllerWithActiveHandle(out var handle);

            controller.SuspendPresentation(VfxPresentationSuspendReason.Visibility);
            controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
            controller.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.Visibility), Is.True);
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.GameplayPause), Is.False);
        }

        [Test]
        [Category("Core")]
        public void VfxSuspend_GameplayPauseThenVisibility_ReleaseVisibility_RemainsSuspendedByGameplayPause()
        {
            var controller = CreateControllerWithActiveHandle(out var handle);

            controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
            controller.SuspendPresentation(VfxPresentationSuspendReason.Visibility);
            controller.ResumePresentation(VfxPresentationSuspendReason.Visibility);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.GameplayPause), Is.True);
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.Visibility), Is.False);
        }

        [Test]
        [Category("Core")]
        public void VfxSuspend_TopologyThenGameplayPause_ReleaseGameplayPause_RemainsSuspendedByTopology()
        {
            var controller = CreateControllerWithActiveHandle(out var handle);

            controller.SuspendPresentation(VfxPresentationSuspendReason.TopologyTransition);
            controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
            controller.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.TopologyTransition), Is.True);
        }

        [Test]
        [Category("Core")]
        public void VfxSuspend_AllReasonsReleased_ResumesOnce()
        {
            var controller = CreateControllerWithActiveHandle(out var handle);

            controller.SuspendPresentation(VfxPresentationSuspendReason.Visibility);
            controller.SuspendPresentation(VfxPresentationSuspendReason.TopologyTransition);
            controller.SuspendPresentation(VfxPresentationSuspendReason.GameplayPause);
            controller.ResumePresentation(VfxPresentationSuspendReason.Visibility);
            controller.ResumePresentation(VfxPresentationSuspendReason.TopologyTransition);
            controller.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(handle.SuspendReasons, Is.EqualTo(VfxPresentationSuspendReason.None));
            Assert.That(handle.ResumePresentationCount, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void VfxSuspend_ReleasingAbsentReason_IsNoOp()
        {
            var controller = CreateControllerWithActiveHandle(out var handle);

            controller.SuspendPresentation(VfxPresentationSuspendReason.Visibility);
            controller.ResumePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.ResumePresentationCount, Is.Zero);
            Assert.That(handle.SuspendReasons, Is.EqualTo(VfxPresentationSuspendReason.Visibility));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxInitialSpawnNotBlockedByMissingSemanticState()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>()));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(registry.TryGet(request.PersistentKey, out var firstHandle), Is.True);
            Assert.That(firstHandle.State, Is.EqualTo(VfxLifetimeState.Active));

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isViewActiveInHierarchy: false));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            var handle = pool.CreatedHandles[0];
            Assert.That(handle.SuspendPresentationCount, Is.EqualTo(1));
            Assert.That(controller.LastVisibilityBlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.EntityViewInactive));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJumpWindupDangerVfxNotShownAsActiveDangerWhenPaused()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];

            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(handle.IsPresentationSuspended, Is.True);
            Assert.That(handle.ActiveDangerVisualEnabled, Is.False);
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
        public void CompletionVisibilityValidation_IncludesPersistentHandles_WithCompletionReplayPolicyNone()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);

            var replayPlan = FilterPersistentOnly(new GameplayVfxRequestPlan(new[] { request }));
            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.ValidatePendingTopologyTransitionVisibility(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(replayPlan.Requests, Is.Empty);
            Assert.That(pool.StartPersistentCallCount, Is.EqualTo(1));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.Visibility), Is.True);
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.TopologyTransition), Is.False);
            Assert.That(controller.LastVisibilityBlockReason, Is.EqualTo(GameplayVfxVisibilityBlockReason.JumpTopologySuspended));
        }

        [Test]
        [Category("Core")]
        public void TopologySuppressionEnd_DoesNotResumeVisibilityBlockedPersistentHandles()
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var controller = CreateController(
                pool,
                registry,
                policy: CreateJumperLandingTargetPolicy());
            var request = CreateJumperLandingTargetRequest();

            controller.SetVisibilityContext(CreateOwnerVisibilityContext());
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            var handle = pool.CreatedHandles[0];
            controller.SetVisibilityContext(CreateOwnerVisibilityContext(isJumpTopologySuspended: true));
            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            controller.ClearForTopologyTransitionStart(epoch: 1);
            controller.SetTopologyTransitionStartSuppression(false, 0);

            controller.ValidatePendingTopologyTransitionVisibility(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.Visibility), Is.True);
            Assert.That(handle.SuspendReasons.HasFlag(VfxPresentationSuspendReason.TopologyTransition), Is.False);
            Assert.That(handle.ActiveDangerVisualEnabled, Is.False);
        }

        [Test]
        [Category("Core")]
        public void JumperLandingTarget_WindupAndAirborne_DoNotShareIncorrectVisibilityContract()
        {
            var sourceTopology = new CubeTopologyState(FaceId.Floor);
            var destinationTopology = sourceTopology.Rotate(CubeRotationKind.Backward);
            var targetCell = new SurfaceCell(sourceTopology.BottomFace, 1, 1);
            var windupRequest = CreateJumperLandingTargetRequest(
                targetCell,
                destinationTopology,
                GameplayVfxJumpTargetPhase.Windup,
                sourceTopology,
                ownerSourceFace: sourceTopology.BottomFace);
            var airborneRequest = CreateJumperLandingTargetRequest(
                targetCell,
                destinationTopology,
                GameplayVfxJumpTargetPhase.Airborne,
                sourceTopology,
                ownerSourceFace: sourceTopology.BottomFace);
            var policy = CreateJumperLandingTargetPolicy();

            var windupDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                windupRequest,
                policy,
                CreateOwnerVisibilityContext());
            var airborneDecision = GameplayVfxVisibilityPolicy.EvaluateBeforeAnchor(
                airborneRequest,
                policy,
                CreateOwnerVisibilityContext(isJumpTopologySuspended: true));

            Assert.That(destinationTopology.IsFaceActive(targetCell.face), Is.True);
            Assert.That(windupDecision.IsVisible, Is.False);
            Assert.That(
                windupDecision.BlockReason,
                Is.EqualTo(GameplayVfxVisibilityBlockReason.JumpWindupSourceTopologyMismatch));
            Assert.That(airborneDecision.IsVisible, Is.False);
            Assert.That(
                airborneDecision.BlockReason,
                Is.EqualTo(GameplayVfxVisibilityBlockReason.JumpTopologySuspended));
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

        private static GameplayVfxPresentationController CreateControllerWithActiveHandle(
            out FakeVfxPlaybackHandle handle)
        {
            var pool = new FakeVfxPool();
            var registry = new VfxPersistentHandleRegistry();
            var request = CreateRequest(isPersistent: true, persistentKey: CreatePersistentKey());
            var controller = CreateController(pool, registry);

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));
            handle = pool.CreatedHandles[0];
            return controller;
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

        private static GameplayVfxRequest CreatePlayerAttachedPersistentRequest()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.RecoveryDust);
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 10,
                presentationSeed: 310,
                sourceEntityId: 10,
                cueId,
                VfxAnchor.ForEntity(10, VfxAnchorSlot.EntityCenter),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(cueId, VfxAnchorKind.Entity, entityId: 10));
        }

        private static GameplayVfxVisibilityContext CreatePlayerVisibilityContext(bool hasView)
        {
            return new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        10,
                        new GameplayVfxEntityVisibilityState(
                            hasView: hasView,
                            isViewActiveInHierarchy: hasView)
                    },
                });
        }

        private static GameplayVfxRequest CreateJumperLandingTargetRequest()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            return CreateJumperLandingTargetRequest(
                new SurfaceCell(FaceId.Floor, 1, 1),
                topology,
                GameplayVfxJumpTargetPhase.Windup,
                topology,
                FaceId.Floor);
        }

        private static GameplayVfxRequest CreateJumperLandingTargetRequest(
            SurfaceCell cell,
            CubeTopologyState anchorTopology,
            GameplayVfxJumpTargetPhase phase,
            CubeTopologyState requestSourceTopology,
            FaceId ownerSourceFace)
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget);
            return new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId: 1,
                presentationSeed: 7,
                sourceEntityId: 7,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    cell,
                    anchorTopology,
                    VfxAnchorSlot.CellFloor),
                timing: VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    cueId,
                    VfxAnchorKind.Cell,
                    entityId: 7,
                    cell: cell,
                    hasCell: true,
                    activationSequence: 1),
                topologyStopMode: GameplayVfxTopologyStopMode.TopologyHelperExempt,
                jumpTargetVisibility: new GameplayVfxJumpTargetVisibilityMetadata(
                    phase,
                    requestSourceTopology,
                    new SurfaceCell(ownerSourceFace, 1, 1),
                    cell));
        }

        private static VfxBindingRuntimePolicy CreateJumperLandingTargetPolicy()
        {
            return CreatePolicy(
                GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget),
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                visibilityMode: GameplayVfxVisibilityMode.DefaultGameplay);
        }

        private static GameplayVfxVisibilityContext CreateOwnerVisibilityContext(
            bool isViewActiveInHierarchy = true,
            bool isJumpTopologySuspended = false)
        {
            return new GameplayVfxVisibilityContext(
                new Dictionary<int, GameplayVfxEntityVisibilityState>
                {
                    {
                        7,
                        new GameplayVfxEntityVisibilityState(
                            hasView: true,
                            isViewActiveInHierarchy: isViewActiveInHierarchy,
                            hasSemanticState: true,
                            isJumpTopologySuspended: isJumpTopologySuspended)
                    },
                });
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
                        handle.State == VfxLifetimeState.TailPlaying ||
                        handle.State == VfxLifetimeState.PresentationSuspended)
                    {
                        if (GameplayVfxTopologyHelperExemptionPolicy.AllowsStopExemption(
                                handle.CueId,
                                handle.TopologyStopMode))
                        {
                            if (GameplayVfxTopologyHelperExemptionPolicy.AllowsPresentationSuspendPreserve(
                                    handle.CueId,
                                    handle.TopologyStopMode))
                            {
                                handle.SuspendPresentation(VfxPresentationSuspendReason.TopologyTransition);
                            }

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

            public void HardCleanupFamily(GameplayVfxFamily family)
            {
                foreach (var handle in CreatedHandles)
                {
                    if (handle.CueId.Family == family &&
                        handle.State != VfxLifetimeState.HardCleanup)
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

            public int SuspendPresentationCount { get; private set; }

            public int ResumePresentationCount { get; private set; }

            public float PresentationProgressSeconds { get; private set; }

            public VfxPresentationSuspendReason SuspendReasons { get; private set; }

            public bool IsPresentationSuspended => State == VfxLifetimeState.PresentationSuspended;

            public bool ActiveDangerVisualEnabled => !IsPresentationSuspended &&
                                                     State != VfxLifetimeState.ReleasedToPool &&
                                                     State != VfxLifetimeState.HardCleanup;

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
                SuspendReasons = VfxPresentationSuspendReason.None;
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
                SuspendReasons = VfxPresentationSuspendReason.None;
                StopEmittingCount++;
                State = VfxLifetimeState.StopEmitting;
            }

            public void Detach()
            {
                SuspendReasons = VfxPresentationSuspendReason.None;
                DetachCount++;
                State = VfxLifetimeState.Detached;
            }

            public void MarkTailPlaying()
            {
                SuspendReasons = VfxPresentationSuspendReason.None;
                TailPlayingCount++;
                State = VfxLifetimeState.TailPlaying;
            }

            public void Reanchor(in VfxResolvedAnchor anchor)
            {
                ReanchorCount++;
            }

            public void SuspendPresentation()
            {
                SuspendPresentation(VfxPresentationSuspendReason.Visibility);
            }

            public void SuspendPresentation(VfxPresentationSuspendReason reason)
            {
                if (reason == VfxPresentationSuspendReason.None ||
                    SuspendReasons.HasFlag(reason))
                {
                    return;
                }

                var wasSuspended = IsPresentationSuspended;
                SuspendReasons |= reason;
                SuspendPresentationCount++;
                if (!wasSuspended)
                {
                    State = VfxLifetimeState.PresentationSuspended;
                }
            }

            public void ResumePresentation()
            {
                ResumePresentation(VfxPresentationSuspendReason.Visibility);
            }

            public void ResumePresentation(VfxPresentationSuspendReason reason)
            {
                if (!IsPresentationSuspended ||
                    reason == VfxPresentationSuspendReason.None ||
                    !SuspendReasons.HasFlag(reason))
                {
                    return;
                }

                ResumePresentationCount++;
                SuspendReasons &= ~reason;
                if (SuspendReasons == VfxPresentationSuspendReason.None)
                {
                    State = VfxLifetimeState.Active;
                }
            }

            public void AdvancePresentationProgress(float deltaSeconds)
            {
                if (IsPresentationSuspended)
                {
                    return;
                }

                PresentationProgressSeconds += deltaSeconds > 0f ? deltaSeconds : 0f;
            }

            public void ReleaseToPool()
            {
                SuspendReasons = VfxPresentationSuspendReason.None;
                ReleaseCount++;
                State = VfxLifetimeState.ReleasedToPool;
            }

            public void HardCleanup()
            {
                SuspendReasons = VfxPresentationSuspendReason.None;
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

        private sealed class MultiCueBindingResolver : IVfxBindingResolver
        {
            private readonly Dictionary<GameplayVfxCueId, VfxBindingRuntimePolicy> policiesByCueId = new();

            public MultiCueBindingResolver(IEnumerable<VfxBindingRuntimePolicy> policies)
            {
                foreach (var policy in policies)
                {
                    policiesByCueId[policy.CueId] = policy;
                }
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                if (policiesByCueId.TryGetValue(request.CueId, out resolvedPolicy))
                {
                    return true;
                }

                resolvedPolicy = default;
                return false;
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
