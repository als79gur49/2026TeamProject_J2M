using Game.Feature.UI.Flow;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class UIBlockPolicyTests
    {
        [Test]
        public void Evaluate_HelpScreenWithoutPopup_BlocksHudOnly()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(ScreenId.Help, null, popupCount: 0);

            Assert.That(snapshot.BlocksHudInteraction, Is.True);
            Assert.That(snapshot.BlocksScreenInteraction, Is.False);
            Assert.That(snapshot.PopupConsumesBack, Is.False);
        }

        [Test]
        public void Evaluate_ModalPopupOnGameplayScreen_BlocksHudAndScreenAndConsumesBack()
        {
            var policy = new UIBlockPolicy();

            var snapshot = policy.Evaluate(
                ScreenId.Gameplay,
                new PopupEntry(PopupId.Pause, isModal: true),
                popupCount: 1);

            Assert.That(snapshot.BlocksHudInteraction, Is.True);
            Assert.That(snapshot.BlocksScreenInteraction, Is.True);
            Assert.That(snapshot.PopupConsumesBack, Is.True);
        }
    }
}
