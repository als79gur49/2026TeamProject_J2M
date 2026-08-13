using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayCameraShakeMixerFoundationTests
    {
        private const float PositionTolerance = 0.00001f;
        private const float RotationTolerance = 0.001f;
        private readonly List<GameplayCameraShakeProfile> _profiles = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var profile in _profiles)
            {
                Object.DestroyImmediate(profile);
            }

            _profiles.Clear();
        }

        [Test]
        [Category("Extended")]
        public void CameraShakeImpulseRequest_CanonicalIdentity_IsStableAndExcludesOptionalPoseMetadata()
        {
            var first = CreateRequest(
                CameraShakeSemantic.PushSlideLaunch,
                CameraShakePriority.Light,
                sourceEntityId: 7,
                sequenceId: 19,
                tickIndex: 31,
                anchor: new SurfaceCell(FaceId.Floor, 1, 2),
                direction: Direction.Left);
            var second = CreateRequest(
                CameraShakeSemantic.PushSlideLaunch,
                CameraShakePriority.Light,
                sourceEntityId: 7,
                sequenceId: 19,
                tickIndex: 31,
                anchor: new SurfaceCell(FaceId.Back, 9, 8),
                direction: Direction.Right);

            Assert.That(first.Identity, Is.EqualTo(second.Identity));
            Assert.That(first.Identity.GetHashCode(), Is.EqualTo(second.Identity.GetHashCode()));
            Assert.That(
                CameraShakeImpulseEvaluator.CreateStableSeed(first.Identity),
                Is.EqualTo(CameraShakeImpulseEvaluator.CreateStableSeed(second.Identity)));
            Assert.That(typeof(CameraShakeImpulseRequest).GetProperty("RequestKey"), Is.Null);
            Assert.That(typeof(CameraShakeImpulseRequest).GetProperty("DelaySeconds"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void Mixer_ExactIdentityDedupes_WhileSemanticAndSourceRemainDistinctIdentityAxes()
        {
            var mixer = CreateMixer();
            var first = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 10, 3);
            var same = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 10, 3);
            var otherSource = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 2, 10, 3);
            var otherSemantic = CreateRequest(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 1, 10, 3);

            Assert.That(mixer.Submit(first), Is.True);
            Assert.That(mixer.Submit(same), Is.False);
            Assert.That(mixer.Submit(otherSource), Is.True);
            Assert.That(mixer.Submit(otherSemantic), Is.True);
            Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_SubmissionOrderReversal_ProducesSameCanonicalResult()
        {
            var firstMixer = CreateMixer();
            var secondMixer = CreateMixer();
            var light = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 5, 20, 8);
            var medium = CreateRequest(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 9, 21, 8);

            Assert.That(firstMixer.Submit(light), Is.True);
            Assert.That(firstMixer.Submit(medium), Is.True);
            Assert.That(secondMixer.Submit(medium), Is.True);
            Assert.That(secondMixer.Submit(light), Is.True);
            firstMixer.Advance(0.1f);
            secondMixer.Advance(0.1f);

            AssertMixEqual(firstMixer.CurrentResult, secondMixer.CurrentResult);
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_CompleteProfile_Validates()
        {
            Assert.DoesNotThrow(() => CreateProfile().ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_DuplicateSemantic_FailsFast()
        {
            var entries = CreateEntries().ToList();
            entries.Add(entries[0].Clone());
            var profile = CreateProfile(entries.ToArray());

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_SameSemanticDifferentHostileVariant_IsAllowedAndResolvesIndependently()
        {
            var profile = CreateProfile();

            Assert.DoesNotThrow(() => profile.ValidateOrThrow());
            var entries = profile.CreateValidatedEntryMap();
            Assert.That(
                entries[new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileStay)].Priority,
                Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(
                entries[new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileDestroySelf)].Priority,
                Is.EqualTo(CameraShakePriority.Medium));
            Assert.That(
                entries[new CameraShakeProfileKey(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakeVariant.FlipHostileFollowThrough)].Priority,
                Is.EqualTo(CameraShakePriority.Heavy));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_DuplicateSameSemanticAndVariant_FailsFast()
        {
            var entries = CreateEntries().ToList();
            entries.Add(entries.Single(entry =>
                entry.Semantic == CameraShakeSemantic.FlipHostileImpact &&
                entry.Variant == CameraShakeVariant.FlipHostileStay).Clone());
            var profile = CreateProfile(entries.ToArray());

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_MissingRequiredHostileVariant_FailsFast()
        {
            var entries = CreateEntries()
                .Where(entry => entry.Variant != CameraShakeVariant.FlipHostileDestroySelf)
                .ToArray();
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void Mixer_HostileVariantIsProfileDiscriminatorButNotRequestIdentityAxis()
        {
            var mixer = CreateMixer();
            var stay = CreateRequest(
                CameraShakeSemantic.FlipHostileImpact,
                CameraShakePriority.Medium,
                sourceEntityId: 7,
                sequenceId: 11,
                tickIndex: 13,
                variant: CameraShakeVariant.FlipHostileStay);
            var followThrough = CreateRequest(
                CameraShakeSemantic.FlipHostileImpact,
                CameraShakePriority.Heavy,
                sourceEntityId: 7,
                sequenceId: 11,
                tickIndex: 13,
                variant: CameraShakeVariant.FlipHostileFollowThrough);

            Assert.That(stay.Identity, Is.EqualTo(followThrough.Identity));
            Assert.That(mixer.Submit(stay), Is.True);
            Assert.That(mixer.Submit(followThrough), Is.False);
            Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_MissingRequiredSemantic_FailsFast()
        {
            var profile = CreateProfile(CreateEntries().Take(5).ToArray());

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_NonPositiveDuration_FailsFast(float duration)
        {
            var entries = CreateEntries();
            entries[0].DurationSeconds = duration;
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [TestCase(0)]
        [TestCase(-1)]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_NonPositiveCycles_FailsFast(int cycles)
        {
            var entries = CreateEntries();
            entries[0].OscillationCycles = cycles;
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_NonFiniteAmplitude_FailsFast(float invalid)
        {
            var entries = CreateEntries();
            entries[0].LocalPositionAmplitude = new Vector3(invalid, 0f, 0f);
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_NegativeCooldown_FailsFast()
        {
            var entries = CreateEntries();
            entries[0].CooldownSeconds = -0.01f;
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void GameplayCameraShakeProfile_InvalidDecayCurve_FailsFast()
        {
            var entries = CreateEntries();
            entries[0].Decay = null;
            var profile = CreateProfile(entries);

            Assert.Throws<InvalidOperationException>(() => profile.ValidateOrThrow());
        }

        [Test]
        [Category("Extended")]
        public void ImpulseEvaluator_AnalyticSamples_AreFiniteDeterministicAndCompleteAtDuration()
        {
            var profile = CreateProfile();
            var entry = profile.CreateValidatedEntryMap()[
                new CameraShakeProfileKey(CameraShakeSemantic.FlipFloorLanding)];
            var request = CreateRequest(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 4, 7, 11);
            var evaluator = new CameraShakeImpulseEvaluator();
            var sampleTimes = new[] { 0.0, 0.025, 0.08, 0.2, 0.399 };

            foreach (var elapsed in sampleTimes)
            {
                var first = evaluator.Evaluate(request, entry, elapsed);
                var second = evaluator.Evaluate(request, entry, elapsed);
                Assert.That(IsFinite(first.LocalPosition), Is.True, $"Position must be finite at {elapsed}.");
                Assert.That(IsFinite(first.LocalRotationDegrees), Is.True, $"Rotation must be finite at {elapsed}.");
                Assert.That(first.LocalPosition, Is.EqualTo(second.LocalPosition));
                Assert.That(first.LocalRotationDegrees, Is.EqualTo(second.LocalRotationDegrees));
            }

            var start = evaluator.Evaluate(request, entry, 0.0);
            Assert.That(start.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(start.LocalRotation, Is.EqualTo(Quaternion.identity));
            var boundary = evaluator.Evaluate(request, entry, entry.DurationSeconds);
            var after = evaluator.Evaluate(request, entry, entry.DurationSeconds + 0.1f);
            Assert.That(boundary.IsActive, Is.False);
            Assert.That(after.IsActive, Is.False);
            Assert.That(boundary.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(boundary.LocalRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        [Category("Extended")]
        public void ImpulseEvaluator_DifferentIdentityVariesButRemainsReproducible()
        {
            var profile = CreateProfile();
            var entry = profile.CreateValidatedEntryMap()[
                new CameraShakeProfileKey(CameraShakeSemantic.PushSlideLaunch)];
            var firstRequest = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 1, 1);
            var secondRequest = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 2, 1, 1);
            var evaluator = new CameraShakeImpulseEvaluator();

            var first = evaluator.Evaluate(firstRequest, entry, 0.1);
            var second = evaluator.Evaluate(secondRequest, entry, 0.1);
            var repeated = evaluator.Evaluate(secondRequest, entry, 0.1);

            Assert.That(Vector3.Distance(first.LocalPosition, second.LocalPosition), Is.GreaterThan(0.000001f));
            Assert.That(second.LocalPosition, Is.EqualTo(repeated.LocalPosition));
            Assert.That(second.LocalRotationDegrees, Is.EqualTo(repeated.LocalRotationDegrees));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_EquivalentAbsoluteElapsed_IsIndependentOfAdvanceStepPartition()
        {
            var oneStep = CreateMixer();
            var twoSteps = CreateMixer();
            var request = CreateRequest(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 3, 8, 13);
            Assert.That(oneStep.Submit(request), Is.True);
            Assert.That(twoSteps.Submit(request), Is.True);

            oneStep.Advance(0.1f);
            twoSteps.Advance(0.05f);
            twoSteps.Advance(0.05f);

            AssertMixEqual(oneStep.CurrentResult, twoSteps.CurrentResult);
        }

        [Test]
        [Category("Extended")]
        public void Mixer_LightPlusLight_UsesCanonicalHalfResidualAndCaps()
        {
            var profile = CreateProfile();
            var mixer = CreateMixer(profile);
            var first = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 1, 2);
            var second = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 2, 2, 2);
            mixer.Submit(first);
            mixer.Submit(second);
            mixer.Advance(0.1f);

            var entry = profile.CreateValidatedEntryMap()[
                new CameraShakeProfileKey(CameraShakeSemantic.PushSlideLaunch)];
            var evaluator = new CameraShakeImpulseEvaluator();
            var expectedFirst = evaluator.Evaluate(first, entry, 0.1);
            var expectedSecond = evaluator.Evaluate(second, entry, 0.1);
            AssertVectorApproximately(
                expectedFirst.LocalPosition + (expectedSecond.LocalPosition * 0.5f),
                mixer.CurrentResult.LocalPosition);
            AssertUnderCaps(mixer.CurrentResult);
        }

        [TestCase(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 0.25f)]
        [TestCase(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy, 0.1f)]
        [Category("Extended")]
        public void Mixer_HigherPriorityPlusLight_UsesCappedResidual(
            CameraShakeSemantic primarySemantic,
            CameraShakePriority primaryPriority,
            float expectedLightRatio)
        {
            var profile = CreateProfile();
            var mixer = CreateMixer(profile);
            var primary = CreateRequest(primarySemantic, primaryPriority, 2, 2, 3);
            var light = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 1, 3);
            mixer.Submit(light);
            mixer.Submit(primary);
            mixer.Advance(0.1f);

            var entries = profile.CreateValidatedEntryMap();
            var evaluator = new CameraShakeImpulseEvaluator();
            var expectedPrimary = evaluator.Evaluate(
                primary,
                entries[new CameraShakeProfileKey(primarySemantic)],
                0.1);
            var expectedLight = evaluator.Evaluate(
                light,
                entries[new CameraShakeProfileKey(CameraShakeSemantic.PushSlideLaunch)],
                0.1);
            AssertVectorApproximately(
                expectedPrimary.LocalPosition + (expectedLight.LocalPosition * expectedLightRatio),
                mixer.CurrentResult.LocalPosition);
            AssertUnderCaps(mixer.CurrentResult);
        }

        [TestCase(CameraShakePriority.Light, CameraShakeSemantic.PushSlideLaunch)]
        [TestCase(CameraShakePriority.Medium, CameraShakeSemantic.FlipFloorLanding)]
        [Category("Extended")]
        public void Mixer_ActiveTopology_SuppressesLightAndMediumWithoutQueue(
            CameraShakePriority priority,
            CameraShakeSemantic semantic)
        {
            var mixer = CreateMixer();
            mixer.SetTopologyContribution(CreateTopologyContribution());
            var request = CreateRequest(semantic, priority, 5, 6, 7);

            Assert.That(mixer.Submit(request), Is.False);
            Assert.That(mixer.ActiveImpulseCount, Is.Zero);
            mixer.SetTopologyContribution(CameraShakeContribution.Inactive);
            mixer.Advance(0.1f);
            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(mixer.Submit(request), Is.False, "A dropped exact request must not replay after topology ends.");
        }

        [Test]
        [Category("Extended")]
        public void Mixer_TopologyBecomingActive_DropsExistingLightWithoutStaleReplay()
        {
            var mixer = CreateMixer();
            var light = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 4, 4, 4);
            Assert.That(mixer.Submit(light), Is.True);
            mixer.Advance(0.1f);
            Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1));

            mixer.SetTopologyContribution(CreateTopologyContribution());
            Assert.That(mixer.ActiveImpulseCount, Is.Zero);
            mixer.SetTopologyContribution(CameraShakeContribution.Inactive);
            mixer.Advance(0.1f);

            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_TopologyPlusHeavy_UsesPerAxisAbsoluteDominanceInsteadOfRawAddition()
        {
            var profile = CreateProfile();
            var mixer = CreateMixer(profile);
            var topology = CreateTopologyContribution();
            var heavy = CreateRequest(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy, 9, 9, 9);
            mixer.SetTopologyContribution(topology);
            Assert.That(mixer.Submit(heavy), Is.True);
            mixer.Advance(0.1f);

            var entry = profile.CreateValidatedEntryMap()[
                new CameraShakeProfileKey(CameraShakeSemantic.PlayerLethalImpact)];
            var heavyContribution = new CameraShakeImpulseEvaluator().Evaluate(heavy, entry, 0.1);
            AssertDominantAxes(topology.LocalPosition, heavyContribution.LocalPosition, mixer.CurrentResult.LocalPosition);
            AssertDominantAxes(
                topology.LocalRotationDegrees,
                heavyContribution.LocalRotationDegrees,
                mixer.CurrentResult.LocalRotationDegrees);
            AssertUnderCaps(mixer.CurrentResult);
        }

        [Test]
        [Category("Extended")]
        public void Mixer_TenLargeLightRequests_RemainFiniteAndWithinGlobalMagnitudeCaps()
        {
            var profile = CreateProfile(positionAmplitude: 0.5f, rotationAmplitude: 20f);
            var mixer = CreateMixer(profile);
            for (var index = 0; index < 10; index++)
            {
                Assert.That(
                    mixer.Submit(CreateRequest(
                        CameraShakeSemantic.PushSlideLaunch,
                        CameraShakePriority.Light,
                        index + 1,
                        index + 1,
                        1)),
                    Is.True);
            }

            mixer.Advance(0.1f);

            Assert.That(IsFinite(mixer.CurrentResult.LocalPosition), Is.True);
            Assert.That(IsFinite(mixer.CurrentResult.LocalRotationDegrees), Is.True);
            AssertUnderCaps(mixer.CurrentResult);
        }

        [Test]
        [Category("Extended")]
        public void Mixer_Cooldown_RejectsBeforeBoundaryAcceptsAtAndAfterBoundaryAndDoesNotAffectOtherSource()
        {
            var mixer = CreateMixer();
            var first = CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 1, 1);
            Assert.That(mixer.Submit(first), Is.True);
            Assert.That(mixer.Submit(first), Is.False, "Exact identity must dedupe before cooldown evaluation.");

            mixer.Advance(0.1f);
            Assert.That(
                mixer.Submit(CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 2, 2)),
                Is.False);
            Assert.That(
                mixer.Submit(CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 2, 2, 2)),
                Is.True,
                "Cooldown is semantic plus source, not global semantic suppression.");

            mixer.Advance(0.1f);
            Assert.That(
                mixer.Submit(CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 3, 3)),
                Is.True,
                "The exact cooldown boundary is inclusive.");
            mixer.Advance(0.2001f);
            Assert.That(
                mixer.Submit(CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 4, 4)),
                Is.True);
        }

        [Test]
        [Category("Core")]
        public void Mixer_HeavyEnemyLandingCooldown_IsPerEnemyAndInclusiveAtBoundary()
        {
            var mixer = CreateMixer();
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 40,
                    sequenceId: 1,
                    tickIndex: 1)),
                Is.True);

            mixer.Advance(0.1999f);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 40,
                    sequenceId: 2,
                    tickIndex: 2)),
                Is.False);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 41,
                    sequenceId: 1,
                    tickIndex: 2)),
                Is.True,
                "A different heavy enemy has an independent cooldown key.");

            mixer.Advance(0.0002f);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 40,
                    sequenceId: 3,
                    tickIndex: 3)),
                Is.True);

            var exactBoundaryMixer = CreateMixer();
            Assert.That(
                exactBoundaryMixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 40,
                    sequenceId: 1,
                    tickIndex: 1)),
                Is.True);
            exactBoundaryMixer.Advance(0.2f);
            Assert.That(
                exactBoundaryMixer.Submit(CreateRequest(
                    CameraShakeSemantic.HeavyEnemyJumpLanding,
                    CameraShakePriority.Medium,
                    sourceEntityId: 40,
                    sequenceId: 2,
                    tickIndex: 2)),
                Is.True,
                "The exact semantic-plus-enemy cooldown boundary is inclusive.");
        }

        [Test]
        [Category("Core")]
        public void Mixer_PlayerDamageCooldown_IsGlobalPerPlayerAndDoesNotSuppressLethalSemantic()
        {
            var mixer = CreateMixer();
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 1,
                    tickIndex: 1)),
                Is.True);

            mixer.Advance(0.1999f);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 2,
                    tickIndex: 2)),
                Is.False,
                "Changing attackers cannot bypass the player-outcome cooldown because the player is the source identity.");
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerLethalImpact,
                    CameraShakePriority.Heavy,
                    sourceEntityId: 10,
                    sequenceId: 2,
                    tickIndex: 2)),
                Is.True,
                "The lethal semantic must remain independent of nonlethal damage cooldown.");

            mixer.Advance(0.0002f);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 3,
                    tickIndex: 3)),
                Is.True,
                "Player damage retriggers after the cooldown boundary.");
            mixer.Advance(0.2001f);
            Assert.That(
                mixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 4,
                    tickIndex: 4)),
                Is.True);

            var exactBoundaryMixer = CreateMixer();
            Assert.That(
                exactBoundaryMixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 5,
                    tickIndex: 5)),
                Is.True);
            exactBoundaryMixer.Advance(0.2f);
            Assert.That(
                exactBoundaryMixer.Submit(CreateRequest(
                    CameraShakeSemantic.PlayerDamageImpact,
                    CameraShakePriority.Light,
                    sourceEntityId: 10,
                    sequenceId: 6,
                    tickIndex: 6)),
                Is.True,
                "The exact player-damage cooldown boundary is inclusive.");
        }

        [Test]
        [Category("Extended")]
        public void Mixer_SettingsFullReducedOffAndRuntimeSwitch_OnlyChangeFinalVisualContribution()
        {
            var mixer = CreateMixer();
            var request = CreateRequest(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, 1, 1, 1);
            Assert.That(mixer.MotionLevel, Is.EqualTo(CameraMotionLevel.Full));
            Assert.That(mixer.Submit(request), Is.True);
            mixer.Advance(0.1f);
            var full = mixer.CurrentResult;

            mixer.SetMotionLevel(CameraMotionLevel.Reduced);
            var reduced = mixer.CurrentResult;
            Assert.That(reduced.LocalPosition.magnitude, Is.LessThan(full.LocalPosition.magnitude));
            Assert.That(reduced.LocalRotationDegrees.magnitude, Is.LessThan(full.LocalRotationDegrees.magnitude));
            Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1));

            mixer.SetMotionLevel(CameraMotionLevel.Off);
            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(mixer.CurrentResult.LocalRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(mixer.ActiveImpulseCount, Is.EqualTo(1));
            mixer.Advance(0.01f);
            mixer.SetMotionLevel(CameraMotionLevel.Full);
            Assert.That(mixer.CurrentResult.LocalPosition.sqrMagnitude, Is.GreaterThan(0f));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_TopologyOnlyFull_IsExactM0Passthrough()
        {
            var visualState = new TopologyTransitionVisualState(
                isActive: true,
                progress01: 0.12f,
                rotationKind: CubeRotationKind.Forward,
                sourceTopology: new CubeTopologyState(FaceId.Floor),
                destinationTopology: new CubeTopologyState(FaceId.Front),
                durationSeconds: 0.2f,
                presentedVisualRotation: Quaternion.Euler(12f, 0f, 0f),
                angularVelocityNormalized: 1f);
            var direct = new TopologyTransitionCameraShakeController().Evaluate(
                visualState,
                TopologyTransitionCameraShakeProfile.CreateDefault());
            var mixer = new GameplayCameraShakeMixer();
            mixer.SetTopologyContribution(TopologyCameraShakeContributionAdapter.Create(direct, true));

            Assert.That(Vector3.Distance(direct.LocalPosition, mixer.CurrentResult.LocalPosition), Is.Zero);
            Assert.That(Quaternion.Angle(direct.LocalRotation, mixer.CurrentResult.LocalRotation), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void Mixer_PauseCancelsGameplayAndHardResetClearsAllRuntimeState()
        {
            var mixer = CreateMixer();
            var request = CreateRequest(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy, 3, 3, 3);
            mixer.SetTopologyContribution(CreateTopologyContribution());
            mixer.Submit(request);
            mixer.Advance(0.1f);
            Assert.That(mixer.CurrentResult.IsActive, Is.True);

            mixer.SetPaused(true);
            Assert.That(mixer.ActiveImpulseCount, Is.Zero);
            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));
            var pausedTime = mixer.PresentationTimeSeconds;
            mixer.Advance(1f);
            Assert.That(mixer.PresentationTimeSeconds, Is.EqualTo(pausedTime));
            mixer.SetPaused(false);
            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));

            mixer.HardReset();
            Assert.That(mixer.ActiveImpulseCount, Is.Zero);
            Assert.That(mixer.AcceptedIdentityCount, Is.Zero);
            Assert.That(mixer.PresentationTimeSeconds, Is.Zero);
            Assert.That(mixer.CurrentResult.LocalRotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        [Category("Extended")]
        public void Mixer_ExpiredImpulse_IsRemovedAndReturnsIdentity()
        {
            var mixer = CreateMixer();
            mixer.Submit(CreateRequest(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, 1, 1, 1));
            mixer.Advance(0.4f);

            Assert.That(mixer.ActiveImpulseCount, Is.Zero);
            Assert.That(mixer.CurrentResult.LocalPosition, Is.EqualTo(Vector3.zero));
            Assert.That(mixer.CurrentResult.LocalRotation, Is.EqualTo(Quaternion.identity));
        }

        private GameplayCameraShakeMixer CreateMixer(GameplayCameraShakeProfile profile = null)
        {
            var mixer = new GameplayCameraShakeMixer();
            mixer.ConfigureProfile(profile ?? CreateProfile());
            return mixer;
        }

        private GameplayCameraShakeProfile CreateProfile(
            float positionAmplitude = 0.02f,
            float rotationAmplitude = 1f)
        {
            return CreateProfile(CreateEntries(positionAmplitude, rotationAmplitude));
        }

        private GameplayCameraShakeProfile CreateProfile(CameraShakeProfileEntry[] entries)
        {
            var profile = ScriptableObject.CreateInstance<GameplayCameraShakeProfile>();
            profile.SetEntriesForTests(entries);
            _profiles.Add(profile);
            return profile;
        }

        private static CameraShakeProfileEntry[] CreateEntries(
            float positionAmplitude = 0.02f,
            float rotationAmplitude = 1f)
        {
            return new[]
            {
                CreateEntry(CameraShakeSemantic.PushSlideLaunch, CameraShakePriority.Light, positionAmplitude, rotationAmplitude),
                CreateEntry(CameraShakeSemantic.FlipFloorLanding, CameraShakePriority.Medium, positionAmplitude, rotationAmplitude),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    positionAmplitude,
                    rotationAmplitude,
                    CameraShakeVariant.FlipHostileStay),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Medium,
                    positionAmplitude,
                    rotationAmplitude,
                    CameraShakeVariant.FlipHostileDestroySelf),
                CreateEntry(
                    CameraShakeSemantic.FlipHostileImpact,
                    CameraShakePriority.Heavy,
                    positionAmplitude,
                    rotationAmplitude,
                    CameraShakeVariant.FlipHostileFollowThrough),
                CreateEntry(CameraShakeSemantic.PlayerDamageImpact, CameraShakePriority.Light, positionAmplitude, rotationAmplitude),
                CreateEntry(CameraShakeSemantic.PlayerLethalImpact, CameraShakePriority.Heavy, positionAmplitude, rotationAmplitude),
                CreateEntry(CameraShakeSemantic.HeavyEnemyJumpLanding, CameraShakePriority.Medium, positionAmplitude, rotationAmplitude),
            };
        }

        private static CameraShakeProfileEntry CreateEntry(
            CameraShakeSemantic semantic,
            CameraShakePriority priority,
            float positionAmplitude,
            float rotationAmplitude,
            CameraShakeVariant variant = CameraShakeVariant.Default)
        {
            return new CameraShakeProfileEntry
            {
                Semantic = semantic,
                Variant = variant,
                Priority = priority,
                DurationSeconds = 0.4f,
                OscillationCycles = 2,
                LocalPositionAmplitude = new Vector3(
                    positionAmplitude,
                    positionAmplitude * 0.75f,
                    positionAmplitude * 1.25f),
                LocalRotationAmplitudeDegrees = new Vector3(
                    rotationAmplitude,
                    rotationAmplitude * 0.75f,
                    rotationAmplitude * 0.5f),
                AttackSeconds = 0.05f,
                Decay = AnimationCurve.Linear(0f, 1f, 1f, 0f),
                CooldownSeconds = 0.2f,
            };
        }

        private static CameraShakeImpulseRequest CreateRequest(
            CameraShakeSemantic semantic,
            CameraShakePriority priority,
            int sourceEntityId,
            int sequenceId,
            int tickIndex,
            SurfaceCell anchor = default,
            Direction direction = Direction.None,
            CameraShakeVariant variant = CameraShakeVariant.Default)
        {
            return new CameraShakeImpulseRequest(
                tickIndex,
                semantic,
                sourceEntityId,
                sequenceId,
                priority,
                anchor,
                hasAnchor: direction != Direction.None,
                semanticDirection: direction,
                hasDirection: direction != Direction.None,
                variant: variant);
        }

        private static CameraShakeContribution CreateTopologyContribution()
        {
            return TopologyCameraShakeContributionAdapter.Create(
                new TopologyTransitionCameraShakeResult(
                    new Vector3(0.018f, -0.012f, 0.02f),
                    Quaternion.Euler(0.7f, -0.4f, 0.25f)),
                true);
        }

        private static void AssertMixEqual(CameraShakeMixResult expected, CameraShakeMixResult actual)
        {
            Assert.That(Vector3.Distance(expected.LocalPosition, actual.LocalPosition), Is.LessThan(PositionTolerance));
            Assert.That(Quaternion.Angle(expected.LocalRotation, actual.LocalRotation), Is.LessThan(RotationTolerance));
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(PositionTolerance));
        }

        private static void AssertUnderCaps(CameraShakeMixResult result)
        {
            Assert.That(
                result.LocalPosition.magnitude,
                Is.LessThanOrEqualTo(GameplayCameraShakeMixer.MaxLocalPositionMagnitude + PositionTolerance));
            Assert.That(
                result.LocalRotationDegrees.magnitude,
                Is.LessThanOrEqualTo(GameplayCameraShakeMixer.MaxLocalRotationDegrees + PositionTolerance));
        }

        private static void AssertDominantAxes(Vector3 first, Vector3 second, Vector3 actual)
        {
            var expected = new Vector3(
                Mathf.Abs(second.x) > Mathf.Abs(first.x) ? second.x : first.x,
                Mathf.Abs(second.y) > Mathf.Abs(first.y) ? second.y : first.y,
                Mathf.Abs(second.z) > Mathf.Abs(first.z) ? second.z : first.z);
            AssertVectorApproximately(expected, actual);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
