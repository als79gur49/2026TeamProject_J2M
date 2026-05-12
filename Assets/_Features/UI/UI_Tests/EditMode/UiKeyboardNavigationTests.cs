using System;
using System.Reflection;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class UiKeyboardNavigationTests
    {
        private const string MainMenuScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";

        [Test]
        public void MainMenuScreenView_NavigateDownThenSubmit_ReusesExistingButtonClickPath()
        {
            using var harness = CreateMainMenuHarness();
            MainMenuCommandIntent? command = null;
            harness.View.CommandRequested += intent => command = intent;

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(command.HasValue, Is.True);
            Assert.That(command.Value.CommandKind, Is.EqualTo(MainMenuCommandKind.OpenSettings));
        }

        [Test]
        public void ConfirmPopupNavigation_CancelInput_UsesClickCancelCompletionPath()
        {
            using var harness = CreateConfirmPopupHarness(isDestructive: false);
            PopupCompletionKind? completion = null;
            harness.View.CompletionRequested += kind => completion = kind;

            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(completion.HasValue, Is.True);
            Assert.That(completion.Value, Is.EqualTo(PopupCompletionKind.Cancelled));
        }

        [Test]
        public void UiNavigationInputRouter_ConfirmPopupTopmost_BlocksMainMenuSelection()
        {
            using var mainMenu = CreateMainMenuHarness();
            using var popup = CreateConfirmPopupHarness(isDestructive: false);
            using var popupLayer = CreatePopupLayerHarness(popup.View);
            using var popupController = new PopupController(new FakePopupRuntimeFactory());
            Assert.That(popupController.Push(
                new PopupRequest(PopupId.Confirm, new ConfirmPopupPayload("Confirm", "Body", "Yes", "No", false)),
                out _), Is.True);

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_ConfirmPopupTopmost_BlocksMainMenuSelection));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    popupController,
                    popupLayer.View,
                    mainMenu.View,
                    () => false,
                    () => false);

                Assert.That(router.DispatchNavigate(UiNavigationCommand.Down), Is.False);

                Assert.That(mainMenu.View.SelectedCommandIndex, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_SubmitOnce_InvokesClickOnce()
        {
            using var harness = CreateMainMenuHarness();
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            var commandCount = 0;
            harness.View.CommandRequested += _ => commandCount++;

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_SubmitOnce_InvokesClickOnce));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(null, null, null, harness.View, () => false, () => false);

                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(commandCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiSelectableButtonGroup_SelectionFrame_OnlySelectedVisible()
        {
            var root = new GameObject(nameof(UiSelectableButtonGroup_SelectionFrame_OnlySelectedVisible));
            var profile = UiSelectionVisualProfile.CreateRuntimeDefault();
            try
            {
                var buttons = new[]
                {
                    CreateButton("One", root.transform),
                    CreateButton("Two", root.transform),
                    CreateButton("Three", root.transform),
                };
                var frames = new[]
                {
                    CreateFrame(buttons[0].transform),
                    CreateFrame(buttons[1].transform),
                    CreateFrame(buttons[2].transform),
                };
                var group = new UiSelectableButtonGroup();
                group.Configure(
                    new[]
                    {
                        new UiSelectableButtonSlot(buttons[0], frames[0]),
                        new UiSelectableButtonSlot(buttons[1], frames[1]),
                        new UiSelectableButtonSlot(buttons[2], frames[2]),
                    },
                    profile,
                    wrap: false,
                    skipNonInteractable: true,
                    selectedIndex: 1);

                Assert.That(frames[0].gameObject.activeSelf, Is.False);
                Assert.That(frames[1].gameObject.activeSelf, Is.True);
                Assert.That(frames[1].color.a, Is.EqualTo(1f).Within(0.001f));
                Assert.That(frames[2].gameObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MainMenuScreenPrefab_CommandButtons_HaveSelectionFrames()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var group = GetPrivateField<UiSelectableButtonGroup>(prefab, "_commandNavigationGroup");

            Assert.That(group, Is.Not.Null);
            Assert.That(group.SlotCount, Is.EqualTo(3));
            for (var i = 0; i < group.SlotCount; i++)
            {
                var slot = group.GetSlot(i);
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.Button, Is.Not.Null);
                Assert.That(slot.SelectionFrame, Is.Not.Null);
                Assert.That(slot.SelectionFrame.transform.IsChildOf(slot.Button.transform), Is.True);
            }
        }

        [Test]
        public void ConfirmPopupPrefab_ActionButtons_HaveSelectionFrames()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<ConfirmPopupView>(
                UiTestPrefabAssetUtility.ConfirmPopupPrefabPath);
            var group = GetPrivateField<UiSelectableButtonGroup>(prefab, "_actionNavigationGroup");

            Assert.That(group, Is.Not.Null);
            Assert.That(group.SlotCount, Is.EqualTo(2));
            for (var i = 0; i < group.SlotCount; i++)
            {
                var slot = group.GetSlot(i);
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.Button, Is.Not.Null);
                Assert.That(slot.SelectionFrame, Is.Not.Null);
                Assert.That(slot.SelectionFrame.transform.IsChildOf(slot.Button.transform), Is.True);
            }
        }

        private static MainMenuHarness CreateMainMenuHarness()
        {
            var root = new GameObject("MainMenuNavigationHarness", typeof(RectTransform));
            root.SetActive(false);
            var view = root.AddComponent<MainMenuScreenView>();
            var commandPanel = new GameObject("MainCommandPanel", typeof(RectTransform)).transform;
            commandPanel.SetParent(root.transform, false);
            var startButton = CreateButton("StartButton", commandPanel);
            var settingsButton = CreateButton("SettingsButton", commandPanel);
            var quitButton = CreateButton("QuitButton", commandPanel);

            SetPrivateField(view, "_root", root);
            SetPrivateField(view, "_mainCommandPanel", commandPanel);
            SetPrivateField(view, "_startButton", startButton);
            SetPrivateField(view, "_settingsButton", settingsButton);
            SetPrivateField(view, "_quitButton", quitButton);
            SetPrivateField(
                view,
                "_commandNavigationGroup",
                CreateNavigationGroup(startButton, settingsButton, quitButton));

            root.SetActive(true);
            return new MainMenuHarness(root, view);
        }

        private static ConfirmPopupHarness CreateConfirmPopupHarness(bool isDestructive)
        {
            var root = new GameObject("ConfirmPopupNavigationHarness", typeof(RectTransform));
            root.SetActive(false);
            var view = root.AddComponent<ConfirmPopupView>();
            var canvasGroup = root.AddComponent<CanvasGroup>();
            var confirmButton = CreateButton("ConfirmButton", root.transform);
            var cancelButton = CreateButton("CancelButton", root.transform);

            SetPrivateField(view, "_root", root);
            SetPrivateField(view, "_canvasGroup", canvasGroup);
            SetPrivateField(view, "_confirmButton", confirmButton);
            SetPrivateField(view, "_cancelButton", cancelButton);
            SetPrivateField(
                view,
                "_actionNavigationGroup",
                CreateNavigationGroup(confirmButton, cancelButton));

            root.SetActive(true);
            view.Bind(CreateConfirmPopupViewModel(isDestructive));
            view.IsVisible = true;
            view.SetIsTopmost(true);
            return new ConfirmPopupHarness(root, view);
        }

        private static PopupLayerHarness CreatePopupLayerHarness(ConfirmPopupView confirmPopupView)
        {
            var root = new GameObject("PopupLayerHarness", typeof(RectTransform));
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(root.transform, false);
            var backdrop = new GameObject("Backdrop", typeof(CanvasGroup));
            backdrop.transform.SetParent(root.transform, false);
            var backdropImage = new GameObject("BackdropImage", typeof(Image));
            backdropImage.transform.SetParent(root.transform, false);
            var backdropButton = new GameObject("BackdropButton", typeof(Button));
            backdropButton.transform.SetParent(root.transform, false);
            confirmPopupView.transform.SetParent(contentObject.transform, false);
            var view = root.AddComponent<PopupLayerView>();
            view.Configure(
                root,
                backdrop.GetComponent<CanvasGroup>(),
                backdropImage.GetComponent<Image>(),
                backdropButton.GetComponent<Button>(),
                (RectTransform)contentObject.transform);
            return new PopupLayerHarness(root, view);
        }

        private static ConfirmPopupViewModel CreateConfirmPopupViewModel(bool isDestructive)
        {
            var viewModel = new ConfirmPopupViewModel();
            viewModel.SetContent("Confirm", "Body", "Yes", "No", isDestructive);
            return viewModel;
        }

        private static UiSelectableButtonGroup CreateNavigationGroup(params Button[] buttons)
        {
            var profile = UiSelectionVisualProfile.CreateRuntimeDefault();
            var slots = new UiSelectableButtonSlot[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
            {
                slots[i] = new UiSelectableButtonSlot(buttons[i], CreateFrame(buttons[i].transform));
            }

            var group = new UiSelectableButtonGroup();
            group.Configure(slots, profile, wrap: false, skipNonInteractable: true);
            return group;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            return buttonObject.AddComponent<Button>();
        }

        private static Image CreateFrame(Transform parent)
        {
            var frameObject = new GameObject("SelectionFrame", typeof(RectTransform));
            frameObject.transform.SetParent(parent, false);
            return frameObject.AddComponent<Image>();
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

        private sealed class MainMenuHarness : IDisposable
        {
            private readonly GameObject _root;

            public MainMenuHarness(GameObject root, MainMenuScreenView view)
            {
                _root = root;
                View = view;
            }

            public MainMenuScreenView View { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private sealed class ConfirmPopupHarness : IDisposable
        {
            private readonly GameObject _root;

            public ConfirmPopupHarness(GameObject root, ConfirmPopupView view)
            {
                _root = root;
                View = view;
            }

            public ConfirmPopupView View { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private sealed class PopupLayerHarness : IDisposable
        {
            private readonly GameObject _root;

            public PopupLayerHarness(GameObject root, PopupLayerView view)
            {
                _root = root;
                View = view;
            }

            public PopupLayerView View { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
