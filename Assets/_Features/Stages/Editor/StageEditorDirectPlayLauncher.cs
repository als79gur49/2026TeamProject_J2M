using System;
using System.IO;
using Game.Feature.UI.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor
{
    [InitializeOnLoad]
    public static class StageEditorDirectPlayLauncher
    {
        private const string LastScenePathSessionKey = "Game.Feature.Stages.LastEditorDirectPlayScenePath";
        private const string CombinedGameplayShowcaseScenePath = "Assets/Scenes/CombinedGameplayShowcase.unity";
        private const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";
        private const string UiAudioScenePath = "Assets/Scenes/UIAudioScene.unity";

        static StageEditorDirectPlayLauncher()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Stages/Direct Play/Launch Current Scene")]
        public static void LaunchCurrentScene()
        {
            var scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("Open a saved stage-backed scene before using direct play.");
            }

            LaunchScene(scenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Replay Last Stage-Backed Scene")]
        public static void LaunchLastScene()
        {
            var scenePath = SessionState.GetString(LastScenePathSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("No stage-backed scene has been launched yet in this editor session.");
            }

            LaunchScene(scenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/Combined Gameplay Showcase")]
        public static void LaunchCombinedGameplayShowcase()
        {
            LaunchScene(CombinedGameplayShowcaseScenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/Tutorial Scene")]
        public static void LaunchTutorialScene()
        {
            LaunchScene(TutorialScenePath);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Scenes/UI Audio Scene")]
        public static void LaunchUiAudioScene()
        {
            LaunchScene(UiAudioScenePath);
        }

        public static StageId PrimePendingLaunchForScene(string scenePath)
        {
            if (!TryPrimePendingLaunchForScene(scenePath, out var stageId))
            {
                throw new InvalidOperationException(BuildMissingCatalogMessage(scenePath));
            }

            return stageId;
        }

        public static bool TryPrimePendingLaunchForScene(string scenePath, out StageId stageId)
        {
            var catalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (catalog == null || !catalog.TryResolveScenePath(scenePath, out stageId))
            {
                stageId = StageId.None;
                return false;
            }

            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            RememberLastLaunch(scenePath);
            return true;
        }

        public static void LaunchStage(
            StageId stageId,
            EditorDirectPlayMode mode,
            int remainingChances,
            int productionSlotNumber = 1)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Direct Play requires a valid StageId.", nameof(stageId));
            }

            var routeConfig = LoadRouteConfigOrThrow();
            var scenePath = routeConfig.GameplayShellScenePath;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException("Direct Play requires a configured gameplay shell scene path.");
            }

            var stageCatalogProvider = LoadStageCatalogProviderOrThrow();
            var sequenceResolver = new CampaignStageSequenceResolver(LoadCampaignSequenceDefinition());
            if (mode == EditorDirectPlayMode.CampaignTempSlot ||
                mode == EditorDirectPlayMode.CampaignProductionSlot)
            {
                ValidateCampaignStage(stageId, stageCatalogProvider, sequenceResolver);
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                switch (mode)
                {
                    case EditorDirectPlayMode.NonCampaign:
                        EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
                        break;

                    case EditorDirectPlayMode.CampaignTempSlot:
                        PrimeCampaignTempSlot(stageId, sequenceResolver, remainingChances);
                        break;

                    case EditorDirectPlayMode.CampaignProductionSlot:
                        PrimeCampaignProductionSlot(stageId, sequenceResolver, remainingChances, productionSlotNumber);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Direct Play mode.");
                }

                StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RememberLastLaunch(scenePath);
                EditorApplication.isPlaying = true;
            }
            catch
            {
                StageLaunchContextStore.Clear();
                EditorDirectPlayContextStore.Clear();
                throw;
            }
        }

        public static void ClearTempDirectPlaySave()
        {
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
        }

        public static bool ExportStandaloneCampaignSaveSeedWithSavePanel(
            StageId stageId,
            int remainingChances,
            int productionSlotNumber = 1)
        {
            var seedPath = EditorUtility.SaveFilePanel(
                "Export Standalone Campaign Save Seed",
                string.Empty,
                StandaloneCampaignSaveSeedImporter.SeedFileName,
                "json");
            if (string.IsNullOrWhiteSpace(seedPath))
            {
                return false;
            }

            ExportStandaloneCampaignSaveSeed(
                stageId,
                remainingChances,
                productionSlotNumber,
                seedPath);
            return true;
        }

        public static void ExportStandaloneCampaignSaveSeed(
            StageId stageId,
            int remainingChances,
            int productionSlotNumber,
            string seedPath)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Standalone campaign seed requires a valid StageId.", nameof(stageId));
            }

            SaveSlotStore.ThrowIfInvalidSlotNumber(productionSlotNumber);
            if (string.IsNullOrWhiteSpace(seedPath))
            {
                throw new ArgumentException("Standalone campaign seed path is required.", nameof(seedPath));
            }

            var stageCatalogProvider = LoadStageCatalogProviderOrThrow();
            var sequenceResolver = new CampaignStageSequenceResolver(LoadCampaignSequenceDefinition());
            ValidateCampaignStage(stageId, stageCatalogProvider, sequenceResolver);

            var directory = Path.GetDirectoryName(seedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                seedPath,
                StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                    stageId,
                    productionSlotNumber,
                    remainingChances));
            Debug.Log(
                $"Standalone campaign save seed exported to '{seedPath}'. Place this file next to the standalone executable or in Application.persistentDataPath before launching the build.");
        }

        public static void PrimeCampaignTempSlotForTests(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances)
        {
            PrimeCampaignTempSlot(stageId, sequenceResolver, remainingChances);
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
        }

        private static void LaunchScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                PrimePendingLaunchForScene(scenePath);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RememberLastLaunch(scenePath);
                EditorApplication.isPlaying = true;
            }
            catch
            {
                StageLaunchContextStore.Clear();
                throw;
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorDirectPlayContextStore.ClearTempDirectPlaySave();
                EditorDirectPlayContextStore.Clear();
                return;
            }

            if (change != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            var scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            var catalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (catalog == null || !catalog.TryResolveScenePath(scenePath, out var expectedStageId))
            {
                return;
            }

            if (StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _))
            {
                return;
            }

            Debug.LogWarning(
                $"Scene '{scenePath}' is stage-backed and requires a canonical StageId launch context. Use Tools/Stages/Direct Play/Launch Current Scene to inject '{expectedStageId.Value}' before entering Play mode.");
        }

        private static void RememberLastLaunch(string scenePath)
        {
            SessionState.SetString(LastScenePathSessionKey, scenePath ?? string.Empty);
        }

        private static string BuildMissingCatalogMessage(string scenePath)
        {
            return
                $"Scene '{scenePath}' is not registered in {nameof(StageEditorDirectPlayCatalog)} at '{StageEditorDirectPlayCatalog.DefaultAssetPath}'.";
        }

        private static void PrimeCampaignTempSlot(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances)
        {
            remainingChances = Mathf.Clamp(remainingChances, 1, SaveSlotStore.DefaultRemainingChances);
            EditorDirectPlayContextStore.ClearTempDirectPlaySave();
            var saveStore = new SaveSlotStore(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
            var activeSlotProvider = new ActiveSlotProvider(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = stageId,
                CurrentLevelGroupId = sequenceResolver.GetLevelGroupId(stageId),
                RemainingChances = remainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlotProvider.SetActiveSlot(1);
            EditorDirectPlayContextStore.SetCurrent(
                EditorDirectPlayContext.CreateCampaignTempSlot(stageId, remainingChances));
        }

        private static void PrimeCampaignProductionSlot(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances,
            int productionSlotNumber)
        {
            SaveSlotStore.ThrowIfInvalidSlotNumber(productionSlotNumber);
            remainingChances = Mathf.Clamp(remainingChances, 1, SaveSlotStore.DefaultRemainingChances);
            if (!EditorUtility.DisplayDialog(
                    "Overwrite Production Campaign Slot",
                    $"Overwrite production campaign slot {productionSlotNumber} for Direct Play?",
                    "Overwrite",
                    "Cancel"))
            {
                throw new OperationCanceledException("Production Direct Play launch was cancelled.");
            }

            var saveStore = new SaveSlotStore();
            var activeSlotProvider = new ActiveSlotProvider();
            saveStore.SaveSlot(new SaveSlotData
            {
                SlotNumber = productionSlotNumber,
                CurrentStageId = stageId,
                CurrentLevelGroupId = sequenceResolver.GetLevelGroupId(stageId),
                RemainingChances = remainingChances,
                LastPlayedAt = DateTimeOffset.UtcNow.ToString("O"),
            });
            activeSlotProvider.SetActiveSlot(productionSlotNumber);
            EditorDirectPlayContextStore.SetCurrent(new EditorDirectPlayContext(
                EditorDirectPlayMode.CampaignProductionSlot,
                stageId,
                string.Empty,
                string.Empty,
                remainingChances,
                suppressCampaignFlow: false));
        }

        private static void ValidateCampaignStage(
            StageId stageId,
            IStageCatalogProvider stageCatalogProvider,
            CampaignStageSequenceResolver sequenceResolver)
        {
            if (!sequenceResolver.Contains(stageId))
            {
                throw new InvalidOperationException(
                    $"Campaign Direct Play stage '{stageId.Value}' is not in the campaign sequence.");
            }

            var catalogResolver = new StageCatalogResolver(stageCatalogProvider);
            if (!catalogResolver.TryResolve(stageId, out _))
            {
                throw new InvalidOperationException(
                    $"Campaign Direct Play stage '{stageId.Value}' is missing from the stage catalog.");
            }
        }

        private static GameplayStageLaunchRouteConfig LoadRouteConfigOrThrow()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(GameplayStageLaunchRouteConfig)}");
            if (guids == null || guids.Length == 0)
            {
                throw new InvalidOperationException("No GameplayStageLaunchRouteConfig asset exists for Direct Play.");
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(path);
            if (routeConfig == null)
            {
                throw new InvalidOperationException("Direct Play could not load GameplayStageLaunchRouteConfig.");
            }

            return routeConfig;
        }

        private static ScriptableObjectStageCatalogProvider LoadStageCatalogProviderOrThrow()
        {
            var provider = AssetDatabase.LoadAssetAtPath<ScriptableObjectStageCatalogProvider>(
                StageContentPaths.StageCatalogProviderAssetPath);
            if (provider == null)
            {
                throw new InvalidOperationException("Direct Play requires the stage catalog provider asset.");
            }

            return provider;
        }

        private static CampaignStageSequenceDefinition LoadCampaignSequenceDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            return definition != null
                ? definition
                : CampaignStageSequenceDefinition.CreateCanonicalRuntimeInstance();
        }
    }
}
