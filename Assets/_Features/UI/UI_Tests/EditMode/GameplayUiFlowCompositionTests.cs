using System.Collections.Generic;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowCompositionTests
    {
        [Test]
        public void GameplayUiFlowInstaller_ComposesCanonicalRootShell_AndAllowlistedLegacyPopupStack_WithFakePorts()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_ComposesCanonicalRootShell_AndAllowlistedLegacyPopupStack_WithFakePorts");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts());

                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.GetComponent("InputSystemUIInputModule"), Is.Not.Null);
                Assert.That(eventSystem.GetComponent<StandaloneInputModule>(), Is.Null);

                Assert.That(installer.RootView, Is.Not.Null);
                Assert.That(installer.RootView.name, Is.EqualTo("GameplayUiCanvasRoot"));
                Assert.That(installer.RootView.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.True);

                installer.GameplayScreenView.ClickHelp();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
                Assert.That(installer.HelpScreenView.IsVisible, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.False);

                installer.HelpScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.False);
                Assert.That(installer.PausePopupView, Is.Null);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_ObjectiveInfoPopup_UsesSharedStackWithoutDirectPopupViewMutation()
        {
            var pauseService = new FakeGameplayPauseService();
            var rootObject = new GameObject("GameplayUiFlowInstaller_ObjectiveInfoPopup_UsesSharedStackWithoutDirectPopupViewMutation");

            try
            {
                var queryFacade = new FakeGameplayQueryFacade(
                    new Game.Feature.Gameplay.UIAccess.Models.GameplaySessionReadModel(7, false, true, false),
                    FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                    new Game.Feature.Gameplay.UIAccess.Models.GameplayObjectiveReadModel(true, true, false, false));
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts(
                    queryFacade: queryFacade,
                    pauseService: pauseService));

                installer.GameplayScreenView.ClickObjectives();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));

                installer.ObjectiveStatusScreenView.ClickInfo();
                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.True);
                Assert.That(installer.ObjectiveInfoPopupView, Is.Not.Null);
                Assert.That(installer.ObjectiveInfoPopupView.BodyText, Is.Not.Empty);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
                Assert.That(installer.Ports.PauseService.IsPaused, Is.False);

                installer.ObjectiveInfoPopupView.ClickClose();
                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.False);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_TooltipAndConfirmShareStackWithDifferentPolicies()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_TooltipAndConfirmShareStackWithDifferentPolicies");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts());

                var completions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Tip", "Tooltip body", TooltipPopupAnchorPreset.UpperRight),
                    completions.Add), Is.True);
                Assert.That(installer.TooltipPopupView, Is.Not.Null);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.True);

                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", true),
                    completions.Add), Is.True);
                Assert.That(installer.ConfirmPopupView, Is.Not.Null);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);
                Assert.That(installer.HudController.ActionBarViewModel.IsInteractive, Is.False);

                Assert.That(installer.Coordinator.HandleBackRequested(), Is.True);
                Assert.That(completions, Has.Count.EqualTo(1));
                Assert.That(completions[0].CompletionKind, Is.EqualTo(PopupCompletionKind.Cancelled));
                Assert.That(installer.TooltipPopupView, Is.Not.Null);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_InventoryScreen_ComposesCatalogDetailAndActionWithoutChangingScreenFlow()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_InventoryScreen_ComposesCatalogDetailAndActionWithoutChangingScreenFlow");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts());

                installer.GameplayScreenView.ClickInventory();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Inventory));
                Assert.That(installer.InventoryScreenView, Is.Not.Null);
                Assert.That(installer.InventoryScreenView.DetailTitleText, Is.EqualTo("Crystal Shard"));
                Assert.That(installer.InventoryScreenView.GetCatalogRowLabel(3), Is.EqualTo("Recon Map"));

                installer.InventoryScreenView.ClickItemRow(3);
                Assert.That(installer.InventoryScreenView.DetailTitleText, Is.EqualTo("Recon Map"));

                installer.InventoryScreenView.ClickPrimaryAction();
                Assert.That(installer.InventoryScreenView.ActionFeedbackText, Does.Contain("Recon Map"));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));

                installer.InventoryScreenView.ClickFilter();
                Assert.That(installer.InventoryScreenView.CatalogSummaryText, Does.Contain("Consumable"));
                Assert.That(installer.InventoryScreenView.DetailTitleText, Is.EqualTo("Crystal Shard"));
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Inventory));

                installer.InventoryScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        private static void DestroyEventSystemIfPresent()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }
        }
    }
}
