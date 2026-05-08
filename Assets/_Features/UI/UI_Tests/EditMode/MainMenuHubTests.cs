using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class MainMenuHubTests
    {
        private const string MainMenuScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";

        [Test]
        public void MainMenuScreenPrefab_HasTopBarContentHostBottomBar()
        {
            var prefab = LoadMainMenuPrefab();

            Assert.That(prefab.transform.Find("TopBar"), Is.Not.Null);
            Assert.That(prefab.transform.Find("ContentHost"), Is.Not.Null);
            Assert.That(prefab.transform.Find("BottomBar"), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel"), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/StartButton"), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/SettingsButton"), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/QuitButton"), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/StartButton").GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(prefab.transform.Find("MainCommandPanel/SettingsButton").GetSiblingIndex(), Is.EqualTo(1));
            Assert.That(prefab.transform.Find("MainCommandPanel/QuitButton").GetSiblingIndex(), Is.EqualTo(2));
            AssertCommandButtonHoverScaleEffect(prefab.transform, "MainCommandPanel/StartButton");
            AssertCommandButtonHoverScaleEffect(prefab.transform, "MainCommandPanel/SettingsButton");
            AssertCommandButtonHoverScaleEffect(prefab.transform, "MainCommandPanel/QuitButton");
        }

        [Test]
        public void MainMenuScreenPrefab_HasExactlyThreeSaveSlotCardsUnderSaveSlotPanel()
        {
            var prefab = LoadMainMenuPrefab();
            var panel = prefab.SaveSlotPanel;

            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.transform.parent.name, Is.EqualTo("ContentHost"));
            Assert.That(panel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
            foreach (var card in panel.SlotCards)
            {
                Assert.That(card, Is.Not.Null);
                Assert.That(card.transform.parent, Is.EqualTo(panel.transform));
            }
        }

        [Test]
        public void MainMenuScreenPrefab_RequiredAuthoredReferencesAreAssigned()
        {
            var prefab = LoadMainMenuPrefab();

            Assert.DoesNotThrow(prefab.ValidateAuthoredStructureOrThrow);
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
                Assert.That(panelLayout.spacing, Is.EqualTo(16f));
                Assert.That(panelLayout.padding.left, Is.Zero);
                Assert.That(panel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
                Assert.That(cards, Has.Length.EqualTo(3));

                for (var i = 0; i < cards.Length; i++)
                {
                    Assert.That(cards[i], Is.Not.Null);
                    Assert.That(cards[i].transform.parent, Is.EqualTo(panel.transform));
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
        public void SaveSlotCardView_PrimaryRestart_HidesDuplicateRestartButton()
        {
            var root = new GameObject(nameof(SaveSlotCardView_PrimaryRestart_HidesDuplicateRestartButton), typeof(RectTransform));
            root.SetActive(false);
            try
            {
                var card = root.AddComponent<SaveSlotCardView>();
                AuthorSaveSlotCard(card);
                root.SetActive(true);
                InvokePrivate(card, "OnEnable");

                card.Bind(CreateCardViewModel(1, SaveSlotIntentKind.Restart));

                Assert.That(GetPrivateField<Button>(card, "_primaryButton").gameObject.activeSelf, Is.True);
                Assert.That(GetPrivateField<Button>(card, "_restartButton").gameObject.activeSelf, Is.False);
                Assert.That(GetPrivateField<Button>(card, "_deleteButton").gameObject.activeSelf, Is.True);
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
            var hub = new MainMenuHubController(
                settingsPort,
                new FakeApplicationQuitPort(),
                new FakeConfirmPopupPort(),
                _ => { });

            hub.HandleCommand(new MainMenuCommandIntent(MainMenuCommandKind.OpenSettings));

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
                showRestart: true,
                showDelete: true);
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
            topBar.SetParent(root.transform, false);
            contentHost.SetParent(root.transform, false);
            bottomBar.SetParent(root.transform, false);
            commandPanel.SetParent(root.transform, false);

            var startButton = CreateButton("StartButton", commandPanel);
            var settingsButton = CreateButton("SettingsButton", commandPanel);
            var quitButton = CreateButton("QuitButton", commandPanel);
            SaveSlotPanelView panel = null;
            if (withPanel)
            {
                var panelObject = new GameObject("SaveSlotPanelView", typeof(RectTransform));
                panelObject.transform.SetParent(contentHost, false);
                panel = panelObject.AddComponent<SaveSlotPanelView>();
            }

            SetPrivateField(view, "_root", root);
            SetPrivateField(view, "_topBar", topBar);
            SetPrivateField(view, "_contentHost", contentHost);
            SetPrivateField(view, "_bottomBar", bottomBar);
            SetPrivateField(view, "_mainCommandPanel", commandPanel);
            SetPrivateField(view, "_saveSlotPanel", panel);
            SetPrivateField(view, "_startButton", startButton);
            SetPrivateField(view, "_settingsButton", settingsButton);
            SetPrivateField(view, "_quitButton", quitButton);
            root.SetActive(true);
            InvokePrivate(view, "OnEnable");
            return new ShellHarness(root, view, startButton, settingsButton, quitButton);
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
            SetPrivateField(card, "_restartButton", CreateActionButton("RestartButton", actionRow, out _));
            SetPrivateField(card, "_deleteButton", CreateActionButton("DeleteButton", actionRow, out _));
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

        private static Button CreateActionButton(string name, Transform parent, out TMP_Text label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            var button = buttonObject.AddComponent<Button>();
            label = CreateLabel("Label", buttonObject.transform);
            return button;
        }

        private static void AssertCommandButtonHoverScaleEffect(Transform root, string path)
        {
            var button = root.Find(path) as RectTransform;
            Assert.That(button, Is.Not.Null, path);

            var effect = button.GetComponent<UiHoverScaleEffect>();
            Assert.That(effect, Is.Not.Null, path);

            var serialized = new SerializedObject(effect);
            Assert.That(serialized.FindProperty("_target").objectReferenceValue, Is.EqualTo(button), path);
            Assert.That(serialized.FindProperty("_hoverScale").floatValue, Is.EqualTo(1.10f).Within(0.001f), path);
            Assert.That(serialized.FindProperty("_pressedScale").floatValue, Is.EqualTo(1.04f).Within(0.001f), path);
            Assert.That(serialized.FindProperty("_durationSeconds").floatValue, Is.EqualTo(0.12f).Within(0.001f), path);
            Assert.That(serialized.FindProperty("_useUnscaledTime").boolValue, Is.True, path);
            Assert.That(serialized.FindProperty("_restoreOnDisable").boolValue, Is.True, path);
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

            public ShellHarness(GameObject root, MainMenuScreenView view, Button startButton, Button settingsButton, Button quitButton)
            {
                _root = root;
                View = view;
                StartButton = startButton;
                SettingsButton = settingsButton;
                QuitButton = quitButton;
            }

            public MainMenuScreenView View { get; }

            public Button StartButton { get; }

            public Button SettingsButton { get; }

            public Button QuitButton { get; }

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
