using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowCompositionTests
    {
        [Test]
        public void GameplayUiFlowInstaller_ComposesDurableViews_WithFakePorts()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_ComposesDurableViews_WithFakePorts");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                installer.Install(UiTestPortFactory.CreatePorts());

                Assert.That(installer.RootView, Is.Not.Null);
                Assert.That(installer.RootView.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.True);

                installer.GameplayScreenView.ClickHelp();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Help));
                Assert.That(installer.HelpScreenView.IsVisible, Is.True);
                Assert.That(installer.HudView.ViewModel.IsInteractive, Is.False);

                installer.HelpScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));

                installer.HudView.ClickPause();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.PausePopupView.IsVisible, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.False);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_ObjectiveInfoPopup_DoesNotPause_WithFakePorts()
        {
            var pauseService = new FakeGameplayPauseService();
            var rootObject = new GameObject("GameplayUiFlowInstaller_ObjectiveInfoPopup_DoesNotPause_WithFakePorts");

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
                Assert.That(installer.ObjectiveStatusScreenView.IsVisible, Is.True);

                installer.ObjectiveStatusScreenView.ClickInfo();
                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.True);
                Assert.That(installer.ObjectiveInfoPopupView.IsVisible, Is.True);
                Assert.That(pauseService.IsPaused, Is.False);

                installer.ObjectiveInfoPopupView.ClickClose();
                Assert.That(installer.PopupController.Contains(PopupId.ObjectiveInfo), Is.False);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(pauseService.IsPaused, Is.False);
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
