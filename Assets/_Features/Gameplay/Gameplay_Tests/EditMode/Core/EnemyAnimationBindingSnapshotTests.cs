using System;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class EnemyAnimationBindingSnapshotTests
    {
        [TestCase(EnemyAnimationCue.None)]
        [TestCase((EnemyAnimationCue)999)]
        [Category("Core")]
        public void Create_RejectsUnknownOrNoneCue(EnemyAnimationCue cue)
        {
            Assert.That(
                () => CreateSnapshot(Binding(cue, EnemyAnimationDispatchMode.Trigger, "Target")),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        [Category("Core")]
        public void Create_RejectsNullEmptyDuplicateAndBlankTarget()
        {
            Assert.That(
                () => EnemyAnimationBindingSnapshot.CreateForTests(null, -1f),
                Throws.InvalidOperationException);
            Assert.That(
                () => EnemyAnimationBindingSnapshot.CreateForTests(Array.Empty<EnemyAnimationCueBinding>(), -1f),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(
                    Binding(EnemyAnimationCue.Hit, EnemyAnimationDispatchMode.Trigger, "Hit"),
                    Binding(EnemyAnimationCue.Hit, EnemyAnimationDispatchMode.Trigger, "OtherHit")),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(EnemyAnimationCue.Hit, EnemyAnimationDispatchMode.Trigger, "  ")),
                Throws.InvalidOperationException);
        }

        [Test]
        [Category("Core")]
        public void Create_RejectsDispatchModesOutsideCuePolicy()
        {
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionExecute,
                    EnemyAnimationDispatchMode.State,
                    "Attack"), crossFadeSeconds: 0f),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpLanding,
                    EnemyAnimationDispatchMode.Trigger,
                    "Land")),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.Hit,
                    EnemyAnimationDispatchMode.None,
                    "Hit")),
                Throws.InvalidOperationException);
        }

        [Test]
        [Category("Core")]
        public void Create_RequiresSustainedStateOnlyForTriggerJumpAirborne()
        {
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.Trigger,
                    "JumpAirborne")),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.Trigger,
                    "JumpAirborne",
                    sustainedStateName: "JumpAirborneState")),
                Throws.Nothing);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.State,
                    "JumpAirborne",
                    sustainedStateName: "Redundant"), crossFadeSeconds: 0f),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Windup",
                    sustainedStateName: "Redundant")),
                Throws.InvalidOperationException);
        }

        [Test]
        [Category("Core")]
        public void Create_RejectsTimingOnCueThatForbidsTiming()
        {
            var clip = CreateClip(1f);
            try
            {
                Assert.That(
                    () => CreateSnapshot(Binding(
                        EnemyAnimationCue.Hit,
                        EnemyAnimationDispatchMode.Trigger,
                        "Hit",
                        durationSeconds: 0.5f,
                        clip: clip)),
                    Throws.InvalidOperationException);
                Assert.That(
                    () => CreateSnapshot(Binding(
                        EnemyAnimationCue.Death,
                        EnemyAnimationDispatchMode.Trigger,
                        "Death",
                        clip: clip)),
                    Throws.InvalidOperationException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(0f)]
        [TestCase(-0.5f)]
        [TestCase(-2f)]
        [Category("Core")]
        public void Create_RejectsInvalidTimingDuration(float durationSeconds)
        {
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Windup",
                    durationSeconds: durationSeconds)),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        [Category("Core")]
        public void Create_ImplementsDurationAndReferenceClipTruthTable()
        {
            var clip = CreateClip(1.25f);
            try
            {
                var noTiming = CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Windup"));
                Assert.That(noTiming.TryGetBinding(EnemyAnimationCue.ActionWindup, out var noTimingBinding), Is.True);
                Assert.That(noTimingBinding.ReferenceClipLengthSeconds, Is.Zero);
                Assert.That(noTimingBinding.AnimatorDurationSeconds, Is.EqualTo(-1f));

                var natural = CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Windup",
                    clip: clip));
                Assert.That(natural.TryGetBinding(EnemyAnimationCue.ActionWindup, out var naturalBinding), Is.True);
                Assert.That(naturalBinding.ReferenceClipLengthSeconds, Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(naturalBinding.AnimatorDurationSeconds, Is.EqualTo(-1f));

                var overridden = CreateSnapshot(Binding(
                    EnemyAnimationCue.ActionWindup,
                    EnemyAnimationDispatchMode.Trigger,
                    "Windup",
                    durationSeconds: 0.5f,
                    clip: clip));
                Assert.That(overridden.TryGetBinding(EnemyAnimationCue.ActionWindup, out var overrideBinding), Is.True);
                Assert.That(overrideBinding.AnimatorDurationSeconds, Is.EqualTo(0.5f));

                Assert.That(
                    () => CreateSnapshot(Binding(
                        EnemyAnimationCue.ActionWindup,
                        EnemyAnimationDispatchMode.Trigger,
                        "Windup",
                        durationSeconds: 0.5f)),
                    Throws.InvalidOperationException);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(-2f)]
        [Category("Core")]
        public void Create_RejectsInvalidCrossFade(float crossFadeSeconds)
        {
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpLanding,
                    EnemyAnimationDispatchMode.State,
                    "Move"), crossFadeSeconds),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        [Category("Core")]
        public void Create_RequiresCrossFadeOnlyForPrimaryStateBinding()
        {
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpLanding,
                    EnemyAnimationDispatchMode.State,
                    "Move")),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpLanding,
                    EnemyAnimationDispatchMode.State,
                    "Move"), crossFadeSeconds: 0f),
                Throws.Nothing);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.Hit,
                    EnemyAnimationDispatchMode.Trigger,
                    "Hit"), crossFadeSeconds: 0f),
                Throws.InvalidOperationException);
            Assert.That(
                () => CreateSnapshot(Binding(
                    EnemyAnimationCue.JumpAirborne,
                    EnemyAnimationDispatchMode.Trigger,
                    "JumpAirborne",
                    sustainedStateName: "JumpAirborneState")),
                Throws.Nothing,
                "A sustained state alone must not require cross-fade authoring.");
        }

        [Test]
        [Category("Core")]
        public void Snapshot_DeepCopiesSourceArrayAndProvidesImmutableLookup()
        {
            var source = new[]
            {
                Binding(EnemyAnimationCue.Hit, EnemyAnimationDispatchMode.Trigger, "Hit"),
            };
            var snapshot = EnemyAnimationBindingSnapshot.CreateForTests(source, -1f);

            source[0] = Binding(EnemyAnimationCue.Death, EnemyAnimationDispatchMode.Trigger, "Death");

            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.Hit, out var hit), Is.True);
            Assert.That(hit.TargetName, Is.EqualTo("Hit"));
            Assert.That(snapshot.TryGetBinding(EnemyAnimationCue.Death, out _), Is.False);
        }

        private static EnemyAnimationBindingSnapshot CreateSnapshot(
            EnemyAnimationCueBinding binding,
            float crossFadeSeconds = -1f)
        {
            return EnemyAnimationBindingSnapshot.CreateForTests(new[] { binding }, crossFadeSeconds);
        }

        private static EnemyAnimationBindingSnapshot CreateSnapshot(
            EnemyAnimationCueBinding first,
            EnemyAnimationCueBinding second)
        {
            return EnemyAnimationBindingSnapshot.CreateForTests(new[] { first, second }, -1f);
        }

        private static EnemyAnimationCueBinding Binding(
            EnemyAnimationCue cue,
            EnemyAnimationDispatchMode mode,
            string targetName,
            string sustainedStateName = "",
            float durationSeconds = -1f,
            AnimationClip clip = null)
        {
            return EnemyAnimationCueBinding.CreateForTests(
                cue,
                mode,
                targetName,
                sustainedStateName,
                durationSeconds,
                clip);
        }

        private static AnimationClip CreateClip(float lengthSeconds)
        {
            var clip = new AnimationClip();
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, lengthSeconds, 1f));
            return clip;
        }
    }
}
