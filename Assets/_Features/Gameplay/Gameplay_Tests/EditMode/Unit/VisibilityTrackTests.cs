using Game.Feature.Gameplay.Host;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class VisibilityTrackTests
    {
        [Test]
        [Category("Core")]
        public void VisibilityClip_SampleWithoutAdvanceDoesNotAdvanceElapsedTime()
        {
            var clip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);

            var beforeElapsed = clip.ElapsedSeconds;
            var beforeComplete = clip.IsComplete;
            var sample = clip.SampleWithoutAdvance(0.75f, fallbackVisibility: true);

            Assert.That(sample, Is.False);
            Assert.That(clip.ElapsedSeconds, Is.EqualTo(beforeElapsed).Within(0.0001f));
            Assert.That(clip.IsComplete, Is.EqualTo(beforeComplete));
        }

        [Test]
        [Category("Core")]
        public void VisibilityClip_SampleWithoutAdvanceMatchesSampleAndAdvanceBeforeAdvance()
        {
            var peekClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);
            var advanceClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);

            var peekSample = peekClip.SampleWithoutAdvance(0.75f, fallbackVisibility: false);
            var advanceSample = advanceClip.SampleAndAdvance(0.75f, fallbackVisibility: false);

            Assert.That(peekSample, Is.EqualTo(advanceSample));
            Assert.That(peekClip.ElapsedSeconds, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(advanceClip.ElapsedSeconds, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void VisibilityClip_SampleAndAdvanceAdvancesOncePerFrame()
        {
            var clip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);

            clip.SampleWithoutAdvance(0.25f, fallbackVisibility: true);
            clip.SampleAndAdvance(0.25f, fallbackVisibility: true);

            Assert.That(clip.ElapsedSeconds, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(clip.IsComplete, Is.False);
        }

        [Test]
        [Category("Core")]
        public void VisibilityClip_SampleWithoutAdvancePlusAdvanceMatchesSampleAndAdvance()
        {
            var splitClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);
            var legacyClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 0.5f);

            var splitSample = splitClip.SampleWithoutAdvance(0.75f, fallbackVisibility: false);
            var splitCompletion = splitClip.AdvanceAndReportCompletion(0.75f);
            var legacySample = legacyClip.SampleAndAdvance(0.75f, fallbackVisibility: false);

            Assert.That(splitSample, Is.EqualTo(legacySample));
            Assert.That(splitClip.ElapsedSeconds, Is.EqualTo(legacyClip.ElapsedSeconds).Within(0.0001f));
            Assert.That(splitCompletion, Is.EqualTo(legacyClip.IsComplete));
        }

        [Test]
        [Category("Core")]
        public void VisibilityClip_AdvanceAndReportCompletion_MatchesSampleAndAdvanceCompletion()
        {
            var splitClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 1f);
            var legacyClip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 1f);

            var splitCompletion = splitClip.AdvanceAndReportCompletion(1f);
            legacyClip.SampleAndAdvance(1f, fallbackVisibility: true);

            Assert.That(splitCompletion, Is.True);
            Assert.That(splitCompletion, Is.EqualTo(legacyClip.IsComplete));
            Assert.That(splitClip.ElapsedSeconds, Is.EqualTo(legacyClip.ElapsedSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void VisibilityClip_AdvanceAndReportCompletion_ReturnsCompletionNotVisibility()
        {
            var clip = VisibilityClip.Create(
                initialVisibility: true,
                finalVisibility: false,
                durationSeconds: 1f,
                transitionThreshold: 1f);

            var visibleSample = clip.SampleWithoutAdvance(0.25f, fallbackVisibility: false);
            var completion = clip.AdvanceAndReportCompletion(0.25f);

            Assert.That(visibleSample, Is.True);
            Assert.That(completion, Is.False);
            Assert.That(clip.ElapsedSeconds, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrack_SampleWithoutAdvanceMatchesSampleAndAdvanceReturnBeforeAdvance()
        {
            var peekTrack = VisibilityTrack.CreateHide(1f);
            var advanceTrack = VisibilityTrack.CreateHide(1f);

            var peekSample = peekTrack.SampleWithoutAdvance(1f, fallbackVisibility: true);
            var advanceSample = advanceTrack.SampleAndAdvance(1f, fallbackVisibility: true);

            Assert.That(peekSample, Is.EqualTo(advanceSample));
        }

        [Test]
        [Category("Core")]
        public void VisibilityTrack_SampleWithoutAdvancePlusAdvance_MatchesSampleAndAdvance()
        {
            var splitTrack = VisibilityTrack.CreateHide(1f);
            var legacyTrack = VisibilityTrack.CreateHide(1f);

            var splitSample = splitTrack.SampleWithoutAdvance(1f, fallbackVisibility: true);
            var splitCompletion = splitTrack.AdvanceAndReportCompletion(1f);
            var legacySample = legacyTrack.SampleAndAdvance(1f, fallbackVisibility: true);

            Assert.That(splitSample, Is.EqualTo(legacySample));
            Assert.That(splitCompletion, Is.EqualTo(legacyTrack.IsComplete));
            Assert.That(splitTrack.IsComplete, Is.EqualTo(legacyTrack.IsComplete));
        }
    }
}
