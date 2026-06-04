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
                }
                catch (Exception exception)
                {
                    failures.Add($"{popupId}: {exception.GetType().Name}: {exception.Message}");
                }
            }

            Assert.That(failures, Is.Empty, "Popup prefab catalog repository smoke failures:\n" + string.Join("\n", failures));
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
                Assert.That(rootView.DiagnosticsLayer, Is.Not.Null);
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
        public void GameplayHudRoot_RequiredViewsPresent()
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
                Assert.That(instance.GetComponentInChildren<ActionBarView>(true), Is.Not.Null);
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
        public void RewardPopupPayload_ActualReadModel_MapsSafely()
        {
            var readModel = CreateActualReadModel("stage-1-1");

            var payload = RewardPopupPayloadMapper.Map(readModel);

            Assert.That(payload.TitleText, Is.EqualTo("Rewards"));
            Assert.That(payload.SummaryText, Is.Not.Empty);
            Assert.That(payload.CloseLabel, Is.EqualTo("Close"));
            Assert.That(payload.Items, Is.Not.Null);
        }

        private static Component ResolveScreenPrefab(ScreenPrefabCatalog catalog, ScreenId screenId)
        {
            return screenId switch
            {
                ScreenId.ObjectiveStatus => catalog.ObjectiveStatusPrefab,
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
                PopupId.ObjectiveInfo => catalog.ObjectiveInfoPrefab,
                PopupId.Confirm => catalog.ConfirmPrefab,
                PopupId.Tooltip => catalog.TooltipPrefab,
                PopupId.Reward => catalog.RewardPrefab,
                _ => throw new ArgumentOutOfRangeException(nameof(popupId), popupId, null),
            };
        }

        private static StageCompletionReadModel CreateActualReadModel(string stageIdValue)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageContentPaths.StageCatalogAssetPath);
            Assert.That(catalog, Is.Not.Null, StageContentPaths.StageCatalogAssetPath);
            var stageId = StageId.CreateOrThrow(stageIdValue);
            var entry = catalog.Entries.FirstOrDefault(candidate => candidate.StageId.Equals(stageId));
            Assert.That(entry, Is.Not.Null, stageIdValue);

            return new StageCompletionReadModel(
                entry.StageId,
                entry.StageId.Value,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                new StageClearResult(
                    entry.StageId,
                    new StageRunId("ui-actual-smoke-" + entry.StageId.Value),
                    StageTerminalReason.Cleared,
                    wasCleared: true,
                    finalTickIndex: 5,
                    default,
                    Array.Empty<StageSessionMetricValue>(),
                    Array.Empty<StageChallengeRuntimeState>()),
                new StageClearEvaluationResult(
                    entry.StageId,
                    new StageRunId("ui-actual-smoke-" + entry.StageId.Value),
                    wasCleared: true,
                    score: 100,
                    starsEarned: 1,
                    rankId: "smoke",
                    challengeResults: Array.Empty<StageChallengeEvaluationResult>()),
                new RewardGrantResult(
                    entry.StageId,
                    new StageRunId("ui-actual-smoke-" + entry.StageId.Value),
                    Array.Empty<RewardGrantEntry>(),
                    Array.Empty<string>(),
                    Array.Empty<RewardGrantId>(),
                    wasFirstClear: false),
                PlayerStageProgress.CreateEmpty(entry.StageId));
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
