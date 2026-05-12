using System;
using System.Collections.Generic;
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
        public void MainMenuScreenView_OnEnable_DoesNotShowCommandFrameBeforeKeyboardInput()
        {
            using var harness = CreateMainMenuHarness();

            AssertAllFramesHidden(harness.View, "_commandNavigationGroup", isHidden: true);
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
        public void ConfirmPopupNavigation_DestructivePayload_DefaultsToConfirm_ByProductPolicy()
        {
            using var harness = CreateConfirmPopupHarness(isDestructive: true);
            PopupCompletionKind? completion = null;
            harness.View.CompletionRequested += kind => completion = kind;

            Assert.That(harness.View.SelectedActionIndex, Is.EqualTo(0));
            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(completion, Is.EqualTo(PopupCompletionKind.Confirmed));
        }

        [Test]
        public void ConfirmPopup_FirstSubmit_RevealsOnly_SecondSubmitConfirms()
        {
            using var popup = CreateConfirmPopupHarness(isDestructive: true);
            PopupCompletionKind? completion = null;
            popup.View.CompletionRequested += kind => completion = kind;

            var routerObject = new GameObject(nameof(ConfirmPopup_FirstSubmit_RevealsOnly_SecondSubmitConfirms));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(popup.View),
                    () => false,
                    () => false);

                AssertAllFramesHidden(popup.View, "_actionNavigationGroup", isHidden: true);
                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(completion.HasValue, Is.False);
                AssertAllFramesHidden(popup.View, "_actionNavigationGroup", isHidden: false);

                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(completion, Is.EqualTo(PopupCompletionKind.Confirmed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void ConfirmPopupNavigation_HorizontalInputMatchesVisualOrder()
        {
            using var harness = CreateConfirmPopupHarness(isDestructive: true);
            PopupCompletionKind? completion = null;
            harness.View.CompletionRequested += kind => completion = kind;

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);
            Assert.That(harness.View.SelectedActionIndex, Is.EqualTo(1));
            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(completion, Is.EqualTo(PopupCompletionKind.Cancelled));

            completion = null;
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(harness.View.SelectedActionIndex, Is.EqualTo(0));
            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(completion, Is.EqualTo(PopupCompletionKind.Confirmed));
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
        public void UiNavigationInputRouter_FirstSubmit_RevealsFocus_DoesNotInvokeSubmit()
        {
            using var harness = CreateMainMenuHarness();
            var navigationCount = 0;
            harness.View.NavigationRequested += _ => navigationCount++;

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_FirstSubmit_RevealsFocus_DoesNotInvokeSubmit));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(null, null, null, harness.View, () => false, () => false);

                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(navigationCount, Is.EqualTo(0));
                AssertAllFramesHidden(harness.View, "_commandNavigationGroup", isHidden: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_SecondSubmit_AfterReveal_InvokesSubmitOnce()
        {
            using var harness = CreateMainMenuHarness();
            var navigationCount = 0;
            harness.View.NavigationRequested += _ => navigationCount++;

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_SecondSubmit_AfterReveal_InvokesSubmitOnce));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(null, null, null, harness.View, () => false, () => false);

                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(navigationCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_FirstNavigate_RevealsFocus_AndMovesSelection()
        {
            using var harness = CreateMainMenuHarness();
            Assert.That(harness.View.SelectedCommandIndex, Is.EqualTo(0));
            AssertAllFramesHidden(harness.View, "_commandNavigationGroup", isHidden: true);

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_FirstNavigate_RevealsFocus_AndMovesSelection));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(null, null, null, harness.View, () => false, () => false);

                Assert.That(router.DispatchNavigate(UiNavigationCommand.Down), Is.True);

                Assert.That(harness.View.SelectedCommandIndex, Is.EqualTo(1));
                AssertAllFramesHidden(harness.View, "_commandNavigationGroup", isHidden: false);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_FirstCancel_DoesNotReveal_UsesExistingBackPath()
        {
            using var harness = CreateMainMenuHarness();
            var backCount = 0;

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_FirstCancel_DoesNotReveal_UsesExistingBackPath));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(null, null, null, harness.View, () =>
                {
                    backCount++;
                    return true;
                }, () => false);

                Assert.That(router.DispatchCancel(), Is.True);

                Assert.That(backCount, Is.EqualTo(1));
                AssertAllFramesHidden(harness.View, "_commandNavigationGroup", isHidden: true);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void PausePopup_FirstSubmit_RevealsOnly_SecondSubmitResumes()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(
                UiTestPrefabAssetUtility.PausePopupPrefabPath);
            var view = UnityEngine.Object.Instantiate(prefab);
            var routerObject = new GameObject(nameof(PausePopup_FirstSubmit_RevealsOnly_SecondSubmitResumes));
            try
            {
                var viewModel = new PausePopupViewModel();
                viewModel.SetContent("Paused", "Body", "Resume", string.Empty, "Settings", "Retry", "Main Menu");
                view.Bind(viewModel);
                view.IsVisible = true;
                view.SetIsTopmost(true);

                PopupCompletionKind? completion = null;
                view.CompletionRequested += kind => completion = kind;

                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(view),
                    () => false,
                    () => false);

                AssertAllFramesHidden(view, "_navigationGroup", isHidden: true);
                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(completion.HasValue, Is.False);
                AssertAllFramesHidden(view, "_navigationGroup", isHidden: false);

                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(completion, Is.EqualTo(PopupCompletionKind.Resumed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_GameplayCurrentScreenTarget_ReceivesNavigation()
        {
            var target = new TrackingNavigationTarget();
            using var screenController = new ScreenController(new NavigationScreenRuntimeFactory(target));
            screenController.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default));

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_GameplayCurrentScreenTarget_ReceivesNavigation));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new UiLayeredNavigationTargetResolver(null, screenController),
                    () => false,
                    () => false);

                Assert.That(router.DispatchNavigate(UiNavigationCommand.Down), Is.True);

                Assert.That(target.NavigateCount, Is.EqualTo(1));
                Assert.That(target.LastNavigateCommand, Is.EqualTo(UiNavigationCommand.Down));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_BlockingPopup_DoesNotFallThroughToScreenOrHud()
        {
            var target = new TrackingNavigationTarget();
            using var screenController = new ScreenController(new NavigationScreenRuntimeFactory(target));
            using var popupController = new PopupController(new PolicyOnlyPopupRuntimeFactory(
                new PopupPolicy(
                    PopupPolicyClass.ModalBlocking,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Consume,
                    PopupBackdropMode.Consume,
                    showsDim: true,
                    blocksLowerLayers: true)));
            screenController.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default));
            popupController.Push(new PopupRequest(PopupId.Pause, PausePopupPayload.Default), out _);

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_BlockingPopup_DoesNotFallThroughToScreenOrHud));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new UiLayeredNavigationTargetResolver(popupController, screenController),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.False);

                Assert.That(target.SubmitCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void UiNavigationInputRouter_NonBlockingTooltip_FallthroughPolicyIsExplicit()
        {
            var target = new TrackingNavigationTarget();
            using var screenController = new ScreenController(new NavigationScreenRuntimeFactory(target));
            using var popupController = new PopupController(new PolicyOnlyPopupRuntimeFactory(
                new PopupPolicy(
                    PopupPolicyClass.AnchoredEphemeral,
                    PopupLifetimeScope.CurrentScreen,
                    PopupBackAction.Close,
                    PopupBackdropMode.None,
                    showsDim: false,
                    blocksLowerLayers: false)));
            screenController.SetRoot(new ScreenRequest(ScreenId.Gameplay, GameplayRootPayload.Default));
            popupController.Push(new PopupRequest(
                    PopupId.Tooltip,
                    new TooltipPopupPayload("Tip", "Body", TooltipPopupAnchorPreset.Center)),
                out _);

            var routerObject = new GameObject(nameof(UiNavigationInputRouter_NonBlockingTooltip_FallthroughPolicyIsExplicit));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new UiLayeredNavigationTargetResolver(popupController, screenController),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(target.SubmitCount, Is.EqualTo(0));
                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(target.SubmitCount, Is.EqualTo(1));
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

        [Test]
        public void GameplayNavigationPrefabs_SimpleTargets_HaveSelectionFrames()
        {
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadScreenPrefab<StageResultScreenView>(UiTestPrefabAssetUtility.StageResultScreenPrefabPath),
                "_navigationGroup",
                1);
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadScreenPrefab<ObjectiveStatusScreenView>(UiTestPrefabAssetUtility.ObjectiveStatusScreenPrefabPath),
                "_navigationGroup",
                2);
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(UiTestPrefabAssetUtility.PausePopupPrefabPath),
                "_navigationGroup",
                4);
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadPopupPrefab<ObjectiveInfoPopupView>(UiTestPrefabAssetUtility.ObjectiveInfoPopupPrefabPath),
                "_navigationGroup",
                1);
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadPopupPrefab<RewardPopupView>(UiTestPrefabAssetUtility.RewardPopupPrefabPath),
                "_navigationGroup",
                1);
            AssertButtonGroupFrames(
                UiTestPrefabAssetUtility.LoadHudPrefab(),
                "_navigationGroup",
                1);
        }

        [Test]
        public void PausePopupPrefab_NavigationOrder_MatchesVisibleLayoutOrder()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(
                UiTestPrefabAssetUtility.PausePopupPrefabPath);
            var group = GetPrivateField<UiSelectableButtonGroup>(prefab, "_navigationGroup");
            var expectedNames = new[]
            {
                "ResumeButton",
                "SettingsButton",
                "RetryButton",
                "MainMenuButton",
            };

            Assert.That(prefab.transform.Find("ObjectiveButton"), Is.Null);
            Assert.That(group.SlotCount, Is.EqualTo(expectedNames.Length));

            var previousSiblingIndex = -1;
            for (var i = 0; i < expectedNames.Length; i++)
            {
                var button = group.GetSlot(i).Button;
                Assert.That(button, Is.Not.Null);
                Assert.That(button.name, Is.EqualTo(expectedNames[i]));
                Assert.That(button.gameObject.activeSelf, Is.True);
                Assert.That(button.transform.GetSiblingIndex(), Is.GreaterThan(previousSiblingIndex));
                previousSiblingIndex = button.transform.GetSiblingIndex();
            }
        }

        [Test]
        public void PausePopupView_SettingsSubmit_ReusesClickPath()
        {
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(
                UiTestPrefabAssetUtility.PausePopupPrefabPath);
            var view = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var viewModel = new PausePopupViewModel();
                viewModel.SetContent("Paused", "Body", "Resume", string.Empty, "Settings", "Retry", "Main Menu");
                view.Bind(viewModel);
                view.IsVisible = true;
                view.OnNavigationFocusGained();

                PopupCompletionKind? completion = null;
                view.CompletionRequested += kind => completion = kind;

                Assert.That(view.HandleNavigate(UiNavigationCommand.Down), Is.True);
                Assert.That(view.HandleSubmit(), Is.True);
                Assert.That(completion, Is.EqualTo(PopupCompletionKind.SettingsRequested));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void SettingsScreenPrefab_AllFocusableNodesHaveSelectionFrames()
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var slots = GetPrivateField<UiFocusNodeSlot[]>(prefab, "_focusNodeSlots");

            Assert.That(slots, Is.Not.Null);

            var expectedIds = new[]
            {
                "Header.AudioTab",
                "Header.DisplayTab",
                "Header.InputTab",
                "Audio.Main.Slider",
                "Audio.Main.Mute",
                "Audio.Bgm.Slider",
                "Audio.Bgm.Mute",
                "Audio.Sfx.Slider",
                "Audio.Sfx.Mute",
                "Display.Resolution.Dropdown",
                "Display.Fullscreen.Toggle",
                "Display.Apply.Button",
                "Display.Revert.Button",
                "Input.Movement.Toggle",
                "Input.Push.Change",
                "Input.Flip.Change",
                "Input.Reset",
            };

            for (var i = 0; i < expectedIds.Length; i++)
            {
                var slot = FindSettingsFocusSlot(slots, expectedIds[i]);
                Assert.That(slot, Is.Not.Null, expectedIds[i]);
                Assert.That(slot.SelectionFrame, Is.Not.Null, expectedIds[i]);
                Assert.That(slot.VisualProfile, Is.Not.Null, expectedIds[i]);
            }
        }

        [Test]
        public void SettingsScreen_OnEnable_DoesNotShowFrameBeforeKeyboardInput()
        {
            using var harness = CreateSettingsHarness();

            AssertSettingsFramesHidden(harness.View, isHidden: true);
        }

        [Test]
        public void SettingsFirstSubmit_ShowsFocusOnly_DoesNotEnterSliderEditMode()
        {
            using var harness = CreateSettingsHarness();
            var routerObject = new GameObject(nameof(SettingsFirstSubmit_ShowsFocusOnly_DoesNotEnterSliderEditMode));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(harness.View),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.True);
                AssertSettingsFramesHidden(harness.View, isHidden: false);
                Assert.That(IsSettingsFocusEditing(harness.View), Is.False);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void SettingsSecondSubmit_OnSlider_EntersEditMode()
        {
            using var harness = CreateSettingsHarness();
            var routerObject = new GameObject(nameof(SettingsSecondSubmit_OnSlider_EntersEditMode));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(harness.View),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(IsSettingsFocusEditing(harness.View), Is.True);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void SettingsFirstSubmit_OnDropdown_ShowsFocusOnly_DoesNotOpenList()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var routerObject = new GameObject(nameof(SettingsFirstSubmit_OnDropdown_ShowsFocusOnly_DoesNotOpenList));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(harness.View),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.True);

                AssertSettingsFramesHidden(harness.View, isHidden: false);
                Assert.That(IsSettingsDropdownListMode(harness.View), Is.False);
                Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.False);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Resolution.Dropdown"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void SettingsSecondSubmit_OnDropdown_OpensDropdownListMode()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var routerObject = new GameObject(nameof(SettingsSecondSubmit_OnDropdown_OpensDropdownListMode));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(harness.View),
                    () => false,
                    () => false);

                Assert.That(router.DispatchSubmit(), Is.True);
                Assert.That(router.DispatchSubmit(), Is.True);

                Assert.That(IsSettingsDropdownListMode(harness.View), Is.True);
                Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.True);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Resolution.Dropdown"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void SettingsFirstNavigate_ShowsFocusAndMoves()
        {
            using var harness = CreateSettingsHarness();
            var routerObject = new GameObject(nameof(SettingsFirstNavigate_ShowsFocusAndMoves));
            try
            {
                var router = routerObject.AddComponent<UiNavigationInputRouter>();
                router.Initialize(
                    null,
                    new FixedNavigationTargetResolver(harness.View),
                    () => false,
                    () => false);

                Assert.That(router.DispatchNavigate(UiNavigationCommand.Down), Is.True);

                AssertSettingsFramesHidden(harness.View, isHidden: false);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Bgm.Slider"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routerObject);
            }
        }

        [Test]
        public void SettingsHeaderTabs_Right_WrapsAudioDisplayInputAudio()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.DisplayTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Display));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.InputTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Input));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.AudioTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Audio));
        }

        [Test]
        public void SettingsHeaderTabs_Left_WrapsAudioInputDisplayAudio()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.InputTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Input));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.DisplayTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Display));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.AudioTab"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Audio));
        }

        [Test]
        public void SettingsHeaderTabs_RightFromInput_DoesNotFocusBack()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Input);
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.AudioTab"));
        }

        [Test]
        public void SettingsHeaderTabs_LeftFromAudio_DoesNotFocusBack()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.InputTab"));
        }

        [Test]
        public void SettingsBackButton_NotPartOfHeaderTabCycle()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            for (var i = 0; i < 6; i++)
            {
                Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
                Assert.That(GetSettingsFocusNodeId(harness.View), Is.Not.EqualTo("Header.Back"));
            }
        }

        [Test]
        public void SettingsHeaderTabs_Move_ActivatesVisibleSection()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Display));
            Assert.That(harness.View.DisplayView.gameObject.activeSelf, Is.True);
            Assert.That(harness.View.AudioView.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SettingsHeaderTabs_Submit_ReusesExistingTabClickPath()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            var selectedCount = 0;
            SettingsSectionId? selectedSection = null;
            harness.View.SectionSelected += section =>
            {
                selectedCount++;
                selectedSection = section;
            };

            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(selectedCount, Is.EqualTo(1));
            Assert.That(selectedSection, Is.EqualTo(SettingsSectionId.Display));
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.DisplayTab"));
        }

        [Test]
        public void SettingsHeaderTabs_DownFromAudio_EntersAudioMainSlider()
        {
            using var harness = CreateSettingsHarness();
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
        }

        [Test]
        public void SettingsHeaderTabs_DownFromDisplay_EntersDisplayResolutionDropdown()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Resolution.Dropdown"));
        }

        [Test]
        public void SettingsHeaderTabs_DownFromInput_EntersInputMovementToggle()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Input);
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Input.Movement.Toggle"));
        }

        [Test]
        public void SettingsHeaderTabs_DownFromDisplay_FallsBackWhenResolutionUnavailable()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, resolutionOptions: Array.Empty<string>());
            MoveToCurrentHeaderTab(harness.View);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Fullscreen.Toggle"));
        }

        [Test]
        public void SettingsContentTop_UpFromAudio_ReturnsToAudioHeaderTab()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.AudioTab"));
        }

        [Test]
        public void SettingsContentTop_UpFromDisplay_ReturnsToDisplayHeaderTab()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.DisplayTab"));
        }

        [Test]
        public void SettingsContentTop_UpFromInput_ReturnsToInputHeaderTab()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Input);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Header.InputTab"));
        }

        [Test]
        public void SettingsContent_LeftRight_DoesNotCrossSections()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Mute"));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.False);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Mute"));
            Assert.That(harness.ScreenViewModel.SelectedSection, Is.EqualTo(SettingsSectionId.Audio));
        }

        [Test]
        public void SettingsAudioSlider_NavigationRight_MovesToMute_DoesNotChangeValue()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Mute"));
            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.EqualTo(initialValue));
        }

        [Test]
        public void SettingsAudioSlider_NavigationDown_PreservesSliderColumn()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Bgm.Slider"));

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Sfx.Slider"));
        }

        [Test]
        public void SettingsAudioSlider_Submit_EntersEditMode()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
        }

        [Test]
        public void SettingsAudioSlider_EditRight_ChangesValue_DoesNotMoveFocus()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.GreaterThan(initialValue));
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
        }

        [Test]
        public void SettingsAudioSlider_EditLeft_ChangesValue_DoesNotMoveFocus()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);

            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.LessThan(initialValue));
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
        }

        [Test]
        public void SettingsAudioSlider_EditUp_IsConsumedNoOp()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.EqualTo(initialValue));
        }

        [Test]
        public void SettingsAudioSlider_EditDown_IsConsumedNoOp()
        {
            using var harness = CreateSettingsHarness();
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Slider"));
            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.EqualTo(initialValue));
        }

        [Test]
        public void SettingsAudioSlider_EditSubmit_CommitsAndExitsEditMode()
        {
            using var harness = CreateSettingsHarness();
            var commitCount = 0;
            harness.View.AudioView.InteractionCompleted += () => commitCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.False);
            Assert.That(commitCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingsAudioSlider_EditCancel_KeepsValueCommitsAndExitsEditMode()
        {
            using var harness = CreateSettingsHarness();
            var backCount = 0;
            var commitCount = 0;
            harness.View.BackRequested += () => backCount++;
            harness.View.AudioView.InteractionCompleted += () => commitCount++;
            harness.View.OnNavigationFocusGained();
            var initialValue = harness.View.AudioView.GetVolume(AudioSettingsChannel.Main);

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.False);
            Assert.That(harness.View.AudioView.GetVolume(AudioSettingsChannel.Main), Is.GreaterThan(initialValue));
            Assert.That(commitCount, Is.EqualTo(1));
            Assert.That(backCount, Is.EqualTo(0));
        }

        [Test]
        public void SettingsAudioMute_Submit_ReusesExistingMutePath()
        {
            using var harness = CreateSettingsHarness();
            AudioSettingsChannel? mutedChannel = null;
            bool? mutedValue = null;
            harness.View.AudioView.MuteChanged += (channel, isMuted) =>
            {
                mutedChannel = channel;
                mutedValue = isMuted;
            };
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Audio.Main.Mute"));
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(mutedChannel, Is.EqualTo(AudioSettingsChannel.Main));
            Assert.That(mutedValue, Is.True);
        }

        [Test]
        public void SettingsCancel_NormalMode_ReusesClickBackPath()
        {
            using var harness = CreateSettingsHarness();
            var backCount = 0;
            harness.View.BackRequested += () => backCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(backCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingsCancel_DoesNotRequireBackButtonFocus()
        {
            using var harness = CreateSettingsHarness();
            var backCount = 0;
            harness.View.BackRequested += () => backCount++;

            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(backCount, Is.EqualTo(1));
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.Not.EqualTo("Header.Back"));
        }

        [Test]
        public void SettingsCancel_SliderEditMode_ExitsEditMode_DoesNotBack()
        {
            using var harness = CreateSettingsHarness();
            var backCount = 0;
            harness.View.BackRequested += () => backCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.False);
            Assert.That(backCount, Is.EqualTo(0));
        }

        [Test]
        public void SettingsCancel_DropdownListMode_ClosesList_DoesNotBack()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var backCount = 0;
            harness.View.BackRequested += () => backCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(IsSettingsDropdownListMode(harness.View), Is.False);
            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.False);
            Assert.That(backCount, Is.EqualTo(0));
        }

        [Test]
        public void SettingsDisplayResolution_Submit_OpensDropdownListMode()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(IsSettingsFocusEditing(harness.View), Is.False);
            Assert.That(IsSettingsDropdownListMode(harness.View), Is.True);
            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Resolution.Dropdown"));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_OpensListBelowDropdown()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.True);
            Assert.That(
                harness.View.DisplayView.transform.Find("ResolutionRow/ResolutionDropdown/Dropdown List"),
                Is.Not.Null);
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_InitialHighlightMatchesCurrentValue()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();
            var initialIndex = harness.View.SelectedDisplayResolutionIndex;

            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(initialIndex));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Down_MovesHighlightToNextOption()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();
            var initialIndex = harness.View.SelectedDisplayResolutionIndex;

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(initialIndex + 1));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(initialIndex));
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Resolution.Dropdown"));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Up_MovesHighlightToPreviousOption()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, selectedResolutionIndex: 1);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(0));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_DownAtLast_Clamps()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, selectedResolutionIndex: 2);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(2));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(2));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_UpAtFirst_Clamps()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Up), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(0));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(0));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Left_IsConsumedNoOp()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, selectedResolutionIndex: 1);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(1));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Right_IsConsumedNoOp()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, selectedResolutionIndex: 1);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(1));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_LeftRight_AreConsumedNoOp()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display, selectedResolutionIndex: 1);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Left), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);

            Assert.That(harness.View.DisplayView.ResolutionKeyboardHighlightedIndex, Is.EqualTo(1));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Submit_SelectsHighlightedOptionAndStagesResolution()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var changedIndex = -1;
            harness.View.DisplayView.ResolutionChanged += index => changedIndex = index;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(changedIndex, Is.EqualTo(1));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.False);
        }

        [Test]
        public void SettingsDisplayResolution_ListMode_Cancel_ClosesList_DoesNotStageNewHighlight()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var changedCount = 0;
            harness.View.DisplayView.ResolutionChanged += _ => changedCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleCancel(), Is.True);

            Assert.That(changedCount, Is.EqualTo(0));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(0));
            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.False);
        }

        [Test]
        public void SettingsDisplayResolution_MouseValueChanged_UsesSameStageResolutionPath()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var changedIndex = -1;
            harness.View.DisplayView.ResolutionChanged += index => changedIndex = index;

            harness.View.DisplayView.SelectResolution(2);

            Assert.That(changedIndex, Is.EqualTo(2));
            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(2));
        }

        [Test]
        public void DropdownKeyboardList_DoesNotRequireEventSystemSelectedObject()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(harness.View.SelectedDisplayResolutionIndex, Is.EqualTo(1));
        }

        [Test]
        public void SettingsDropdownSubmitOnce_DoesNotOpenDuplicateLists()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(harness.View.DisplayView.IsResolutionKeyboardListOpen, Is.False);
        }

        [Test]
        public void SettingsDropdownSubmitOnce_DoesNotDoubleStageResolution()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var changedCount = 0;
            harness.View.DisplayView.ResolutionChanged += _ => changedCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleSubmit(), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingsScreen_SubmitOnce_DoesNotDoubleInvokeToggle()
        {
            using var harness = CreateSettingsHarness();
            var muteCount = 0;
            harness.View.AudioView.MuteChanged += (_, _) => muteCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Right), Is.True);
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(muteCount, Is.EqualTo(1));
        }

        [Test]
        public void SettingsScreen_ApplySubmitOnce_DoesNotDoubleOpenConfirm()
        {
            using var harness = CreateSettingsHarness(SettingsSectionId.Display);
            var applyCount = 0;
            harness.View.DisplayView.ApplyRequested += () => applyCount++;
            harness.View.OnNavigationFocusGained();

            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(harness.View.HandleNavigate(UiNavigationCommand.Down), Is.True);
            Assert.That(GetSettingsFocusNodeId(harness.View), Is.EqualTo("Display.Apply.Button"));
            Assert.That(harness.View.HandleSubmit(), Is.True);

            Assert.That(applyCount, Is.EqualTo(1));
        }

        private static SettingsHarness CreateSettingsHarness(
            SettingsSectionId selectedSection = SettingsSectionId.Audio,
            IReadOnlyList<string> resolutionOptions = null,
            int selectedResolutionIndex = 0,
            bool inputControlsInteractable = true)
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var view = UnityEngine.Object.Instantiate(prefab);
            var screenViewModel = new SettingsScreenViewModel();
            var audioViewModel = new SettingsAudioViewModel();
            var displayViewModel = new SettingsDisplayViewModel();
            var inputViewModel = new SettingsInputViewModel();

            ConfigureSettingsScreenViewModel(screenViewModel, selectedSection);
            audioViewModel.SetContent(
                new AudioSettingsRowViewModel("50%", 0.5f, false),
                new AudioSettingsRowViewModel("40%", 0.4f, false),
                new AudioSettingsRowViewModel("30%", 0.3f, false));
            displayViewModel.SetContent(
                "Current",
                resolutionOptions ?? new[] { "800 x 600", "1280 x 720", "1920 x 1080" },
                selectedResolutionIndex,
                false,
                string.Empty,
                true,
                true,
                false,
                string.Empty,
                0f,
                false,
                isDisplayStatusVisible: false);
            inputViewModel.SetContent(
                "Movement",
                "Use arrow keys",
                false,
                "WASD",
                "Push",
                "Space",
                "Change",
                "Flip",
                "F",
                "Change",
                "Reset",
                string.Empty,
                false,
                null,
                inputControlsInteractable);

            view.Bind(screenViewModel);
            view.AudioView.Bind(audioViewModel);
            view.DisplayView.Bind(displayViewModel);
            view.InputView.Bind(inputViewModel);
            view.SectionSelected += sectionId => ConfigureSettingsScreenViewModel(screenViewModel, sectionId);
            view.SetIsCurrent(true);

            return new SettingsHarness(view, screenViewModel, displayViewModel, inputViewModel);
        }

        private static void ConfigureSettingsScreenViewModel(
            SettingsScreenViewModel viewModel,
            SettingsSectionId selectedSection)
        {
            viewModel.SetContent("Settings", "Back", "Audio", "Display", "Input", selectedSection);
        }

        private static string GetSettingsFocusNodeId(SettingsScreenView view)
        {
            return GetPrivateField<UiFocusGraphNavigator>(view, "_focusGraph").CurrentNodeId.Value;
        }

        private static bool IsSettingsFocusEditing(SettingsScreenView view)
        {
            return GetPrivateField<UiFocusGraphNavigator>(view, "_focusGraph").IsEditing;
        }

        private static bool IsSettingsDropdownListMode(SettingsScreenView view)
        {
            return GetPrivateField<UiFocusGraphNavigator>(view, "_focusGraph").IsDropdownListMode;
        }

        private static void MoveToCurrentHeaderTab(SettingsScreenView view)
        {
            view.OnNavigationFocusGained();
            Assert.That(view.HandleNavigate(UiNavigationCommand.Up), Is.True);
        }

        private static UiFocusNodeSlot FindSettingsFocusSlot(IReadOnlyList<UiFocusNodeSlot> slots, string id)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && string.Equals(slots[i].Id, id, StringComparison.Ordinal))
                {
                    return slots[i];
                }
            }

            return null;
        }

        private static void AssertSettingsFramesHidden(SettingsScreenView view, bool isHidden)
        {
            var slots = GetPrivateField<UiFocusNodeSlot[]>(view, "_focusNodeSlots");
            Assert.That(slots, Is.Not.Null);

            var activeFrameCount = 0;
            for (var i = 0; i < slots.Length; i++)
            {
                Assert.That(slots[i], Is.Not.Null);
                Assert.That(slots[i].SelectionFrame, Is.Not.Null, slots[i].Id);
                if (slots[i].SelectionFrame.gameObject.activeSelf)
                {
                    activeFrameCount++;
                }
            }

            if (isHidden)
            {
                Assert.That(activeFrameCount, Is.EqualTo(0));
            }
            else
            {
                Assert.That(activeFrameCount, Is.GreaterThan(0));
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
            view.OnNavigationFocusLost();
            return new MainMenuHarness(root, view);
        }

        private static ConfirmPopupHarness CreateConfirmPopupHarness(bool isDestructive)
        {
            var root = new GameObject("ConfirmPopupNavigationHarness", typeof(RectTransform));
            root.SetActive(false);
            var view = root.AddComponent<ConfirmPopupView>();
            var canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
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

        private static void AssertButtonGroupFrames(object prefab, string fieldName, int expectedSlotCount)
        {
            var group = GetPrivateField<UiSelectableButtonGroup>(prefab, fieldName);
            Assert.That(group, Is.Not.Null);
            Assert.That(group.SlotCount, Is.EqualTo(expectedSlotCount));
            for (var i = 0; i < group.SlotCount; i++)
            {
                var slot = group.GetSlot(i);
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.Button, Is.Not.Null);
                Assert.That(slot.SelectionFrame, Is.Not.Null);
                Assert.That(slot.SelectionFrame.transform.IsChildOf(slot.Button.transform), Is.True);
            }
        }

        private static void AssertAllFramesHidden(object target, string fieldName, bool isHidden)
        {
            var group = GetPrivateField<UiSelectableButtonGroup>(target, fieldName);
            Assert.That(group, Is.Not.Null);

            var activeFrameCount = 0;
            for (var i = 0; i < group.SlotCount; i++)
            {
                var frame = group.GetSlot(i)?.SelectionFrame;
                Assert.That(frame, Is.Not.Null);
                if (frame.gameObject.activeSelf)
                {
                    activeFrameCount++;
                }
            }

            if (isHidden)
            {
                Assert.That(activeFrameCount, Is.EqualTo(0));
            }
            else
            {
                Assert.That(activeFrameCount, Is.GreaterThan(0));
            }
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

        private sealed class SettingsHarness : IDisposable
        {
            public SettingsHarness(
                SettingsScreenView view,
                SettingsScreenViewModel screenViewModel,
                SettingsDisplayViewModel displayViewModel,
                SettingsInputViewModel inputViewModel)
            {
                View = view;
                ScreenViewModel = screenViewModel;
                DisplayViewModel = displayViewModel;
                InputViewModel = inputViewModel;
            }

            public SettingsScreenView View { get; }

            public SettingsScreenViewModel ScreenViewModel { get; }

            public SettingsDisplayViewModel DisplayViewModel { get; }

            public SettingsInputViewModel InputViewModel { get; }

            public void Dispose()
            {
                if (View != null)
                {
                    UnityEngine.Object.DestroyImmediate(View.gameObject);
                }
            }
        }

        private sealed class FixedNavigationTargetResolver : IUiNavigationTargetResolver
        {
            private readonly IUiNavigationTarget _target;

            public FixedNavigationTargetResolver(IUiNavigationTarget target)
            {
                _target = target;
            }

            public UiNavigationTargetResolution Resolve()
            {
                return UiNavigationTargetResolution.Open(_target);
            }
        }

        private sealed class TrackingNavigationTarget : IUiNavigationTarget
        {
            public int NavigateCount { get; private set; }

            public int SubmitCount { get; private set; }

            public UiNavigationCommand LastNavigateCommand { get; private set; }

            public bool CanHandleUiNavigation => true;

            public bool HandleNavigate(UiNavigationCommand command)
            {
                NavigateCount++;
                LastNavigateCommand = command;
                return true;
            }

            public bool HandleSubmit()
            {
                SubmitCount++;
                return true;
            }

            public bool HandleCancel()
            {
                return false;
            }

            public void OnNavigationFocusGained()
            {
            }

            public void OnNavigationFocusLost()
            {
            }
        }

        private sealed class NavigationScreenRuntimeFactory : IScreenRuntimeFactory
        {
            private readonly IUiNavigationTarget _target;

            public NavigationScreenRuntimeFactory(IUiNavigationTarget target)
            {
                _target = target;
            }

            public ScreenRuntimeFactoryResult Create(ScreenRequest request)
            {
                return new ScreenRuntimeFactoryResult(
                    new ScreenPolicy(
                        ScreenPolicyClass.GameplayRoot,
                        ScreenRetentionMode.RetainMountedHistory,
                        ScreenBackAction.None,
                        HudShellMode.Visible,
                        blocksUiGameplayInput: false),
                    new NavigationScreenRuntime(_target));
            }
        }

        private sealed class NavigationScreenRuntime : IScreenRuntime, IUiNavigationTargetProvider
        {
            private readonly IUiNavigationTarget _target;

            public NavigationScreenRuntime(IUiNavigationTarget target)
            {
                _target = target;
            }

            public event Action<ScreenAction> ActionRequested
            {
                add { }
                remove { }
            }

            public void ApplyPayload(IScreenPayload payload)
            {
            }

            public void SetIsCurrent(bool isCurrent)
            {
            }

            public void Dispose()
            {
            }

            public bool TryGetNavigationTarget(out IUiNavigationTarget target)
            {
                target = _target;
                return target != null;
            }
        }

        private sealed class PolicyOnlyPopupRuntimeFactory : IPopupRuntimeFactory
        {
            private readonly PopupPolicy _policy;

            public PolicyOnlyPopupRuntimeFactory(PopupPolicy policy)
            {
                _policy = policy;
            }

            public PopupRuntimeFactoryResult Create(PopupRequest request)
            {
                return new PopupRuntimeFactoryResult(_policy, new PolicyOnlyPopupRuntime());
            }
        }

        private sealed class PolicyOnlyPopupRuntime : IPopupRuntime
        {
            public event Action<PopupCompletionKind> CompletionRequested
            {
                add { }
                remove { }
            }

            public void SetIsTopmost(bool isTopmost)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
