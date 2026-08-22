using System;
using System.IO;
using Game.Feature.UI.Composition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Feature.Stages.Editor
{
    internal enum OwnedDirectPlayRuntimeCleanupResult
    {
        MalformedExpectedOwnership = 0,
        NoCurrentContext = 1,
        ExactContextCleared = 2,
        DifferentContextPreserved = 3,
    }

    internal readonly struct EditorDirectPlayExitCleanupResult
    {
        public EditorDirectPlayExitCleanupResult(
            OwnedDirectPlayRuntimeCleanupResult runtimeContextResult,
            bool matchingPrimeCleared,
            bool differentPrimePreserved,
            bool editorContextCleared,
            bool ownershipReleased)
        {
            RuntimeContextResult = runtimeContextResult;
            MatchingPrimeCleared = matchingPrimeCleared;
            DifferentPrimePreserved = differentPrimePreserved;
            EditorContextCleared = editorContextCleared;
            OwnershipReleased = ownershipReleased;
        }

        public OwnedDirectPlayRuntimeCleanupResult RuntimeContextResult { get; }

        public bool MatchingPrimeCleared { get; }

        public bool DifferentPrimePreserved { get; }

        public bool EditorContextCleared { get; }

        public bool OwnershipReleased { get; }
    }

    [InitializeOnLoad]
    public static class StageEditorDirectPlayLauncher
    {
        private const string LastStageIdSessionKey = "Game.Feature.Stages.LastEditorDirectPlayStageId";
        private const string Stage0_1StageId = "stage-0-1";
        private const string Stage1_1StageId = "stage-1-1";

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

            var catalog = StageEditorDirectPlayCatalog.LoadDefault();
            if (catalog == null || !catalog.IsCanonicalShellScenePath(scenePath))
            {
                throw new InvalidOperationException(BuildUnsupportedSceneMessage(scenePath));
            }

            LaunchLastStage();
        }

        [MenuItem("Tools/Stages/Direct Play/Replay Last Stage")]
        public static void LaunchLastStage()
        {
            var stageIdValue = SessionState.GetString(LastStageIdSessionKey, string.Empty);
            if (!StageId.TryCreate(stageIdValue, out var stageId))
            {
                throw new InvalidOperationException("No stage id has been launched yet in this editor session. Use Tools/Stages/Direct Play/Launch Stage... first.");
            }

            LaunchStage(stageId, EditorDirectPlayMode.NonCampaign, CampaignSaveSlotPolicy.DefaultRemainingChances);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Stage Ids/stage-0-1")]
        public static void LaunchStage0_1()
        {
            LaunchStage(
                StageId.CreateOrThrow(Stage0_1StageId),
                EditorDirectPlayMode.NonCampaign,
                CampaignSaveSlotPolicy.DefaultRemainingChances);
        }

        [MenuItem("Tools/Stages/Direct Play/Supported Stage Ids/stage-1-1")]
        public static void LaunchStage1_1()
        {
            LaunchStage(
                StageId.CreateOrThrow(Stage1_1StageId),
                EditorDirectPlayMode.NonCampaign,
                CampaignSaveSlotPolicy.DefaultRemainingChances);
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

            ThrowIfLaunchIsAlreadyInProgress(
                EditorApplication.isPlaying,
                EditorApplication.isPlayingOrWillChangePlaymode);

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

            var ownership = default(EditorDirectPlayLaunchOwnershipRecord);
            var ownsEditorContext = false;
            try
            {
                switch (mode)
                {
                    case EditorDirectPlayMode.NonCampaign:
                        EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
                        ownsEditorContext = true;
                        break;

                    case EditorDirectPlayMode.CampaignTempSlot:
                        PrimeCampaignTempSlot(stageId, sequenceResolver, remainingChances);
                        ownsEditorContext = true;
                        break;

                    case EditorDirectPlayMode.CampaignProductionSlot:
                        PrimeCampaignProductionSlot(stageId, sequenceResolver, remainingChances, productionSlotNumber);
                        ownsEditorContext = true;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported Direct Play mode.");
                }

                ownership = CaptureDirectPlayOwnership(mode, stageId);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                RememberLastStage(stageId);
                EditorApplication.isPlaying = true;
            }
            catch
            {
                if (ownership.IsValid)
                {
                    CleanupOwnedDirectPlay(ownership);
                }
                else if (ownsEditorContext)
                {
                    if (mode == EditorDirectPlayMode.CampaignTempSlot)
                    {
                        EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                    }

                    EditorDirectPlayContextStore.Clear();
                }

                throw;
            }
        }

        internal static void ThrowIfLaunchIsAlreadyInProgress(
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            if (!isPlaying &&
                !isPlayingOrWillChangePlaymode &&
                !StageLaunchContextStore.TryPeek(out _) &&
                !StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _) &&
                !EditorDirectPlayLaunchOwnershipStore.TryPeek(out _))
            {
                return;
            }

            throw new InvalidOperationException(
                "Direct Play launch was rejected because Play Mode entry or another stage transition is already in progress.");
        }

        public static void ClearTemporaryCampaignState()
        {
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        public static void PrimeNonCampaignForTests(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Direct Play requires a valid StageId.", nameof(stageId));
            }

            EditorDirectPlayContextStore.SetCurrent(EditorDirectPlayContext.CreateNonCampaign(stageId));
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            RememberLastStage(stageId);
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

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(productionSlotNumber);
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
            RememberLastStage(stageId);
        }

        public static void PrimeCampaignProductionSlotForTests(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances,
            int productionSlotNumber,
            ICampaignSaveSlotStore saveStore,
            ActiveSlotProvider activeSlotProvider)
        {
            PrimeCampaignProductionSlotCore(
                stageId,
                sequenceResolver,
                remainingChances,
                productionSlotNumber,
                saveStore,
                activeSlotProvider);
            StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            RememberLastStage(stageId);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                CleanupCurrentOwnedDirectPlay();
                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode)
            {
                CleanupCurrentOwnedDirectPlay();
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
            if (catalog == null || !catalog.IsCanonicalShellScenePath(scenePath))
            {
                return;
            }

            if (StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _))
            {
                return;
            }

            Debug.LogWarning(
                $"Scene '{scenePath}' is the canonical stage-backed gameplay shell and requires a StageId launch context. Use Tools/Stages/Direct Play/Launch Stage... before entering Play mode.");
        }

        internal static EditorDirectPlayExitCleanupResult CleanupOwnedDirectPlayForTests(
            EditorDirectPlayLaunchOwnershipRecord ownership)
        {
            return CleanupOwnedDirectPlay(ownership);
        }

        internal static void HandlePlayModeStateChangedForTests(PlayModeStateChange change)
        {
            HandlePlayModeStateChanged(change);
        }

        internal static OwnedDirectPlayRuntimeCleanupResult TryClearOwnedDirectPlayRuntimeContext(
            EditorDirectPlayLaunchOwnershipRecord ownership)
        {
            if (!ownership.IsValid)
            {
                return OwnedDirectPlayRuntimeCleanupResult.MalformedExpectedOwnership;
            }

            if (!StageLaunchContextStore.TryPeek(out var current))
            {
                return OwnedDirectPlayRuntimeCleanupResult.NoCurrentContext;
            }

            if (!current.Equals(ownership.ExpectedRuntimeContext) ||
                !EditorDirectPlayContextStore.TryGetCurrent(out var editorContext) ||
                !ownership.Matches(editorContext))
            {
                return OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved;
            }

            return StageLaunchContextStore.TryClear(ownership.ExpectedRuntimeContext)
                ? OwnedDirectPlayRuntimeCleanupResult.ExactContextCleared
                : OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved;
        }

        private static EditorDirectPlayLaunchOwnershipRecord CaptureDirectPlayOwnership(
            EditorDirectPlayMode mode,
            StageId stageId)
        {
            var expectedRuntimeContext = StageLaunchContextStore.PrimePendingEditorDirectPlay(stageId);
            var ownership = new EditorDirectPlayLaunchOwnershipRecord(mode, expectedRuntimeContext);
            if (EditorDirectPlayLaunchOwnershipStore.TrySetCurrent(ownership))
            {
                return ownership;
            }

            StageLaunchContextStore.TryClearPendingEditorDirectPlay(expectedRuntimeContext);
            throw new InvalidOperationException(
                "Direct Play launch could not capture exact lifecycle ownership because another editor operation already owns cleanup.");
        }

        private static void CleanupCurrentOwnedDirectPlay()
        {
            if (EditorDirectPlayLaunchOwnershipStore.TryPeek(out var ownership))
            {
                CleanupOwnedDirectPlay(ownership);
            }
        }

        private static EditorDirectPlayExitCleanupResult CleanupOwnedDirectPlay(
            EditorDirectPlayLaunchOwnershipRecord ownership)
        {
            var runtimeContextResult = TryClearOwnedDirectPlayRuntimeContext(ownership);
            var matchesEditorContext =
                EditorDirectPlayContextStore.TryGetCurrent(out var editorContext) &&
                ownership.Matches(editorContext);
            var matchingPrimeCleared = matchesEditorContext &&
                                       StageLaunchContextStore.TryClearPendingEditorDirectPlay(
                                           ownership.ExpectedRuntimeContext);
            var differentPrimePreserved =
                StageLaunchContextStore.TryPeekPendingEditorDirectPlayContext(out var remainingPrime) &&
                !remainingPrime.Equals(ownership.ExpectedRuntimeContext);
            var differentRuntimeContextPreserved =
                runtimeContextResult == OwnedDirectPlayRuntimeCleanupResult.DifferentContextPreserved;
            var editorContextCleared = false;
            var ownershipReleased = EditorDirectPlayLaunchOwnershipStore.TryClear(ownership);

            if (matchesEditorContext &&
                !differentRuntimeContextPreserved &&
                !differentPrimePreserved)
            {
                if (ownership.Mode == EditorDirectPlayMode.CampaignTempSlot)
                {
                    EditorDirectPlayContextStore.ClearTemporaryCampaignState();
                }

                EditorDirectPlayContextStore.Clear();
                editorContextCleared = true;
            }

            return new EditorDirectPlayExitCleanupResult(
                runtimeContextResult,
                matchingPrimeCleared,
                differentPrimePreserved,
                editorContextCleared,
                ownershipReleased);
        }

        private static void RememberLastStage(StageId stageId)
        {
            SessionState.SetString(LastStageIdSessionKey, stageId.IsValid ? stageId.Value : string.Empty);
        }

        private static string BuildUnsupportedSceneMessage(string scenePath)
        {
            return
                $"Scene '{scenePath}' is not the canonical direct-play gameplay shell. Use Tools/Stages/Direct Play/Launch Stage... to choose a StageId and open the configured gameplay shell.";
        }

        private static void PrimeCampaignTempSlot(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances)
        {
            remainingChances = Mathf.Clamp(remainingChances, 1, CampaignSaveSlotPolicy.DefaultRemainingChances);
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            var saveStore = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            var activeSlotProvider = CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(
                saveStore);
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
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(productionSlotNumber);
            remainingChances = Mathf.Clamp(remainingChances, 1, CampaignSaveSlotPolicy.DefaultRemainingChances);
            if (!EditorUtility.DisplayDialog(
                    "Overwrite Production Campaign Slot",
                    $"Overwrite production campaign slot {productionSlotNumber} for Direct Play?",
                    "Overwrite",
                    "Cancel"))
            {
                throw new OperationCanceledException("Production Direct Play launch was cancelled.");
            }

            var saveStore = CampaignSaveCompositionProvider.CreateProductionProfileBacked();
            var activeSlotProvider = CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveStore);
            PrimeCampaignProductionSlotCore(
                stageId,
                sequenceResolver,
                remainingChances,
                productionSlotNumber,
                saveStore,
                activeSlotProvider);
        }

        private static void PrimeCampaignProductionSlotCore(
            StageId stageId,
            CampaignStageSequenceResolver sequenceResolver,
            int remainingChances,
            int productionSlotNumber,
            ICampaignSaveSlotStore saveStore,
            ActiveSlotProvider activeSlotProvider)
        {
            if (saveStore == null)
            {
                throw new ArgumentNullException(nameof(saveStore));
            }

            if (activeSlotProvider == null)
            {
                throw new ArgumentNullException(nameof(activeSlotProvider));
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(productionSlotNumber);
            remainingChances = Mathf.Clamp(remainingChances, 1, CampaignSaveSlotPolicy.DefaultRemainingChances);
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
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"Campaign Direct Play requires the authoritative campaign sequence asset at '{StageContentPaths.CampaignStageSequenceAssetPath}'.");
            }

            return definition;
        }
    }
}
