using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.Timing;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using Game.Shared.Display;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class CampaignProductionEntryTests
    {
        private const string MainMenuScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/MainMenuScreen.prefab";
        private const string SettingsScreenPrefabPath = "Assets/_Features/UI/UI_Screens/Prefabs/SettingsScreen.prefab";
        private const string PopupCatalogPath = "Assets/_Features/UI/UI_Popups/Prefabs/GameplayPopupPrefabCatalog.asset";
        private const string UiAudioCueMapPath = "Assets/_Features/UI/UI_Composition/Authoring/UiAudioCueMap_V1.asset";
        private const string RouteConfigPath = "Assets/_Features/UI/UI_Composition/Authoring/GameplayStageLaunchRouteConfig.asset";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenuScene.unity";
        private const string GameplayShellScenePath = "Assets/Scenes/UIAudioScene.unity";
        private const string CombinedStageId = "stage-1-1";
        private const string StageCatalogProviderAssetPath =
            StageContentPaths.StageCatalogProviderAssetPath;
        private const string DefaultSimulationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplaySimulationTimingPreset_DefaultShowcase.asset";
        private const string DefaultPresentationTimingPresetAssetPath =
            "Assets/_Features/Gameplay/Gameplay_Timing/Showcase/GameplayPresentationTimingPreset_DefaultShowcase.asset";

        [TearDown]
        public void TearDown()
        {
            GameplayEntryTransitionVisualSnapshotRegistry.ResetForTests();
            MainMenuTransitionVisualPolicy.ResetForTests();
            SceneEntryPresentationRegistry.ResetForTests();
            MainMenuEntryPresentationRegistry.ResetForTests();
            TerminalSessionRegistry.ResetForTests();
            ResultTransitionVisualSnapshotRegistry.ResetForTests();
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            CampaignLaunchHandoffSessionStore.ResetForTests();
            CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
            CampaignChanceHudDiagnostics.IsEnabled = false;
            CampaignChanceHudDiagnostics.Clear();
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(null);
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            }
        }

        [Test]
        public void MainMenuGameplayEntrySource_UsesScreenCenterIrisAndBlocksAllMenuInteraction()
        {
            var root = new GameObject("main-menu-gameplay-entry-source");
            var provider = CreateProvider("stage-0-1");
            var catalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(PopupCatalogPath);
            var uiAudioCueMap = AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(UiAudioCueMapPath);
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);
            try
            {
                TerminalSessionRegistry.ResetForTests();
                SceneEntryPresentationRegistry.ResetForTests();
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_mainMenuScreenPrefab", prefab);
                SetPrivateField(installer, "_screenPrefabCatalog", UiTestPrefabAssetUtility.LoadScreenCatalog());
                SetPrivateField(installer, "_popupPrefabCatalog", catalog);
                SetPrivateField(installer, "_uiAudioCueMap", uiAudioCueMap);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);
                SetPrivateField(
                    installer,
                    "_campaignStageSequenceDefinition",
                    LoadProductionCampaignSequence());
                installer.Install();

                var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(
                    SceneTransitionIntent.GameplayEntry);
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.GameplayEntry,
                        StageId.CreateOrThrow("stage-0-1"),
                        sourceSceneGeneration: 1,
                        launchProvenance: "main-menu-new-game",
                        launchSlotNumber: 1,
                        launchToken: Guid.NewGuid(),
                        out var token),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryBindTransition(token, transitionId: 301),
                    Is.True);
                var visual = GameplayEntryTransitionVisualSnapshotRegistry.Capture(
                    token,
                    policy);

                Assert.That(
                    installer.TryBeginGameplayEntrySourceClose(
                        token,
                        visual,
                        out var playback),
                    Is.True);
                Assert.That(installer.IsGameplayEntryInteractionBlocked, Is.True);
                Assert.That(installer.MainMenuScreenView.CanHandleUiNavigation, Is.False);
                Assert.That(
                    installer.MainMenuScreenView.SaveSlotPanel.HasFocusableCards,
                    Is.False);
                Assert.That(playback.FocusTarget.IsFallback, Is.True);
                Assert.That(playback.FocusTarget.NormalizedCenter, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(visual.SourceFocusPolicy, Is.EqualTo(GameplayEntryFocusPolicy.AuthoredThenScreenCenter));
                Assert.That(visual.SourceCloseColor, Is.EqualTo(visual.HoldColor));
                Assert.That(visual.HoldColor, Is.EqualTo(visual.DestinationOpenColor));
                Assert.That(visual.HoldColor.b, Is.GreaterThan(visual.HoldColor.r));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                provider.Dispose();
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_CreatesController_AndBindsMainMenuScreenView()
        {
            var root = new GameObject("main-menu-installer");
            var provider = CreateProvider("stage-0-1");
            var catalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(PopupCatalogPath);
            var uiAudioCueMap = AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(UiAudioCueMapPath);
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            var screenCatalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);

            try
            {
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_mainMenuScreenPrefab", prefab);
                SetPrivateField(installer, "_screenPrefabCatalog", screenCatalog);
                SetPrivateField(installer, "_popupPrefabCatalog", catalog);
                SetPrivateField(installer, "_uiAudioCueMap", uiAudioCueMap);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);
                SetPrivateField(installer, "_campaignStageSequenceDefinition", LoadProductionCampaignSequence());

                installer.Install();

                Assert.That(installer.Controller, Is.Not.Null);
                Assert.That(installer.HubController, Is.Not.Null);
                var compositionResolver = GetPrivateField<CampaignStageSequenceResolver>(
                    installer,
                    "_campaignStageSequenceResolver");
                var controllerResolver = GetPrivateField<CampaignStageSequenceResolver>(
                    installer.Controller,
                    "_sequenceResolver");
                var launchEvaluator = GetPrivateField<CampaignSlotLaunchEvaluator>(
                    installer.Controller,
                    "_slotLaunchEvaluator");
                var evaluationResolver = GetPrivateField<CampaignStageSequenceResolver>(
                    launchEvaluator,
                    "_sequenceResolver");
                Assert.That(controllerResolver, Is.SameAs(compositionResolver));
                Assert.That(evaluationResolver, Is.SameAs(compositionResolver));
                UiTestPrefabAssetUtility.AssertOverlayCanvasScaling(root);
                Assert.That(installer.MainMenuScreenView, Is.Not.Null);
                Assert.That(installer.MainMenuScreenView.SaveSlotPanel, Is.Not.Null);
                Assert.That(installer.MainMenuScreenView.SaveSlotPanel.gameObject.activeSelf, Is.False);
                Assert.That(installer.MainMenuScreenView.SaveSlotPanel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                provider.Dispose();
            }
        }

        [Test]
        public void MainMenuUiFlowInstaller_DeleteRequest_ShowsConfirmPopupLayerAboveMainMenuScreen()
        {
            var root = new GameObject("main-menu-delete-popup-installer");
            var provider = CreateProvider("stage-0-1");
            var saveHarness = new TemporaryProductionSaveHarness();
            var catalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(PopupCatalogPath);
            var uiAudioCueMap = AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(UiAudioCueMapPath);
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            var screenCatalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);

            try
            {
                saveHarness.PrepareDefaultSlot(
                    StageId.CreateOrThrow("stage-0-1"),
                    CampaignSaveSlotPolicy.DefaultRemainingChances);
                CampaignSaveCompositionProvider.SetProductionCompositionForTests(
                    saveHarness.SaveStore,
                    saveHarness.RecoveryPort,
                    saveHarness.ActiveSlotStorage);
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_mainMenuScreenPrefab", prefab);
                SetPrivateField(installer, "_screenPrefabCatalog", screenCatalog);
                SetPrivateField(installer, "_popupPrefabCatalog", catalog);
                SetPrivateField(installer, "_uiAudioCueMap", uiAudioCueMap);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);
                SetPrivateField(installer, "_campaignStageSequenceDefinition", LoadProductionCampaignSequence());

                installer.Install();
                var popupLayer = GetPrivateField<PopupLayerView>(installer, "_popupLayerView");
                Assert.That(popupLayer.gameObject.activeSelf, Is.False);

                installer.Controller.RequestDelete(1);

                Assert.That(installer.PopupController.Contains(PopupId.Confirm), Is.True);
                Assert.That(popupLayer.gameObject.activeSelf, Is.True);
                Assert.That(popupLayer.IsDimVisible, Is.True);
                Assert.That(popupLayer.FindPopupView<ConfirmPopupView>(), Is.Not.Null);
                Assert.That(
                    popupLayer.transform.GetSiblingIndex(),
                    Is.GreaterThan(installer.MainMenuScreenView.transform.GetSiblingIndex()));
            }
            finally
            {
                CampaignSaveCompositionProvider.ResetProductionProfileBackedForTests();
                UnityEngine.Object.DestroyImmediate(root);
                provider.Dispose();
                saveHarness.Dispose();
            }
        }

        [Test]
        public void MainMenuScreenPrefab_HasExactlyThreeSaveSlotCards_WiredToSaveSlotPanelView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.SaveSlotPanel, Is.Not.Null);
            Assert.That(prefab.SaveSlotPanel.GetComponentsInChildren<SaveSlotCardView>(true).Length, Is.EqualTo(3));
            prefab.ValidateAuthoredStructureOrThrow();
            Assert.That(prefab.transform.Find("MainCommandPanel/StartButton/Label").GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/SettingsButton/Label").GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("MainCommandPanel/QuitButton/Label").GetComponent<TMP_Text>(), Is.Not.Null);

            var serialized = new SerializedObject(prefab.SaveSlotPanel);
            var cards = serialized.FindProperty("_slotCards");
            Assert.That(cards, Is.Not.Null);
            Assert.That(cards.arraySize, Is.EqualTo(3));
            for (var i = 0; i < cards.arraySize; i++)
            {
                var card = cards.GetArrayElementAtIndex(i).objectReferenceValue as SaveSlotCardView;
                Assert.That(card, Is.Not.Null);
                card.ValidateAuthoredStructureOrThrow();
                Assert.That(card.GetComponent<Image>(), Is.Not.Null);
                Assert.That(card.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
                Assert.That(card.transform.Find("HeaderRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(card.transform.Find("DetailRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(card.transform.Find("MetaRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(card.transform.Find("ActionRow").GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);

                var serializedCard = new SerializedObject(card);
                AssertSerializedReference(serializedCard, "_titleLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_statusLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_stageLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_chancesLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_deathsLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_lastPlayedLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_primaryButton", typeof(Button));
                AssertSerializedReference(serializedCard, "_primaryButtonLabel", typeof(TMP_Text));
                AssertSerializedReference(serializedCard, "_deleteButton", typeof(Button));
                AssertSerializedReference(serializedCard, "_deleteButtonLabel", typeof(TMP_Text));
            }
        }

        [Test]
        public void MainMenuScreenSource_DoesNotCreateAuthoredPrefabUiAtRuntime()
        {
            var mainMenuSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/MainMenuScreenView.cs");
            var saveSlotSource = ReadRepoFile("Assets/_Features/UI/UI_Screens/Runtime/SaveSlotCardView.cs");

            Assert.That(mainMenuSource, Does.Not.Contain("new GameObject"));
            Assert.That(mainMenuSource, Does.Not.Contain("AddComponent<"));
            Assert.That(saveSlotSource, Does.Not.Contain("new GameObject"));
            Assert.That(saveSlotSource, Does.Not.Contain("AddComponent<"));
            Assert.That(saveSlotSource, Does.Not.Contain("EnsureDefaultHierarchy"));
        }

        [Test]
        public void EmptySlot_NewGame_InitializesFirstStage_CreatesPendingHandoff_AndLaunches()
        {
            var harness = CreateControllerHarness("stage-0-1");
            try
            {
                harness.Controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

                Assert.That(harness.SaveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(harness.ActiveSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
                Assert.That(harness.LaunchHandoffStore.TryPeek(out var handoff), Is.True);
                Assert.That(handoff.SlotNumber, Is.EqualTo(1));
                Assert.That(handoff.Source, Is.EqualTo("main-menu-new-game"));
                Assert.That(harness.Router.Requests.Count, Is.EqualTo(1));
                Assert.That(harness.Router.Requests[0].StageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(
                    harness.Router.Requests[0].TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.GameplayEntry));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void ExistingValidSlot_Continue_ValidatesSyncsPendingHandoff_AndLaunchesSavedStage()
        {
            var harness = CreateControllerHarness("stage-2-2");
            try
            {
                SeedSlot(harness.SaveStore, 2, "stage-2-2", "stale");

                harness.Controller.Continue(2);

                Assert.That(harness.SaveStore.LoadSlot(2).CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(harness.ActiveSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
                Assert.That(harness.LaunchHandoffStore.TryPeek(out var handoff), Is.True);
                Assert.That(handoff.SlotNumber, Is.EqualTo(2));
                Assert.That(handoff.Source, Is.EqualTo("main-menu-continue"));
                Assert.That(harness.Router.Requests[0].StageId.Value, Is.EqualTo("stage-2-2"));
                Assert.That(
                    harness.Router.Requests[0].TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.GameplayEntry));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenu_NewGameAndContinue_UseInjectedDivergentSequence()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            var provider = CreateProvider("fixture-a", "fixture-c", "fixture-b");
            var saveKey = CreateTransientNamespace("divergent-menu-saves");
            var activeKey = CreateTransientNamespace("divergent-menu-active");
            var saveStore = new TransientCampaignSaveSlotStore(saveKey);
            var activeStorage = new TransientActiveSlotStorage(activeKey);
            var handoffStore = new RecordingCampaignLaunchHandoffStore();
            try
            {
                definition.SetEntries(new[]
                {
                    CreateCampaignSequenceEntry("fixture-a", "group-a"),
                    CreateCampaignSequenceEntry("fixture-c", "group-b"),
                    CreateCampaignSequenceEntry("fixture-b", "group-b"),
                });
                var resolver = new CampaignStageSequenceResolver(definition);
                var router = new FakeStageLaunchRouter();
                var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                    saveStore,
                    activeStorage,
                    handoffStore);
                using var controller = new MainMenuController(
                    repairingStore,
                    repairingStore,
                    repairingStore,
                    handoffStore,
                    resolver,
                    new CampaignSlotLaunchEvaluator(resolver, provider.Provider),
                    router,
                    new FakeConfirmPopupPort());
                saveStore.ClearAll();

                controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

                Assert.That(saveStore.LoadSlot(1).CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-a")));
                Assert.That(saveStore.LoadSlot(1).CurrentLevelGroupId, Is.EqualTo("group-a"));
                Assert.That(router.Requests[0].StageId, Is.EqualTo(StageId.CreateOrThrow("fixture-a")));
                Assert.That(handoffStore.TryPeek(out var newGameHandoff), Is.True);
                Assert.That(handoffStore.TryClear(newGameHandoff.Token), Is.True);

                SeedSlot(saveStore, 2, "fixture-c", "stale-group", 2);
                controller.Continue(2);

                Assert.That(saveStore.LoadSlot(2).CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
                Assert.That(saveStore.LoadSlot(2).CurrentLevelGroupId, Is.EqualTo("group-b"));
                Assert.That(router.Requests[1].StageId, Is.EqualTo(StageId.CreateOrThrow("fixture-c")));
            }
            finally
            {
                saveStore.ClearAll();
                activeStorage.ClearActiveSlot();
                if (handoffStore.TryPeek(out var handoff))
                {
                    handoffStore.TryClear(handoff.Token);
                }

                provider.Dispose();
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CompletedSlot_Continue_IsRejectedAtControllerLevel()
        {
            var harness = CreateControllerHarness("stage-4-3");
            try
            {
                CompleteCampaign(harness.SaveStore);

                harness.Controller.Continue(1);

                Assert.That(harness.Router.Requests, Is.Empty);
                Assert.That(harness.ActiveSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void CompletedSlot_RestartRequiresConfirm_AndOverwritesOnlyAfterConfirmed()
        {
            var harness = CreateControllerHarness("stage-0-1", "stage-4-3");
            try
            {
                CompleteCampaign(harness.SaveStore);

                harness.Controller.RequestRestart(1);
                harness.ConfirmPort.Complete(false);
                Assert.That(harness.SaveStore.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(harness.Router.Requests, Is.Empty);

                harness.Controller.RequestRestart(1);
                harness.ConfirmPort.Complete(true);
                Assert.That(harness.SaveStore.LoadSlot(1).CampaignCompleted, Is.False);
                Assert.That(harness.SaveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(harness.Router.Requests.Count, Is.EqualTo(1));
                Assert.That(
                    harness.Router.Requests[0].Source,
                    Is.EqualTo("main-menu-completed-restart"));
                Assert.That(
                    harness.Router.Requests[0].TransitionIntent,
                    Is.EqualTo(SceneTransitionIntent.GameplayEntry));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void CorruptedOrUnsupportedSlot_Continue_IsRejected()
        {
            var harness = CreateControllerHarness("stage-0-1");
            try
            {
                SeedSlot(harness.SaveStore, 1, "stage-9-9", "level-9");

                harness.Controller.Continue(1);

                Assert.That(harness.Router.Requests, Is.Empty);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void DeleteRequiresConfirm_AndClearsActiveSlot_WhenDeletingActiveSlot()
        {
            var harness = CreateControllerHarness("stage-0-1");
            try
            {
                SeedSlot(harness.SaveStore, 1, "stage-0-1", string.Empty);
                harness.ActiveSlotProvider.SetActiveSlot(1);

                harness.Controller.RequestDelete(1);
                harness.ConfirmPort.Complete(false);
                Assert.That(harness.SaveStore.LoadSlot(1).IsEmpty, Is.False);
                Assert.That(harness.ActiveSlotProvider.ActiveSlotNumber, Is.EqualTo(1));

                harness.Controller.RequestDelete(1);
                harness.ConfirmPort.Complete(true);
                Assert.That(harness.SaveStore.LoadSlot(1).IsEmpty, Is.True);
                Assert.That(harness.ActiveSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void MainMenuController_DoesNotCallStageLaunchContextStoreSetCurrentDirectly()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Application/Runtime/MainMenuController.cs");
            Assert.That(source, Does.Not.Contain("StageLaunchContextStore.SetCurrent"));
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_SetsLaunchContext_AndLoadsConfiguredScene()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var sceneLoader = new FakeSceneLoadPort();
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                Assert.That(
                    CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                        1,
                        stageId,
                        StageNavigationKind.Continue,
                        "test",
                        out var handoff),
                    Is.True);
                new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(
                    new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test"));

                Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
                Assert.That(current, Is.EqualTo(stageId));
                Assert.That(StageLaunchContextStore.TryPeek(out var context), Is.True);
                Assert.That(context.Matches(handoff), Is.True);
                Assert.That(sceneLoader.LoadedScenes, Is.EqualTo(new[] { "UIAudioScene" }));
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_LoadFailure_ClearsMatchingPendingAndContext()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Continue,
                    "load-failure");
                Assert.That(
                    handoffStore.TryBegin(
                        1,
                        request.StageId,
                        request.NavigationKind,
                        request.Source,
                        out _),
                    Is.True);
                var router = new ConfiguredGameplayStageLaunchRouter(
                    routeConfig,
                    new FakeSceneLoadPort(_ => throw new InvalidOperationException("load failed")));

                Assert.Throws<InvalidOperationException>(() => router.Launch(request));

                Assert.That(handoffStore.TryPeek(out _), Is.False);
                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_DuplicateExactRoute_DoesNotStartSecondLoadOrClearOriginalOwners()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var sceneLoader = new FakeSceneLoadPort();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Continue,
                    "duplicate-exact-route");
                Assert.That(
                    handoffStore.TryBegin(
                        1,
                        request.StageId,
                        request.NavigationKind,
                        request.Source,
                        out var handoff),
                    Is.True);
                var router = new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader);

                router.Launch(request);
                Assert.Throws<InvalidOperationException>(() => router.Launch(request));

                Assert.That(sceneLoader.LoadedScenes.Count, Is.EqualTo(1));
                Assert.That(StageLaunchContextStore.TryPeek(out var currentContext), Is.True);
                Assert.That(currentContext.Matches(handoff), Is.True);
                Assert.That(handoffStore.TryPeek(out var currentHandoff), Is.True);
                Assert.That(currentHandoff, Is.SameAs(handoff));
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_MismatchedRequest_DoesNotOverwriteAcceptedContextOrHandoff()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var firstStage = StageId.CreateOrThrow("stage-0-1");
                var secondStage = StageId.CreateOrThrow("stage-0-2");
                Assert.That(
                    handoffStore.TryBegin(
                        1,
                        firstStage,
                        StageNavigationKind.Continue,
                        "first",
                        out var firstHandoff),
                    Is.True);
                StageLaunchContextStore.SetCurrent(firstStage);
                var router = new ConfiguredGameplayStageLaunchRouter(
                    routeConfig,
                    new FakeSceneLoadPort());

                Assert.Throws<InvalidOperationException>(() => router.Launch(
                    new StageNavigationRequest(
                        secondStage,
                        StageNavigationKind.Continue,
                        "second")));

                Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
                Assert.That(stillPending, Is.SameAs(firstHandoff));
                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(firstStage));
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_Launch_ClearsStaleCampaignTempDirectPlayContext_BeforeStageTransition()
        {
            AssertConfiguredGameplayLaunchClearsStaleDirectPlayContext(
                EditorDirectPlayContext.CreateCampaignTempSlot(StageId.CreateOrThrow("stage-0-1"), remainingChances: 2));
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_Launch_ClearsStaleNonCampaignDirectPlayContext_BeforeStageTransition()
        {
            AssertConfiguredGameplayLaunchClearsStaleDirectPlayContext(
                EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow("stage-0-1")));
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_Launch_CarriesCampaignProductionDirectPlayProvenanceToNextStage()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var completedStageId = StageId.CreateOrThrow("stage-0-1");
            var nextStageId = StageId.CreateOrThrow("stage-0-2");
            var directPlayContext = new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignProductionSlot,
                completedStageId,
                remainingChances: 2,
                suppressCampaignFlow: false);
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                EditorDirectPlayContextStore.SetCurrent(directPlayContext);
                var request = new StageNavigationRequest(
                    nextStageId,
                    StageNavigationKind.NextStage,
                    "campaign-auto-next",
                    transitionIntent: SceneTransitionIntent.StageAdvance,
                    editorDirectPlayContext: directPlayContext.ForStage(nextStageId));
                var sceneLoader = new FakeSceneLoadPort(_ =>
                {
                    Assert.That(
                        EditorDirectPlayContextStore.GetCurrentOrNone(),
                        Is.EqualTo(directPlayContext.ForStage(nextStageId)));
                    Assert.That(StageLaunchContextStore.TryPeek(out var launchContext), Is.True);
                    Assert.That(launchContext.EditorDirectPlayContext.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignProductionSlot));
                    Assert.That(launchContext.EditorDirectPlayContext.StageId, Is.EqualTo(nextStageId));
                });

                new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(request);

                Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
                Assert.That(current.Matches(request), Is.True);
                Assert.That(current.EditorDirectPlayContext.Mode, Is.EqualTo(EditorDirectPlayMode.CampaignProductionSlot));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void ConfiguredGameplayStageLaunchRouter_RejectedDirectPlayRetry_RestoresContextForSecondAttempt(
            EditorDirectPlayMode mode)
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var stageId = StageId.CreateOrThrow("stage-0-1");
            var directPlayContext = mode == EditorDirectPlayMode.CampaignTempSlot
                ? EditorDirectPlayContext.CreateCampaignTempSlot(stageId, remainingChances: 2)
                : new EditorDirectPlayContext(
                    EditorDirectPlayMode.CampaignProductionSlot,
                    stageId,
                    remainingChances: 2,
                    suppressCampaignFlow: false);
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "pause-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry,
                directPlayContext);
            var attemptCount = 0;
            var sceneLoader = new FakeSceneLoadPort(_ =>
            {
                attemptCount += 1;
                if (attemptCount == 1)
                {
                    throw new InvalidOperationException("Injected first retry rejection.");
                }
            });
            var initialBootstrap = StageLaunchContext.CreateDirectPlay(stageId);
            var temporaryPathProvider = new TemporaryCampaignSavePathProvider();
            var temporaryProfilePath = temporaryPathProvider.GetSaveFilePath(
                FileCampaignProfileRepository.ProfileFileName);
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                EditorDirectPlayContextStore.SetCurrent(directPlayContext);
                Assert.That(StageLaunchContextStore.TrySetCurrent(initialBootstrap), Is.True);
                Directory.CreateDirectory(temporaryPathProvider.SaveRootPath);
                File.WriteAllText(temporaryProfilePath, "temporary-campaign-marker");
                var router = new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader);

                Assert.Throws<InvalidOperationException>(() => router.Launch(request));

                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone(), Is.EqualTo(directPlayContext));
                Assert.That(StageLaunchContextStore.TryPeek(out var restoredBootstrap), Is.True);
                Assert.That(restoredBootstrap, Is.SameAs(initialBootstrap));
                Assert.That(File.Exists(temporaryProfilePath), Is.True);

                Assert.DoesNotThrow(() => router.Launch(request));

                Assert.That(attemptCount, Is.EqualTo(2));
                Assert.That(
                    EditorDirectPlayContextStore.GetCurrentOrNone(),
                    Is.EqualTo(directPlayContext));
                Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
                Assert.That(current.EditorDirectPlayContext, Is.EqualTo(directPlayContext));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_OldFailurePreservesNewerDirectPlayOwners()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var attemptedStageId = StageId.CreateOrThrow("stage-0-1");
            var newerStageId = StageId.CreateOrThrow("stage-0-2");
            var attemptedDirectPlay = EditorDirectPlayContext.CreateCampaignTempSlot(
                attemptedStageId,
                remainingChances: 2);
            var newerDirectPlay = EditorDirectPlayContext.CreateCampaignTempSlot(
                newerStageId,
                remainingChances: 3);
            var request = new StageNavigationRequest(
                attemptedStageId,
                StageNavigationKind.Retry,
                "pause-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry,
                attemptedDirectPlay);
            StageLaunchContext newerPrime = null;
            var sceneLoader = new FakeSceneLoadPort(_ =>
            {
                EditorDirectPlayContextStore.SetCurrent(newerDirectPlay);
                newerPrime = StageLaunchContextStore.PrimePendingEditorDirectPlay(newerStageId);
                throw new InvalidOperationException("Injected stale launch failure.");
            });
            var initialBootstrap = StageLaunchContext.CreateDirectPlay(attemptedStageId);
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                EditorDirectPlayContextStore.SetCurrent(attemptedDirectPlay);
                Assert.That(StageLaunchContextStore.TrySetCurrent(initialBootstrap), Is.True);

                Assert.Throws<InvalidOperationException>(() =>
                    new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(request));

                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone(), Is.EqualTo(newerDirectPlay));
                Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
                Assert.That(
                    StageLaunchContextStore.TryPeekPendingEditorDirectPlayContext(out var pending),
                    Is.True);
                Assert.That(pending, Is.EqualTo(newerPrime));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void SceneTransitionCoordinator_AsynchronousFailure_RestoresDirectPlayContextWithoutOverwritingNewer(
            EditorDirectPlayMode mode)
        {
            var stageId = StageId.CreateOrThrow("stage-0-1");
            var directPlayContext = mode == EditorDirectPlayMode.CampaignTempSlot
                ? EditorDirectPlayContext.CreateCampaignTempSlot(stageId, remainingChances: 2)
                : new EditorDirectPlayContext(
                    EditorDirectPlayMode.CampaignProductionSlot,
                    stageId,
                    remainingChances: 2,
                    suppressCampaignFlow: false);
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "failed-asynchronous-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry,
                directPlayContext);

            EditorDirectPlayContextStore.Clear();
            var attemptedOwnershipGeneration =
                EditorDirectPlayContextStore.OwnershipGeneration;
            SceneTransitionCoordinator.TryRestoreDirectPlayContextAfterFailure(
                request,
                attemptedOwnershipGeneration);
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone(), Is.EqualTo(directPlayContext));

            var newerContext = EditorDirectPlayContext.CreateNonCampaign(
                StageId.CreateOrThrow("stage-0-2"));
            EditorDirectPlayContextStore.SetCurrent(newerContext);
            SceneTransitionCoordinator.TryRestoreDirectPlayContextAfterFailure(
                request,
                attemptedOwnershipGeneration);
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone(), Is.EqualTo(newerContext));

            EditorDirectPlayContextStore.Clear();
            SceneTransitionCoordinator.TryRestoreDirectPlayContextAfterFailure(
                request,
                attemptedOwnershipGeneration);
            Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone(),
                Is.EqualTo(EditorDirectPlayContext.None));
        }

        [Test]
        [Category("Full")]
        public void StageBackedInstaller_ConsumesCarriedProductionDirectPlayContextAndAllowsFollowingHop()
        {
            var installerObject = new GameObject("carried-production-direct-play-installer");
            var saveHarness = new TemporaryProductionSaveHarness();
            var stageId = StageId.CreateOrThrow(CombinedStageId);
            var directPlayContext = new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignProductionSlot,
                stageId,
                remainingChances: 2,
                suppressCampaignFlow: false);
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.NextStage,
                "campaign-auto-next",
                transitionIntent: SceneTransitionIntent.StageAdvance,
                editorDirectPlayContext: directPlayContext);
            var carriedLaunchContext = StageLaunchContext.CreatePendinglessReload(request);
            try
            {
                saveHarness.PrepareDefaultSlot(stageId, remainingChances: 2);
                Assert.That(StageLaunchContextStore.TrySetCurrent(carriedLaunchContext), Is.True);
                installerObject.AddComponent<CampaignProductionEntryTerminalSessionAuthorityProvider>();
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                DisableAutoCreateViews(installer);
                AssignStageCatalogProvider(installer);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveHarness.SaveStore, saveHarness.ActiveSlotProvider);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
                Assert.That(StageLaunchContextStore.TryPeek(out _), Is.False);
                var followingContext = StageLaunchContext.CreatePendinglessReload(request);
                Assert.That(StageLaunchContextStore.TrySetCurrent(followingContext), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(installerObject);
                saveHarness.Dispose();
            }
        }

        [TestCase(EditorDirectPlayMode.CampaignTempSlot)]
        [TestCase(EditorDirectPlayMode.CampaignProductionSlot)]
        public void ConfiguredRouter_ConsumesRetainedInitialDirectPlayContextForFirstContinuation(
            EditorDirectPlayMode mode)
        {
            var installerObject = new GameObject("initial-direct-play-installer");
            var saveHarness = new TemporaryProductionSaveHarness();
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var stageId = StageId.CreateOrThrow(CombinedStageId);
            var directPlayContext = mode == EditorDirectPlayMode.CampaignTempSlot
                ? EditorDirectPlayContext.CreateCampaignTempSlot(stageId, remainingChances: 2)
                : new EditorDirectPlayContext(
                    EditorDirectPlayMode.CampaignProductionSlot,
                    stageId,
                    remainingChances: 2,
                    suppressCampaignFlow: false);
            var initialLaunchContext = StageLaunchContext.CreateDirectPlay(stageId);
            var continuationRequest = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "pause-retry",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                SceneTransitionIntent.ManualRetry,
                directPlayContext);
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                saveHarness.PrepareDefaultSlot(stageId, remainingChances: 2);
                EditorDirectPlayContextStore.SetCurrent(directPlayContext);
                Assert.That(StageLaunchContextStore.TrySetCurrent(initialLaunchContext), Is.True);
                installerObject.AddComponent<CampaignProductionEntryTerminalSessionAuthorityProvider>();
                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                DisableAutoCreateViews(installer);
                AssignStageCatalogProvider(installer);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveHarness.SaveStore, saveHarness.ActiveSlotProvider);

                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
                Assert.That(StageLaunchContextStore.TryPeek(out var retainedBootstrap), Is.True);
                Assert.That(retainedBootstrap, Is.SameAs(initialLaunchContext));

                new ConfiguredGameplayStageLaunchRouter(
                        routeConfig,
                        new FakeSceneLoadPort())
                    .Launch(continuationRequest);

                Assert.That(StageLaunchContextStore.TryPeek(out var continuationContext), Is.True);
                Assert.That(continuationContext.Matches(continuationRequest), Is.True);
                Assert.That(continuationContext.EditorDirectPlayContext, Is.EqualTo(directPlayContext));
                Assert.That(
                    EditorDirectPlayContextStore.GetCurrentOrNone(),
                    Is.EqualTo(directPlayContext));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
                UnityEngine.Object.DestroyImmediate(installerObject);
                saveHarness.Dispose();
            }
        }

        [Test]
        public void ProductionSaveComposition_WiresPersistentDataRepositories_WithoutWritingSaveRoot()
        {
            var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();
            var expectedRoot = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                ApplicationPersistentDataSavePathProvider.SavesDirectoryName);
            var before = CaptureFileSetSnapshot(expectedRoot);

            Assert.That(options.PathProvider, Is.TypeOf<ApplicationPersistentDataSavePathProvider>());
            Assert.That(options.PathProvider.SaveRootPath, Is.EqualTo(expectedRoot));

            var profileServices = CampaignSaveServiceFactory.Create(
                new CampaignSaveServiceFactoryOptions
                {
                    PathProvider = options.PathProvider,
                    ProductVersion = options.ProductVersion,
                    ProfileId = options.ProfileId,
                    UtcNow = options.UtcNow,
                });
            var localStateRepository = new FileCampaignLocalLaunchStateRepository(
                new AtomicTextFileStore(options.PathProvider.SaveRootPath));

            Assert.That(profileServices.TextFileStore, Is.TypeOf<AtomicTextFileStore>());
            Assert.That(profileServices.Repository, Is.TypeOf<FileCampaignProfileRepository>());
            Assert.That(localStateRepository, Is.TypeOf<FileCampaignLocalLaunchStateRepository>());

            var compositionSource = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveCompositionProvider.cs");
            Assert.That(compositionSource, Does.Contain("new ApplicationPersistentDataSavePathProvider()"));
            Assert.That(compositionSource, Does.Contain("new FileCampaignLocalLaunchStateRepository("));
            Assert.That(compositionSource, Does.Contain("new AtomicTextFileStore(pathProvider.SaveRootPath)"));

            AssertFileSetSnapshotEqual(before, CaptureFileSetSnapshot(expectedRoot));
        }

        [Test]
        [Category("Full")]
        public void ProductionMainMenuLaunch_WithStaleCampaignTempDirectPlayContext_InjectsChanceReadSource()
        {
            AssertProductionMainMenuLaunchWithStaleContextInjectsChanceReadSource(
                EditorDirectPlayContext.CreateCampaignTempSlot(StageId.CreateOrThrow(CombinedStageId), remainingChances: 2));
        }

        [Test]
        [Category("Full")]
        public void ProductionMainMenuLaunch_WithStaleNonCampaignDirectPlayContext_InjectsChanceReadSource()
        {
            AssertProductionMainMenuLaunchWithStaleContextInjectsChanceReadSource(
                EditorDirectPlayContext.CreateNonCampaign(StageId.CreateOrThrow(CombinedStageId)));
        }

        [Test]
        public void ConfiguredGameplayStageLaunchRouter_RejectsMissingOrInvalidSceneConfig()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            try
            {
                var router = new ConfiguredGameplayStageLaunchRouter(routeConfig, new FakeSceneLoadPort());
                Assert.Throws<InvalidOperationException>(() => router.Launch(
                    new StageNavigationRequest(StageId.CreateOrThrow("stage-0-1"), StageNavigationKind.Continue, "test")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void ConfiguredMainMenuReturnRouter_ClearsStageContext_PreservesActiveSlot_AndLoadsMainMenu()
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var sceneLoader = new FakeSceneLoadPort();
            var activeKey = CreateTransientNamespace("return-active");
            var activeSlotProvider = new ActiveSlotProvider(new TransientActiveSlotStorage(activeKey));
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                activeSlotProvider.SetActiveSlot(2);
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-0-1"));

                new ConfiguredMainMenuReturnRouter(routeConfig, sceneLoader).ReturnToMainMenu(
                    SceneTransitionIntent.ReturnToMainMenu);

                Assert.That(StageLaunchContextStore.TryGetCurrent(out _), Is.False);
                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(sceneLoader.LoadedScenes, Is.EqualTo(new[] { "MainMenuScene" }));
            }
            finally
            {
                activeSlotProvider.ClearActiveSlot();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        [Test]
        public void RouteConfigScenes_ExistAndAreIncludedInBuildSettings()
        {
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);
            Assert.That(routeConfig, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), routeConfig.MainMenuScenePath)), Is.True);
            Assert.That(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), routeConfig.GameplayShellScenePath)), Is.True);

            var buildScenes = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    buildScenes.Add(scene.path);
                }
            }

            Assert.That(buildScenes, Does.Contain(routeConfig.MainMenuScenePath));
            Assert.That(buildScenes, Does.Contain(routeConfig.GameplayShellScenePath));
        }

        [Test]
        public void CompletedSlotDisplay_UsesStageLocalizationTableWithoutSequenceDisplayField()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(StageId.CreateOrThrow("stage-4-2"), "level-4");
                definition.SetEntries(new[] { entry });
                var resolver = new CampaignStageSequenceResolver(definition);
                var immutableEntry = CampaignSlotRawDataMapper.ToEntry(
                        new SaveSlotData
                        {
                            SlotNumber = 1,
                            CurrentStageId = StageId.CreateOrThrow("stage-4-2"),
                            CurrentLevelGroupId = "level-4",
                            CampaignCompleted = true,
                        });
                var evaluation = CampaignStageSequenceTestAsset
                    .LoadProductionLaunchEvaluator(resolver)
                    .Evaluate(immutableEntry);
                var viewModel = MainMenuSlotViewModelMapper.MapSlot(
                    immutableEntry,
                    evaluation,
                    CampaignSlotActionPolicy.Evaluate(evaluation));

                Assert.That(viewModel.StageText, Is.EqualTo("Stage Morgue-02"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RuntimeSource_PreventsHardCodedGameplayOrMainMenuSceneLiterals()
        {
            foreach (var path in Directory.GetFiles("Assets/_Features", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.Ordinal) ||
                    normalized.Contains("_Tests/", StringComparison.Ordinal))
                {
                    continue;
                }

                var source = File.ReadAllText(path);
                Assert.That(source, Does.Not.Contain("\"MainMenuScene\""), normalized);
                Assert.That(source, Does.Not.Contain("\"UIAudioScene\""), normalized);
            }
        }

        [Test]
        public void MainMenuComposition_DoesNotInstantiateCurrentSceneStageLaunchRouter()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");
            Assert.That(source, Does.Not.Contain("CurrentSceneStageLaunchRouter"));
            Assert.That(source, Does.Contain("ConfiguredGameplayStageLaunchRouter"));
        }

        [Test]
        public void MainMenuComposition_MissingCampaignSequence_FailsBeforeUiBootstrap()
        {
            var root = new GameObject("main-menu-missing-campaign-sequence");
            var provider = CreateProvider("fixture-a");
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);

                var exception = Assert.Throws<InvalidOperationException>(() => installer.Install());

                Assert.That(exception.Message, Does.Contain("MainMenuUiFlowInstaller"));
                Assert.That(exception.Message, Does.Contain("authoritative serialized CampaignStageSequenceDefinition"));
                Assert.That(installer.Controller, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(routeConfig);
                provider.Dispose();
            }
        }

        [Test]
        public void MainMenuSequenceProvider_BeforeStart_LazilyCreatesOneSerializedResolver()
        {
            var root = new GameObject("main-menu-sequence-provider-before-start");
            var definition = LoadProductionCampaignSequence();
            try
            {
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_campaignStageSequenceDefinition", definition);
                var provider = (ICampaignStageSequenceResolverProvider)installer;

                Assert.That(
                    provider.TryCreateCampaignStageSequenceResolver(out var first),
                    Is.True);
                Assert.That(first, Is.Not.Null);
                Assert.That(first.Contains(first.FirstStageId), Is.True);
                Assert.That(
                    provider.TryCreateCampaignStageSequenceResolver(out var second),
                    Is.True);
                Assert.That(second, Is.SameAs(first));
                Assert.That(
                    GetPrivateField<CampaignStageSequenceResolver>(
                        installer,
                        "_campaignStageSequenceResolver"),
                    Is.SameAs(first));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeathRetryProductionRouter_IsCoordinatorBacked()
        {
            var root = new GameObject("gameplay-ui-router-provider");
            try
            {
                var installer = root.AddComponent<GameplayUiFlowInstaller>();
                Assert.That(installer, Is.InstanceOf<IStageLaunchRouterProvider>());

                var provider = (IStageLaunchRouterProvider)installer;
                Assert.That(provider.TryCreateStageLaunchRouter("UIAudioScene", out var router), Is.True);
                Assert.That(router, Is.TypeOf<CurrentSceneStageLaunchRouter>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CurrentSceneStageLaunchRouter_GuardRejection_IsSurfaced()
        {
            var result = RunCurrentSceneGuardRejection(injectPendingDuringAttempt: false);

            Assert.That(result.Exception, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void CurrentSceneStageLaunchRouter_GuardRejection_DoesNotStartLoad()
        {
            var result = RunCurrentSceneGuardRejection(injectPendingDuringAttempt: false);

            Assert.That(result.TransitionAttemptCount, Is.EqualTo(1));
            Assert.That(result.LoadStartCount, Is.EqualTo(0));
        }

        [Test]
        public void CurrentSceneStageLaunchRouter_GuardRejection_DoesNotWriteOrClearContext()
        {
            var result = RunCurrentSceneGuardRejection(injectPendingDuringAttempt: false);

            Assert.That(StageLaunchContextStore.TryPeek(out var current), Is.True);
            Assert.That(current, Is.SameAs(result.OriginalContext));
        }

        [Test]
        public void CurrentSceneStageLaunchRouter_GuardRejection_DoesNotClearPending()
        {
            var result = RunCurrentSceneGuardRejection(injectPendingDuringAttempt: true);

            Assert.That(CampaignLaunchHandoffSessionStore.Instance.TryPeek(out var pending), Is.True);
            Assert.That(pending, Is.SameAs(result.PendingCreatedDuringAttempt));
        }

        [Test]
        public void CurrentSceneStageLaunchRouter_AcceptedRoute_ReturnsNormally()
        {
            var transitionAttemptCount = 0;
            var router = new CurrentSceneStageLaunchRouter(
                "UIAudioScene",
                sceneLoadPort: null,
                tryStartStageTransition: (_, __) =>
                {
                    transitionAttemptCount++;
                    return true;
                });

            Assert.DoesNotThrow(() => router.Launch(new StageNavigationRequest(
                StageId.CreateOrThrow("stage-0-1"),
                StageNavigationKind.Retry,
                "stage-result-retry")));
            Assert.That(transitionAttemptCount, Is.EqualTo(1));
        }

        [Test]
        public void MinimumVisible_StartsAfterOverlayVisible()
        {
            var profile = new StageTransitionProfile(
                StageTransitionKind.DeathRetryChanceLost,
                string.Empty,
                string.Empty,
                0.2f,
                true,
                true,
                true,
                TransitionOverlayKind.ChanceLost,
                preOverlayDelaySeconds: 0.5f,
                blockInputDuringPreOverlayDelay: true,
                startAsyncLoadBeforeOverlay: true);
            const float transitionStartedAt = 10f;
            const float overlayShownAt = 10.5f;

            Assert.That(
                SceneTransitionCoordinator.IsMinimumVisibleElapsedForActivation(
                    profile,
                    overlayShownAt,
                    transitionStartedAt + 0.25f),
                Is.False);
            Assert.That(
                SceneTransitionCoordinator.IsMinimumVisibleElapsedForActivation(
                    profile,
                    overlayShownAt,
                    overlayShownAt + 0.21f),
                Is.True);
        }

        [Test]
        public void SceneTransitionCoordinator_StageAdvanceMissingResultSnapshot_CancelsClaimAndReleasesGuard()
        {
            var stageId = StageId.CreateOrThrow("stage-0-2");
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                6101,
                "stage-advance-missing-result-test");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.StageAdvance,
                    stageId,
                    generation,
                    out _),
                Is.True);
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_StageAdvanceMissingResultSnapshot_CancelsClaimAndReleasesGuard));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.NextStage,
                    "campaign-auto-next",
                    StageTransitionHint.ForKind(StageTransitionKind.StageClearNext),
                    SceneTransitionIntent.StageAdvance);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "unused-gameplay-scene"));

                Assert.That(exception.Message, Does.Contain("Result transition visual"));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(coordinator.IsTransitionInProgress, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_GameplayCaptureThrow_CancelsExactClaimAndPreservesOtherSnapshot()
        {
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                6103,
                "gameplay-capture-failure-test");
            var stageId = StageId.CreateOrThrow("stage-0-1");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ManualRetry,
                    stageId,
                    generation,
                    out var claimedToken),
                Is.True);
            var staleToken = new SceneEntrySessionToken(991);
            Assert.That(staleToken, Is.Not.EqualTo(claimedToken));
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(
                SceneTransitionIntent.ManualRetry);
            GameplayEntryTransitionVisualSnapshotRegistry.Capture(
                staleToken,
                policy);
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_GameplayCaptureThrow_CancelsExactClaimAndPreservesOtherSnapshot));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    stageId,
                    StageNavigationKind.Retry,
                    "stage-result-retry",
                    StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                    SceneTransitionIntent.ManualRetry);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "unused-gameplay-scene"));

                Assert.That(exception.Message, Does.Contain("still owns the immutable snapshot"));
                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(coordinator.IsTransitionInProgress, Is.False);
                Assert.DoesNotThrow(() =>
                    GameplayEntryTransitionVisualSnapshotRegistry.Require(
                        staleToken,
                        SceneTransitionIntent.ManualRetry));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_MainMenuCaptureThrow_CancelsExactClaimAndPreservesOtherSnapshot()
        {
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                6104,
                "main-menu-capture-failure-test");
            Assert.That(
                MainMenuEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ReturnToMainMenu,
                    generation,
                    "capture-failure-test",
                    out var claimedToken),
                Is.True);
            var staleToken = new MainMenuEntrySessionToken(992);
            Assert.That(staleToken, Is.Not.EqualTo(claimedToken));
            var policy = SceneTransitionRoutePolicyCatalog.ResolveProduction(
                SceneTransitionIntent.ReturnToMainMenu);
            MainMenuTransitionVisualPolicy.Capture(staleToken, policy);
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_MainMenuCaptureThrow_CancelsExactClaimAndPreservesOtherSnapshot));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartMainMenuReturn(
                        "unused-main-menu-scene",
                        SceneTransitionIntent.ReturnToMainMenu));

                Assert.That(exception.Message, Does.Contain("still owns the immutable snapshot"));
                Assert.That(MainMenuEntryPresentationRegistry.IsActive, Is.False);
                Assert.That(coordinator.IsTransitionInProgress, Is.False);
                Assert.DoesNotThrow(() =>
                    MainMenuTransitionVisualPolicy.Require(
                        staleToken,
                        SceneTransitionIntent.ReturnToMainMenu));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_TerminalBindFailure_TerminalizesBoundEntryAndKeepsGuardWithCover()
        {
            var authority = TerminalSessionRegistry.Authority;
            var generation = authority.RegisterSceneBootstrap(
                6102,
                "terminal-bind-failure-test");
            var terminalClaim = authority.TryClaim(new TerminalClaimRequest(
                TerminalTransitionKind.Defeat,
                generation,
                TerminalDestinationKind.ReloadedGameplay));
            Assert.That(terminalClaim.Accepted, Is.True);
            Assert.That(
                authority.TryBindTransition(
                    terminalClaim.Token,
                    transitionId: 777,
                    TerminalDestinationKind.ReloadedGameplay),
                Is.True);
            SceneTransitionCoordinator.SetOverlayShellResourceLoaderForTests(() =>
                AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayShellView>(
                    "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/SceneTransitionOverlayShell.prefab"));
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_TerminalBindFailure_TerminalizesBoundEntryAndKeepsGuardWithCover));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Retry,
                    "stage-result-retry",
                    StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual)
                        .WithTerminalClaim(terminalClaim.Token),
                    SceneTransitionIntent.ManualRetry);

                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "unused-gameplay-scene"));

                Assert.That(SceneEntryPresentationRegistry.IsActive, Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.FailedHoldingCover));
                Assert.That(coordinator.IsTransitionInProgress, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_PreCoroutineFailure_PreservesRouterAdvancedSessionAndExistingLaunchContext()
        {
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                6105,
                "advanced-session-preservation-test");
            var requestedStage = StageId.CreateOrThrow("stage-0-1");
            var existingStage = StageId.CreateOrThrow("stage-0-2");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ManualRetry,
                    requestedStage,
                    generation,
                    out var token),
                Is.True);
            StageLaunchContextStore.SetCurrent(existingStage);
            void AdvanceAfterBind(SceneEntryPresentationSnapshot session)
            {
                if (session.Token == token &&
                    session.Phase ==
                    SceneEntryPresentationPhase.PersistentCoverRequested)
                {
                    SceneEntryPresentationRegistry.TryAdvance(
                        token,
                        SceneEntryPresentationPhase.PersistentCoverReady);
                }
            }

            SceneEntryPresentationRegistry.ReadModel.Changed += AdvanceAfterBind;
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_PreCoroutineFailure_PreservesRouterAdvancedSessionAndExistingLaunchContext));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    requestedStage,
                    StageNavigationKind.Retry,
                    "stage-result-retry",
                    StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                    SceneTransitionIntent.ManualRetry);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "unused-gameplay-scene"));

                Assert.That(exception.Message, Does.Contain("already owns the context"));
                Assert.That(SceneEntryPresentationRegistry.Current.Token, Is.EqualTo(token));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.PersistentCoverReady));
                Assert.That(coordinator.IsTransitionInProgress, Is.True);
                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(existingStage));
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -= AdvanceAfterBind;
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_PreCoroutineFailure_PreservesNewerSessionAndClearsOnlyFailedSnapshot()
        {
            var generation = TerminalSessionRegistry.Authority.RegisterSceneBootstrap(
                6106,
                "newer-session-preservation-test");
            var requestedStage = StageId.CreateOrThrow("stage-0-1");
            var existingStage = StageId.CreateOrThrow("stage-0-2");
            Assert.That(
                SceneEntryPresentationRegistry.TryClaim(
                    SceneTransitionIntent.ManualRetry,
                    requestedStage,
                    generation,
                    out var failedToken),
                Is.True);
            StageLaunchContextStore.SetCurrent(existingStage);
            var newerToken = default(SceneEntrySessionToken);
            void ReplaceAfterBind(SceneEntryPresentationSnapshot session)
            {
                if (session.Token != failedToken ||
                    session.Phase !=
                    SceneEntryPresentationPhase.PersistentCoverRequested)
                {
                    return;
                }

                SceneEntryPresentationRegistry.ResetForTests();
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        requestedStage,
                        generation,
                        out var supersededToken),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryCancelClaim(
                        supersededToken),
                    Is.True);
                Assert.That(
                    SceneEntryPresentationRegistry.TryClaim(
                        SceneTransitionIntent.ManualRetry,
                        requestedStage,
                        generation,
                        out newerToken),
                    Is.True);
            }

            SceneEntryPresentationRegistry.ReadModel.Changed += ReplaceAfterBind;
            var coordinatorObject = new GameObject(nameof(
                SceneTransitionCoordinator_PreCoroutineFailure_PreservesNewerSessionAndClearsOnlyFailedSnapshot));
            coordinatorObject.SetActive(false);
            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var request = new StageNavigationRequest(
                    requestedStage,
                    StageNavigationKind.Retry,
                    "stage-result-retry",
                    StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual),
                    SceneTransitionIntent.ManualRetry);

                Assert.Throws<InvalidOperationException>(() =>
                    coordinator.TryStartStageTransition(request, "unused-gameplay-scene"));

                Assert.That(newerToken.IsValid, Is.True);
                Assert.That(SceneEntryPresentationRegistry.Current.Token, Is.EqualTo(newerToken));
                Assert.That(
                    SceneEntryPresentationRegistry.Current.Phase,
                    Is.EqualTo(SceneEntryPresentationPhase.Claimed));
                Assert.That(coordinator.IsTransitionInProgress, Is.False);
                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(existingStage));
                Assert.Throws<InvalidOperationException>(() =>
                    GameplayEntryTransitionVisualSnapshotRegistry.Require(
                        failedToken,
                        SceneTransitionIntent.ManualRetry));
            }
            finally
            {
                SceneEntryPresentationRegistry.ReadModel.Changed -= ReplaceAfterBind;
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void SceneTransitionCoordinator_PreCoroutineSetup_CapturesBeforeBindInsideSharedFailureBoundary()
        {
            var source = ReadRepoFile(
                "Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var setupTryIndex = source.IndexOf(
                "try\n            {\n                if (routePolicy.ImplementsSceneTransitionSession",
                StringComparison.Ordinal);
            var gameplayCaptureIndex = source.IndexOf(
                "GameplayEntryTransitionVisualSnapshotRegistry.Capture(",
                setupTryIndex,
                StringComparison.Ordinal);
            var gameplayBindIndex = source.IndexOf(
                "SceneEntryPresentationRegistry.TryBindTransition(",
                gameplayCaptureIndex,
                StringComparison.Ordinal);
            var mainMenuCaptureIndex = source.IndexOf(
                "MainMenuTransitionVisualPolicy.Capture(",
                gameplayBindIndex,
                StringComparison.Ordinal);
            var mainMenuBindIndex = source.IndexOf(
                "MainMenuEntryPresentationRegistry.TryBindTransition(",
                mainMenuCaptureIndex,
                StringComparison.Ordinal);
            var terminalBindIndex = source.IndexOf(
                "TerminalSessionRegistry.Authority.TryBindTransition(",
                mainMenuBindIndex,
                StringComparison.Ordinal);
            var coroutineStartIndex = source.IndexOf(
                "StartCoroutine(RunTransition(",
                terminalBindIndex,
                StringComparison.Ordinal);
            var setupCatchIndex = source.IndexOf(
                "catch\n            {\n                ClearFailedCampaignLaunch(launchContext",
                coroutineStartIndex,
                StringComparison.Ordinal);

            Assert.That(setupTryIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(gameplayCaptureIndex, Is.GreaterThan(setupTryIndex));
            Assert.That(gameplayBindIndex, Is.GreaterThan(gameplayCaptureIndex));
            Assert.That(mainMenuCaptureIndex, Is.GreaterThan(gameplayBindIndex));
            Assert.That(mainMenuBindIndex, Is.GreaterThan(mainMenuCaptureIndex));
            Assert.That(terminalBindIndex, Is.GreaterThan(mainMenuBindIndex));
            Assert.That(coroutineStartIndex, Is.GreaterThan(terminalBindIndex));
            Assert.That(setupCatchIndex, Is.GreaterThan(coroutineStartIndex));
            Assert.That(source, Does.Contain("current.Token != expected.Token"));
            Assert.That(
                source,
                Does.Contain(
                    "current.Phase == SceneEntryPresentationPhase.Claimed"));
            Assert.That(
                source,
                Does.Contain(
                    "SceneEntryPresentationPhase.PersistentCoverRequested"));
        }

        [Test]
        public void LaunchGuard_PrecedesSetCurrent()
        {
            var source = ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/SceneTransitionCoordinator.cs");
            var guardIndex = source.IndexOf("_guard.TryBegin", StringComparison.Ordinal);
            var beforeLoadIndex = source.IndexOf("beforeLoad?.Invoke()", StringComparison.Ordinal);

            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(beforeLoadIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(guardIndex, Is.LessThan(beforeLoadIndex));
        }

        [Test]
        public void DuplicateLaunch_DoesNotOverwriteStageContext()
        {
            var guard = new StageTransitionLaunchGuard();
            StageLaunchContextStore.Clear();
            var firstStage = StageId.CreateOrThrow("stage-0-1");
            var secondStage = StageId.CreateOrThrow("stage-0-2");

            Assert.That(guard.TryBegin(out var transitionId), Is.True);
            StageLaunchContextStore.SetCurrent(firstStage);
            Assert.That(guard.TryBegin(out _), Is.False);

            Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
            Assert.That(current, Is.EqualTo(firstStage));
            Assert.That(current, Is.Not.EqualTo(secondStage));

            guard.Complete(transitionId);
            StageLaunchContextStore.Clear();
        }

        [Test]
        public void TransitionGuardRejection_PreservesPendingAndDoesNotOverwriteStageContext()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var firstStage = StageId.CreateOrThrow("stage-0-1");
            var rejectedStage = StageId.CreateOrThrow("stage-0-2");
            var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
            var guardField = typeof(SceneTransitionCoordinator).GetField(
                "_guard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(guardField, Is.Not.Null);
            var guard = (StageTransitionLaunchGuard)guardField.GetValue(coordinator);
            Assert.That(guard.TryBegin(out var transitionId), Is.True);
            StageLaunchContextStore.SetCurrent(firstStage);

            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    rejectedStage,
                    StageNavigationKind.Continue,
                    "guard-rejection",
                    out var handoff),
                Is.True);

            try
            {
                var accepted = coordinator.TryStartStageTransition(
                    new StageNavigationRequest(
                        rejectedStage,
                        StageNavigationKind.Continue,
                        "guard-rejection",
                        transitionIntent: SceneTransitionIntent.GameplayEntry),
                    "unused-scene",
                    handoff.Token);

                Assert.That(accepted, Is.False);
                Assert.That(coordinator.LastResolvedRoutePolicy.HasValue, Is.True);
                Assert.That(
                    coordinator.LastResolvedRoutePolicy.Value.Intent,
                    Is.EqualTo(SceneTransitionIntent.GameplayEntry));
                Assert.That(handoffStore.TryPeek(out var stillPending), Is.True);
                Assert.That(stillPending, Is.SameAs(handoff));
                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(firstStage));
            }
            finally
            {
                guard.Complete(transitionId);
                StageLaunchContextStore.Clear();
                CampaignLaunchHandoffSessionStore.ResetForTests();
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        [Test]
        public void TransitionRequestMismatch_WithMatchingToken_ClearsPendingWithoutMutatingStageContext()
        {
            CampaignLaunchHandoffSessionStore.ResetForTests();
            var coordinatorObject = new GameObject("Coordinator");
            coordinatorObject.SetActive(false);
            var currentStage = StageId.CreateOrThrow("stage-0-1");
            var handoffStage = StageId.CreateOrThrow("stage-0-2");
            var mismatchedStage = StageId.CreateOrThrow("stage-1-1");
            StageLaunchContextStore.SetCurrent(currentStage);
            var handoffStore = CampaignLaunchHandoffSessionStore.Instance;
            Assert.That(
                handoffStore.TryBegin(
                    1,
                    handoffStage,
                    StageNavigationKind.Continue,
                    "matching-token-mismatch",
                    out var handoff),
                Is.True);

            try
            {
                var coordinator = coordinatorObject.AddComponent<SceneTransitionCoordinator>();
                var accepted = coordinator.TryStartStageTransition(
                    new StageNavigationRequest(
                        mismatchedStage,
                        StageNavigationKind.Continue,
                        "matching-token-mismatch",
                        transitionIntent: SceneTransitionIntent.GameplayEntry),
                    "unused-scene",
                    handoff.Token);

                Assert.That(accepted, Is.False);
                Assert.That(handoffStore.TryPeek(out _), Is.False);
                Assert.That(StageLaunchContextStore.CurrentStageId, Is.EqualTo(currentStage));
            }
            finally
            {
                StageLaunchContextStore.Clear();
                CampaignLaunchHandoffSessionStore.ResetForTests();
                UnityEngine.Object.DestroyImmediate(coordinatorObject);
            }
        }

        private static CampaignSlotState SeedSlot(
            ICampaignSlotSeedImportPort store,
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances = CampaignSaveSlotPolicy.DefaultRemainingChances)
        {
            return store.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                slotNumber,
                StageId.CreateOrThrow(stageId),
                levelGroupId,
                remainingChances,
                DateTimeOffset.UtcNow.ToString("O")));
        }

        private static void CompleteCampaign(TransientCampaignSaveSlotStore store)
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var finalStageId = StageId.CreateOrThrow("stage-4-3");
            SeedSlot(store, 1, finalStageId.Value, resolver.GetLevelGroupId(finalStageId));
            store.CommitStageClear(
                1,
                new CampaignStageClearCommitRequest
                {
                    Plan = new CampaignProgressionTransitionPlanner(resolver)
                        .PlanStageClear(finalStageId),
                });
        }

        private static ControllerHarness CreateControllerHarness(params string[] catalogStageIds)
        {
            var provider = CreateProvider(catalogStageIds);
            var saveKey = CreateTransientNamespace("saves");
            var activeKey = CreateTransientNamespace("active");
            var saveStore = new TransientCampaignSaveSlotStore(saveKey);
            var activeSlotStorage = new TransientActiveSlotStorage(activeKey);
            var activeSlotProvider = new ActiveSlotProvider(activeSlotStorage);
            var launchHandoffStore = new RecordingCampaignLaunchHandoffStore();
            var repairingStore = new CampaignLaunchStateRepairingCampaignSaveSlotStore(
                saveStore,
                activeSlotStorage,
                launchHandoffStore);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            var resolver = new CampaignStageSequenceResolver(LoadProductionCampaignSequence());
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var launchEvaluator = new CampaignSlotLaunchEvaluator(resolver, provider.Provider);
            var controller = new MainMenuController(
                repairingStore,
                repairingStore,
                repairingStore,
                launchHandoffStore,
                resolver,
                launchEvaluator,
                router,
                confirmPort);
            return new ControllerHarness(
                provider,
                saveStore,
                activeSlotProvider,
                launchHandoffStore,
                confirmPort,
                router,
                controller);
        }

        private static CurrentSceneGuardRejectionResult RunCurrentSceneGuardRejection(
            bool injectPendingDuringAttempt)
        {
            StageLaunchContextStore.Clear();
            CampaignLaunchHandoffSessionStore.ResetForTests();
            var originalContext = new StageLaunchContext(
                Guid.NewGuid(),
                1,
                StageId.CreateOrThrow("stage-0-2"),
                StageNavigationKind.Continue,
                "existing-owner");
            Assert.That(StageLaunchContextStore.TrySetCurrent(originalContext), Is.True);
            var transitionAttemptCount = 0;
            var loadStartCount = 0;
            CampaignLaunchHandoff pendingCreatedDuringAttempt = null;
            var router = new CurrentSceneStageLaunchRouter(
                "UIAudioScene",
                sceneLoadPort: null,
                tryStartStageTransition: (_, __) =>
                {
                    transitionAttemptCount++;
                    if (injectPendingDuringAttempt)
                    {
                        Assert.That(
                            CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                                1,
                                StageId.CreateOrThrow("stage-0-1"),
                                StageNavigationKind.Continue,
                                "concurrent-owner",
                                out pendingCreatedDuringAttempt),
                            Is.True);
                    }

                    return false;
                });

            var exception = Assert.Throws<InvalidOperationException>(() => router.Launch(
                new StageNavigationRequest(
                    StageId.CreateOrThrow("stage-0-1"),
                    StageNavigationKind.Retry,
                    "stage-result-retry")));
            return new CurrentSceneGuardRejectionResult(
                exception,
                originalContext,
                pendingCreatedDuringAttempt,
                transitionAttemptCount,
                loadStartCount);
        }

        private static ProviderHarness CreateProvider(params string[] stageIds)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalog>();
            var provider = ScriptableObject.CreateInstance<ScriptableObjectStageCatalogProvider>();
            var entries = new StageContentEntry[stageIds.Length];
            for (var i = 0; i < stageIds.Length; i++)
            {
                entries[i] = ScriptableObject.CreateInstance<StageContentEntry>();
                entries[i].AssignStageId(StageId.CreateOrThrow(stageIds[i]));
            }

            catalog.SetEntries(entries);
            provider.AssignCatalog(catalog);
            return new ProviderHarness(provider, catalog, entries);
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static void AssertSerializedReference(SerializedObject serializedObject, string propertyName, Type expectedType)
        {
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
            Assert.That(expectedType.IsInstanceOfType(property.objectReferenceValue), Is.True, propertyName);
        }

        private static string CreateTransientNamespace(string suffix)
        {
            return "Game.Feature.UI.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private static void AssertConfiguredGameplayLaunchClearsStaleDirectPlayContext(
            EditorDirectPlayContext staleContext)
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var stageId = StageId.CreateOrThrow("stage-0-1");
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                EditorDirectPlayContextStore.SetCurrent(staleContext);
                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(staleContext.Mode));
                Assert.That(
                    CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                        1,
                        stageId,
                        StageNavigationKind.Continue,
                        "test",
                        out _),
                    Is.True);

                var sceneLoader = new FakeSceneLoadPort(sceneName =>
                {
                    Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(EditorDirectPlayMode.None));
                    Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
                    Assert.That(current, Is.EqualTo(stageId));
                    Assert.That(sceneName, Is.EqualTo("UIAudioScene"));
                });

                new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(
                    new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test"));

                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(EditorDirectPlayMode.None));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out var currentAfterLaunch), Is.True);
                Assert.That(currentAfterLaunch, Is.EqualTo(stageId));
                Assert.That(sceneLoader.LoadedScenes, Is.EqualTo(new[] { "UIAudioScene" }));
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
        }

        private static void AssertProductionMainMenuLaunchWithStaleContextInjectsChanceReadSource(
            EditorDirectPlayContext staleContext)
        {
            var routeConfig = ScriptableObject.CreateInstance<GameplayStageLaunchRouteConfig>();
            var installerObject = new GameObject("ProductionMainMenuLaunch_WithStaleDirectPlayContext_InjectsChanceReadSource");
            var stageId = StageId.CreateOrThrow(CombinedStageId);
            var saveHarness = new TemporaryProductionSaveHarness();
            var tempTestRoot = saveHarness.TestRootPath;
            try
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                saveHarness.PrepareDefaultSlot(stageId, remainingChances: 2);
                EditorDirectPlayContextStore.SetCurrent(staleContext);
                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(staleContext.Mode));
                Assert.That(
                    CampaignLaunchHandoffSessionStore.Instance.TryBegin(
                        1,
                        stageId,
                        StageNavigationKind.Continue,
                        "test",
                        out _),
                    Is.True);

                var sceneLoader = new FakeSceneLoadPort();
                new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(
                    new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test"));

                Assert.That(EditorDirectPlayContextStore.GetCurrentOrNone().Mode, Is.EqualTo(EditorDirectPlayMode.None));
                Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
                Assert.That(current, Is.EqualTo(stageId));
                Assert.That(sceneLoader.LoadedScenes, Is.EqualTo(new[] { "UIAudioScene" }));

                var installer = installerObject.AddComponent<StageBackedGameplaySceneInstaller>();
                var uiInstaller = installerObject.AddComponent<GameplayUiFlowInstaller>();
                UiTestPrefabAssetUtility.AssignCanonicalUiPrefabs(uiInstaller);
                DisableAutoCreateViews(installer);
                AssignStageCatalogProvider(installer);
                AssignTimingPresets(installer);
                AssignCampaignStores(installer, saveHarness.SaveStore, saveHarness.ActiveSlotProvider);
                var configuration = BuildConfiguration(installer);

                Assert.That(configuration.CampaignChancesReadSource, Is.Not.Null);
                Assert.That(
                    configuration.CampaignChancesReadSource.TryReadChances(
                        out var remaining,
                        out var max,
                        out var audioPolicy),
                    Is.True);
                Assert.That(remaining, Is.GreaterThanOrEqualTo(0));
                Assert.That(max, Is.GreaterThan(0));
                Assert.That(audioPolicy, Is.EqualTo(GameplayChanceAudioPolicy.Default));

                var host = installerObject.AddComponent<GameplaySceneHost>();
                host.Initialize(configuration);
                var playerHud = host.UiAccess.QueryFacade.PlayerHud.Read();

                Assert.That(playerHud.HasRemainingChances, Is.True);
                Assert.That(playerHud.MaxChances, Is.GreaterThan(0));

                uiInstaller.Install(host);
                var chancePanelView = uiInstaller.HudView.ChancePanelView;
                var chancePanelRoot = GetPrivateField<GameObject>(chancePanelView, "_root");

                Assert.That(chancePanelView.ViewModel.HasChances, Is.True);
                Assert.That(chancePanelRoot.activeSelf, Is.True);
            }
            finally
            {
                CampaignLaunchHandoffSessionStore.ResetForTests();
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                UnityEngine.Object.DestroyImmediate(installerObject);
                UnityEngine.Object.DestroyImmediate(routeConfig);
                saveHarness.Dispose();
                Assert.That(Directory.Exists(tempTestRoot), Is.False, tempTestRoot);
            }
        }

        private static void AssignCampaignStores(
            StageBackedGameplaySceneInstaller installer,
            ICampaignSaveQuery saveStore,
            ActiveSlotProvider activeSlotProvider)
        {
            var saveStoreField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "_saveSlotStore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(saveStoreField, Is.Not.Null);
            saveStoreField.SetValue(installer, saveStore);

            var activeSlotProviderField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "_activeSlotProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeSlotProviderField, Is.Not.Null);
            activeSlotProviderField.SetValue(installer, activeSlotProvider);
        }

        private static void AssignStageCatalogProvider(StageBackedGameplaySceneInstaller installer)
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageCatalogProviderAssetPath);
            Assert.That(provider, Is.Not.Null, $"Missing stage catalog provider at '{StageCatalogProviderAssetPath}'.");

            var providerField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "stageCatalogProvider",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(providerField, Is.Not.Null);
            providerField.SetValue(installer, provider);
        }

        private static CampaignStageSequenceDefinition LoadProductionCampaignSequence()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            Assert.That(
                definition,
                Is.Not.Null,
                $"Missing campaign sequence at '{StageContentPaths.CampaignStageSequenceAssetPath}'.");
            return definition;
        }

        private static CampaignStageSequenceEntry CreateCampaignSequenceEntry(
            string stageId,
            string levelGroupId)
        {
            var entry = new CampaignStageSequenceEntry();
            entry.Set(
                StageId.CreateOrThrow(stageId),
                levelGroupId);
            return entry;
        }

        private static void DisableAutoCreateViews(StageBackedGameplaySceneInstaller installer)
        {
            var autoCreateViewsField = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                "autoCreateViews",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(autoCreateViewsField, Is.Not.Null);
            autoCreateViewsField.SetValue(installer, false);
        }

        private static void AssignTimingPresets(StageBackedGameplaySceneInstaller installer)
        {
            var simulationPreset = AssetDatabase.LoadAssetAtPath<GameplaySimulationTimingPreset>(
                DefaultSimulationTimingPresetAssetPath);
            Assert.That(
                simulationPreset,
                Is.Not.Null,
                $"Missing simulation timing preset asset at '{DefaultSimulationTimingPresetAssetPath}'.");

            var presentationPreset = AssetDatabase.LoadAssetAtPath<GameplayPresentationTimingPreset>(
                DefaultPresentationTimingPresetAssetPath);
            Assert.That(
                presentationPreset,
                Is.Not.Null,
                $"Missing presentation timing preset asset at '{DefaultPresentationTimingPresetAssetPath}'.");

            var simulationField = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                "simulationTimingPreset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(simulationField, Is.Not.Null);
            simulationField.SetValue(installer, simulationPreset);

            var presentationField = typeof(GameplayShowcaseSceneInstallerBase).GetField(
                "presentationTimingPreset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(presentationField, Is.Not.Null);
            presentationField.SetValue(installer, presentationPreset);
        }

        private static GameplaySceneHostConfiguration BuildConfiguration(StageBackedGameplaySceneInstaller installer)
        {
            EnsureCameraTopologyAuthoring(installer);

            var initialState = BuildInitialGameplayState(installer);
            var createConfigurationMethod = typeof(GameplayShowcaseSceneInstallerBase).GetMethod(
                "CreateConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { initialState.GetType(), typeof(GameplayCameraSettings) },
                modifiers: null);

            Assert.That(createConfigurationMethod, Is.Not.Null);
            return (GameplaySceneHostConfiguration)createConfigurationMethod.Invoke(
                installer,
                new object[] { initialState, installer.GetCameraSettings() });
        }

        private static object BuildInitialGameplayState(StageBackedGameplaySceneInstaller installer)
        {
            var sequenceField = typeof(StageBackedGameplaySceneInstallerBase).GetField(
                "campaignStageSequenceDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sequenceField, Is.Not.Null);
            sequenceField.SetValue(installer, LoadProductionCampaignSequence());

            var buildInitialStateMethod = typeof(StageBackedGameplaySceneInstallerBase).GetMethod(
                "BuildInitialGameplayState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(buildInitialStateMethod, Is.Not.Null);
            return buildInitialStateMethod.Invoke(installer, Array.Empty<object>());
        }

        private static GameplayCameraTopologyAuthoring EnsureCameraTopologyAuthoring(Component owner)
        {
            return owner.GetComponent<GameplayCameraTopologyAuthoring>() ??
                   owner.gameObject.AddComponent<GameplayCameraTopologyAuthoring>();
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

        private static Dictionary<string, byte[]> CaptureFileSetSnapshot(string rootPath)
        {
            var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (!Directory.Exists(rootPath))
            {
                return snapshot;
            }

            var rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
            foreach (var filePath in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = filePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? filePath.Substring(rootPrefix.Length)
                    : filePath;
                snapshot[relativePath.Replace('\\', '/')] = File.ReadAllBytes(filePath);
            }

            return snapshot;
        }

        private static void AssertFileSetSnapshotEqual(
            IReadOnlyDictionary<string, byte[]> expected,
            IReadOnlyDictionary<string, byte[]> actual)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var pair in expected)
            {
                Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key);
            }
        }

        private sealed class FakeConfirmPopupPort : IConfirmPopupPort
        {
            private Action<bool> _completion;

            public void Request(ConfirmPopupPayload payload, Action<bool> completion)
            {
                _completion = completion;
            }

            public void Complete(bool confirmed)
            {
                _completion?.Invoke(confirmed);
                _completion = null;
            }
        }

        private readonly struct CurrentSceneGuardRejectionResult
        {
            public CurrentSceneGuardRejectionResult(
                Exception exception,
                StageLaunchContext originalContext,
                CampaignLaunchHandoff pendingCreatedDuringAttempt,
                int transitionAttemptCount,
                int loadStartCount)
            {
                Exception = exception;
                OriginalContext = originalContext;
                PendingCreatedDuringAttempt = pendingCreatedDuringAttempt;
                TransitionAttemptCount = transitionAttemptCount;
                LoadStartCount = loadStartCount;
            }

            public Exception Exception { get; }
            public StageLaunchContext OriginalContext { get; }
            public CampaignLaunchHandoff PendingCreatedDuringAttempt { get; }
            public int TransitionAttemptCount { get; }
            public int LoadStartCount { get; }
        }

        private sealed class FakeStageLaunchRouter : IStageLaunchRouter
        {
            private readonly List<StageNavigationRequest> _requests = new();

            public IReadOnlyList<StageNavigationRequest> Requests => _requests;

            public void Launch(StageNavigationRequest request)
            {
                _requests.Add(request);
            }
        }

        private sealed class FakeSceneLoadPort : ISceneLoadPort
        {
            private readonly List<string> _loadedScenes = new();
            private readonly Action<string> _beforeLoad;

            public FakeSceneLoadPort(Action<string> beforeLoad = null)
            {
                _beforeLoad = beforeLoad;
            }

            public IReadOnlyList<string> LoadedScenes => _loadedScenes;

            public void LoadScene(string sceneName)
            {
                _beforeLoad?.Invoke(sceneName);
                _loadedScenes.Add(sceneName);
            }
        }

        private sealed class TemporaryProductionSaveHarness : IDisposable
        {
            private readonly IAtomicTextFileStore _localStateTextFileStore;
            private readonly IAtomicTextFileStore _profileTextFileStore;

            public TemporaryProductionSaveHarness()
            {
                var id = Guid.NewGuid().ToString("N");
                TestRootPath = Path.Combine("Temp", "CampaignProductionEntryTests", id);
                SaveRootPath = Path.Combine(TestRootPath, "Saves");
                var options = CampaignSaveCompositionProvider.CreateProductionProfileBackedOptions();
                options.PathProvider = new TemporarySavePathProvider(SaveRootPath);

                var facade = CampaignSaveFacadeFactory.Create(options);
                SaveStore = facade.CampaignSaveSlots;
                _profileTextFileStore = facade.ProfileServices.TextFileStore;
                _localStateTextFileStore = new AtomicTextFileStore(SaveRootPath);
                ActiveSlotStorage = new LocalStateActiveSlotStorage(
                    new FileCampaignLocalLaunchStateRepository(_localStateTextFileStore),
                    SaveStore);
                ActiveSlotProvider = new ActiveSlotProvider(ActiveSlotStorage);
                RecoveryPort = new CampaignLaunchStateRepairingCampaignSaveRecoveryPort(
                    facade.ProfileServices.Recovery,
                    ActiveSlotStorage,
                    CampaignLaunchHandoffSessionStore.Instance);
            }

            public string TestRootPath { get; }

            public string SaveRootPath { get; }

            public ICampaignSaveRuntime SaveStore { get; }

            public ActiveSlotProvider ActiveSlotProvider { get; }

            public IActiveSlotStorage ActiveSlotStorage { get; }

            public ICampaignSaveRecoveryPort RecoveryPort { get; }

            public void PrepareDefaultSlot(StageId stageId, int remainingChances)
            {
                var originalSlot = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = stageId,
                    CurrentLevelGroupId = "level-01",
                    RemainingChances = remainingChances,
                    LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
                };

                SaveStore.ClearAll();
                ActiveSlotProvider.ClearActiveSlot();
                SaveStore.ImportSlotSeed(
                    CampaignSlotRawDataMapper.ToSeedImportRequest(originalSlot));
                ActiveSlotProvider.SetActiveSlot(1);

                Assert.That(SaveStore.LoadSlot(1).State.RemainingChances, Is.EqualTo(remainingChances));
                Assert.That(ActiveSlotProvider.TryGetActiveSlotNumber(out var activeSlot), Is.True);
                Assert.That(activeSlot, Is.EqualTo(1));
                AssertExpectedFileSet();

                SeedSlot(
                    SaveStore,
                    1,
                    stageId.Value,
                    "level-01",
                    remainingChances - 1);
                Assert.That(
                    _profileTextFileStore.TryRestoreBackup(FileCampaignProfileRepository.ProfileFileName),
                    Is.True);
                Assert.That(SaveStore.LoadSlot(1).State.RemainingChances, Is.EqualTo(remainingChances));

                ActiveSlotProvider.ClearActiveSlot();
                Assert.That(
                    _localStateTextFileStore.TryRestoreBackup(CampaignLocalLaunchStateRepository.FileName),
                    Is.True);
                Assert.That(ActiveSlotProvider.TryGetActiveSlotNumber(out activeSlot), Is.True);
                Assert.That(activeSlot, Is.EqualTo(1));
                AssertExpectedFileSet();
            }

            public void Dispose()
            {
                if (Directory.Exists(TestRootPath))
                {
                    Directory.Delete(TestRootPath, recursive: true);
                }
            }

            private void AssertExpectedFileSet()
            {
                var actual = Directory.GetFiles(SaveRootPath, "*", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < actual.Length; i++)
                {
                    actual[i] = Path.GetFileName(actual[i]);
                }

                Assert.That(
                    actual,
                    Is.EquivalentTo(new[]
                    {
                        FileCampaignProfileRepository.ProfileFileName,
                        FileCampaignProfileRepository.ProfileFileName + ".bak",
                        CampaignLocalLaunchStateRepository.FileName,
                        CampaignLocalLaunchStateRepository.FileName + ".bak",
                    }));
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
            }
        }

        private sealed class ControllerHarness : IDisposable
        {
            private readonly ProviderHarness _provider;

            public ControllerHarness(
                ProviderHarness provider,
                TransientCampaignSaveSlotStore saveStore,
                ActiveSlotProvider activeSlotProvider,
                RecordingCampaignLaunchHandoffStore launchHandoffStore,
                FakeConfirmPopupPort confirmPort,
                FakeStageLaunchRouter router,
                MainMenuController controller)
            {
                _provider = provider;
                SaveStore = saveStore;
                ActiveSlotProvider = activeSlotProvider;
                LaunchHandoffStore = launchHandoffStore;
                ConfirmPort = confirmPort;
                Router = router;
                Controller = controller;
            }

            public TransientCampaignSaveSlotStore SaveStore { get; }

            public ActiveSlotProvider ActiveSlotProvider { get; }

            public RecordingCampaignLaunchHandoffStore LaunchHandoffStore { get; }

            public FakeConfirmPopupPort ConfirmPort { get; }

            public FakeStageLaunchRouter Router { get; }

            public MainMenuController Controller { get; }

            public void Dispose()
            {
                SaveStore.ClearAll();
                ActiveSlotProvider.ClearActiveSlot();
                if (LaunchHandoffStore.TryPeek(out var handoff))
                {
                    LaunchHandoffStore.TryClear(handoff.Token);
                }
                _provider.Dispose();
            }
        }

        private sealed class ProviderHarness : IDisposable
        {
            private readonly StageCatalog _catalog;
            private readonly StageContentEntry[] _entries;

            public ProviderHarness(
                ScriptableObjectStageCatalogProvider provider,
                StageCatalog catalog,
                StageContentEntry[] entries)
            {
                Provider = provider;
                _catalog = catalog;
                _entries = entries;
            }

            public ScriptableObjectStageCatalogProvider Provider { get; }

            public void Dispose()
            {
                for (var i = 0; i < _entries.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(_entries[i]);
                }

                UnityEngine.Object.DestroyImmediate(Provider);
                UnityEngine.Object.DestroyImmediate(_catalog);
            }
        }
    }

    internal sealed class CampaignProductionEntryTerminalSessionAuthorityProvider : MonoBehaviour,
        ITerminalSessionAuthorityProvider
    {
        public bool TryGetTerminalSessionAuthority(
            out ITerminalSessionReadModel readModel,
            out ITerminalSessionAuthority authority)
        {
            authority = TerminalSessionRegistry.Authority;
            readModel = authority;
            return true;
        }
    }
}
