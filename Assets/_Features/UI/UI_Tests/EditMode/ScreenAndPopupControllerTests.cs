using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ScreenAndPopupControllerTests
    {
        [Test]
        public void ScreenController_SetRootPushPop_MaintainsBackStack()
        {
            var controller = new Game.Feature.UI.Flow.ScreenController();

            controller.SetRoot(Game.Feature.UI.Flow.ScreenId.Gameplay);

            Assert.That(controller.CurrentScreenId, Is.EqualTo(Game.Feature.UI.Flow.ScreenId.Gameplay));
            Assert.That(controller.BackStackCount, Is.EqualTo(0));

            Assert.That(controller.Push(Game.Feature.UI.Flow.ScreenId.Help), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(Game.Feature.UI.Flow.ScreenId.Help));
            Assert.That(controller.BackStackCount, Is.EqualTo(1));

            Assert.That(controller.Pop(), Is.True);
            Assert.That(controller.CurrentScreenId, Is.EqualTo(Game.Feature.UI.Flow.ScreenId.Gameplay));
            Assert.That(controller.BackStackCount, Is.EqualTo(0));
        }

        [Test]
        public void PopupController_PushAndPop_TracksTopPopup()
        {
            var controller = new Game.Feature.UI.Flow.PopupController();

            Assert.That(controller.Push(new Game.Feature.UI.Flow.PopupEntry(Game.Feature.UI.Flow.PopupId.Pause, isModal: true)), Is.True);
            Assert.That(controller.PopupCount, Is.EqualTo(1));
            Assert.That(controller.TopPopup.HasValue, Is.True);
            Assert.That(controller.TopPopup.Value.PopupId, Is.EqualTo(Game.Feature.UI.Flow.PopupId.Pause));

            Assert.That(controller.PopTop(out var poppedEntry), Is.True);
            Assert.That(poppedEntry.PopupId, Is.EqualTo(Game.Feature.UI.Flow.PopupId.Pause));
            Assert.That(controller.PopupCount, Is.EqualTo(0));
        }
    }
}
