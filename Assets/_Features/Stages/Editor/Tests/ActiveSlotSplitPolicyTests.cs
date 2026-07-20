using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class ActiveSlotSplitPolicyTests
    {
        private const string HandoffOwnerPath =
            "Assets/_Features/Stages/Runtime/Campaign/PendingLaunchSlotProvider.cs";

        [Test]
        public void ProductionComposition_UsesApplicationSessionHandoffOwner_NotActiveAdapter()
        {
            var owner = Read(HandoffOwnerPath);
            var mainMenuInstaller = Read(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            var controller = Read(
                "Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            var gameplayInstaller = Read(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");

            Assert.That(owner, Does.Contain("CampaignLaunchHandoffSessionStore"));
            Assert.That(owner, Does.Contain("RuntimeInitializeLoadType.SubsystemRegistration"));
            Assert.That(owner, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
            Assert.That(mainMenuInstaller, Does.Contain("CampaignLaunchHandoffSessionStore.Instance"));
            Assert.That(controller, Does.Contain("ICampaignLaunchHandoffStore"));
            Assert.That(gameplayInstaller, Does.Contain("CampaignLaunchHandoffSessionStore.Instance"));
            Assert.That(mainMenuInstaller, Does.Not.Contain("ActiveSlotProviderPendingLaunchAdapter"));
        }

        [Test]
        public void LastPlayedSlotNumber_RemainsProfileMetadataOnly()
        {
            var profileDocument = Read(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignProfileDocument.cs");
            var launchSources = string.Join(
                "\n",
                Read(HandoffOwnerPath),
                Read("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs"),
                Read("Assets/_Features/UI/UI_Composition/Runtime/CinematicStageLaunchRouter.cs"),
                Read("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"));

            Assert.That(profileDocument, Does.Contain("public int LastPlayedSlotNumber"));
            Assert.That(launchSources, Does.Not.Contain("LastPlayedSlotNumber"));
            Assert.That(launchSources, Does.Not.Contain("CampaignProfileDocument"));
        }

        [Test]
        public void ProfileAndLocalStateDtos_ContainNoPendingOrRunningFields()
        {
            var profileFields = typeof(CampaignProfileDocument)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();
            var localStateFields = typeof(CampaignLocalLaunchStateCampaignDocument)
                .GetFields()
                .Select(field => field.Name)
                .ToArray();

            Assert.That(profileFields, Does.Not.Contain("PendingLaunchSlotNumber"));
            Assert.That(profileFields, Does.Not.Contain("RunningSlotNumber"));
            Assert.That(localStateFields, Is.EqualTo(new[] { "activeSlotNumber", "lastUpdatedUtc" }));
            Assert.That(CampaignLocalLaunchStateRepository.SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void GameplayInstaller_CommitsActiveThenPinsRunningThenConsumesMatchingPending()
        {
            var source = Read(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs");
            var setActive = source.IndexOf(
                "_activeSlotProvider.SetActiveSlot(pendingHandoff.SlotNumber)",
                StringComparison.Ordinal);
            var createRunning = source.IndexOf(
                "new CampaignRunningSlotContext(pendingHandoff.SlotNumber)",
                StringComparison.Ordinal);
            var consume = source.IndexOf(
                "launchHandoffStore.TryConsume(pendingHandoff.Token",
                StringComparison.Ordinal);

            Assert.That(setActive, Is.GreaterThanOrEqualTo(0));
            Assert.That(createRunning, Is.GreaterThan(setActive));
            Assert.That(consume, Is.GreaterThan(createRunning));
            Assert.That(source, Does.Contain("LoadNonEmptySlot"));
            Assert.That(source, Does.Contain("ValidateLaunchStageIds"));
        }

        [Test]
        public void TransitionGuard_PrecedesStageContextMutation()
        {
            var source = Read(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var guard = source.IndexOf("_guard.TryBegin", StringComparison.Ordinal);
            var beforeLoad = source.IndexOf("beforeLoad?.Invoke()", StringComparison.Ordinal);

            Assert.That(guard, Is.GreaterThanOrEqualTo(0));
            Assert.That(beforeLoad, Is.GreaterThan(guard));
        }

        [Test]
        public void RunningMutationUsesSceneLocalContext_NotActiveOrLastPlayed()
        {
            foreach (var path in new[]
                     {
                         "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
                         "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignChancesReadSource.cs",
                     })
            {
                var source = Read(path);
                Assert.That(source, Does.Contain("CampaignRunningSlotContext"), path);
                Assert.That(source, Does.Not.Contain("LastPlayedSlotNumber"), path);
                Assert.That(source, Does.Not.Contain("ActiveSlotProvider"), path);
            }
        }

        [Test]
        public void DirectPlayProductionAndTempBoundariesRemainExplicit()
        {
            var launcher = Read(
                "Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var context = Read(
                "Assets/_Features/Stages/Runtime/Load/EditorDirectPlayContextStore.cs");

            Assert.That(launcher, Does.Contain("PrimeCampaignProductionSlotCore"));
            Assert.That(launcher, Does.Contain("activeSlotProvider.SetActiveSlot(productionSlotNumber)"));
            Assert.That(launcher, Does.Contain("EditorDirectPlayContextStore.TempActiveSlotProviderKey"));
            Assert.That(context, Does.Contain("Game.Feature.Stages.DirectPlay.TempActiveSaveSlot"));
            Assert.That(launcher, Does.Not.Contain("CampaignLaunchHandoff"));
            Assert.That(context, Does.Not.Contain("CampaignLaunchHandoff"));
        }

        [Test]
        public void CurrentArchitectureDocsDefineCommittedPendingAndRunningOwners()
        {
            var localStatePolicy = Read(
                "Docs/Architecture/Campaign-LocalState-Launch-State.md");
            var architectureIndex = Read("Docs/Architecture/README.md");
            var cloudPolicy = Read(
                "Docs/Architecture/Steam-Cloud-File-Inventory-Policy.md");

            Assert.That(localStatePolicy, Does.Contain("committed"));
            Assert.That(localStatePolicy, Does.Contain("application-session"));
            Assert.That(localStatePolicy, Does.Contain("CampaignRunningSlotContext"));
            Assert.That(localStatePolicy, Does.Contain("first accepted"));
            Assert.That(architectureIndex, Does.Contain("active commit point"));
            Assert.That(cloudPolicy, Does.Contain("not written to any file or PlayerPrefs key"));
        }

        private static string Read(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }
    }
}
