using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Flow;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Shared.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.UI.Tests
{
    public sealed class UiRepositoryPrefabCatalogSmokeTests
    {
        private const string TransitionContentCatalogPath =
            "Assets/_Features/UI/UI_Composition/Resources/UI/Transitions/SceneTransitionOverlayContentCatalog.asset";

        [Test]
        public void ScreenPrefabCatalog_RepositoryAsset_AllScreenIdsHaveValidPrefab()
        {
            var catalog = UiTestPrefabAssetUtility.LoadScreenCatalog();
            var expectedScreens = Enum.GetValues(typeof(ScreenId))
                .Cast<ScreenId>()
                .Where(screenId => screenId != ScreenId.None && screenId != ScreenId.Gameplay)
                .ToArray();
            var failures = new List<string>();

            foreach (var screenId in expectedScreens)
            {
                try
                {
                    var prefab = ResolveScreenPrefab(catalog, screenId);
                    Assert.That(prefab, Is.Not.Null, screenId.ToString());
                    Assert.That(prefab.GetComponent<IScreenView>(), Is.Not.Null, screenId.ToString());
                    if (prefab is SettingsScreenView settings)
                    {
                        settings.ValidateAuthoredStructureOrThrow();
                        settings.AudioView.ValidateAuthoredControlsOrThrow();
                        settings.DisplayView.ValidateAuthoredControlsOrThrow();
                        settings.InputView.ValidateAuthoredControlsOrThrow();
                    }
                    else if (prefab is StageResultScreenView stageResult)
                    {
                        Assert.That(FindChildByName(stageResult.transform, "ContinueButton (1)"), Is.Null);

                        var continueButton = FindChildByName(stageResult.transform, "ContinueButton");
                        Assert.That(continueButton, Is.Not.Null);
                        Assert.That(FindChildByName(continueButton, "SelectionFrame"), Is.Not.Null);
                    }
                    else if (prefab is GameClearScreenView gameClear)
                    {
                        AssertGameClearResultOnlyPrefab(gameClear);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{screenId}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Screen prefab catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void PopupPrefabCatalog_RepositoryAsset_AllPopupIdsHaveValidPrefab()
        {
            var catalog = UiTestPrefabAssetUtility.LoadPopupCatalog();
            var expectedPopups = Enum.GetValues(typeof(PopupId))
                .Cast<PopupId>()
                .Where(popupId => popupId != PopupId.None && popupId != PopupId.DemoStageControl)
                .ToArray();
            var failures = new List<string>();

            foreach (var popupId in expectedPopups)
            {
                try
                {
                    var prefab = ResolvePopupPrefab(catalog, popupId);
                    Assert.That(prefab, Is.Not.Null, popupId.ToString());
                    Assert.That(prefab.GetComponent<IPopupView>(), Is.Not.Null, popupId.ToString());
                    if (prefab is PausePopupView pausePopup)
                    {
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "ResumeButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "SettingsButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "RetryButton");
                        AssertPausePopupButtonHasSingleHoverScaleEffect(pausePopup, "MainMenuButton");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{popupId}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Popup prefab catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void SceneTransitionContentCatalog_RepositoryAsset_ContentPrefabsValidate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SceneTransitionOverlayContentCatalog>(TransitionContentCatalogPath);
            Assert.That(catalog, Is.Not.Null, TransitionContentCatalogPath);

            var contentPrefabs = new HashSet<SceneTransitionOverlayContentView>();
            if (catalog.GenericFallbackPrefab != null)
            {
                contentPrefabs.Add(catalog.GenericFallbackPrefab);
            }

            foreach (var entry in catalog.Entries)
            {
                if (entry?.ContentPrefab != null)
                {
                    contentPrefabs.Add(entry.ContentPrefab);
                }
            }

            var failures = new List<string>();
            foreach (var prefab in contentPrefabs.OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal))
            {
                try
                {
                    Assert.That(prefab.CollectValidationIssues(), Is.Empty, Describe(prefab));
                    Assert.That(CountMissingScripts(prefab.gameObject), Is.EqualTo(0), Describe(prefab));
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(prefab)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Scene transition content catalog repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void GameplayUiCanvasRootShell_RequiredLayersAndEventSystemContract()
        {
            var shellPrefab = Resources.Load<GameObject>("UI/GameplayUiCanvasRootShell");
            Assert.That(shellPrefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(shellPrefab);
            try
            {
                var rootView = instance.GetComponent<GameplayUiCanvasRootView>();
                Assert.That(rootView, Is.Not.Null);

                rootView.EnsureHierarchy();

                Assert.That(rootView.HudLayer, Is.Not.Null);
                Assert.That(rootView.ScreenLayer, Is.Not.Null);
                Assert.That(rootView.PopupLayer, Is.Not.Null);
                Assert.That(instance.transform.Find("DiagnosticsLayer"), Is.Null);
                Assert.That(rootView.ScreenLayerView, Is.Not.Null);
                Assert.That(rootView.PopupLayerView, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
                DestroySceneObject("EventSystem");
            }
        }

        [Test]
        public void GameplayHudRoot_RequiredBoundViewsPresent_AndRetiredProofResidueRemoved()
        {
            var hudPrefab = UiTestPrefabAssetUtility.LoadHudPrefab();
            var instance = UnityEngine.Object.Instantiate(hudPrefab);
            try
            {
                instance.ValidateAuthoredStructureOrThrow();

                Assert.That(instance.PlayerStatusView, Is.Not.Null);
                Assert.That(instance.ObjectiveHudView, Is.Not.Null);
                Assert.That(instance.ChancePanelView, Is.Not.Null);
                Assert.That(instance.SurfaceBeltIndicatorView, Is.Not.Null);

                Assert.That(FindChildByName(instance.transform, "PauseButton"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "Label_StageName"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "CenterArrow"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "LegacyTopologyDebugText"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "LegacyCenterArrow"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "Action" + "Bar"), Is.Null);
                Assert.That(CountMissingScripts(instance.gameObject), Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void UiAudioCueMaps_RepositoryAssets_AllCuesExplicitUiOneShot()
        {
            var cueMaps = LoadAllAssets<UiAudioCueMap>();
            Assert.That(cueMaps, Is.Not.Empty, "Repository scan found no UiAudioCueMap assets.");

            var failures = new List<string>();
            foreach (var cueMap in cueMaps)
            {
                try
                {
                    cueMap.ValidateOrThrow();
                    Assert.That(cueMap.Entries.Count, Is.EqualTo(Enum.GetValues(typeof(UiAudioCueId)).Length));
                    foreach (var entry in cueMap.Entries)
                    {
                        Assert.That(entry.Binding.Definition.Category, Is.EqualTo(AudioCategory.Ui), Describe(cueMap));
                        Assert.That(entry.Binding.Definition.Loop, Is.False, Describe(cueMap));
                        Assert.That(entry.Binding.HasAttachmentSlot, Is.False, Describe(cueMap));
                        Assert.That(entry.Binding.Policy, Is.Null, Describe(cueMap));
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{Describe(cueMap)}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "UiAudioCueMap repository smoke failures:\n" + string.Join("\n", failures));
        }

        [Test]
        public void SettingsScreenPrefab_AuthoredSectionsValidate()
        {
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<SettingsScreenView>(
                UiTestPrefabAssetUtility.SettingsScreenPrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.ValidateAuthoredStructureOrThrow();
                instance.AudioView.ValidateAuthoredControlsOrThrow();
                instance.DisplayView.ValidateAuthoredControlsOrThrow();
                instance.InputView.ValidateAuthoredControlsOrThrow();

                Assert.That(FindChildByName(instance.transform, "ResetInput_Legacy"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayApplyButton"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayRevertButton"), Is.Null);
                Assert.That(FindChildByName(instance.transform, "ResetInput_New"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayApplyButton_New"), Is.Not.Null);
                Assert.That(FindChildByName(instance.transform, "DisplayReveryButton_New"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void StageResultPayload_ActualReadModel_MapsToNavigationRequest()
        {
            var readModel = CreateActualReadModel("stage-1-1");

            var payload = StageResultPayloadMapper.Map(readModel);

            Assert.That(payload.TitleText, Is.Not.Empty);
            Assert.That(payload.ContinueStageRequest.IsValid, Is.True);
            Assert.That(payload.RetryStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.IsValid, Is.True);
            Assert.That(payload.NextStageRequest.StageId.Value, Is.EqualTo("stage-2-1"));
        }

        [Test]
        private static Component ResolveScreenPrefab(ScreenPrefabCatalog catalog, ScreenId screenId)
        {
            return screenId switch
            {
                ScreenId.Settings => catalog.SettingsPrefab,
                ScreenId.StageResult => catalog.StageResultPrefab,
                ScreenId.LevelFailed => catalog.LevelFailedPrefab,
                ScreenId.GameClear => catalog.GameClearPrefab,
                _ => throw new ArgumentOutOfRangeException(nameof(screenId), screenId, null),
            };
        }

        private static Component ResolvePopupPrefab(PopupPrefabCatalog catalog, PopupId popupId)
        {
            return popupId switch
            {
                PopupId.Pause => catalog.PausePrefab,
                PopupId.Confirm => catalog.ConfirmPrefab,
                PopupId.Tooltip => catalog.TooltipPrefab,
                _ => throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null),
            };
        }

        private static MinimalStageCompletionReadModel CreateActualReadModel(string stageIdValue)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, StageContentPaths.StageCatalogAssetPath);
            var stageId = StageId.CreateOrThrow(stageIdValue);
            var entry = catalog.Entries.FirstOrDefault(candidate => candidate.StageId.Equals(stageId));
            Assert.That(entry, Is.Not.Null, stageIdValue);

            return MinimalStageCompletionReadModelBuilder.Build(
                entry,
                new StageClearResult(
                    entry.StageId,
                    new StageRunId("ui-actual-smoke-" + entry.StageId.Value),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 5,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()));
        }

        private static IReadOnlyList<T> LoadAllAssets<T>() where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            Array.Sort(guids, StringComparer.Ordinal);
            return guids
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null)
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                .ToArray();
        }

        private static string Describe(UnityEngine.Object asset)
        {
            return asset == null
                ? "<null>"
                : $"{asset.GetType().Name} '{asset.name}' Path='{AssetDatabase.GetAssetPath(asset)}'";
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => string.Equals(child.name, childName, StringComparison.Ordinal));
        }

        private static void AssertGameClearResultOnlyPrefab(GameClearScreenView gameClear)
        {
            var serialized = new SerializedObject(gameClear);
            AssertRequiredObjectReference(serialized, "_titleLabel", nameof(GameClearScreenView));
            AssertRequiredObjectReference(serialized, "_mainButton", nameof(GameClearScreenView));
            AssertRequiredObjectReference(serialized, "_mainButtonLabel", nameof(GameClearScreenView));
            Assert.That(serialized.FindProperty("_detailLabel"), Is.Null);
            Assert.That(serialized.FindProperty("_restartLevelButton"), Is.Null);
            Assert.That(serialized.FindProperty("_restartLevelButtonLabel"), Is.Null);

            Assert.That(FindChildByName(gameClear.transform, "Title"), Is.Not.Null);
            Assert.That(FindChildByName(gameClear.transform, "ResultDetail"), Is.Null);
            Assert.That(FindChildByName(gameClear.transform, "Detail"), Is.Null);
            Assert.That(FindChildByName(gameClear.transform, "RestartLevelButton"), Is.Null);

            var mainButton = FindChildByName(gameClear.transform, "MainButton");
            Assert.That(mainButton, Is.Not.Null);
            Assert.That(FindChildByName(mainButton, "SelectionFrame"), Is.Not.Null);
        }

        private static void AssertRequiredObjectReference(
            SerializedObject serializedObject,
            string propertyName,
            string ownerName)
        {
            var property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"{ownerName}.{propertyName}");
            Assert.That(property.objectReferenceValue, Is.Not.Null, $"{ownerName}.{propertyName}");
        }

        private static void AssertPausePopupButtonHasSingleHoverScaleEffect(PausePopupView pausePopup, string buttonName)
        {
            var button = FindChildByName(pausePopup.transform, buttonName);
            Assert.That(button, Is.Not.Null, buttonName);
            Assert.That(button.GetComponents<UiHoverScaleEffect>().Length, Is.EqualTo(1), buttonName);
            Assert.That(FindChildByName(button, "SelectionFrame"), Is.Not.Null, buttonName);
        }

        private static int CountMissingScripts(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Sum(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject));
        }

        private static void DestroySceneObject(string objectName)
        {
            var sceneObject = GameObject.Find(objectName);
            if (sceneObject != null)
            {
                UnityEngine.Object.DestroyImmediate(sceneObject);
            }
        }
    }
}
