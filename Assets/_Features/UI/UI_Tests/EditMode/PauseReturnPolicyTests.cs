using Game.Feature.UI.Flow;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class PauseReturnPolicyTests
    {
        [Test]
        public void SettingsBack_FromPause_ReopensFreshPause()
        {
            var decision = PauseReturnPolicy.Decide(new PauseReturnContext(
                shouldRestorePausePopupAfterBack: true,
                screenHandledBack: true,
                ScreenId.Gameplay));

            Assert.That(decision.ShouldReopenPausePopup, Is.True);
            Assert.That(decision.ShouldReturnToGameplayScreen, Is.True);
            Assert.That(decision.ShouldDismissSettingsScreen, Is.True);
            Assert.That(decision.ShouldClearPauseReturnMode, Is.True);
        }

        [Test]
        public void SettingsBack_FromGameplay_ReturnsToGameplayWithoutPause()
        {
            var decision = PauseReturnPolicy.Decide(new PauseReturnContext(
                shouldRestorePausePopupAfterBack: false,
                screenHandledBack: true,
                ScreenId.Gameplay));

            Assert.That(decision.ShouldReopenPausePopup, Is.False);
            Assert.That(decision.ShouldReturnToGameplayScreen, Is.True);
            Assert.That(decision.ShouldDismissSettingsScreen, Is.True);
            Assert.That(decision.ShouldClearPauseReturnMode, Is.False);
        }

        [Test]
        public void Policy_DoesNotRequireUnityObjects()
        {
            var decision = PauseReturnPolicy.Decide(new PauseReturnContext(
                shouldRestorePausePopupAfterBack: true,
                screenHandledBack: false,
                ScreenId.Settings));

            Assert.That(decision.ShouldReopenPausePopup, Is.False);
            Assert.That(decision.ShouldReturnToGameplayScreen, Is.False);
            Assert.That(decision.ShouldDismissSettingsScreen, Is.False);
        }
    }
}
