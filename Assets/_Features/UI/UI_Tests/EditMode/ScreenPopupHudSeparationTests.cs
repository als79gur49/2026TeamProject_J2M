using System.IO;
using System.Linq;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;

namespace Game.Feature.UI.Tests
{
    public sealed class ScreenPopupHudSeparationTests
    {
        [Test]
        public void Hud_IsNeverPushedAsScreen()
        {
            var screenIds = typeof(ScreenId).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(screenIds, Does.Not.Contain("HUD"));
            Assert.That(screenIds, Does.Not.Contain("Hud"));
            Assert.That(typeof(HUDRootView).GetInterfaces(), Has.None.EqualTo(typeof(IScreenView)));
        }

        [Test]
        public void PopupBackdrop_IsOwnedByPopupControllerOrCoordinator()
        {
            Assert.That(typeof(PopupLayerView).GetProperties()
                .Select(property => property.PropertyType), Has.None.EqualTo(typeof(ScreenController)));
            Assert.That(typeof(PopupController).GetMethod("HandleBackdropClicked"), Is.Not.Null);
            Assert.That(typeof(UIFlowCoordinator).GetMethod("HandlePopupBackdropClicked"), Is.Not.Null);
        }

        [Test]
        public void ScreenRuntime_RaisesPopupRequestInsteadOfMutatingPopupStack()
        {
            var screenFactorySource = File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs");
            var settingsBuilderSource = File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/SettingsScreenRuntimeBuilder.cs");

            Assert.That(settingsBuilderSource, Does.Contain("ScreenAction.Popup"));
            Assert.That(screenFactorySource, Does.Not.Contain("_popupController"));
            Assert.That(settingsBuilderSource, Does.Not.Contain("_popupController"));
        }
    }
}
