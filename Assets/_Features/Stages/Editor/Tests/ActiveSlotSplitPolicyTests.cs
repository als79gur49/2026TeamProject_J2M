using System;
using System.IO;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class ActiveSlotSplitPolicyTests
    {
        private const string PolicyPath = "Docs/Architecture/Save-Architecture-V2-Phase4-Policy-Closeout.md";
        private const string PendingLaunchProviderPath =
            "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs";

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
            Assert.That(mainMenuController, Does.Contain("IPendingLaunchSlotProvider"));
            Assert.That(mainMenuController, Does.Contain("_pendingLaunchSlotProvider"));
            Assert.That(mainMenuController, Does.Contain("ActiveSlotProviderKey = _pendingLaunchSlotProviderDiagnosticsKey"));
            Assert.That(mainMenuController, Does.Not.Contain("ActiveSlotProvider _activeSlotProvider"));
            Assert.That(mainMenuInstaller, Does.Contain("ActiveSlotProviderPendingLaunchAdapter"));
            Assert.That(gameplayInstaller, Does.Not.Contain("IPendingLaunchSlotProvider"));
            Assert.That(mainMenuController, Does.Not.Contain("CampaignProfileDocument"));
            Assert.That(mainMenuController, Does.Not.Contain("LastPlayedSlotNumber"));
        }

        [Test]
        public void Factory_DoesNotExposeActiveSlotProviderAsProfileMetadata()
        {
            var factorySource = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveServiceFactory.cs");

            Assert.That(factorySource, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(factorySource, Does.Not.Contain("LastPlayedSlotNumber"));
        }

        [Test]
        public void Phase9Policy_WiresCinematicLaunchThroughPendingLaunchAdapterOnly()
        {
            var pendingLaunchProvider = File.ReadAllText(PendingLaunchProviderPath);
            var launchRouter = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs");
            var mainMenuInstaller = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var gameplayInstaller = File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var stageInstaller = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");

            Assert.That(pendingLaunchProvider, Does.Contain("IPendingLaunchSlotProvider"));
            Assert.That(pendingLaunchProvider, Does.Contain("ActiveSlotProviderPendingLaunchAdapter"));
            Assert.That(pendingLaunchProvider, Does.Contain("CampaignRunningSlotContext"));
            Assert.That(launchRouter, Does.Contain("IPendingLaunchSlotProvider"));
            Assert.That(launchRouter, Does.Contain("TryGetPendingLaunchSlot"));
            Assert.That(launchRouter, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(mainMenuInstaller, Does.Contain("ActiveSlotProviderPendingLaunchAdapter"));
            Assert.That(gameplayInstaller, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
            Assert.That(stageInstaller, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
        }

        [Test]
        public void LastPlayedSlotNumber_RemainsProfileMetadataOnly()
        {
            var profileDocument = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocument.cs");
            var pendingLaunchProvider = File.ReadAllText(PendingLaunchProviderPath);
            var mainMenuController = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(profileDocument, Does.Contain("public int LastPlayedSlotNumber"));
            Assert.That(pendingLaunchProvider, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(pendingLaunchProvider, Does.Not.Contain("CampaignProfileDocument"));
            Assert.That(mainMenuController, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(mainMenuController, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void MainMenuSemantics_KeepExplicitSlotIntentAsPendingLaunchState()
        {
            var source = File.ReadAllText(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(source, Does.Contain("_saveSlotStore.InitializeNewGame("));
            Assert.That(source, Does.Contain("_pendingLaunchSlotProvider.SetPendingLaunchSlot(slotNumber);"));
            Assert.That(source, Does.Contain("Launch(validation.Slot.CurrentStageId, StageNavigationKind.Continue, \"main-menu-new-game\")"));
            Assert.That(source, Does.Contain("Launch(validation.Slot.CurrentStageId, StageNavigationKind.Continue, \"main-menu-continue\")"));
            Assert.That(source, Does.Contain("_saveSlotStore.DeleteSlot(slotNumber);"));
            Assert.That(source, Does.Contain("_pendingLaunchSlotProvider.IsPendingLaunchSlot(slotNumber)"));
            Assert.That(source, Does.Contain("_pendingLaunchSlotProvider.ClearPendingLaunchSlot();"));
            Assert.That(source, Does.Contain("_saveSlotStore.LoadAll()"));
            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"));
        }

        [Test]
        public void StageLaunchAndGameplayMutation_DoNotUseLastPlayedSlotNumberAsSlotIdentity()
        {
            AssertCinematicLaunchSourceUsesPendingLaunchSlot(
                "Assets/_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs");
            AssertStageLaunchSourceUsesPendingLaunchSlot(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");
            AssertGameplayMutationSourceUsesRunningSlotContext(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");
            AssertGameplayMutationSourceUsesRunningSlotContext(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs");
            AssertSaveSlotStageClearProfileStoreUsesRunningSlotContext(
                "Assets/_Features/Stages/Runtime/Campaign/SaveSlotModels.cs");
        }

        [Test]
        public void DirectPlayAndDebugCampaignBridge_KeepTempActiveSlotIsolatedFromProfileMetadata()
        {
            var directPlayLauncher = File.ReadAllText(
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var directPlayContext = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Load/EditorDirectPlayContextStore.cs");
            var demoBridge = File.ReadAllText(
                "Assets/_Features/DemoStageControl/Runtime/DemoStageControlBridges.cs");

            Assert.That(directPlayContext, Does.Contain("TempActiveSlotProviderKey"));
            Assert.That(directPlayContext, Does.Contain("Game.Feature.Stages.DirectPlay.TempActiveSaveSlot"));
            Assert.That(directPlayLauncher, Does.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
            Assert.That(directPlayLauncher, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(directPlayLauncher, Does.Not.Contain("CampaignProfileDocument"));
            Assert.That(demoBridge, Does.Contain("DemoStageControlCampaignBridge"));
            Assert.That(demoBridge, Does.Contain("ActiveSlotProvider"));
            Assert.That(demoBridge, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(demoBridge, Does.Not.Contain("CampaignProfileDocument"));
        }

        private static void AssertCinematicLaunchSourceUsesPendingLaunchSlot(string path)
        {
            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"), path);
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"), path);
            Assert.That(source, Does.Not.Contain("TryGetActiveSlotNumber"), path);
            Assert.That(source, Does.Contain("IPendingLaunchSlotProvider"), path);
            Assert.That(source, Does.Contain("TryGetPendingLaunchSlot"), path);
        }

        private static void AssertStageLaunchSourceUsesPendingLaunchSlot(string path)
        {
            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"), path);
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"), path);
            Assert.That(source, Does.Contain("ActiveSlotProvider"), path);
            Assert.That(source, Does.Contain("CampaignRunningSlotContext"), path);
        }

        private static void AssertGameplayMutationSourceUsesRunningSlotContext(string path)
        {
            var source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"), path);
            Assert.That(source, Does.Not.Contain("CampaignProfileDocument"), path);
            Assert.That(source, Does.Not.Contain("ActiveSlotProvider"), path);
            Assert.That(source, Does.Contain("CampaignRunningSlotContext"), path);
        }

        private static void AssertSaveSlotStageClearProfileStoreUsesRunningSlotContext(string path)
        {
            var source = File.ReadAllText(path);
            var start = source.IndexOf("public sealed class SaveSlotStageClearProfileStore", StringComparison.Ordinal);
            var end = source.IndexOf("[Serializable]", start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), path);
            Assert.That(end, Is.GreaterThan(start), path);
            var profileStoreSource = source.Substring(start, end - start);

            Assert.That(profileStoreSource, Does.Not.Contain("LastPlayedSlotNumber"), path);
            Assert.That(profileStoreSource, Does.Not.Contain("CampaignProfileDocument"), path);
            Assert.That(profileStoreSource, Does.Not.Contain("ActiveSlotProvider"), path);
            Assert.That(profileStoreSource, Does.Contain("CampaignRunningSlotContext"), path);
        }
    }
}
