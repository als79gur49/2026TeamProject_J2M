using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UiFlowAudioOutcomeMapperTests
    {
        [Test]
        public void NavigateForward_MapsToNavigateForwardCue()
        {
            Assert.That(
                UiFlowAudioOutcomeMapper.TryMap(UiFlowAudioOutcomeKind.NavigateForward, out var cueId),
                Is.True);
            Assert.That(cueId, Is.EqualTo(UiAudioCueId.NavigateForward));
        }

        [Test]
        public void Back_MapsToNavigateBackCue()
        {
            Assert.That(
                UiFlowAudioOutcomeMapper.TryMap(UiFlowAudioOutcomeKind.NavigateBack, out var cueId),
                Is.True);
            Assert.That(cueId, Is.EqualTo(UiAudioCueId.NavigateBack));
        }

        [Test]
        public void Cancel_MapsToCancelCue()
        {
            Assert.That(
                UiFlowAudioOutcomeMapper.TryMap(UiFlowAudioOutcomeKind.Cancel, out var cueId),
                Is.True);
            Assert.That(cueId, Is.EqualTo(UiAudioCueId.Cancel));
        }

        [Test]
        public void NoOp_DoesNotPlayCue()
        {
            Assert.That(
                UiFlowAudioOutcomeMapper.TryMap(UiFlowAudioOutcomeKind.Silent, out _),
                Is.False);
        }
    }
}
