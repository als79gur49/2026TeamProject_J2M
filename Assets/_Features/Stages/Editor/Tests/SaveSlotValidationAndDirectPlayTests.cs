using System;
using System.IO;
using Game.Feature.UI.Composition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class SaveSlotValidationAndDirectPlayTests
    {
        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            EditorDirectPlayContextStore.Clear();
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
        }

        [Test]
        public void NonCampaignDirectPlay_PrimesSuppressContext()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");

            StageEditorDirectPlayLauncher.PrimeNonCampaignForTests(stageId);

            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.Mode, Is.EqualTo(EditorDirectPlayMode.NonCampaign));
            Assert.That(context.StageId, Is.EqualTo(stageId));
            Assert.That(context.SuppressCampaignFlow, Is.True);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out var pending), Is.True);
            Assert.That(pending, Is.EqualTo(stageId));
        }

        [Test]
        public void CampaignTempDirectPlay_UsesIsolatedJsonProfileAndLocalState()
        {
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var stageId = StageId.CreateOrThrow("stage-2-2");
            var pathProvider = new TemporaryCampaignSavePathProvider();

            StageEditorDirectPlayLauncher.PrimeCampaignTempSlotForTests(stageId, resolver, remainingChances: 2);

            Assert.That(File.Exists(pathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName)), Is.True);
            Assert.That(File.Exists(pathProvider.GetSaveFilePath(CampaignLocalLaunchStateRepository.FileName)), Is.True);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out var context), Is.True);
            Assert.That(context.UsesTemporaryCampaignState, Is.True);
        }

        [Test]
        public void TemporaryCampaignPath_EditorUsesWorktreePrivateLibraryRoot()
        {
            var projectRoot = Path.Combine("Temp", "TemporaryCampaignPathTests", "Project");
            var dataPath = Path.Combine(projectRoot, "Assets");

            var resolved = TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                isEditor: true,
                dataPath,
                Path.Combine("Temp", "unused-cache"),
                Guid.NewGuid().ToString("N"));

            Assert.That(
                Path.GetFullPath(resolved),
                Is.EqualTo(Path.GetFullPath(Path.Combine(
                    projectRoot,
                    "Library",
                    "J2M",
                    "DirectPlayCampaign",
                    ApplicationPersistentDataSavePathProvider.SavesDirectoryName))));
        }

        [Test]
        public void TemporaryCampaignPath_PlayerScopeIsStablePerProcessIdAndIsolatedAcrossIds()
        {
            var cacheRoot = Path.Combine("Temp", "TemporaryCampaignPathTests", "Cache");
            var firstScope = Guid.NewGuid().ToString("N");
            var secondScope = Guid.NewGuid().ToString("N");

            var first = TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                isEditor: false,
                string.Empty,
                cacheRoot,
                firstScope);
            var sameProcess = TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                isEditor: false,
                string.Empty,
                cacheRoot,
                firstScope);
            var nextProcess = TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                isEditor: false,
                string.Empty,
                cacheRoot,
                secondScope);

            Assert.That(sameProcess, Is.EqualTo(first));
            Assert.That(nextProcess, Is.Not.EqualTo(first));
            Assert.That(first, Does.Contain(Path.Combine("J2M", "PlayerCaptureCampaign", firstScope)));
        }

        [Test]
        public void TemporaryCampaignPath_PlayerRejectsNonGuidScope()
        {
            Assert.Throws<ArgumentException>(() =>
                TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                    isEditor: false,
                    string.Empty,
                    Path.Combine("Temp", "TemporaryCampaignPathTests", "Cache"),
                    ".."));
        }

        [Test]
        public void TemporaryCampaignPath_PlayerCleanupDeletesOnlyExactGuidScope()
        {
            var cacheRoot = Path.GetFullPath(Path.Combine(
                "Temp",
                "TemporaryCampaignPathTests",
                Guid.NewGuid().ToString("N"),
                "Cache"));
            var scope = Guid.NewGuid().ToString("N");
            var saveRoot = TemporaryCampaignSavePathProvider.ResolveSaveRootPath(
                isEditor: false,
                string.Empty,
                cacheRoot,
                scope);
            Directory.CreateDirectory(saveRoot);
            File.WriteAllText(Path.Combine(saveRoot, "marker.json"), "marker");

            var deleted = TemporaryCampaignSavePathProvider.TryDeletePlayerProcessScope(
                saveRoot,
                cacheRoot);

            Assert.That(deleted, Is.True);
            Assert.That(Directory.Exists(Directory.GetParent(saveRoot).FullName), Is.False);
        }

        [Test]
        public void TemporaryCampaignPath_PlayerCleanupRejectsNonGuidOrEscapingRoot()
        {
            var cacheRoot = Path.GetFullPath(Path.Combine(
                "Temp",
                "TemporaryCampaignPathTests",
                Guid.NewGuid().ToString("N"),
                "Cache"));
            var outsideRoot = Path.Combine(
                cacheRoot,
                "J2M",
                "outside",
                Guid.NewGuid().ToString("N"),
                ApplicationPersistentDataSavePathProvider.SavesDirectoryName);
            Directory.CreateDirectory(outsideRoot);

            var deleted = TemporaryCampaignSavePathProvider.TryDeletePlayerProcessScope(
                outsideRoot,
                cacheRoot);

            Assert.That(deleted, Is.False);
            Assert.That(Directory.Exists(outsideRoot), Is.True);
            Directory.Delete(Path.Combine(cacheRoot, "J2M"), recursive: true);
        }

        [Test]
        public void CampaignTempDirectPlay_PrimesSlot1WithSelectedStageDerivedLevelGroupAndRemainingChances()
        {
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var stageId = StageId.CreateOrThrow("stage-3-2");

            StageEditorDirectPlayLauncher.PrimeCampaignTempSlotForTests(stageId, resolver, remainingChances: 1);

            var store = CampaignSaveCompositionProvider.CreateTemporaryProfileBacked();
            var activeSlotProvider = CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider(store);
            var slot = store.LoadSlot(1);
            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(slot.CurrentStageId, Is.EqualTo(stageId));
            Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-3"));
            Assert.That(slot.RemainingChances, Is.EqualTo(1));
        }

        [Test]
        public void StandaloneCampaignSeedImport_WritesProfileBackedStore_NotPlayerPrefs()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-2-2");
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var saveRootPath = Path.Combine("Temp", "StandaloneCampaignSeedProfileTests", Guid.NewGuid().ToString("N"));
            var saveStore = CampaignSaveCompositionProvider.Create(new CampaignSaveCompositionOptions
            {
                PathProvider = new TemporarySavePathProvider(saveRootPath),
                ProductVersion = "seed-import-test",
                ProfileId = "seed-import-test-profile",
            });
            var activeSlotProvider = new ActiveSlotProvider(
                new TransientActiveSlotStorage(CreateTransientNamespace("seed-active")));
            activeSlotProvider.ClearActiveSlot();
            try
            {
                File.WriteAllText(
                    seedPath,
                    StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                        StageId.CreateOrThrow("stage-2-2"),
                        slotNumber: 2,
                        remainingChances: 1));

                Assert.That(
                    StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                        seedPath,
                        saveStore,
                        activeSlotProvider,
                        resolver,
                        provider.Provider,
                        deleteAfterImport: true,
                        out var result),
                    Is.True);

                var slot = saveStore.LoadSlot(2);
                Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.Imported));
                Assert.That(File.Exists(seedPath), Is.False);
                Assert.That(File.Exists(Path.Combine(saveRootPath, FileCampaignProfileRepository.ProfileFileName)), Is.True);
                Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(2));
                Assert.That(slot.CurrentStageId, Is.EqualTo(StageId.CreateOrThrow("stage-2-2")));
                Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(slot.RemainingChances, Is.EqualTo(1));
            }
            finally
            {
                DeleteFileIfExists(seedPath);
                if (Directory.Exists(saveRootPath))
                {
                    Directory.Delete(saveRootPath, recursive: true);
                }

                activeSlotProvider.ClearActiveSlot();
                provider.Dispose();
            }
        }

        [Test]
        public void StandaloneCampaignSeedImport_RejectsMissingCatalogStageWithoutSaving()
        {
            var seedPath = CreateTempSeedPath();
            var provider = CreateProvider("stage-0-1");
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var saveStore = new TransientCampaignSaveSlotStore(CreateTransientNamespace("seed-missing-catalog"));
            var activeSlotProvider = new ActiveSlotProvider(
                new TransientActiveSlotStorage(CreateTransientNamespace("seed-missing-catalog-active")));
            saveStore.ClearAll();
            activeSlotProvider.ClearActiveSlot();
            try
            {
                File.WriteAllText(
                    seedPath,
                    StandaloneCampaignSaveSeedImporter.BuildSeedJson(
                        StageId.CreateOrThrow("stage-2-2"),
                        slotNumber: 2,
                        remainingChances: 1));

                Assert.That(
                    StandaloneCampaignSaveSeedImporter.TryImportSeedFile(
                        seedPath,
                        saveStore,
                        activeSlotProvider,
                        resolver,
                        provider.Provider,
                        deleteAfterImport: false,
                        out var result),
                    Is.False);

                Assert.That(result.Status, Is.EqualTo(StandaloneCampaignSaveSeedImportStatus.StageMissingFromCatalog));
                Assert.That(saveStore.LoadSlot(2).IsEmpty, Is.True);
                Assert.That(activeSlotProvider.TryGetActiveSlotNumber(out _), Is.False);
                Assert.That(File.Exists(seedPath), Is.True);
            }
            finally
            {
                DeleteFileIfExists(seedPath);
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignDirectPlay_RejectsSelectedStageMissingFromSequenceOrCatalog()
        {
            var provider = CreateProvider("stage-0-1");
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                Assert.That(
                    validation.Validate(new SaveSlotData
                    {
                        SlotNumber = 1,
                        CurrentStageId = StageId.CreateOrThrow("stage-9-9"),
                    }).Status,
                    Is.EqualTo(SaveSlotValidationStatus.StageMissingFromSequence));

	                Assert.That(
	                    validation.Validate(new SaveSlotData
	                    {
	                        SlotNumber = 1,
	                        CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
	                    }).Status,
	                    Is.EqualTo(SaveSlotValidationStatus.StageMissingFromCatalog));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void DirectPlayTempAndProduction_UseDedicatedJsonCompositions()
        {
            var source = File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs");
            var tempMethod = ExtractSourceRange(source, "private static void PrimeCampaignTempSlot", "private static void PrimeCampaignProductionSlot");
            var productionMethod = ExtractSourceRange(source, "private static void PrimeCampaignProductionSlot", "private static void ValidateCampaignStage");

            Assert.That(tempMethod, Does.Contain("CampaignSaveCompositionProvider.CreateTemporaryProfileBacked()"));
            Assert.That(tempMethod, Does.Contain("CampaignSaveCompositionProvider.CreateTemporaryActiveSlotProvider("));
            Assert.That(productionMethod, Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
            Assert.That(productionMethod, Does.Contain("CampaignSaveCompositionProvider.CreateProductionActiveSlotProvider(saveStore)"));
        }

        [Test]
        public void SaveSlotValidation_RejectsStageIdNotInSequence()
        {
            var provider = CreateProvider("stage-9-9");
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-9-9"),
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.StageMissingFromSequence));
                Assert.That(result.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_RejectsStageIdMissingFromCatalog()
        {
            var provider = CreateProvider("stage-0-1");
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-1"),
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.StageMissingFromCatalog));
                Assert.That(result.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_SyncsLevelGroupFromCurrentStage()
        {
            var provider = CreateProvider("stage-2-2");
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                var result = validation.Validate(new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-2-2"),
                    CurrentLevelGroupId = "level-5",
                });

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.Valid));
                Assert.That(result.LevelGroupWasSynced, Is.True);
                Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo("level-2"));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void SaveSlotValidation_MigratesRetiredStageFiveOne_ToCompletedFinalStage()
        {
            var provider = CreateProvider("stage-4-3");
            var saveStore = new TransientCampaignSaveSlotStore();
            saveStore.ClearAll();
            try
            {
                var validation = new SaveSlotValidationService(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);
                var legacySlot = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-5-1"),
                    CurrentLevelGroupId = "level-5",
                    RemainingChances = 2,
                };

                var result = validation.Validate(legacySlot);

                Assert.That(result.Status, Is.EqualTo(SaveSlotValidationStatus.Completed));
                Assert.That(result.RequiresSaveSync, Is.True);
                Assert.That(result.Slot.CurrentStageId.Value, Is.EqualTo("stage-4-3"));
                Assert.That(result.Slot.CurrentLevelGroupId, Is.EqualTo("level-4"));
                Assert.That(result.Slot.CampaignCompleted, Is.True);

                saveStore.SaveSlot(legacySlot);
                var synced = validation.ValidateAndSync(saveStore, 1);
                var persisted = saveStore.LoadSlot(1);

                Assert.That(synced.Status, Is.EqualTo(SaveSlotValidationStatus.Completed));
                Assert.That(synced.RequiresSaveSync, Is.False);
                Assert.That(persisted.CurrentStageId.Value, Is.EqualTo("stage-4-3"));
                Assert.That(persisted.CurrentLevelGroupId, Is.EqualTo("level-4"));
                Assert.That(persisted.CampaignCompleted, Is.True);
            }
            finally
            {
                saveStore.ClearAll();
                provider.Dispose();
            }
        }

        [Test]
        public void DirectPlayRouteConfigAsset_UsesBuildSettingsScenes()
        {
            var routeConfig = AssetDatabase.LoadAssetAtPath<GameplayStageLaunchRouteConfig>(
                "Assets/_Features/UI/UI_Composition/Authoring/GameplayStageLaunchRouteConfig.asset");
            Assert.That(routeConfig, Is.Not.Null);
            Assert.That(routeConfig.MainMenuScenePath, Is.EqualTo("Assets/Scenes/MainMenuScene.unity"));
            Assert.That(routeConfig.GameplayShellScenePath, Is.EqualTo("Assets/Scenes/UIAudioScene.unity"));
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

        private static string CreateTempSeedPath()
        {
            var directory = Path.Combine("Temp", "StandaloneCampaignSeedTests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        }

        private static string CreateTransientNamespace(string suffix)
        {
            return "Game.Feature.Stages.Editor.Tests." + suffix + "." + Guid.NewGuid().ToString("N");
        }

        private static string ExtractSourceRange(string source, string startToken, string endToken)
        {
            var start = source.IndexOf(startToken, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startToken);
            var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endToken);
            return source.Substring(start, end - start);
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private sealed class TemporarySavePathProvider : SavePathProviderBase
        {
            public TemporarySavePathProvider(string saveRootPath)
                : base(saveRootPath)
            {
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
