using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Flow.Audio;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Feature.UI.Tests
{
    public sealed class GameplayUiFlowCompositionTests
    {
        [Test]
        public void AudioRuntimeInstaller_IsGuardedAgainstSameRootDuplicates()
        {
            Assert.That(typeof(AudioRuntimeInstaller).GetCustomAttributes(typeof(DisallowMultipleComponent), true), Is.Not.Empty);
        }

        [Test]
        public void DisplayRuntimeInstaller_IsGuardedAgainstSameRootDuplicates()
        {
            Assert.That(typeof(DisplayRuntimeInstaller).GetCustomAttributes(typeof(DisallowMultipleComponent), true), Is.Not.Empty);
        }

        [Test]
        public void GameplayUiFlowInstaller_RequiresCoLocatedAudioRuntimeInstaller_ForSettingsAudio()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_RequiresCoLocatedAudioRuntimeInstaller_ForSettingsAudio");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignHudPrefab(installer);
                UiTestPrefabAssetUtility.AssignScreenPrefabCatalog(installer);
                UiTestPrefabAssetUtility.AssignPopupPrefabCatalog(installer);

                var exception = Assert.Throws<System.InvalidOperationException>(() => installer.Install(UiTestPortFactory.CreatePorts()));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller on the canonical bootstrap root for SettingsScreen audio controls."));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_RequiresCoLocatedDisplayRuntimeInstaller_ForSettingsDisplay()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_RequiresCoLocatedDisplayRuntimeInstaller_ForSettingsDisplay");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignHudPrefab(installer);
                UiTestPrefabAssetUtility.AssignScreenPrefabCatalog(installer);
                UiTestPrefabAssetUtility.AssignPopupPrefabCatalog(installer);
                rootObject.AddComponent<AudioRuntimeInstaller>();

                var exception = Assert.Throws<System.InvalidOperationException>(() => installer.Install(UiTestPortFactory.CreatePorts()));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplayUiFlowInstaller requires a co-located DisplayRuntimeInstaller on the canonical bootstrap root for SettingsScreen display controls."));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_DoesNotUseSceneGlobalAudioInstallerFallback()
        {
            var canonicalRoot = new GameObject("GameplayUiFlowInstaller_DoesNotUseSceneGlobalAudioInstallerFallback");
            var strayRoot = new GameObject("StrayAudioInstallerRoot");

            try
            {
                var installer = canonicalRoot.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignHudPrefab(installer);
                UiTestPrefabAssetUtility.AssignScreenPrefabCatalog(installer);
                UiTestPrefabAssetUtility.AssignPopupPrefabCatalog(installer);
                strayRoot.AddComponent<AudioRuntimeInstaller>();

                var exception = Assert.Throws<System.InvalidOperationException>(() => installer.Install(UiTestPortFactory.CreatePorts()));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplayUiFlowInstaller requires a co-located AudioRuntimeInstaller on the canonical bootstrap root for SettingsScreen audio controls."));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(canonicalRoot);
                Object.DestroyImmediate(strayRoot);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_DoesNotUseSceneGlobalDisplayInstallerFallback()
        {
            var canonicalRoot = new GameObject("GameplayUiFlowInstaller_DoesNotUseSceneGlobalDisplayInstallerFallback");
            var strayRoot = new GameObject("StrayDisplayInstallerRoot");

            try
            {
                var installer = canonicalRoot.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignHudPrefab(installer);
                UiTestPrefabAssetUtility.AssignScreenPrefabCatalog(installer);
                UiTestPrefabAssetUtility.AssignPopupPrefabCatalog(installer);
                canonicalRoot.AddComponent<AudioRuntimeInstaller>();
                strayRoot.AddComponent<DisplayRuntimeInstaller>();

                var exception = Assert.Throws<System.InvalidOperationException>(() => installer.Install(UiTestPortFactory.CreatePorts()));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplayUiFlowInstaller requires a co-located DisplayRuntimeInstaller on the canonical bootstrap root for SettingsScreen display controls."));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(canonicalRoot);
                Object.DestroyImmediate(strayRoot);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_RequiresSerializedUiAudioCueMap_ForUiSfxV1()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_RequiresSerializedUiAudioCueMap_ForUiSfxV1");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignHudPrefab(installer);
                UiTestPrefabAssetUtility.AssignScreenPrefabCatalog(installer);
                UiTestPrefabAssetUtility.AssignPopupPrefabCatalog(installer);
                rootObject.AddComponent<AudioRuntimeInstaller>();
                rootObject.AddComponent<DisplayRuntimeInstaller>();

                var exception = Assert.Throws<System.InvalidOperationException>(() => installer.Install(UiTestPortFactory.CreatePorts()));
                Assert.That(
                    exception.Message,
                    Is.EqualTo("GameplayUiFlowInstaller requires a serialized UiAudioCueMap on the canonical bootstrap root for UI SFX v1."));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void UiTestPrefabAssetUtility_AssignPersistentAudioFlowBootstrap_ConfiguresSameRootAccessSeam()
        {
            var bootstrapRoot = new GameObject("UiTestPrefabAssetUtility_AssignPersistentAudioFlowBootstrap_ConfiguresSameRootAccessSeam");

            try
            {
                var bootstrap = UiTestPrefabAssetUtility.AssignPersistentAudioFlowBootstrap(bootstrapRoot);
                var installer = bootstrapRoot.GetComponent<AudioRuntimeInstaller>();

                Assert.That(bootstrap, Is.Not.Null);
                Assert.That(installer, Is.Not.Null);
                Assert.That(installer.BindingMode, Is.EqualTo(AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime));
                Assert.That(bootstrap.AudioRuntimeInstaller, Is.SameAs(installer));
                Assert.That(bootstrap.PersistentRoot, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapRoot);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_ComposesCanonicalRootShell_AndCanonicalPopupStack_WithFakePorts()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_ComposesCanonicalRootShell_AndCanonicalPopupStack_WithFakePorts");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                var eventSystem = Object.FindFirstObjectByType<EventSystem>();
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.GetComponent("InputSystemUIInputModule"), Is.Not.Null);
                Assert.That(eventSystem.GetComponent<StandaloneInputModule>(), Is.Null);

                Assert.That(installer.RootView, Is.Not.Null);
                Assert.That(installer.RootView.name, Is.EqualTo("GameplayUiCanvasRoot"));
                Assert.That(installer.RootView.GetComponent<Canvas>(), Is.Not.Null);
                UiTestPrefabAssetUtility.AssertOverlayCanvasScaling(installer.RootView.gameObject);
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.HudView.IsVisible, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.False);

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
        public void GameplayUiFlowInstaller_PausePopupResume_UpdatesGameplayPresentationAudioPauseGroup()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_PausePopupResume_UpdatesGameplayPresentationAudioPauseGroup");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(CreatePortsWithValidStage());
                var pauseService = rootObject.GetComponent<AudioRuntimeInstaller>().AudioPlaybackPauseService;

                installer.HudView.ClickPause();
                Assert.That(
                    pauseService.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause),
                    Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(
                    pauseService.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause),
                    Is.False);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_PauseSettingsBack_KeepsGameplayPresentationAudioPaused()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_PauseSettingsBack_KeepsGameplayPresentationAudioPaused");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(CreatePortsWithValidStage());
                var pauseService = rootObject.GetComponent<AudioRuntimeInstaller>().AudioPlaybackPauseService;

                installer.HudView.ClickPause();
                installer.PausePopupView.ClickSettings();
                installer.SettingsScreenView.ClickBack();

                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(
                    pauseService.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause),
                    Is.True);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_PauseRetry_SuppressesGameplayPresentationAudioResume()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_PauseRetry_SuppressesGameplayPresentationAudioResume");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                var pause = new FakeGameplayPauseService();
                installer.Install(CreatePortsWithValidStage(pauseService: pause));
                var pauseService = rootObject.GetComponent<AudioRuntimeInstaller>().AudioPlaybackPauseService;

                installer.HudView.ClickPause();
                installer.PausePopupView.ClickRetry();

                Assert.That(pause.IsPaused, Is.False);
                Assert.That(
                    pauseService.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause),
                    Is.True);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_PauseMainMenu_SuppressesGameplayPresentationAudioResume()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_PauseMainMenu_SuppressesGameplayPresentationAudioResume");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                var pause = new FakeGameplayPauseService();
                installer.Install(CreatePortsWithValidStage(pauseService: pause));
                var pauseService = rootObject.GetComponent<AudioRuntimeInstaller>().AudioPlaybackPauseService;

                installer.HudView.ClickPause();
                installer.PausePopupView.ClickMainMenu();

                Assert.That(pause.IsPaused, Is.False);
                Assert.That(
                    pauseService.IsGroupPaused(AudioPlaybackPauseGroup.GameplayPresentation, AudioPauseReason.GameplayPause),
                    Is.True);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_PauseSettingsBackRestore_UsesFreshPausePopup_AndResumesOnlyOnResume()
        {
            var pauseService = new FakeGameplayPauseService();
            var rootObject = new GameObject("GameplayUiFlowInstaller_PauseSettingsBackRestore_UsesFreshPausePopup_AndResumesOnlyOnResume");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts(pauseService: pauseService));

                installer.HudView.ClickPause();
                var originalPausePopup = installer.PausePopupView;

                Assert.That(originalPausePopup, Is.Not.Null);
                Assert.That(pauseService.IsPaused, Is.True);

                originalPausePopup.ClickSettings();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Settings));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(pauseService.IsPaused, Is.True);

                installer.SettingsScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(installer.PausePopupView, Is.Not.SameAs(originalPausePopup));
                Assert.That(pauseService.IsPaused, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(pauseService.IsPaused, Is.False);
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void GameplayUiFlowInstaller_PauseObjectiveBackRestore_UsesFreshPausePopup_AndResumesOnlyOnResume()
        {
            var pauseService = new FakeGameplayPauseService();
            var rootObject = new GameObject("GameplayUiFlowInstaller_PauseObjectiveBackRestore_UsesFreshPausePopup_AndResumesOnlyOnResume");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts(pauseService: pauseService));

                installer.HudView.ClickPause();
                var originalPausePopup = installer.PausePopupView;

                Assert.That(originalPausePopup, Is.Not.Null);
                Assert.That(pauseService.IsPaused, Is.True);

                originalPausePopup.ClickObjective();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.ObjectiveStatus));
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(pauseService.IsPaused, Is.True);

                installer.ObjectiveStatusScreenView.ClickBack();
                Assert.That(installer.ScreenController.CurrentScreenId, Is.EqualTo(ScreenId.Gameplay));
                Assert.That(installer.PopupController.Contains(PopupId.Pause), Is.True);
                Assert.That(installer.PausePopupView, Is.Not.Null);
                Assert.That(installer.PausePopupView, Is.Not.SameAs(originalPausePopup));
                Assert.That(pauseService.IsPaused, Is.True);

                installer.PausePopupView.ClickResume();
                Assert.That(installer.PopupController.PopupCount, Is.EqualTo(0));
                Assert.That(pauseService.IsPaused, Is.False);
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
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts(
                    queryFacade: queryFacade,
                    pauseService: pauseService));

                Assert.That(installer.Coordinator.OpenObjectiveStatusScreen(), Is.True);
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
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                var completions = new List<PopupCompletion>();
                Assert.That(installer.Coordinator.RequestTooltipPopup(
                    new TooltipPopupPayload("Tip", "Tooltip body", TooltipPopupAnchorPreset.UpperRight),
                    completions.Add), Is.True);
                Assert.That(installer.TooltipPopupView, Is.Not.Null);
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.False);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.False);

                Assert.That(installer.Coordinator.RequestConfirmPopup(
                    new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", true),
                    completions.Add), Is.True);
                Assert.That(installer.ConfirmPopupView, Is.Not.Null);
                Assert.That(installer.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
                Assert.That(installer.PopupLayerView.IsDimVisible, Is.True);
                Assert.That(installer.HudController.IsGameplayReadOnly, Is.True);

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
        public void GameplayUiFlowInstaller_SettingsScreen_KeepsSectionViewsNestedUnderOneScreenShell()
        {
            var rootObject = new GameObject("GameplayUiFlowInstaller_SettingsScreen_KeepsSectionViewsNestedUnderOneScreenShell");

            try
            {
                var installer = rootObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(installer);
                installer.Install(UiTestPortFactory.CreatePorts());

                Assert.That(installer.Coordinator.OpenSettingsScreen(), Is.True);
                var settingsView = installer.SettingsScreenView;

                Assert.That(settingsView, Is.Not.Null);
                Assert.That(settingsView.transform.parent, Is.EqualTo(installer.ScreenLayerView.ContentRoot));
                Assert.That(settingsView.AudioView, Is.Not.Null);
                Assert.That(settingsView.DisplayView, Is.Not.Null);
                Assert.That(settingsView.AudioView.transform.IsChildOf(settingsView.transform), Is.True);
                Assert.That(settingsView.DisplayView.transform.IsChildOf(settingsView.transform), Is.True);
                Assert.That(settingsView.AudioView.transform.parent, Is.Not.EqualTo(installer.ScreenLayerView.ContentRoot));
                Assert.That(settingsView.DisplayView.transform.parent, Is.Not.EqualTo(installer.ScreenLayerView.ContentRoot));

                var mainAudioRow = settingsView.AudioView.transform.Find("MainAudioRow");
                var resolutionDropdown = settingsView.DisplayView.transform.Find("ResolutionRow/ResolutionDropdown");

                Assert.That(mainAudioRow, Is.Not.Null);
                Assert.That(resolutionDropdown, Is.Not.Null);
                Assert.That(mainAudioRow.IsChildOf(settingsView.AudioView.transform), Is.True);
                Assert.That(resolutionDropdown.IsChildOf(settingsView.DisplayView.transform), Is.True);
                Assert.That(mainAudioRow.parent, Is.Not.EqualTo(settingsView.transform));
                Assert.That(resolutionDropdown.parent, Is.Not.EqualTo(settingsView.transform));
            }
            finally
            {
                DestroyEventSystemIfPresent();
                Object.DestroyImmediate(rootObject);
            }
        }

        private static GameplayUiFlowPorts CreatePortsWithValidStage(FakeGameplayPauseService pauseService = null)
        {
            var queryFacade = new FakeGameplayQueryFacade(
                new GameplaySessionReadModel(1, false, true, false),
                FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                new GameplayObjectiveReadModel(false, false, false, false),
                new GameplayStageReadModel(StageId.CreateOrThrow("ui-audio-pause-test"), "UI Audio Pause Test"));
            return UiTestPortFactory.CreatePorts(queryFacade: queryFacade, pauseService: pauseService);
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
