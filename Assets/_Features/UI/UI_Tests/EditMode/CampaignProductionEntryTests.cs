using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
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
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<SettingsScreenView>(SettingsScreenPrefabPath);
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);

            try
            {
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_mainMenuScreenPrefab", prefab);
                SetPrivateField(installer, "_settingsScreenPrefab", settingsPrefab);
                SetPrivateField(installer, "_popupPrefabCatalog", catalog);
                SetPrivateField(installer, "_uiAudioCueMap", uiAudioCueMap);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);
                SetPrivateField(installer, "_campaignStageSequenceDefinition", CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());

                installer.Install();

                Assert.That(installer.Controller, Is.Not.Null);
                Assert.That(installer.HubController, Is.Not.Null);
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
            var catalog = AssetDatabase.LoadAssetAtPath<PopupPrefabCatalog>(PopupCatalogPath);
            var uiAudioCueMap = AssetDatabase.LoadAssetAtPath<UiAudioCueMap>(UiAudioCueMapPath);
            var prefab = AssetDatabase.LoadAssetAtPath<MainMenuScreenView>(MainMenuScreenPrefabPath);
            var settingsPrefab = AssetDatabase.LoadAssetAtPath<SettingsScreenView>(SettingsScreenPrefabPath);
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(RouteConfigPath);

            try
            {
                var installer = root.AddComponent<MainMenuUiFlowInstaller>();
                root.AddComponent<AudioRuntimeInstaller>();
                root.AddComponent<DisplayRuntimeInstaller>();
                SetPrivateField(installer, "_installOnStart", false);
                SetPrivateField(installer, "_mainMenuScreenPrefab", prefab);
                SetPrivateField(installer, "_settingsScreenPrefab", settingsPrefab);
                SetPrivateField(installer, "_popupPrefabCatalog", catalog);
                SetPrivateField(installer, "_uiAudioCueMap", uiAudioCueMap);
                SetPrivateField(installer, "_routeConfig", routeConfig);
                SetPrivateField(installer, "_stageCatalogProvider", provider.Provider);
                SetPrivateField(installer, "_campaignStageSequenceDefinition", CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());

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
                UnityEngine.Object.DestroyImmediate(root);
                provider.Dispose();
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
        public void EmptySlot_NewGame_InitializesFirstStage_SetsActiveSlot_AndLaunches()
        {
            var harness = CreateControllerHarness("stage-0-1");
            try
            {
                harness.Controller.HandleIntent(new SaveSlotIntent(1, SaveSlotIntentKind.NewGame));

                Assert.That(harness.SaveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(harness.ActiveSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
                Assert.That(harness.Router.Requests.Count, Is.EqualTo(1));
                Assert.That(harness.Router.Requests[0].StageId.Value, Is.EqualTo("stage-0-1"));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void ExistingValidSlot_Continue_ValidatesSyncsActiveSlot_AndLaunchesSavedStage()
        {
            var harness = CreateControllerHarness("stage-2-2");
            try
            {
                harness.SaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 2,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "stale",
                });

                harness.Controller.Continue(2);

                Assert.That(harness.SaveStore.LoadSlot(2).CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(harness.ActiveSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(harness.Router.Requests[0].StageId.Value, Is.EqualTo("stage-2-2"));
            }
            finally
            {
                harness.Dispose();
            }
        }

        [Test]
        public void CompletedSlot_Continue_IsRejectedAtControllerLevel()
        {
            var harness = CreateControllerHarness("stage-5-1");
            try
            {
                harness.SaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-5-1"),
                    CurrentLevelGroupId = "level-5",
                    CampaignCompleted = true,
                });

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
            var harness = CreateControllerHarness("stage-0-1", "stage-5-1");
            try
            {
                harness.SaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-5-1"),
                    CurrentLevelGroupId = "level-5",
                    CampaignCompleted = true,
                });

                harness.Controller.RequestRestart(1);
                harness.ConfirmPort.Complete(false);
                Assert.That(harness.SaveStore.LoadSlot(1).CampaignCompleted, Is.True);
                Assert.That(harness.Router.Requests, Is.Empty);

                harness.Controller.RequestRestart(1);
                harness.ConfirmPort.Complete(true);
                Assert.That(harness.SaveStore.LoadSlot(1).CampaignCompleted, Is.False);
                Assert.That(harness.SaveStore.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-0-1"));
                Assert.That(harness.Router.Requests.Count, Is.EqualTo(1));
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
                harness.SaveStore.SaveSlot(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-9-9"),
                    CurrentLevelGroupId = "level-9",
                });

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
                harness.SaveStore.SaveSlot(new SaveSlotData { SlotNumber = 1, CurrentStageId = StageId.CreateOrThrow("stage-0-1") });
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
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                var stageId = StageId.CreateOrThrow("stage-0-1");
                new ConfiguredGameplayStageLaunchRouter(routeConfig, sceneLoader).Launch(
                    new StageNavigationRequest(stageId, StageNavigationKind.Continue, "test"));

                Assert.That(StageLaunchContextStore.TryGetCurrent(out var current), Is.True);
                Assert.That(current, Is.EqualTo(stageId));
                Assert.That(sceneLoader.LoadedScenes, Is.EqualTo(new[] { "UIAudioScene" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(routeConfig);
            }
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
            var activeKey = CreatePrefsKey("return-active");
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            try
            {
                routeConfig.SetScenePathsForTests(MainMenuScenePath, GameplayShellScenePath);
                activeSlotProvider.SetActiveSlot(2);
                StageLaunchContextStore.SetCurrent(StageId.CreateOrThrow("stage-0-1"));

                new ConfiguredMainMenuReturnRouter(routeConfig, sceneLoader).ReturnToMainMenu();

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
        public void CompletedSlotDisplay_UsesSequenceDisplay_NotHardCodedFinalStageText()
        {
            var definition = ScriptableObject.CreateInstance<CampaignStageSequenceDefinition>();
            try
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(StageId.CreateOrThrow("stage-custom-final"), "Final Custom", "level-custom");
                definition.SetEntries(new[] { entry });
                var resolver = new CampaignStageSequenceResolver(definition);
                var viewModel = MainMenuSlotViewModelMapper.MapSlot(
                    new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-custom-final"),
                        CurrentLevelGroupId = "level-custom",
                        CampaignCompleted = true,
                    },
                    resolver);

                Assert.That(viewModel.StageText, Is.EqualTo("Stage Final Custom"));
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

        private static ControllerHarness CreateControllerHarness(params string[] catalogStageIds)
        {
            var provider = CreateProvider(catalogStageIds);
            var saveKey = CreatePrefsKey("saves");
            var activeKey = CreatePrefsKey("active");
            var saveStore = new SaveSlotStore(saveKey);
            var activeSlotProvider = new ActiveSlotProvider(activeKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            var resolver = new CampaignStageSequenceResolver(CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance());
            var confirmPort = new FakeConfirmPopupPort();
            var router = new FakeStageLaunchRouter();
            var validationService = new SaveSlotValidationService(resolver, provider.Provider);
            var controller = new MainMenuController(
                saveStore,
                activeSlotProvider,
                resolver,
                router,
                confirmPort,
                validationService);
            return new ControllerHarness(
                provider,
                saveStore,
                activeSlotProvider,
                confirmPort,
                router,
                controller);
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

        private static string CreatePrefsKey(string suffix)
        {
            return "Game.Feature.UI.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
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

            public IReadOnlyList<string> LoadedScenes => _loadedScenes;

            public void LoadScene(string sceneName)
            {
                _loadedScenes.Add(sceneName);
            }
        }

        private sealed class ControllerHarness : IDisposable
        {
            private readonly ProviderHarness _provider;

            public ControllerHarness(
                ProviderHarness provider,
                SaveSlotStore saveStore,
                ActiveSlotProvider activeSlotProvider,
                FakeConfirmPopupPort confirmPort,
                FakeStageLaunchRouter router,
                MainMenuController controller)
            {
                _provider = provider;
                SaveStore = saveStore;
                ActiveSlotProvider = activeSlotProvider;
                ConfirmPort = confirmPort;
                Router = router;
                Controller = controller;
            }

            public SaveSlotStore SaveStore { get; }

            public ActiveSlotProvider ActiveSlotProvider { get; }

            public FakeConfirmPopupPort ConfirmPort { get; }

            public FakeStageLaunchRouter Router { get; }

            public MainMenuController Controller { get; }

            public void Dispose()
            {
                SaveStore.ClearAll();
                ActiveSlotProvider.ClearActiveSlot();
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
}
