using System.Collections.Generic;
using Game.Feature.Gameplay;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
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
            VfxStyleKey styleKey = default)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                playbackMode,
                stopPolicy,
                styleKey: styleKey);
        }

        private static VfxPersistentKey CreatePersistentKey()
        {
            return new VfxPersistentKey(
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchorKind.Entity,
                entityId: 7);
        }

        private sealed class FakeVfxPool : IVfxPool
        {
            private int nextHandleId;

            public int PlayTransientCallCount { get; private set; }

            public int StartPersistentCallCount { get; private set; }

            public int ReleaseCallCount { get; private set; }

            public int HardCleanupCallCount { get; private set; }

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
            }

            public int HandleId { get; }

            public GameplayVfxCueId CueId { get; }

            public VfxPersistentKey PersistentKey { get; }

            public bool IsPersistent { get; }

            public VfxLifetimeState State { get; private set; }

            public int StopEmittingCount { get; private set; }

            public int DetachCount { get; private set; }

            public int ReleaseCount { get; private set; }

            public int HardCleanupCount { get; private set; }

            public void MarkSpawned()
            {
                State = VfxLifetimeState.Spawned;
            }

            public void MarkActive()
            {
                State = VfxLifetimeState.Active;
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
                State = VfxLifetimeState.TailPlaying;
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
            public bool TryResolve(in GameplayVfxRequest request, out VfxResolvedAnchor resolvedAnchor)
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
                    VfxStopPolicy.StopEmittingThenRelease);
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
