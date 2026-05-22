using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuHubTests
    {
        private const string MainMenuScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";

        [Test]
        public void UiHoverScaleEffect_Defaults_EnablePointerClickPunch()
        {
            var root = new GameObject(nameof(UiHoverScaleEffect_Defaults_EnablePointerClickPunch), typeof(RectTransform));
            try
            {
                var effect = root.AddComponent<UiHoverScaleEffect>();

                Assert.That(effect, Is.AssignableTo<IPointerClickHandler>());

                var serialized = new SerializedObject(effect);
                Assert.That(serialized.FindProperty("_clickPunchStrength").floatValue, Is.EqualTo(0.08f).Within(0.001f));
                Assert.That(serialized.FindProperty("_clickPunchDurationSeconds").floatValue, Is.EqualTo(0.18f).Within(0.001f));
                Assert.That(serialized.FindProperty("_clickPunchVibrato").intValue, Is.EqualTo(6));
                Assert.That(serialized.FindProperty("_clickPunchElasticity").floatValue, Is.EqualTo(0.65f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainMenuScreenPrefab_HasTopBarContentHostBottomBar()
        {
            var prefab = LoadMainMenuPrefab();
            var topBar = GetPrivateField<RectTransform>(prefab, "_topBar");
            var contentHost = GetPrivateField<RectTransform>(prefab, "_contentHost");
            var bottomBar = GetPrivateField<RectTransform>(prefab, "_bottomBar");
            var commandPanel = GetPrivateField<RectTransform>(prefab, "_mainCommandPanel");
            var saveSlotOverlayLayer = GetPrivateField<RectTransform>(prefab, "_saveSlotOverlayLayer");
            var startButton = GetPrivateField<Button>(prefab, "_startButton");
            var settingsButton = GetPrivateField<Button>(prefab, "_settingsButton");
            var quitButton = GetPrivateField<Button>(prefab, "_quitButton");
            var saveSlotBlockerRoot = GetPrivateField<GameObject>(prefab, "_saveSlotBlockerRoot");
            var saveSlotBlockerCanvasGroup = GetPrivateField<CanvasGroup>(prefab, "_saveSlotBlockerCanvasGroup");
            var saveSlotBlockerImage = GetPrivateField<Image>(prefab, "_saveSlotBlockerImage");

            Assert.That(topBar.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(contentHost.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(bottomBar.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(commandPanel.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(saveSlotOverlayLayer.transform.IsChildOf(prefab.transform), Is.True);
            Assert.That(saveSlotOverlayLayer.GetSiblingIndex(), Is.GreaterThan(commandPanel.GetSiblingIndex()));
            Assert.That(saveSlotBlockerRoot.transform.IsChildOf(saveSlotOverlayLayer), Is.True);
            Assert.That(saveSlotBlockerCanvasGroup, Is.Not.Null);
            Assert.That(saveSlotBlockerImage, Is.Not.Null);
            Assert.That(saveSlotBlockerImage.color.a, Is.EqualTo(0.48f).Within(0.001f));
            Assert.That(saveSlotBlockerCanvasGroup.blocksRaycasts, Is.False);
            Assert.That(saveSlotBlockerImage.raycastTarget, Is.False);
            Assert.That(startButton.transform.IsChildOf(commandPanel), Is.True);
            Assert.That(settingsButton.transform.IsChildOf(commandPanel), Is.True);
            Assert.That(quitButton.transform.IsChildOf(commandPanel), Is.True);
            AssertCommandButtonHoverScaleEffect(startButton.transform);
            AssertCommandButtonHoverScaleEffect(settingsButton.transform);
            AssertCommandButtonHoverScaleEffect(quitButton.transform);
        }

        [Test]
        public void MainMenuScreenPrefab_HasExactlyThreeSaveSlotCardsUnderSaveSlotPanel()
        {
            var prefab = LoadMainMenuPrefab();
            var panel = prefab.SaveSlotPanel;
            var saveSlotOverlayLayer = GetPrivateField<RectTransform>(prefab, "_saveSlotOverlayLayer");

            Assert.That(panel, Is.Not.Null);
            Assert.That(saveSlotOverlayLayer, Is.Not.Null);
            Assert.That(panel.transform.IsChildOf(saveSlotOverlayLayer), Is.True);
            Assert.That(panel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
            foreach (var card in panel.SlotCards)
            {
                Assert.That(card, Is.Not.Null);
                Assert.That(card.transform.IsChildOf(panel.transform), Is.True);
            }
        }

        [Test]
        public void MainMenuScreenPrefab_RequiredAuthoredReferencesAreAssigned()
        {
            var prefab = LoadMainMenuPrefab();

            Assert.DoesNotThrow(prefab.ValidateAuthoredStructureOrThrow);
        }

        [Test]
        public void MainMenuScreenView_ValidateAuthoredStructure_AllowsNestedButtonLabelsAndSaveSlotPanel()
        {
            var root = new GameObject(nameof(MainMenuScreenView_ValidateAuthoredStructure_AllowsNestedButtonLabelsAndSaveSlotPanel), typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var view = root.AddComponent<MainMenuScreenView>();
                var topBar = new GameObject("TopBar", typeof(RectTransform)).GetComponent<RectTransform>();
                var contentHost = new GameObject("ContentHost", typeof(RectTransform)).GetComponent<RectTransform>();
                var bottomBar = new GameObject("BottomBar", typeof(RectTransform)).GetComponent<RectTransform>();
                var commandPanel = new GameObject("MainCommandPanel", typeof(RectTransform)).GetComponent<RectTransform>();
                var saveSlotOverlayLayer = new GameObject("SaveSlotOverlayLayer", typeof(RectTransform)).GetComponent<RectTransform>();
                topBar.SetParent(root.transform, false);
                contentHost.SetParent(root.transform, false);
                bottomBar.SetParent(root.transform, false);
                commandPanel.SetParent(root.transform, false);
                saveSlotOverlayLayer.SetParent(root.transform, false);

                var saveSlotWrapper = new GameObject("SaveSlotWrapper", typeof(RectTransform)).transform;
                saveSlotWrapper.SetParent(saveSlotOverlayLayer, false);
                var panelObject = new GameObject("SaveSlotPanelView", typeof(RectTransform));
                panelObject.transform.SetParent(saveSlotWrapper, false);
                var panel = panelObject.AddComponent<SaveSlotPanelView>();
                var cards = new SaveSlotCardView[SaveSlotPanelView.RequiredSlotCardCount];
                for (var i = 0; i < cards.Length; i++)
                {
                    cards[i] = CreateAuthoredSaveSlotCard($"SaveSlotCard{i + 1}", panel.transform);
                }

                SetPrivateField(panel, "_slotCards", cards);

                var saveSlotBlockerRoot = new GameObject("SaveSlotBlocker", typeof(RectTransform));
                saveSlotBlockerRoot.transform.SetParent(saveSlotOverlayLayer, false);
                var saveSlotBlockerCanvasGroup = saveSlotBlockerRoot.AddComponent<CanvasGroup>();
                var saveSlotBlockerImage = saveSlotBlockerRoot.AddComponent<Image>();

                var startButton = CreateButton("StartButton", commandPanel);
                var settingsButton = CreateButton("SettingsButton", commandPanel);
                var quitButton = CreateButton("QuitButton", commandPanel);
                var startLabel = CreateNestedButtonLabel(startButton.transform);
                var settingsLabel = CreateNestedButtonLabel(settingsButton.transform);
                var quitLabel = CreateNestedButtonLabel(quitButton.transform);

                SetPrivateField(view, "_root", root);
                SetPrivateField(view, "_topBar", topBar);
                SetPrivateField(view, "_contentHost", contentHost);
                SetPrivateField(view, "_bottomBar", bottomBar);
                SetPrivateField(view, "_mainCommandPanel", commandPanel);
                SetPrivateField(view, "_saveSlotOverlayLayer", saveSlotOverlayLayer);
                SetPrivateField(view, "_saveSlotPanel", panel);
                SetPrivateField(view, "_saveSlotBlockerRoot", saveSlotBlockerRoot);
                SetPrivateField(view, "_saveSlotBlockerCanvasGroup", saveSlotBlockerCanvasGroup);
                SetPrivateField(view, "_saveSlotBlockerImage", saveSlotBlockerImage);
                SetPrivateField(view, "_startButton", startButton);
                SetPrivateField(view, "_startButtonLabel", startLabel);
                SetPrivateField(view, "_settingsButton", settingsButton);
                SetPrivateField(view, "_settingsButtonLabel", settingsLabel);
                SetPrivateField(view, "_quitButton", quitButton);
                SetPrivateField(view, "_quitButtonLabel", quitLabel);
                SetPrivateField(
                    view,
                    "_commandNavigationGroup",
                    CreateNavigationGroup(
                        startButton,
                        settingsButton,
                        quitButton));

                Assert.DoesNotThrow(view.ValidateAuthoredStructureOrThrow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveSlotPanelView_ValidateAuthoredStructure_AllowsCardsUnderContentWrapper()
        {
            var root = new GameObject(nameof(SaveSlotPanelView_ValidateAuthoredStructure_AllowsCardsUnderContentWrapper), typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<SaveSlotPanelView>();
                var content = new GameObject("Content", typeof(RectTransform)).transform;
                content.SetParent(root.transform, false);
                var cards = new SaveSlotCardView[SaveSlotPanelView.RequiredSlotCardCount];
                for (var i = 0; i < cards.Length; i++)
                {
                    cards[i] = CreateAuthoredSaveSlotCard($"SaveSlotCard{i + 1}", content);
                }

                SetPrivateField(panel, "_slotCards", cards);

                Assert.DoesNotThrow(panel.ValidateAuthoredStructureOrThrow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainMenuScreenRuntime_KeepsSaveSlotCardsInSerializedOrder()
        {
            var parentObject = CreateSizedRectParent("MainMenuScreenRuntimeCardLayoutParent", 1280f, 720f);
            var view = UnityEngine.Object.Instantiate(LoadMainMenuPrefab(), parentObject.transform, false);

            try
            {
                view.SetVisible(true);
                var viewRect = (RectTransform)view.transform;
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewRect);

                var panel = view.SaveSlotPanel;
                var panelLayout = panel.GetComponent<VerticalLayoutGroup>();
                var cards = panel.SlotCards;

                Assert.That(panelLayout, Is.Not.Null);
                Assert.That(panelLayout.spacing, Is.GreaterThanOrEqualTo(0f));
                Assert.That(panelLayout.padding.left, Is.Zero);
                Assert.That(panel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
                Assert.That(cards, Has.Length.EqualTo(3));

                for (var i = 0; i < cards.Length; i++)
                {
                    Assert.That(cards[i], Is.Not.Null);
                    Assert.That(cards[i].transform.IsChildOf(panel.transform), Is.True);
                    Assert.That(cards[i].transform.GetSiblingIndex(), Is.EqualTo(i));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void SaveSlotPanelView_CardIntent_BubblesSaveSlotIntentOnly()
        {
            var root = new GameObject(nameof(SaveSlotPanelView_CardIntent_BubblesSaveSlotIntentOnly));
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<SaveSlotPanelView>();
                var cards = new SaveSlotCardView[SaveSlotPanelView.RequiredSlotCardCount];
                for (var i = 0; i < cards.Length; i++)
                {
                    cards[i] = CreateAuthoredSaveSlotCard($"SaveSlotCard{i + 1}", root.transform);
                }

                SetPrivateField(panel, "_slotCards", cards);
                root.SetActive(true);
                for (var i = 0; i < cards.Length; i++)
                {
                    InvokePrivate(cards[i], "OnEnable");
                }

                InvokePrivate(panel, "OnEnable");
                panel.Bind(new SaveSlotPanelViewModel(new[]
                {
                    CreateCardViewModel(1, SaveSlotIntentKind.NewGame),
                    CreateCardViewModel(2, SaveSlotIntentKind.Continue),
                    CreateCardViewModel(3, SaveSlotIntentKind.Restart),
                }));

                var received = new List<SaveSlotIntent>();
                panel.SaveSlotIntentRequested += received.Add;

                GetPrivateField<Button>(cards[1], "_primaryButton").onClick.Invoke();

                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].SlotNumber, Is.EqualTo(2));
                Assert.That(received[0].IntentKind, Is.EqualTo(SaveSlotIntentKind.Continue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveSlotPanelView_DeleteButton_BubblesDeleteIntent()
        {
            var root = new GameObject(nameof(SaveSlotPanelView_DeleteButton_BubblesDeleteIntent));
            root.SetActive(false);
            try
            {
                var panel = root.AddComponent<SaveSlotPanelView>();
                var card = CreateAuthoredSaveSlotCard("SaveSlotCard1", root.transform);
                SetPrivateField(panel, "_slotCards", new[] { card, null, null });

                root.SetActive(true);
                InvokePrivate(card, "OnEnable");
                InvokePrivate(panel, "OnEnable");
                panel.Bind(new SaveSlotPanelViewModel(new[]
                {
                    CreateCardViewModel(1, SaveSlotIntentKind.Continue),
                }));

                SaveSlotIntent? received = null;
                panel.SaveSlotIntentRequested += intent => received = intent;

                GetPrivateField<Button>(card, "_deleteButton").onClick.Invoke();

                Assert.That(received.HasValue, Is.True);
                Assert.That(received.Value.SlotNumber, Is.EqualTo(1));
                Assert.That(received.Value.IntentKind, Is.EqualTo(SaveSlotIntentKind.Delete));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SaveSlotCardView_EmptySlot_KeepsDetailAndActionRowsInLayout()
        {
            var root = new GameObject(nameof(SaveSlotCardView_EmptySlot_KeepsDetailAndActionRowsInLayout), typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var card = root.AddComponent<SaveSlotCardView>();
                AuthorSaveSlotCard(card);
                root.SetActive(true);
                InvokePrivate(card, "OnEnable");

                card.Bind(CreateEmptyCardViewModel(1));

                Assert.That(card.transform.Find("DetailRow").gameObject.activeSelf, Is.True);
                Assert.That(card.transform.Find("MetaRow").gameObject.activeSelf, Is.True);
                Assert.That(card.transform.Find("ActionRow").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<TMP_Text>(card, "_stageLabel").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<TMP_Text>(card, "_chancesLabel").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<TMP_Text>(card, "_deathsLabel").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<TMP_Text>(card, "_lastPlayedLabel").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<Button>(card, "_primaryButton").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<Button>(card, "_primaryButton").interactable, Is.True);
                Assert.That(GetPrivateField<Button>(card, "_deleteButton").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<Button>(card, "_deleteButton").interactable, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainMenuScreenView_SettingsButton_RaisesCommandIntentOnly()
        {
            var harness = CreateShellHarness(withPanel: false);
            try
            {
                MainMenuCommandIntent? command = null;
                MainMenuNavigationIntent? navigation = null;
                harness.View.CommandRequested += intent => command = intent;
                harness.View.NavigationRequested += intent => navigation = intent;

                harness.SettingsButton.onClick.Invoke();

                Assert.That(command.HasValue, Is.True);
                Assert.That(command.Value.CommandKind, Is.EqualTo(MainMenuCommandKind.OpenSettings));
                Assert.That(navigation.HasValue, Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_StartButton_RaisesSaveSlotNavigationIntentOnly()
        {
            var harness = CreateShellHarness(withPanel: false);
            try
            {
                MainMenuCommandIntent? command = null;
                MainMenuNavigationIntent? navigation = null;
                harness.View.CommandRequested += intent => command = intent;
                harness.View.NavigationRequested += intent => navigation = intent;

                harness.StartButton.onClick.Invoke();

                Assert.That(navigation.HasValue, Is.True);
                Assert.That(navigation.Value.SectionId, Is.EqualTo(MainMenuSectionId.SaveSlots));
                Assert.That(command.HasValue, Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_QuitButton_RaisesCommandIntentOnly()
        {
            var harness = CreateShellHarness(withPanel: false);
            try
            {
                MainMenuCommandIntent? command = null;
                MainMenuNavigationIntent? navigation = null;
                harness.View.CommandRequested += intent => command = intent;
                harness.View.NavigationRequested += intent => navigation = intent;

                harness.QuitButton.onClick.Invoke();

                Assert.That(command.HasValue, Is.True);
                Assert.That(command.Value.CommandKind, Is.EqualTo(MainMenuCommandKind.Quit));
                Assert.That(navigation.HasValue, Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_ShowSection_TogglesSaveSlotPanel()
        {
            var harness = CreateShellHarness(withPanel: true);
            try
            {
                harness.View.ShowSection(MainMenuSectionId.None);

                Assert.That(harness.View.ActiveSection, Is.EqualTo(MainMenuSectionId.None));
                Assert.That(harness.View.SaveSlotPanel.gameObject.activeSelf, Is.False);

                harness.View.ShowSection(MainMenuSectionId.SaveSlots);

                Assert.That(harness.View.ActiveSection, Is.EqualTo(MainMenuSectionId.SaveSlots));
                Assert.That(harness.View.SaveSlotPanel.gameObject.activeSelf, Is.True);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_ShowSectionSaveSlots_EnablesBlockerAndDisablesCommands()
        {
            var harness = CreateShellHarness(withPanel: true);
            try
            {
                harness.View.ShowSection(MainMenuSectionId.None);

                Assert.That(harness.SaveSlotOverlayLayer.gameObject.activeSelf, Is.False);
                Assert.That(harness.SaveSlotBlockerRoot.activeSelf, Is.False);
                Assert.That(harness.SaveSlotBlockerCanvasGroup.blocksRaycasts, Is.False);
                Assert.That(harness.SaveSlotBlockerImage.raycastTarget, Is.False);
                Assert.That(harness.StartButton.interactable, Is.True);
                Assert.That(harness.SettingsButton.interactable, Is.True);
                Assert.That(harness.QuitButton.interactable, Is.True);

                harness.View.ShowSection(MainMenuSectionId.SaveSlots);

                Assert.That(harness.SaveSlotOverlayLayer.gameObject.activeSelf, Is.True);
                Assert.That(harness.SaveSlotOverlayLayer.GetSiblingIndex(), Is.GreaterThan(harness.CommandPanel.GetSiblingIndex()));
                Assert.That(harness.SaveSlotBlockerRoot.activeSelf, Is.True);
                Assert.That(harness.SaveSlotBlockerCanvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(harness.SaveSlotBlockerCanvasGroup.blocksRaycasts, Is.True);
                Assert.That(harness.SaveSlotBlockerImage.raycastTarget, Is.True);
                Assert.That(harness.StartButton.interactable, Is.False);
                Assert.That(harness.SettingsButton.interactable, Is.False);
                Assert.That(harness.QuitButton.interactable, Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_CommandAndNavigationIntent_DoNotDependOnSaveSlotPanel()
        {
            var harness = CreateShellHarness(withPanel: false);
            try
            {
                MainMenuCommandIntent? command = null;
                MainMenuNavigationIntent? navigation = null;
                harness.View.CommandRequested += intent => command = intent;
                harness.View.NavigationRequested += intent => navigation = intent;

                harness.View.ClickSettings();
                harness.View.RequestSection(MainMenuSectionId.Profile);

                Assert.That(command.HasValue, Is.True);
                Assert.That(command.Value.CommandKind, Is.EqualTo(MainMenuCommandKind.OpenSettings));
                Assert.That(navigation.HasValue, Is.True);
                Assert.That(navigation.Value.SectionId, Is.EqualTo(MainMenuSectionId.Profile));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenView_DoesNotReferenceApplicationQuitOrSceneManager()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/MainMenuScreenView.cs");

            Assert.That(source, Does.Not.Contain("Application.Quit"));
            Assert.That(source, Does.Not.Contain("SceneManager"));
        }

        [Test]
        public void MainMenuScreenView_DoesNotReferenceSaveSlotStoreOrStageLaunchContextStore()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/MainMenuScreenView.cs");

            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(source, Does.Not.Contain("StageLaunchContextStore"));
        }

        [Test]
        public void SaveSlotPanelView_DoesNotReferenceSaveSlotStoreOrSceneManager()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SaveSlotPanelView.cs");

            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(source, Does.Not.Contain("SceneManager"));
            Assert.That(source, Does.Not.Contain("StageLaunchContextStore"));
        }

        [Test]
        public void MainMenuController_DoesNotHandleSettingsOrQuit()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");

            Assert.That(source, Does.Not.Contain("OpenSettings"));
            Assert.That(source, Does.Not.Contain("MainMenuCommand"));
            Assert.That(source, Does.Not.Contain("Quit"));
        }

        [Test]
        public void MainMenuHubController_DoesNotMutateSaveSlots()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuHubController.cs");

            Assert.That(source, Does.Not.Contain("SaveSlotStore"));
            Assert.That(source, Does.Not.Contain("ActiveSlotProvider"));
            Assert.That(source, Does.Not.Contain("IStageLaunchRouter"));
            Assert.That(source, Does.Not.Contain("StageNavigationRequest"));
        }

        [Test]
        public void MainMenuHubController_NavigationIntent_DoesNotCallSaveSlotController()
        {
            var shownSections = new List<MainMenuSectionId>();
            var hub = new MainMenuHubController(
                new FakeSettingsPort(),
                new FakeApplicationQuitPort(),
                new FakeConfirmPopupPort(),
                shownSections.Add);

            hub.HandleNavigation(new MainMenuNavigationIntent(MainMenuSectionId.History));

            Assert.That(shownSections, Is.EqualTo(new[] { MainMenuSectionId.History }));
        }

        [Test]
        public void MainMenuQuit_RequiresConfirm_ThenInvokesQuitPort()
        {
            var confirmPort = new FakeConfirmPopupPort();
            var quitPort = new FakeApplicationQuitPort();
            var hub = new MainMenuHubController(
                new FakeSettingsPort(),
                quitPort,
                confirmPort,
                _ => { });

            hub.HandleCommand(new MainMenuCommandIntent(MainMenuCommandKind.Quit));

            Assert.That(confirmPort.Requests.Count, Is.EqualTo(1));
            Assert.That(confirmPort.Requests[0].IsConfirmDestructive, Is.True);
            Assert.That(quitPort.QuitCount, Is.Zero);

            confirmPort.Complete(true);

            Assert.That(quitPort.QuitCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenuQuit_Cancel_DoesNotInvokeQuitPort()
        {
            var confirmPort = new FakeConfirmPopupPort();
            var quitPort = new FakeApplicationQuitPort();
            var hub = new MainMenuHubController(
                new FakeSettingsPort(),
                quitPort,
                confirmPort,
                _ => { });

            hub.HandleCommand(new MainMenuCommandIntent(MainMenuCommandKind.Quit));
            confirmPort.Complete(false);

            Assert.That(quitPort.QuitCount, Is.Zero);
        }

        [Test]
        public void MainMenuSettings_Command_UsesSettingsPort()
        {
            var settingsPort = new FakeSettingsPort();
            var shownSections = new List<MainMenuSectionId>();
            var hub = new MainMenuHubController(
                settingsPort,
                new FakeApplicationQuitPort(),
                new FakeConfirmPopupPort(),
                shownSections.Add);

            hub.HandleCommand(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));

            Assert.That(shownSections, Is.EqualTo(new[] { MainMenuSectionId.None }));
            Assert.That(settingsPort.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void MainMenuHubController_CanShowSaveSlotsSection()
        {
            var shownSections = new List<MainMenuSectionId>();
            var hub = new MainMenuHubController(
                new FakeSettingsPort(),
                new FakeApplicationQuitPort(),
                new FakeConfirmPopupPort(),
                shownSections.Add);

            hub.HandleNavigation(new MainMenuNavigationIntent(MainMenuSectionId.SaveSlots));

            Assert.That(shownSections, Is.EqualTo(new[] { MainMenuSectionId.SaveSlots }));
        }

        [Test]
        public void MainMenuUiFlowInstaller_DoesNotReferenceGameplayHostHudPresentationSource()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(source, Does.Not.Contain("GameplaySceneHost"));
            Assert.That(source, Does.Not.Contain("GameplayUiPresentationSource"));
            Assert.That(source, Does.Not.Contain("HUD"));
            Assert.That(source, Does.Not.Contain("Hud"));
            Assert.That(source, Does.Not.Contain("GameplayUiPresentationSource"));
        }

        [Test]
        public void MainMenuUiFlowInstaller_WiringIsSplitIntoPopupSaveSlotAndHubModules()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(source, Does.Contain("BuildPopupModule()"));
            Assert.That(source, Does.Contain("BuildSaveSlotModule()"));
            Assert.That(source, Does.Contain("BuildHubModule()"));
        }

        private static MainMenuScreenView LoadMainMenuPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            Assert.That(prefab, Is.Not.Null, MainMenuScreenPrefabPath);
            return prefab;
        }

        private static SaveSlotCardViewModel CreateCardViewModel(int slotNumber, SaveSlotIntentKind primaryIntentKind)
        {
            return new SaveSlotCardViewModel(
                slotNumber,
                SaveSlotCardState.Existing,
                $"Slot {slotNumber}",
                "Continue",
                "Stage 1",
                "Chances 3",
                "Deaths 0",
                string.Empty,
                primaryIntentKind.ToString(),
                primaryIntentKind,
                showDelete: true);
        }

        private static SaveSlotCardViewModel CreateEmptyCardViewModel(int slotNumber)
        {
            return new SaveSlotCardViewModel(
                slotNumber,
                SaveSlotCardState.Empty,
                $"Slot {slotNumber}",
                "Empty",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "New Game",
                SaveSlotIntentKind.NewGame,
                showDelete: false);
        }

        private static ShellHarness CreateShellHarness(bool withPanel)
        {
            var root = new GameObject("MainMenuScreenViewTestRoot", typeof(RectTransform));
            root.SetActive(false);
            var view = root.AddComponent<MainMenuScreenView>();
            var topBar = new GameObject("TopBar", typeof(RectTransform)).GetComponent<RectTransform>();
            var contentHost = new GameObject("ContentHost", typeof(RectTransform)).GetComponent<RectTransform>();
            var bottomBar = new GameObject("BottomBar", typeof(RectTransform)).GetComponent<RectTransform>();
            var commandPanel = new GameObject("MainCommandPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            var saveSlotOverlayLayer = new GameObject("SaveSlotOverlayLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            topBar.SetParent(root.transform, false);
            contentHost.SetParent(root.transform, false);
            bottomBar.SetParent(root.transform, false);
            commandPanel.SetParent(root.transform, false);
            saveSlotOverlayLayer.SetParent(root.transform, false);

            var startButton = CreateButton("StartButton", commandPanel);
            var settingsButton = CreateButton("SettingsButton", commandPanel);
            var quitButton = CreateButton("QuitButton", commandPanel);
            var saveSlotBlockerRoot = new GameObject("SaveSlotBlocker", typeof(RectTransform));
            saveSlotBlockerRoot.transform.SetParent(saveSlotOverlayLayer, false);
            saveSlotBlockerRoot.SetActive(false);
            var saveSlotBlockerCanvasGroup = saveSlotBlockerRoot.AddComponent<CanvasGroup>();
            var saveSlotBlockerImage = saveSlotBlockerRoot.AddComponent<Image>();
            SaveSlotPanelView panel = null;
            if (withPanel)
            {
                var panelObject = new GameObject("SaveSlotPanelView", typeof(RectTransform));
                panelObject.transform.SetParent(saveSlotOverlayLayer, false);
                panel = panelObject.AddComponent<SaveSlotPanelView>();
            }

            SetPrivateField(view, "_root", root);
            SetPrivateField(view, "_topBar", topBar);
            SetPrivateField(view, "_contentHost", contentHost);
            SetPrivateField(view, "_bottomBar", bottomBar);
            SetPrivateField(view, "_mainCommandPanel", commandPanel);
            SetPrivateField(view, "_saveSlotOverlayLayer", saveSlotOverlayLayer);
            SetPrivateField(view, "_saveSlotPanel", panel);
            SetPrivateField(view, "_saveSlotBlockerRoot", saveSlotBlockerRoot);
            SetPrivateField(view, "_saveSlotBlockerCanvasGroup", saveSlotBlockerCanvasGroup);
            SetPrivateField(view, "_saveSlotBlockerImage", saveSlotBlockerImage);
            SetPrivateField(view, "_startButton", startButton);
            SetPrivateField(view, "_settingsButton", settingsButton);
            SetPrivateField(view, "_quitButton", quitButton);
            root.SetActive(true);
            InvokePrivate(view, "OnEnable");
            return new ShellHarness(
                root,
                view,
                startButton,
                settingsButton,
                quitButton,
                commandPanel,
                saveSlotOverlayLayer,
                saveSlotBlockerRoot,
                saveSlotBlockerCanvasGroup,
                saveSlotBlockerImage);
        }

        private static GameObject CreateSizedRectParent(string objectName, float width, float height)
        {
            var parentObject = new GameObject(objectName, typeof(RectTransform));
            var parentRect = (RectTransform)parentObject.transform;
            parentRect.anchorMin = Vector2.zero;
            parentRect.anchorMax = Vector2.zero;
            parentRect.pivot = Vector2.zero;
            parentRect.sizeDelta = new Vector2(width, height);
            return parentObject;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.AddComponent<Button>();
        }

        private static UiSelectableButtonGroup CreateNavigationGroup(params Button[] buttons)
        {
            var slots = new UiSelectableButtonSlot[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
            {
                var frameObject = new GameObject("SelectionFrame", typeof(RectTransform));
                frameObject.transform.SetParent(buttons[i].transform, false);
                slots[i] = new UiSelectableButtonSlot(buttons[i], frameObject.AddComponent<Image>());
            }

            var group = new UiSelectableButtonGroup();
            group.Configure(slots, UiSelectionVisualProfile.CreateRuntimeDefault(), wrap: false, skipNonInteractable: true);
            return group;
        }

        private static SaveSlotCardView CreateAuthoredSaveSlotCard(string name, Transform parent)
        {
            var cardObject = new GameObject(name, typeof(RectTransform));
            cardObject.transform.SetParent(parent, false);
            var card = cardObject.AddComponent<SaveSlotCardView>();
            AuthorSaveSlotCard(card);
            return card;
        }

        private static void AuthorSaveSlotCard(SaveSlotCardView card)
        {
            var headerRow = CreateRow("HeaderRow", card.transform);
            var detailRow = CreateRow("DetailRow", card.transform);
            var metaRow = CreateRow("MetaRow", card.transform);
            var actionRow = CreateRow("ActionRow", card.transform);

            SetPrivateField(card, "_titleLabel", CreateLabel("Title", headerRow));
            SetPrivateField(card, "_statusLabel", CreateLabel("Status", headerRow));
            SetPrivateField(card, "_stageLabel", CreateLabel("Stage", detailRow));
            SetPrivateField(card, "_chancesLabel", CreateLabel("Chances", detailRow));
            SetPrivateField(card, "_deathsLabel", CreateLabel("Deaths", metaRow));
            SetPrivateField(card, "_lastPlayedLabel", CreateLabel("LastPlayed", metaRow));

            var primaryButton = CreateActionButton("PrimaryButton", actionRow, out var primaryButtonLabel);
            SetPrivateField(card, "_primaryButton", primaryButton);
            SetPrivateField(card, "_primaryButtonLabel", primaryButtonLabel);
            var deleteButton = CreateActionButton("DeleteButton", actionRow, out _);
            SetPrivateField(card, "_deleteButton", deleteButton);
            SetPrivateField(card, "_primarySelectionFrame", CreateSelectionFrame("PrimarySelectionFrame", primaryButton.transform));
            SetPrivateField(card, "_deleteSelectionFrame", CreateSelectionFrame("DeleteSelectionFrame", deleteButton.transform));
            SetPrivateField(card, "_selectionVisualProfile", UiSelectionVisualProfile.CreateRuntimeDefault());
        }

        private static Transform CreateRow(string name, Transform parent)
        {
            var rowObject = new GameObject(name, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);
            return rowObject.transform;
        }

        private static TMP_Text CreateLabel(string name, Transform parent)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            return labelObject.AddComponent<TextMeshProUGUI>();
        }

        private static TMP_Text CreateNestedButtonLabel(Transform button)
        {
            var labelWrapper = new GameObject("LabelWrapper", typeof(RectTransform)).transform;
            labelWrapper.SetParent(button, false);
            return CreateLabel("Label", labelWrapper);
        }

        private static Button CreateActionButton(string name, Transform parent, out TMP_Text label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var button = buttonObject.AddComponent<Button>();
            label = CreateLabel("Label", buttonObject.transform);
            return button;
        }

        private static Image CreateSelectionFrame(string name, Transform parent)
        {
            var frameObject = new GameObject(name, typeof(RectTransform));
            frameObject.transform.SetParent(parent, false);
            return frameObject.AddComponent<Image>();
        }

        private static void AssertCommandButtonHoverScaleEffect(Transform button)
        {
            Assert.That(button, Is.Not.Null);
            var buttonRect = button as RectTransform;
            Assert.That(buttonRect, Is.Not.Null, button.name);

            var effect = button.GetComponent<UiHoverScaleEffect>();
            Assert.That(effect, Is.Not.Null, button.name);

            var serialized = new SerializedObject(effect);
            Assert.That(serialized.FindProperty("_target").objectReferenceValue, Is.EqualTo(buttonRect), button.name);
            Assert.That(serialized.FindProperty("_hoverScale").floatValue, Is.EqualTo(1.10f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_pressedScale").floatValue, Is.EqualTo(1.04f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_clickPunchStrength").floatValue, Is.EqualTo(0.08f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_clickPunchDurationSeconds").floatValue, Is.EqualTo(0.18f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_clickPunchVibrato").intValue, Is.EqualTo(6), button.name);
            Assert.That(serialized.FindProperty("_clickPunchElasticity").floatValue, Is.EqualTo(0.65f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_durationSeconds").floatValue, Is.EqualTo(0.12f).Within(0.001f), button.name);
            Assert.That(serialized.FindProperty("_useUnscaledTime").boolValue, Is.True, button.name);
            Assert.That(serialized.FindProperty("_restoreOnDisable").boolValue, Is.True, button.name);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}");
            method.Invoke(target, null);
        }

        private sealed class ShellHarness : IDisposable
        {
            private readonly GameObject _root;

            public ShellHarness(
                GameObject root,
                MainMenuScreenView view,
                Button startButton,
                Button settingsButton,
                Button quitButton,
                RectTransform commandPanel,
                RectTransform saveSlotOverlayLayer,
                GameObject saveSlotBlockerRoot,
                CanvasGroup saveSlotBlockerCanvasGroup,
                Image saveSlotBlockerImage)
            {
                _root = root;
                View = view;
                StartButton = startButton;
                SettingsButton = settingsButton;
                QuitButton = quitButton;
                CommandPanel = commandPanel;
                SaveSlotOverlayLayer = saveSlotOverlayLayer;
                SaveSlotBlockerRoot = saveSlotBlockerRoot;
                SaveSlotBlockerCanvasGroup = saveSlotBlockerCanvasGroup;
                SaveSlotBlockerImage = saveSlotBlockerImage;
            }

            public MainMenuScreenView View { get; }

            public Button StartButton { get; }

            public Button SettingsButton { get; }

            public Button QuitButton { get; }

            public RectTransform CommandPanel { get; }

            public RectTransform SaveSlotOverlayLayer { get; }

            public GameObject SaveSlotBlockerRoot { get; }

            public CanvasGroup SaveSlotBlockerCanvasGroup { get; }

            public Image SaveSlotBlockerImage { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private sealed class FakeSettingsPort : IMainMenuSettingsPort
        {
            public int OpenCount { get; private set; }

            public void OpenSettings()
            {
                OpenCount++;
            }
        }

        private sealed class FakeApplicationQuitPort : IApplicationQuitPort
        {
            public int QuitCount { get; private set; }

            public void Quit()
            {
                QuitCount++;
            }
        }

        private sealed class FakeConfirmPopupPort : IConfirmPopupPort
        {
            private Action<bool> _completion;

            public List<ConfirmPopupPayload> Requests { get; } = new();

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                Requests.Add(payload);
                _completion = completion;
            }

            public void Complete(bool confirmed)
            {
                _completion?.Invoke(confirmed);
                _completion = null;
            }
        }
    }
}
