using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.UI;
using UiDisplayWindowMode = Game.Feature.UI.Application.DisplayWindowMode;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuSettingsOverlayTests
    {
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private static readonly string[] SettingsImplementationSourcePaths =
        {
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuSettingsPortAdapter.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuSettingsOverlayController.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuSettingsRuntime.cs",
            "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
        };

        [Test]
        public void MainMenuSettingsButton_InvokesMainMenuSettingsPort()
        {
            var settingsPort = new RecordingMainMenuSettingsPort();
            var hub = new MainMenuHubController(
                settingsPort,
                new RecordingApplicationQuitPort(),
                new RecordingConfirmPopupPort(),
                _ => { });

            hub.HandleCommand(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));

            Assert.That(settingsPort.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenuSettingsPortAdapter_InstantiatesSettingsPrefabWithoutGameplayHost()
        {
            using var harness = new OverlayHarness();
            var settingsPort = new MainMenuSettingsPortAdapter(harness.OverlayController);

            settingsPort.OpenSettings();

            Assert.That(harness.CreatedRuntimeCount, Is.EqualTo(1));
            Assert.That(harness.Root.GetComponentsInChildren<SettingsScreenView>(true), Has.Length.EqualTo(1));
            AssertForbiddenMainMenuSettingsReferences();
        }

        [Test]
        public void MainMenuSettingsPortAdapter_DoesNotReferenceGameplayUiFlowInstallerOrUIFlowCoordinator()
        {
            foreach (var path in SettingsImplementationSourcePaths)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain("GameplayUiFlowInstaller"), path);
                Assert.That(source, Does.Not.Contain("UIFlowCoordinator"), path);
            }
        }

        [Test]
        public void MainMenuSettingsRuntime_BindsSettingsScreenPresenter()
        {
            using var harness = new RuntimeHarness();

            harness.Runtime.Open();

            Assert.That(harness.Runtime.View, Is.Not.Null);
            Assert.That(harness.Runtime.View.DisplayView.CurrentDisplayValueText, Is.EqualTo("1280 x 720"));
            Assert.That(harness.Runtime.View.DisplayView.IsDisplayApplyInteractable, Is.False);
        }

        [Test]
        public void MainMenuSettingsRuntime_CloseDisposesViewAndFlushesSettings()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            var viewObject = harness.Runtime.View.gameObject;

            harness.Runtime.Dispose();

            Assert.That(harness.AudioPort.FlushCount, Is.EqualTo(1));
            Assert.That(viewObject == null, Is.True);
            Assert.That(harness.Runtime.IsOpen, Is.False);
        }

        [Test]
        public void MainMenuSettingsRuntime_BackClosesOverlay()
        {
            using var harness = new OverlayHarness();
            harness.OverlayController.Open();

            harness.CreatedRuntime.View.ClickBack();

            Assert.That(harness.OverlayController.IsOpen, Is.False);
            Assert.That(harness.OverlayController.OverlayLayer.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void MainMenuSettingsRuntime_BackDuringRebind_CancelsRebindWithoutCloseRequest()
        {
            var keyboardPort = new ControllableKeyboardSettingsPort();
            using var harness = new RuntimeHarness(keyboardPort);
            var closeRequestCount = 0;
            harness.Runtime.CloseRequested += () => closeRequestCount++;
            harness.Runtime.Open();
            harness.Runtime.View.ClickInputTab();

            harness.Runtime.View.InputView.ClickPushRebind();
            Assert.That(keyboardPort.IsRebinding, Is.True);

            Assert.That(harness.Runtime.TryHandleBackRequested(), Is.True);

            Assert.That(keyboardPort.CancelRebindCount, Is.EqualTo(1));
            Assert.That(keyboardPort.IsRebinding, Is.False);
            Assert.That(closeRequestCount, Is.Zero);
            Assert.That(harness.Runtime.IsOpen, Is.True);
        }

        [Test]
        public void MainMenuSettingsRuntime_InputKeycapRebind_EmitsSelectThenConfirmCues()
        {
            var keyboardPort = new ControllableKeyboardSettingsPort();
            using var harness = new RuntimeHarness(keyboardPort);
            harness.Runtime.Open();
            harness.Runtime.View.ClickInputTab();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.InputView.ClickPushRebind();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));

            harness.UiAudioPort.Clear();
            keyboardPort.CompleteRebind(KeyboardBindingValidationResult.Success);

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Confirm }));
        }

        [Test]
        public void MainMenuSettingsRuntime_InputMovementToggleClick_EmitsToggleCue()
        {
            var keyboardPort = new ControllableKeyboardSettingsPort();
            using var harness = new RuntimeHarness(keyboardPort);
            harness.Runtime.Open();
            harness.Runtime.View.ClickInputTab();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.InputView.ClickMovementScheme();

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Toggle }));
        }

        [Test]
        public void MainMenuUiFlowInstaller_BackWithSettingsOverlayOpen_ClosesOverlayThroughSettingsPolicy()
        {
            using var harness = new OverlayHarness();
            var installerObject = new GameObject(nameof(MainMenuUiFlowInstaller_BackWithSettingsOverlayOpen_ClosesOverlayThroughSettingsPolicy));
            try
            {
                var installer = installerObject.AddComponent<MainMenuUiFlowInstaller>();
                SetPrivateField(installer, "_settingsOverlayController", harness.OverlayController);
                harness.OverlayController.Open();

                Assert.That(installer.TryHandleBackRequested(), Is.True);

                Assert.That(harness.OverlayController.IsOpen, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_BackDuringKeyboardRebind_ConsumesWithoutClosingPopupSettingsOrSection()
        {
            using var harness = new OverlayHarness();
            var installerObject = new GameObject(nameof(MainMenuUiFlowInstaller_BackDuringKeyboardRebind_ConsumesWithoutClosingPopupSettingsOrSection));
            var menuObject = new GameObject("MainMenuScreenView");
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            try
            {
                var installer = installerObject.AddComponent<MainMenuUiFlowInstaller>();
                var mainMenuView = menuObject.AddComponent<MainMenuScreenView>();
                mainMenuView.ShowSection(MainMenuSectionId.SaveSlots);
                harness.OverlayController.Open();
                popupController.Push(
                    new PopupRequest(
                        PopupId.Confirm,
                        new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                    out _);

                SetPrivateField(installer, "_keyboardBindingSettingsPort", new RebindingKeyboardSettingsPort());
                SetPrivateField(installer, "PopupController", popupController);
                SetPrivateField(installer, "_settingsOverlayController", harness.OverlayController);
                SetPrivateField(installer, "_mainMenuScreenView", mainMenuView);

                Assert.That(installer.TryHandleBackRequested(), Is.True);

                Assert.That(popupController.PopupCount, Is.EqualTo(1));
                Assert.That(harness.OverlayController.IsOpen, Is.True);
                Assert.That(mainMenuView.ActiveSection, Is.EqualTo(MainMenuSectionId.SaveSlots));
            }
            finally
            {
                popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(menuObject);
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_BackWithPopupOpen_UsesPopupPolicyBeforeSettingsOrSection()
        {
            using var harness = new OverlayHarness();
            var installerObject = new GameObject(nameof(MainMenuUiFlowInstaller_BackWithPopupOpen_UsesPopupPolicyBeforeSettingsOrSection));
            var menuObject = new GameObject("MainMenuScreenView");
            var popupController = new PopupController(new FakePopupRuntimeFactory());
            try
            {
                var installer = installerObject.AddComponent<MainMenuUiFlowInstaller>();
                var mainMenuView = menuObject.AddComponent<MainMenuScreenView>();
                mainMenuView.ShowSection(MainMenuSectionId.SaveSlots);
                harness.OverlayController.Open();
                popupController.Push(
                    new PopupRequest(
                        PopupId.Confirm,
                        new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                    out _);

                SetPrivateField(installer, "PopupController", popupController);
                SetPrivateField(installer, "_settingsOverlayController", harness.OverlayController);
                SetPrivateField(installer, "_mainMenuScreenView", mainMenuView);

                Assert.That(installer.TryHandleBackRequested(), Is.True);

                Assert.That(popupController.PopupCount, Is.Zero);
                Assert.That(harness.OverlayController.IsOpen, Is.True);
                Assert.That(mainMenuView.ActiveSection, Is.EqualTo(MainMenuSectionId.SaveSlots));
            }
            finally
            {
                popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(menuObject);
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_BackWithSaveSlotSectionOpen_ReturnsToRootSection()
        {
            var installerObject = new GameObject(nameof(MainMenuUiFlowInstaller_BackWithSaveSlotSectionOpen_ReturnsToRootSection));
            var menuObject = new GameObject("MainMenuScreenView");
            try
            {
                var installer = installerObject.AddComponent<MainMenuUiFlowInstaller>();
                var mainMenuView = menuObject.AddComponent<MainMenuScreenView>();
                mainMenuView.ShowSection(MainMenuSectionId.SaveSlots);
                SetPrivateField(installer, "_mainMenuScreenView", mainMenuView);

                Assert.That(installer.TryHandleBackRequested(), Is.True);

                Assert.That(mainMenuView.ActiveSection, Is.EqualTo(MainMenuSectionId.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menuObject);
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_BackAtRoot_IsNoOp()
        {
            var installerObject = new GameObject(nameof(MainMenuUiFlowInstaller_BackAtRoot_IsNoOp));
            var menuObject = new GameObject("MainMenuScreenView");
            try
            {
                var installer = installerObject.AddComponent<MainMenuUiFlowInstaller>();
                var mainMenuView = menuObject.AddComponent<MainMenuScreenView>();
                mainMenuView.ShowSection(MainMenuSectionId.None);
                SetPrivateField(installer, "_mainMenuScreenView", mainMenuView);

                Assert.That(installer.TryHandleBackRequested(), Is.False);

                Assert.That(mainMenuView.ActiveSection, Is.EqualTo(MainMenuSectionId.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menuObject);
                UnityEngine.Object.DestroyImmediate(installerObject);
            }
        }

        [Test]
        public void MainMenuScreenView_ShowSection_EmitsOnlyWhenSectionChanges()
        {
            var menuObject = new GameObject(nameof(MainMenuScreenView_ShowSection_EmitsOnlyWhenSectionChanges));
            try
            {
                var mainMenuView = menuObject.AddComponent<MainMenuScreenView>();
                var changedCount = 0;
                var lastSection = MainMenuSectionId.SaveSlots;
                mainMenuView.SectionChanged += sectionId =>
                {
                    changedCount++;
                    lastSection = sectionId;
                };

                mainMenuView.ShowSection(MainMenuSectionId.SaveSlots);
                Assert.That(changedCount, Is.Zero);

                mainMenuView.ShowSection(MainMenuSectionId.None);
                Assert.That(changedCount, Is.EqualTo(1));
                Assert.That(lastSection, Is.EqualTo(MainMenuSectionId.None));

                mainMenuView.ShowSection(MainMenuSectionId.None);
                Assert.That(changedCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(menuObject);
            }
        }

        [Test]
        public void MainMenuCameraPresentationController_AttachAtRoot_UsesIdleTargetAndKnot()
        {
            using var harness = new CameraPresentationHarness();
            harness.MainMenuView.ShowSection(MainMenuSectionId.None);

            harness.Controller.Attach(harness.MainMenuView, harness.OverlayHarness.OverlayController);

            Assert.That(harness.Camera.LookAt, Is.SameAs(harness.LookAtProxy));
            Assert.That(harness.LookAtProxy.position, Is.EqualTo(harness.IdleTarget.position));
            Assert.That(harness.Dolly.PositionUnits, Is.EqualTo(PathIndexUnit.Knot));
            Assert.That(harness.Dolly.CameraPosition, Is.EqualTo(1f));
        }

        [Test]
        public void MainMenuCameraPresentationController_SaveSlotsSection_UsesStartTargetAndKnot()
        {
            using var harness = new CameraPresentationHarness();
            harness.MainMenuView.ShowSection(MainMenuSectionId.None);
            harness.Controller.Attach(harness.MainMenuView, harness.OverlayHarness.OverlayController);

            harness.MainMenuView.ShowSection(MainMenuSectionId.SaveSlots);

            Assert.That(harness.Camera.LookAt, Is.SameAs(harness.LookAtProxy));
            Assert.That(harness.LookAtProxy.position, Is.EqualTo(harness.SaveSlotsTarget.position));
            Assert.That(harness.Dolly.CameraPosition, Is.EqualTo(4f));
        }

        [Test]
        public void MainMenuCameraPresentationController_SettingsOpen_OverridesAndRestoresSaveSlots()
        {
            using var harness = new CameraPresentationHarness();
            harness.MainMenuView.ShowSection(MainMenuSectionId.SaveSlots);
            harness.Controller.Attach(harness.MainMenuView, harness.OverlayHarness.OverlayController);

            harness.OverlayHarness.OverlayController.Open();

            Assert.That(harness.Camera.LookAt, Is.SameAs(harness.LookAtProxy));
            Assert.That(harness.LookAtProxy.position, Is.EqualTo(harness.SettingsTarget.position));
            Assert.That(harness.Dolly.CameraPosition, Is.EqualTo(0f));

            harness.OverlayHarness.OverlayController.Close();

            Assert.That(harness.Camera.LookAt, Is.SameAs(harness.LookAtProxy));
            Assert.That(harness.LookAtProxy.position, Is.EqualTo(harness.SaveSlotsTarget.position));
            Assert.That(harness.Dolly.CameraPosition, Is.EqualTo(4f));

            harness.MainMenuView.ShowSection(MainMenuSectionId.None);

            Assert.That(harness.Camera.LookAt, Is.SameAs(harness.LookAtProxy));
            Assert.That(harness.LookAtProxy.position, Is.EqualTo(harness.IdleTarget.position));
            Assert.That(harness.Dolly.CameraPosition, Is.EqualTo(1f));
        }

        [Test]
        public void MainMenuSettingsRuntime_OpenTwice_DoesNotDuplicateSettingsView()
        {
            using var harness = new OverlayHarness();

            harness.OverlayController.Open();
            harness.OverlayController.Open();

            Assert.That(harness.CreatedRuntimeCount, Is.EqualTo(1));
            Assert.That(harness.Root.GetComponentsInChildren<SettingsScreenView>(true), Has.Length.EqualTo(1));
        }

        [Test]
        public void MainMenuSettingsRuntime_SectionSelection_UsesCommonRuntimeCueOnce()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.ClickAudioTab();
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.Empty);

            harness.Runtime.View.ClickDisplayTab();
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));

            harness.Runtime.View.ClickDisplayTab();
            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));
        }

        [Test]
        public void MainMenuSettingsRuntime_AudioChange_UsesAudioSettingsPort()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.AudioView.BeginInteraction(AudioSettingsChannel.Bgm);
            harness.Runtime.View.AudioView.SetVolume(AudioSettingsChannel.Bgm, 0.25f);
            harness.Runtime.View.AudioView.SetMuted(AudioSettingsChannel.Sfx, true);
            harness.Runtime.View.AudioView.CommitInteraction(AudioSettingsChannel.Bgm);

            Assert.That(harness.AudioPort.SetVolumeCount, Is.EqualTo(1));
            Assert.That(harness.AudioPort.LastVolumeChannel, Is.EqualTo(AudioSettingsChannel.Bgm));
            Assert.That(harness.AudioPort.LastVolume, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(harness.AudioPort.SetMutedCount, Is.EqualTo(1));
            Assert.That(harness.AudioPort.LastMutedChannel, Is.EqualTo(AudioSettingsChannel.Sfx));
            Assert.That(harness.AudioPort.FlushCount, Is.EqualTo(1));
            Assert.That(
                harness.UiAudioPort.PlayedCueIds,
                Is.EqualTo(new[]
                {
                    UiAudioCueId.Toggle,
                    UiAudioCueId.AdjustValueCommit,
                }));
        }

        [Test]
        public void MainMenuSettingsRuntime_DisplayApply_UsesDisplaySettingsPortAndConfirmPopup()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();

            harness.Runtime.View.DisplayView.SelectResolution(1);
            harness.Runtime.View.DisplayView.ClickApply();

            Assert.That(harness.DisplayPort.BeginPreviewCount, Is.EqualTo(1));
            Assert.That(harness.DisplayPort.LastPreviewRequest.ModeIndex, Is.EqualTo(1));
            Assert.That(harness.PopupHarness.PopupController.PopupCount, Is.EqualTo(1));
            Assert.That(harness.PopupHarness.PopupController.TopPopup.Value.PopupId, Is.EqualTo(PopupId.Confirm));
        }

        [Test]
        public void MainMenuSettingsRuntime_DisplayResolutionChange_EmitsSelectCue()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.DisplayView.SelectResolution(1);

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.EqualTo(new[] { UiAudioCueId.Select }));
        }

        [Test]
        public void MainMenuSettingsRuntime_DisplayResolutionSameValue_EmitsNoCue()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();
            harness.UiAudioPort.Clear();

            harness.Runtime.View.DisplayView.SelectResolution(harness.Runtime.View.DisplayView.SelectedResolutionIndex);

            Assert.That(harness.UiAudioPort.PlayedCueIds, Is.Empty);
        }

        [Test]
        public void MainMenuSettingsRuntime_DisplayConfirm_HidesTransientStatusAfterDelay()
        {
            using var harness = new RuntimeHarness();
            double now = 0d;
            harness.PopupHarness.TransientStatusRelay.SetTimeProviderForTesting(() => now);
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();

            harness.Runtime.View.DisplayView.SelectResolution(1);
            harness.Runtime.View.DisplayView.ClickApply();
            harness.PopupHarness.PopupController.CloseTop(PopupCloseReason.UserAction, PopupCompletionKind.Confirmed);

            Assert.That(harness.Runtime.View.DisplayView.DisplayStatusText, Is.EqualTo("Display settings saved."));

            now = 2.1d;
            InvokePrivateMethod(harness.PopupHarness.TransientStatusRelay, "Update");

            Assert.That(harness.Runtime.View.DisplayView.DisplayStatusText, Is.Empty);
        }

        [Test]
        public void MainMenuSettingsRuntime_CancelOrClose_RevertsActiveDisplayPreview()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();
            harness.Runtime.View.DisplayView.SelectResolution(1);
            harness.Runtime.View.DisplayView.ClickApply();

            harness.Runtime.Dispose();

            Assert.That(harness.DisplayPort.RevertPreviewCount, Is.EqualTo(1));
            Assert.That(harness.DisplayPort.IsPreviewActive, Is.False);
            Assert.That(harness.PopupHarness.PopupController.PopupCount, Is.Zero);
        }

        [Test]
        public void MainMenuSettingsPopup_BlocksSaveSlotInteractionWhileOpen()
        {
            using var harness = new OverlayHarness();

            harness.OverlayController.Open();

            var blocker = harness.OverlayController.OverlayLayer.Find("SettingsBlocker");
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            Assert.That(blocker.GetComponent<Image>().raycastTarget, Is.True);
        }

        [Test]
        public void ConfirmPopup_BlocksSettingsInteractionWhileDisplayPreviewOpen()
        {
            using var harness = new RuntimeHarness();
            harness.Runtime.Open();
            harness.Runtime.View.ClickDisplayTab();

            harness.Runtime.View.DisplayView.SelectResolution(1);
            harness.Runtime.View.DisplayView.ClickApply();

            Assert.That(harness.PopupHarness.PopupLayerView.BlocksLowerLayerPointer, Is.True);
            Assert.That(harness.PopupHarness.PopupLayerView.BackdropMode, Is.EqualTo(PopupBackdropMode.Consume));
        }

        [Test]
        public void MainMenuSettings_SourceGuard_DoesNotReferenceGameplaySceneHostHUDPausePresentationSource()
        {
            AssertForbiddenMainMenuSettingsReferences();
        }

        [Test]
        public void MainMenuSettings_SourceGuard_DoesNotReferenceScreenControllerOrGameplayScreenRuntimeFactory()
        {
            foreach (var path in SettingsImplementationSourcePaths)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain("ScreenController"), path);
                Assert.That(source, Does.Not.Contain("GameplayScreenRuntimeFactory"), path);
                Assert.That(source, Does.Not.Contain("ScreenRequest"), path);
            }

            var adapterSource = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuSettingsRuntime.cs");
            Assert.That(adapterSource, Does.Contain("SettingsScreenRuntimeBuilder"));
            Assert.That(adapterSource, Does.Contain("ScreenActionKind.BackRequested"));
            Assert.That(adapterSource, Does.Contain("ScreenActionKind.RequestPopup"));
        }

        [Test]
        public void MainMenuSettings_ViewDoesNotTouchPlayerPrefsOrScreenSetResolution()
        {
            var guardedViewSources = new[]
            {
                "Assets/_Features/UI/UI_Screens/Runtime/SettingsScreenView.cs",
                "Assets/_Features/UI/UI_Screens/Runtime/SettingsAudioView.cs",
                "Assets/_Features/UI/UI_Screens/Runtime/SettingsDisplayView.cs",
                "Assets/_Features/UI/UI_Screens/Runtime/SettingsInputView.cs",
            };

            foreach (var path in guardedViewSources)
            {
                var source = ReadRepoFile(path);

                Assert.That(source, Does.Not.Contain("PlayerPrefs"), path);
                Assert.That(source, Does.Not.Contain("AudioSettingsStore"), path);
                Assert.That(source, Does.Not.Contain("DisplaySettingsStore"), path);
                Assert.That(source, Does.Not.Contain("Screen.SetResolution"), path);
            }
        }

        [Test]
        public void MainMenuScene_HasSettingsAudioAndDisplayRuntimeInstallers()
        {
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var rootObjects = scene.GetRootGameObjects();
            var uiRoot = rootObjects.Single(root => root.GetComponent<MainMenuUiFlowInstaller>() != null);
            var installer = uiRoot.GetComponent<MainMenuUiFlowInstaller>();
            var audioInstaller = uiRoot.GetComponent<AudioRuntimeInstaller>();
            var displayInstaller = uiRoot.GetComponent<DisplayRuntimeInstaller>();

            Assert.That(audioInstaller, Is.Not.Null);
            Assert.That(displayInstaller, Is.Not.Null);
            var serializedInstaller = new SerializedObject(installer);
            var serializedAudioInstaller = new SerializedObject(audioInstaller);
            Assert.That(serializedAudioInstaller.FindProperty("bindingMode").enumValueIndex, Is.EqualTo((int)AudioRuntimeInstallerBindingMode.PreferRegisteredPersistentRuntime));
            Assert.That(
                serializedInstaller.FindProperty("_screenPrefabCatalog").objectReferenceValue,
                Is.SameAs(UiTestPrefabAssetUtility.LoadScreenCatalog()));
            Assert.That(serializedInstaller.FindProperty("_settingsPreviewTimeoutSeconds").doubleValue, Is.EqualTo(15d));

            var cameraPresentationController = uiRoot.GetComponent<MainMenuCameraPresentationController>();
            Assert.That(cameraPresentationController, Is.Not.Null);
            Assert.That(serializedInstaller.FindProperty("_cameraPresentationController").objectReferenceValue, Is.SameAs(cameraPresentationController));

            var serializedCameraController = new SerializedObject(cameraPresentationController);
            Assert.That(serializedCameraController.FindProperty("_cinemachineCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_splineDolly").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_lookAtProxy").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_settingsTarget").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_idleTarget").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_saveSlotsTarget").objectReferenceValue, Is.Not.Null);
            Assert.That(serializedCameraController.FindProperty("_settingsKnotIndex").intValue, Is.EqualTo(0));
            Assert.That(serializedCameraController.FindProperty("_idleKnotIndex").intValue, Is.EqualTo(1));
            Assert.That(serializedCameraController.FindProperty("_saveSlotsKnotIndex").intValue, Is.EqualTo(4));
        }

        private static void AssertForbiddenMainMenuSettingsReferences()
        {
            var forbiddenTokens = new[]
            {
                "GameplaySceneHost",
                "HUDRootView",
                "GameplayUiPresentationSource",
                "IGameplayQueryFacade",
                "IGameplayCommandGateway",
                "IUiFlowPauseService",
                "Game.Feature.Gameplay",
                "PlayerPrefs",
                "Screen.SetResolution",
            };

            foreach (var path in SettingsImplementationSourcePaths)
            {
                var source = ReadRepoFile(path);
                foreach (var token in forbiddenTokens)
                {
                    Assert.That(source, Does.Not.Contain(token), $"{path} must not reference {token}");
                }
            }
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static void SetPrivateField(object target, string memberName, object value)
        {
            var type = target.GetType();
            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{type.Name}.{memberName}");
            var setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, $"{type.Name}.{memberName} setter");
            setter.Invoke(target, new[] { value });
        }

        private static void InvokePrivateMethod(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private sealed class CameraPresentationHarness : IDisposable
        {
            public CameraPresentationHarness()
            {
                Root = new GameObject("MainMenuCameraPresentationTestRoot");
                MainMenuViewObject = new GameObject("MainMenuScreenView");
                MainMenuView = MainMenuViewObject.AddComponent<MainMenuScreenView>();
                SettingsTarget = new GameObject("Target01").transform;
                IdleTarget = new GameObject("Target02").transform;
                SaveSlotsTarget = new GameObject("Target03").transform;
                LookAtProxy = new GameObject("MainMenuLookAtProxy").transform;
                CameraObject = new GameObject("CinemachineCamera");
                Camera = CameraObject.AddComponent<CinemachineCamera>();
                Dolly = CameraObject.AddComponent<CinemachineSplineDolly>();
                Controller = Root.AddComponent<MainMenuCameraPresentationController>();
                OverlayHarness = new OverlayHarness();

                SetPrivateField(Controller, "_cinemachineCamera", Camera);
                SetPrivateField(Controller, "_splineDolly", Dolly);
                SetPrivateField(Controller, "_lookAtProxy", LookAtProxy);
                SetPrivateField(Controller, "_settingsTarget", SettingsTarget);
                SetPrivateField(Controller, "_idleTarget", IdleTarget);
                SetPrivateField(Controller, "_saveSlotsTarget", SaveSlotsTarget);
                SetPrivateField(Controller, "_transitionDurationSeconds", 0f);
            }

            public GameObject Root { get; }

            public GameObject MainMenuViewObject { get; }

            public MainMenuScreenView MainMenuView { get; }

            public Transform SettingsTarget { get; }

            public Transform IdleTarget { get; }

            public Transform SaveSlotsTarget { get; }

            public Transform LookAtProxy { get; }

            public GameObject CameraObject { get; }

            public CinemachineCamera Camera { get; }

            public CinemachineSplineDolly Dolly { get; }

            public MainMenuCameraPresentationController Controller { get; }

            public OverlayHarness OverlayHarness { get; }

            public void Dispose()
            {
                Controller.Detach();
                OverlayHarness.Dispose();
                UnityEngine.Object.DestroyImmediate(CameraObject);
                UnityEngine.Object.DestroyImmediate(SettingsTarget.gameObject);
                UnityEngine.Object.DestroyImmediate(IdleTarget.gameObject);
                UnityEngine.Object.DestroyImmediate(SaveSlotsTarget.gameObject);
                UnityEngine.Object.DestroyImmediate(LookAtProxy.gameObject);
                UnityEngine.Object.DestroyImmediate(MainMenuViewObject);
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class RuntimeHarness : IDisposable
        {
            public RuntimeHarness(IKeyboardBindingSettingsPort keyboardBindingSettingsPort = null)
            {
                Root = new GameObject("MainMenuSettingsRuntimeTestRoot", typeof(RectTransform));
                ContentRoot = CreateFullScreenRect("SettingsContentRoot", Root.transform);
                PopupHarness = new PopupHarness(Root.transform);
                AudioPort = new RecordingAudioSettingsPort();
                DisplayPort = new RecordingDisplaySettingsPort();
                UiAudioPort = new RecordingUiAudioPort();
                Runtime = CreateRuntime(ContentRoot, PopupHarness, AudioPort, DisplayPort, keyboardBindingSettingsPort, UiAudioPort);
            }

            public GameObject Root { get; }

            public RectTransform ContentRoot { get; }

            public PopupHarness PopupHarness { get; }

            public RecordingAudioSettingsPort AudioPort { get; }

            public RecordingDisplaySettingsPort DisplayPort { get; }

            public RecordingUiAudioPort UiAudioPort { get; }

            public MainMenuSettingsRuntime Runtime { get; }

            public void Dispose()
            {
                Runtime.Dispose();
                PopupHarness.Dispose();
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class OverlayHarness : IDisposable
        {
            public OverlayHarness()
            {
                Root = new GameObject("MainMenuSettingsOverlayTestRoot", typeof(RectTransform));
                PopupHarness = new PopupHarness(Root.transform);
                AudioPort = new RecordingAudioSettingsPort();
                DisplayPort = new RecordingDisplaySettingsPort();
                UiAudioPort = new RecordingUiAudioPort();
                OverlayController = new MainMenuSettingsOverlayController(
                    Root.transform,
                    PopupHarness.PopupLayerView.transform,
                    contentRoot =>
                    {
                        CreatedRuntimeCount++;
                        CreatedRuntime = CreateRuntime(contentRoot, PopupHarness, AudioPort, DisplayPort, uiAudioPort: UiAudioPort);
                        return CreatedRuntime;
                    });
            }

            public GameObject Root { get; }

            public PopupHarness PopupHarness { get; }

            public RecordingAudioSettingsPort AudioPort { get; }

            public RecordingDisplaySettingsPort DisplayPort { get; }

            public RecordingUiAudioPort UiAudioPort { get; }

            public MainMenuSettingsOverlayController OverlayController { get; }

            public MainMenuSettingsRuntime CreatedRuntime { get; private set; }

            public int CreatedRuntimeCount { get; private set; }

            public void Dispose()
            {
                OverlayController.Dispose();
                PopupHarness.Dispose();
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        private sealed class PopupHarness : IDisposable
        {
            public PopupHarness(Transform parent)
            {
                PopupLayerView = CreatePopupLayer(parent);
                var popupCatalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(UiTestPrefabAssetUtility.PopupCatalogPath);
                Assert.That(popupCatalog, Is.Not.Null);
                PopupController = new PopupController(new GameplayPopupRuntimeFactory(
                    PopupLayerView,
                    popupCatalog,
                    localizedTextResolver: PackageFreeLocalizedTextResolver.CreateSettingsDefault()));
                PopupController.StateChanged += SyncPopupLayer;
                TimeoutRelay = PopupLayerView.gameObject.AddComponent<DisplayPreviewTimeoutRelay>();
                TransientStatusRelay = PopupLayerView.gameObject.AddComponent<DisplayStatusTransientRelay>();
                DisplayLifecycleRelay = PopupLayerView.gameObject.AddComponent<DisplaySettingsLifecycleRelay>();
                DisplayPreviewSessionHost = new DisplayPreviewSessionHost(PopupController, TimeoutRelay, 15d);
            }

            public PopupLayerView PopupLayerView { get; }

            public PopupController PopupController { get; }

            public DisplayPreviewTimeoutRelay TimeoutRelay { get; }

            public DisplayStatusTransientRelay TransientStatusRelay { get; }

            public DisplaySettingsLifecycleRelay DisplayLifecycleRelay { get; }

            public DisplayPreviewSessionHost DisplayPreviewSessionHost { get; }

            public void Dispose()
            {
                PopupController.StateChanged -= SyncPopupLayer;
                PopupController.Dispose();
            }

            private void SyncPopupLayer()
            {
                var topPopup = PopupController.TopPopup;
                if (!topPopup.HasValue)
                {
                    PopupLayerView.SetState(false, false, false, PopupBackdropMode.None);
                    return;
                }

                var policy = topPopup.Value.Policy;
                PopupLayerView.SetState(
                    true,
                    policy.ShowsDim,
                    policy.BlocksLowerLayers || policy.BackdropMode != PopupBackdropMode.None,
                    policy.BackdropMode);
            }
        }

        private static MainMenuSettingsRuntime CreateRuntime(
            RectTransform contentRoot,
            PopupHarness popupHarness,
            RecordingAudioSettingsPort audioPort,
            RecordingDisplaySettingsPort displayPort,
            IKeyboardBindingSettingsPort keyboardBindingSettingsPort = null,
            RecordingUiAudioPort uiAudioPort = null)
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var resolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            return new MainMenuSettingsRuntime(
                new SettingsScreenRuntimeBuildContext(
                    parent: contentRoot,
                    prefab: catalog.SettingsPrefab,
                    audioSettingsPort: audioPort,
                    displaySettingsPort: displayPort,
                    keyboardBindingSettingsPort: keyboardBindingSettingsPort ?? NoOpKeyboardBindingSettingsPort.Instance,
                    uiAudioPort: uiAudioPort ?? new RecordingUiAudioPort(),
                    displayPreviewSessionHost: popupHarness.DisplayPreviewSessionHost,
                    displaySettingsLifecycleRelay: popupHarness.DisplayLifecycleRelay,
                    typographyTheme: catalog.SettingsTypographyTheme,
                    displayStatusTransientRelay: popupHarness.TransientStatusRelay,
                    localizedTextResolver: resolver),
                SettingsScreenPayload.Default,
                popupHarness.PopupController);
        }

        private static PopupLayerView CreatePopupLayer(Transform parent)
        {
            var layerRoot = new GameObject("MainMenuPopupLayer", typeof(RectTransform));
            layerRoot.transform.SetParent(parent, false);
            var layerRect = (RectTransform)layerRoot.transform;
            StretchToParent(layerRect);
            var popupLayerView = layerRoot.AddComponent<PopupLayerView>();

            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(layerRoot.transform, false);
            StretchToParent((RectTransform)backdrop.transform);
            var backdropCanvasGroup = backdrop.AddComponent<CanvasGroup>();
            var backdropImage = backdrop.AddComponent<Image>();
            var backdropButton = backdrop.AddComponent<Button>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(layerRoot.transform, false);
            var contentRect = (RectTransform)content.transform;
            StretchToParent(contentRect);

            popupLayerView.Configure(layerRoot, backdropCanvasGroup, backdropImage, backdropButton, contentRect);
            return popupLayerView;
        }

        private static RectTransform CreateFullScreenRect(string name, Transform parent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rectTransform = (RectTransform)root.transform;
            StretchToParent(rectTransform);
            return rectTransform;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private sealed class RecordingMainMenuSettingsPort : IMainMenuSettingsPort
        {
            public int OpenCount { get; private set; }

            public void OpenSettings()
            {
                OpenCount++;
            }
        }

        private sealed class RecordingApplicationQuitPort : IApplicationQuitPort
        {
            public void Quit()
            {
            }
        }

        private sealed class RecordingConfirmPopupPort : IConfirmPopupPort
        {
            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
            }
        }

        private sealed class ControllableKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            private Action<KeyboardRebindResult> completed;
            private KeyboardBindableAction rebindingAction;
            private KeyboardMovementScheme movementScheme = KeyboardMovementScheme.Wasd;

            public bool IsRebinding { get; private set; }

            public int CancelRebindCount { get; private set; }

            public KeyboardBindingSettingsSnapshot Read()
            {
                return BuildSnapshot(IsRebinding);
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
            {
                if (IsRebinding)
                {
                    return KeyboardBindingValidationResult.AlreadyRebinding;
                }

                movementScheme = scheme;
                return KeyboardBindingValidationResult.Success;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                if (IsRebinding)
                {
                    return new KeyboardRebindStartResult(
                        false,
                        KeyboardBindingValidationResult.AlreadyRebinding,
                        BuildSnapshot(isRebinding: true));
                }

                IsRebinding = true;
                rebindingAction = action;
                this.completed = completed;
                return new KeyboardRebindStartResult(
                    true,
                    KeyboardBindingValidationResult.Success,
                    BuildSnapshot(isRebinding: true));
            }

            public void CancelRebind()
            {
                CancelRebindCount++;
                IsRebinding = false;
                completed = null;
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                IsRebinding = false;
                movementScheme = KeyboardMovementScheme.Wasd;
                return BuildSnapshot(movementScheme, isRebinding: false);
            }

            public void CompleteRebind(KeyboardBindingValidationResult result)
            {
                if (!IsRebinding)
                {
                    return;
                }

                IsRebinding = false;
                var completion = completed;
                completed = null;
                completion?.Invoke(new KeyboardRebindResult(rebindingAction, result, BuildSnapshot(movementScheme, isRebinding: false)));
            }

            private KeyboardBindingSettingsSnapshot BuildSnapshot(bool isRebinding)
            {
                return BuildSnapshot(movementScheme, isRebinding);
            }

            private static KeyboardBindingSettingsSnapshot BuildSnapshot(
                KeyboardMovementScheme movementScheme,
                bool isRebinding)
            {
                return new KeyboardBindingSettingsSnapshot(
                    movementScheme,
                    movementScheme == KeyboardMovementScheme.ArrowKeys ? "Arrow Keys" : "WASD",
                    "J",
                    "K",
                    isRebinding,
                    isRebinding ? (KeyboardBindableAction?)KeyboardBindableAction.Push : null);
            }
        }

        private sealed class RebindingKeyboardSettingsPort : IKeyboardBindingSettingsPort
        {
            public bool IsRebinding => true;

            public KeyboardBindingSettingsSnapshot Read()
            {
                return new KeyboardBindingSettingsSnapshot(
                    KeyboardMovementScheme.Wasd,
                    "WASD",
                    "J",
                    "K",
                    isRebinding: true,
                    rebindingAction: KeyboardBindableAction.Push);
            }

            public KeyboardBindingValidationResult TrySetMovementScheme(KeyboardMovementScheme scheme)
            {
                return KeyboardBindingValidationResult.AlreadyRebinding;
            }

            public KeyboardRebindStartResult StartRebind(
                KeyboardBindableAction action,
                Action<KeyboardRebindResult> completed)
            {
                return new KeyboardRebindStartResult(
                    false,
                    KeyboardBindingValidationResult.AlreadyRebinding,
                    Read());
            }

            public void CancelRebind()
            {
            }

            public KeyboardBindingSettingsSnapshot ResetToDefaults()
            {
                return Read();
            }
        }

        public sealed class RecordingAudioSettingsPort : IAudioSettingsPort
        {
            private AudioSettingsPortChannelState main = new(1f, false);
            private AudioSettingsPortChannelState bgm = new(0.8f, false);
            private AudioSettingsPortChannelState sfx = new(0.6f, false);

            public int FlushCount { get; private set; }

            public int SetMutedCount { get; private set; }

            public int SetVolumeCount { get; private set; }

            public AudioSettingsChannel LastMutedChannel { get; private set; }

            public AudioSettingsChannel LastVolumeChannel { get; private set; }

            public float LastVolume { get; private set; }

            public AudioSettingsPortSnapshot Read()
            {
                return new AudioSettingsPortSnapshot(main, bgm, sfx);
            }

            public void SetVolume(AudioSettingsChannel channel, float volume)
            {
                SetVolumeCount++;
                LastVolumeChannel = channel;
                LastVolume = volume;
                SetChannelState(channel, new AudioSettingsPortChannelState(volume, Read().GetChannelState(channel).IsMuted));
            }

            public void SetMuted(AudioSettingsChannel channel, bool isMuted)
            {
                SetMutedCount++;
                LastMutedChannel = channel;
                SetChannelState(channel, new AudioSettingsPortChannelState(Read().GetChannelState(channel).Volume, isMuted));
            }

            public void Flush()
            {
                FlushCount++;
            }

            private void SetChannelState(AudioSettingsChannel channel, AudioSettingsPortChannelState state)
            {
                switch (channel)
                {
                    case AudioSettingsChannel.Main:
                        main = state;
                        break;
                    case AudioSettingsChannel.Bgm:
                        bgm = state;
                        break;
                    case AudioSettingsChannel.Sfx:
                        sfx = state;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
                }
            }
        }

        public sealed class RecordingDisplaySettingsPort : IDisplaySettingsPort
        {
            private readonly DisplaySettingsPortModeOption[] modes =
            {
                new(1280, 720, "1280 x 720"),
                new(1920, 1080, "1920 x 1080"),
            };

            private int committedModeIndex;
            private int currentModeIndex;
            private UiDisplayWindowMode committedWindowMode = UiDisplayWindowMode.Windowed;
            private UiDisplayWindowMode currentWindowMode = UiDisplayWindowMode.Windowed;

            public int BeginPreviewCount { get; private set; }

            public int CommitPreviewCount { get; private set; }

            public int RevertPreviewCount { get; private set; }

            public bool IsPreviewActive { get; private set; }

            public DisplaySettingsPortPreviewRequest LastPreviewRequest { get; private set; }

            public DisplaySettingsPortSnapshot Read()
            {
                return new DisplaySettingsPortSnapshot(
                    modes,
                    committedModeIndex,
                    committedWindowMode,
                    modes[currentModeIndex].LabelText,
                    currentWindowMode,
                    IsPreviewActive);
            }

            public bool BeginPreview(DisplaySettingsPortPreviewRequest request)
            {
                BeginPreviewCount++;
                LastPreviewRequest = request;
                currentModeIndex = Mathf.Clamp(request.ModeIndex, 0, modes.Length - 1);
                currentWindowMode = request.WindowMode;
                IsPreviewActive = true;
                return true;
            }

            public bool CommitPreview()
            {
                CommitPreviewCount++;
                if (!IsPreviewActive)
                {
                    return false;
                }

                committedModeIndex = currentModeIndex;
                committedWindowMode = currentWindowMode;
                IsPreviewActive = false;
                return true;
            }

            public bool RevertPreview()
            {
                RevertPreviewCount++;
                if (!IsPreviewActive)
                {
                    return false;
                }

                currentModeIndex = committedModeIndex;
                currentWindowMode = committedWindowMode;
                IsPreviewActive = false;
                return true;
            }
        }
    }
}
