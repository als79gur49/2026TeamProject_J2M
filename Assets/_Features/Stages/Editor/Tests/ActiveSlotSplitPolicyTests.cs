using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class ActiveSlotSplitPolicyTests
    {
        private const string PolicyPath = "Docs/Architecture/Save-Architecture-V2-Phase4-Policy-Closeout.md";

        [Test]
        public void PolicyCloseout_DistinguishesLastPlayedSlotFromPendingLaunchSlot()
        {
            var source = File.ReadAllText(PolicyPath);

            Assert.That(source, Does.Contain("`LastPlayedSlotNumber` and pending launch slot are separate concepts"));
            Assert.That(source, Does.Contain("CampaignProfileDocument.LastPlayedSlotNumber"));
            Assert.That(source, Does.Contain("profile metadata"));
            Assert.That(source, Does.Contain("ActiveSlotProvider"));
            Assert.That(source, Does.Contain("local/session state"));
        }

        [Test]
        public void ProductionCode_StillUsesActiveSlotProviderAsLocalLaunchState()
        {
            var mainMenuInstaller = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var gameplayInstaller = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var mainMenuController = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(mainMenuInstaller, Does.Contain("new ActiveSlotProvider()"));
            Assert.That(gameplayInstaller, Does.Contain("new ActiveSlotProvider()"));
            Assert.That(mainMenuController, Does.Contain("ActiveSlotProviderKey = _activeSlotProvider.PlayerPrefsKey"));
        }

        [Test]
        public void Factory_DoesNotExposeActiveSlotProviderAsProfileMetadata()
        {
            var factorySource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveServiceFactory.cs");

            Assert.That(factorySource, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(factorySource, Does.Not.Contain("LastPlayedSlotNumber"));
        }
    }
}
