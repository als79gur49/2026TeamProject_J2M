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
        public void VisibilityTrack_SampleWithoutAdvanceMatchesSampleAndAdvanceReturnBeforeAdvance()
        {
            var peekTrack = VisibilityTrack.CreateHide(1f);
            var advanceTrack = VisibilityTrack.CreateHide(1f);

            var peekSample = peekTrack.SampleWithoutAdvance(1f, fallbackVisibility: true);
            var advanceSample = advanceTrack.SampleAndAdvance(1f, fallbackVisibility: true);

            Assert.That(peekSample, Is.EqualTo(advanceSample));
        }
    }
}
