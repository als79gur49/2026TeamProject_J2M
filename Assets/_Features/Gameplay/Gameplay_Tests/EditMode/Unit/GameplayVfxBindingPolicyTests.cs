using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBindingPolicyTests
    {
        [Test]
        [Category("Extended")]
        public void BindingPolicy_OwnsExecutionPolicy()
        {
            var cueId = GameplayVfxCueId.From(PlayerVfxCue.PushWindup);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Required,
                VfxMissingAnchorPolicy.FailFast,
                VfxPlaybackMode.Follow,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                defaultLifetimeSeconds: 1.25f,
                tailSeconds: 0.5f,
                maxConcurrentInstances: 3);

            Assert.That(policy.CueId, Is.EqualTo(cueId));
            Assert.That(policy.Requirement, Is.EqualTo(VfxBindingRequirement.Required));
            Assert.That(policy.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.FailFast));
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.Follow));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.DetachThenStopEmittingThenRelease));
            Assert.That(policy.DefaultLifetimeSeconds, Is.EqualTo(1.25f));
            Assert.That(policy.TailSeconds, Is.EqualTo(0.5f));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(3));
            Assert.DoesNotThrow(() => new VfxBinding(policy));
        }

        [Test]
        [Category("Extended")]
        public void BindingPolicy_RejectsInvalidDurationsAndCounts()
        {
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: -0.1f));
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: float.NaN));
            AssertInvalid(CreatePolicy(defaultLifetimeSeconds: float.PositiveInfinity));
            AssertInvalid(CreatePolicy(tailSeconds: -0.1f));
            AssertInvalid(CreatePolicy(tailSeconds: float.NegativeInfinity));
            AssertInvalid(CreatePolicy(maxConcurrentInstances: -1));
            AssertInvalid(new VfxBindingRuntimePolicy(
                GameplayVfxCueId.None,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration));
        }

        [Test]
        [Category("Extended")]
        public void BindingPolicy_AllowsZeroMaxConcurrentAsUnlimited()
        {
            var policy = CreatePolicy(maxConcurrentInstances: 0);

            Assert.That(policy.IsValid, Is.True);
            Assert.DoesNotThrow(() => policy.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void CueMap_RejectsDuplicateOrInvalidCue()
        {
            var cueId = GameplayVfxCueId.From(BoxVfxCue.DestroySmoke);
            var policy = CreatePolicy(cueId: cueId);

            Assert.Throws<InvalidOperationException>(() => new VfxCueMap(new[] { policy, policy }));
            Assert.Throws<InvalidOperationException>(() => new VfxCueMap(new[]
            {
                new VfxBindingRuntimePolicy(
                    GameplayVfxCueId.None,
                    VfxBindingRequirement.Optional,
                    VfxMissingAnchorPolicy.SkipOptional,
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration),
            }));

            var map = new VfxCueMap(new[] { policy });
            Assert.That(map.TryResolve(cueId, out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(policy));
            Assert.Throws<InvalidOperationException>(() => map.ResolveOrThrow(GameplayVfxCueId.From(BoxVfxCue.ItemConsume)));
        }

        [Test]
        [Category("Extended")]
        public void Profile_RejectsCrossFamilyCue()
        {
            var boxPolicy = CreatePolicy(cueId: GameplayVfxCueId.From(BoxVfxCue.DestroySmoke));

            Assert.Throws<InvalidOperationException>(() => new VfxProfile(GameplayVfxFamily.Player, new[] { boxPolicy }));
        }

        [Test]
        [Category("Extended")]
        public void ResolvedCommand_CarriesRequestPolicyAndAnchor()
        {
            var request = new GameplayVfxRequest(
                1,
                2,
                3,
                GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                VfxAnchor.ForEntity(9),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: true,
                persistentKey: new VfxPersistentKey(
                    GameplayVfxCueId.From(EnemyVfxCue.Spawn),
                    VfxAnchorKind.Entity,
                    entityId: 9));
            var policy = CreatePolicy(
                cueId: request.CueId,
                playbackMode: VfxPlaybackMode.Loop,
                stopPolicy: VfxStopPolicy.StopEmittingThenRelease);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter);

            var command = new ResolvedVfxPlaybackCommand(request, policy, anchor);

            Assert.That(command.Request, Is.EqualTo(request));
            Assert.That(command.Policy, Is.EqualTo(policy));
            Assert.That(command.Anchor, Is.EqualTo(anchor));
            Assert.That(command.CueId, Is.EqualTo(request.CueId));
            Assert.That(command.IsPersistent, Is.True);
            Assert.That(command.PersistentKey, Is.EqualTo(request.PersistentKey));
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId = default,
            VfxPlaybackMode playbackMode = VfxPlaybackMode.OneShot,
            VfxStopPolicy stopPolicy = VfxStopPolicy.AuthoredDuration,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0)
        {
            return new VfxBindingRuntimePolicy(
                cueId.IsNone ? GameplayVfxCueId.From(PlayerVfxCue.Damage) : cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances);
        }

        private static void AssertInvalid(VfxBindingRuntimePolicy policy)
        {
            Assert.That(policy.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => policy.ValidateOrThrow());
        }
    }
}
