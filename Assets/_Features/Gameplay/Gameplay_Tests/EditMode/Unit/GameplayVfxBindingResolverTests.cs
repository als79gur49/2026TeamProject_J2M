using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBindingResolverTests
    {
        [Test]
        [Category("Extended")]
        public void BindingResolver_UsesFamilyProfileBeforeHostDefault()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.SkipOptional);
            var profilePolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.FailFast);
            var resolver = new CompositeVfxBindingResolver(
                new VfxCueMap(new[] { hostPolicy }),
                new Dictionary<GameplayVfxFamily, VfxProfile>
                {
                    { GameplayVfxFamily.Player, new VfxProfile(GameplayVfxFamily.Player, new[] { profilePolicy }) },
                });

            Assert.That(resolver.TryResolve(CreateRequest(cueId), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(profilePolicy));
        }

        [Test]
        [Category("Extended")]
        public void BindingResolver_FallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var hostPolicy = CreatePolicy(cueId, VfxMissingAnchorPolicy.ReportDiagnostic);
            var resolver = new CompositeVfxBindingResolver(
                new VfxCueMap(new[] { hostPolicy }),
                new Dictionary<GameplayVfxFamily, VfxProfile>());

            Assert.That(resolver.TryResolve(CreateRequest(cueId), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsPersistentRequestWithOneShotPolicy()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var request = CreateRequest(
                cueId,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(cueId, VfxAnchorKind.Entity, entityId: 3));
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.OneShot));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsTransientRequestWithLoopPolicy()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.Loop));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { CreateRequest(cueId) })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_RejectsPersistentRequestWithoutKey()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var request = CreateRequest(cueId, isPersistent: true);
            var controller = CreateController(CreatePolicy(cueId, playbackMode: VfxPlaybackMode.Loop));

            Assert.Throws<InvalidOperationException>(() => controller.Refresh(new GameplayVfxRequestPlan(new[] { request })));
            Assert.That(controller.CompatibilityFailureCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Controller_UsesFallbackCellWhenBindingPolicyAllowsIt()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.Damage);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 2, 3);
            var request = new GameplayVfxRequest(
                1,
                1,
                17,
                cueId,
                VfxAnchor.ForEntity(
                    3,
                    VfxAnchorSlot.EntityCenter,
                    fallbackCell,
                    new CubeTopologyState(FaceId.Floor),
                    hasFallbackCell: true),
                VfxTimingKind.ImmediateOnTickPresentation);
            var pool = new FakeVfxPool();
            var controller = CreateController(
                CreatePolicy(cueId, missingAnchorPolicy: VfxMissingAnchorPolicy.UseFallbackCell),
                pool,
                new FakeVfxAnchorResolver { ResolveSuccess = false });

            controller.Refresh(new GameplayVfxRequestPlan(new[] { request }));

            Assert.That(controller.MissingAnchorCount, Is.EqualTo(1));
            Assert.That(pool.PlayTransientCallCount, Is.EqualTo(1));
            Assert.That(pool.LastCommand.Anchor.UsedFallback, Is.True);
            Assert.That(pool.LastCommand.Anchor.Cell, Is.EqualTo(fallbackCell));
        }

        private static GameplayVfxPresentationController CreateController(
            VfxBindingRuntimePolicy policy,
            FakeVfxPool pool = null,
            FakeVfxAnchorResolver anchorResolver = null)
        {
            return new GameplayVfxPresentationController(
                pool ?? new FakeVfxPool(),
                anchorResolver ?? new FakeVfxAnchorResolver(),
                new SinglePolicyResolver(policy),
                new VfxPersistentHandleRegistry(),
                new VfxLifetimeRunner());
        }

        private static GameplayVfxRequest CreateRequest(
            GameplayVfxCueId cueId,
            bool isPersistent = false,
            VfxPersistentKey persistentKey = default)
        {
            return new GameplayVfxRequest(
                1,
                1,
                17,
                cueId,
                VfxAnchor.ForEntity(3),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent,
                persistentKey);
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            VfxMissingAnchorPolicy missingAnchorPolicy = VfxMissingAnchorPolicy.SkipOptional,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                missingAnchorPolicy,
                playbackMode,
                VfxStopPolicy.AuthoredDuration);
        }

        private sealed class SinglePolicyResolver : IVfxBindingResolver
        {
            private readonly VfxBindingRuntimePolicy policy;

            public SinglePolicyResolver(VfxBindingRuntimePolicy policy)
            {
                this.policy = policy;
            }

            public bool TryResolve(in GameplayVfxRequest request, out VfxBindingRuntimePolicy resolvedPolicy)
            {
                resolvedPolicy = policy;
                return true;
            }
        }

        private sealed class FakeVfxAnchorResolver : IVfxAnchorResolver
        {
            public bool ResolveSuccess { get; set; } = true;

            public bool TryResolve(in GameplayVfxRequest request, out VfxResolvedAnchor resolvedAnchor)
            {
                if (!ResolveSuccess)
                {
                    resolvedAnchor = VfxResolvedAnchor.Unresolved(VfxMissingAnchorPolicy.SkipOptional);
                    return false;
                }

                resolvedAnchor = VfxResolvedAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter);
                return true;
            }
        }

        private sealed class FakeVfxPool : IVfxPool
        {
            public int PlayTransientCallCount { get; private set; }

            public ResolvedVfxPlaybackCommand LastCommand { get; private set; }

            public IVfxPlaybackHandle PlayTransient(in ResolvedVfxPlaybackCommand command)
            {
                PlayTransientCallCount++;
                LastCommand = command;
                return null;
            }

            public IVfxPlaybackHandle StartPersistent(in ResolvedVfxPlaybackCommand command)
            {
                LastCommand = command;
                return null;
            }

            public void Release(IVfxPlaybackHandle handle)
            {
            }

            public void HardCleanupAll()
            {
            }
        }
    }
}
