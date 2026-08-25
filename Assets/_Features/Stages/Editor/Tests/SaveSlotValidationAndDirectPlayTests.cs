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
            var slot = store.LoadSlot(1).State;
            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(slot.CurrentStageId, Is.EqualTo(stageId));
            Assert.That(slot.CurrentLevelGroupId, Is.EqualTo("level-3"));
            Assert.That(slot.RemainingChances, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void CampaignTempDirectPlay_InvalidChancesFailBeforeChangingTemporaryState(
            int invalidChances)
        {
            EditorDirectPlayContextStore.ClearTemporaryCampaignState();
            var pathProvider = new TemporaryCampaignSavePathProvider();
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StageEditorDirectPlayLauncher.PrimeCampaignTempSlotForTests(
                    StageId.CreateOrThrow("stage-2-2"),
                    resolver,
                    invalidChances));

            Assert.That(
                File.Exists(pathProvider.GetSaveFilePath(FileCampaignProfileRepository.ProfileFileName)),
                Is.False);
            Assert.That(
                File.Exists(pathProvider.GetSaveFilePath(CampaignLocalLaunchStateRepository.FileName)),
                Is.False);
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void CampaignProductionDirectPlay_InvalidChancesFailBeforeChangingInjectedState(
            int invalidChances)
        {
            var store = new TransientCampaignSaveSlotStore(
                CreateTransientNamespace("production-invalid-chances"));
            var activeSlotProvider = new ActiveSlotProvider(
                new TransientActiveSlotStorage(
                    CreateTransientNamespace("production-invalid-chances-active")));
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            store.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                StageId.CreateOrThrow("stage-1-1"),
                "level-1",
                2,
                string.Empty));
            activeSlotProvider.SetActiveSlot(1);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StageEditorDirectPlayLauncher.PrimeCampaignProductionSlotForTests(
                    StageId.CreateOrThrow("stage-2-2"),
                    resolver,
                    invalidChances,
                    1,
                    store,
                    activeSlotProvider));

            Assert.That(store.LoadSlot(1).CurrentStageId.Value, Is.EqualTo("stage-1-1"));
            Assert.That(store.LoadSlot(1).RemainingChances, Is.EqualTo(2));
            Assert.That(activeSlotProvider.ActiveSlotNumber, Is.EqualTo(1));
            Assert.That(EditorDirectPlayContextStore.TryGetCurrent(out _), Is.False);
            Assert.That(StageLaunchContextStore.TryPeekPendingEditorDirectPlay(out _), Is.False);
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

                var slot = saveStore.LoadSlot(2).State;
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
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                Assert.That(
                    evaluator.Evaluate(CreateState("stage-9-9", "level-9", false)).Status,
                    Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromSequence));

	                Assert.That(
	                    evaluator.Evaluate(CreateState("stage-2-1", "level-2", false)).Status,
	                    Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromCatalog));
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
        public void DirectPlayChanceIngress_DoesNotClampInvalidCampaignValues()
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayLauncher.cs"),
                Does.Not.Contain("Mathf.Clamp(remainingChances"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Stages/Editor/StageEditorDirectPlayWindow.cs"),
                Does.Not.Contain("Mathf.Clamp(_remainingChances"));
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_RejectsStageIdNotInSequence()
        {
            var provider = CreateProvider("stage-9-9");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                var result = evaluator.Evaluate(CreateState("stage-9-9", "level-9", false));
                var actionPolicy = CampaignSlotActionPolicy.Evaluate(result);

                Assert.That(result.Status, Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromSequence));
                Assert.That(actionPolicy.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_RejectsStageIdMissingFromCatalog()
        {
            var provider = CreateProvider("stage-0-1");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                var result = evaluator.Evaluate(CreateState("stage-2-1", "level-2", false));
                var actionPolicy = CampaignSlotActionPolicy.Evaluate(result);

                Assert.That(result.Status, Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromCatalog));
                Assert.That(actionPolicy.CanContinue, Is.False);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_ClassifiesCanonicalLaunchMatrixWithoutMutatingState()
        {
            var provider = CreateProvider("stage-1-1", "stage-2-2");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);
                var empty = evaluator.Evaluate(CampaignSlotStateFactory.CreateEmptyEntry(1));
                var readyState = CreateState("stage-1-1", "level-1", campaignCompleted: false);
                var ready = evaluator.Evaluate(readyState);
                var completedState = CreateState("stage-1-1", "level-1", campaignCompleted: true);
                var completed = evaluator.Evaluate(completedState);
                var sequenceMissingState = CreateState("stage-9-9", "level-9", campaignCompleted: false);
                var sequenceMissing = evaluator.Evaluate(sequenceMissingState);
                var catalogMissingState = CreateState("stage-2-1", "level-2", campaignCompleted: false);
                var catalogMissing = evaluator.Evaluate(catalogMissingState);
                var synchronizationState = CreateState("stage-2-2", "level-5", campaignCompleted: false);
                var synchronizationRequired = evaluator.Evaluate(synchronizationState);

                Assert.That(empty.Status, Is.EqualTo(CampaignSlotLaunchStatus.Empty));
                Assert.That(empty.State, Is.Null);
                Assert.That(empty.ResolvedStageId, Is.EqualTo(StageId.None));
                Assert.That(empty.ResolvedLevelGroupId, Is.Empty);

                Assert.That(ready.Status, Is.EqualTo(CampaignSlotLaunchStatus.Ready));
                Assert.That(ready.State, Is.SameAs(readyState));
                Assert.That(ready.ResolvedStageId, Is.EqualTo(readyState.CurrentStageId));
                Assert.That(ready.ResolvedLevelGroupId, Is.EqualTo("level-1"));

                Assert.That(completed.Status, Is.EqualTo(CampaignSlotLaunchStatus.Completed));
                Assert.That(completed.State, Is.SameAs(completedState));

                Assert.That(
                    sequenceMissing.Status,
                    Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromSequence));
                Assert.That(sequenceMissing.State, Is.SameAs(sequenceMissingState));
                Assert.That(sequenceMissing.ResolvedStageId, Is.EqualTo(sequenceMissingState.CurrentStageId));
                Assert.That(sequenceMissing.ResolvedLevelGroupId, Is.Empty);

                Assert.That(
                    catalogMissing.Status,
                    Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromCatalog));
                Assert.That(catalogMissing.State, Is.SameAs(catalogMissingState));
                Assert.That(catalogMissing.ResolvedLevelGroupId, Is.EqualTo("level-2"));

                Assert.That(
                    synchronizationRequired.Status,
                    Is.EqualTo(CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired));
                Assert.That(synchronizationRequired.State, Is.SameAs(synchronizationState));
                Assert.That(synchronizationRequired.ResolvedLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(synchronizationState.CurrentLevelGroupId, Is.EqualTo("level-5"));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotActionPolicy_SeparatesLaunchAndLifecycleAvailability()
        {
            var provider = CreateProvider("stage-1-1", "stage-2-2");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);

                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CampaignSlotStateFactory.CreateEmptyEntry(1))),
                    canContinue: false,
                    canRestart: false,
                    canDelete: false);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-1-1", "level-1", false))),
                    canContinue: true,
                    canRestart: true,
                    canDelete: true);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-1-1", "level-1", true))),
                    canContinue: false,
                    canRestart: true,
                    canDelete: true);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-9-9", "level-9", false))),
                    canContinue: false,
                    canRestart: true,
                    canDelete: true);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-2-1", "level-2", false))),
                    canContinue: false,
                    canRestart: true,
                    canDelete: true);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-2-2", "level-5", false))),
                    canContinue: true,
                    canRestart: true,
                    canDelete: true);
                AssertPolicy(
                    CampaignSlotActionPolicy.Evaluate(
                        evaluator.Evaluate(CreateState("stage-2-2", "level-5", true))),
                    canContinue: false,
                    canRestart: true,
                    canDelete: true);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotLaunchEvaluationAndPolicy_RemainSeparatedAndReadOnly()
        {
            var provider = CreateProvider("stage-2-2");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);
                var source = CreateState("stage-2-2", "level-5", false);

                var evaluation = evaluator.Evaluate(source);
                var actionPolicy = CampaignSlotActionPolicy.Evaluate(evaluation);

                Assert.That(
                    evaluation.Status,
                    Is.EqualTo(CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired));
                Assert.That(evaluation.State, Is.SameAs(source));
                Assert.That(evaluation.ResolvedLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(actionPolicy.CanContinue, Is.True);
                Assert.That(source.CurrentLevelGroupId, Is.EqualTo("level-5"));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotLaunchStatus_DoesNotContainProfileLoadFailures()
        {
            Assert.That(
                Enum.GetNames(typeof(CampaignSlotLaunchStatus)),
                Does.Not.Contain("UnsupportedVersion"));
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_SourceIsReadOnlyAndRepositoryFree()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/CampaignSlotLaunchEvaluation.cs");

            Assert.That(source, Does.Not.Contain("ICampaignSaveQuery"));
            Assert.That(source, Does.Not.Contain("ICampaignSlotMaintenancePort"));
            Assert.That(source, Does.Not.Contain("ReplaceValidatedSlot("));
            Assert.That(source, Does.Not.Contain("ValidateAndSync"));
            Assert.That(source, Does.Not.Contain("PlayerPrefs"));
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1, false)]
        [TestCase(-1, true)]
        [TestCase(0, true)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1, true)]
        public void CampaignSlotRawDataMapper_RejectsInvalidChancesBeforeLaunchEvaluation(
            int invalidChances,
            bool campaignCompleted)
        {
            var raw = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    CurrentLevelGroupId = "level-1",
                    RemainingChances = invalidChances,
                    CampaignCompleted = campaignCompleted,
                };

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(raw));
        }

        [TestCase(0)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void CampaignSlotRawDataMapper_InvalidEmptyShapedSlotPreservesRawValue(
            int invalidChances)
        {
            var slot = SaveSlotData.CreateEmpty(1);
            slot.RemainingChances = invalidChances;
            var diagnosticClone = slot.Clone();

            Assert.That(slot.IsEmpty, Is.False);
            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
            Assert.That(diagnosticClone.RemainingChances, Is.EqualTo(invalidChances));
        }

        [Test]
        public void CampaignSlotRawDataMapper_NullInputDoesNotSynthesizeFallbackSlotState()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CampaignSlotRawDataMapper.ToDocument(null));
        }

        [Test]
        public void CampaignSlotRawDataMapper_CorruptNestedStatePreservesDiagnosticCloneShape()
        {
            var slot = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                    RemainingChances = 2,
                    NormalStagePerformanceRecords = new NormalStagePerformanceRecord[] { null },
                    StageClearProfileSnapshot = new StageClearProfileSnapshot
                    {
                        ClearRecordsByStageId = null,
                        ProcessedStageRunIds = null,
                        ProcessedClearAttemptIds = null,
                    },
                };

            SaveSlotData diagnosticClone = null;
            Assert.DoesNotThrow(() => diagnosticClone = slot.Clone());

            Assert.Throws<ArgumentException>(() =>
                CampaignSlotRawDataMapper.ToDocument(slot));
            Assert.That(diagnosticClone.NormalStagePerformanceRecords, Has.Length.EqualTo(1));
            Assert.That(diagnosticClone.NormalStagePerformanceRecords[0], Is.Null);
            Assert.That(diagnosticClone.StageClearProfileSnapshot, Is.Not.SameAs(
                    slot.StageClearProfileSnapshot));
            Assert.That(diagnosticClone.StageClearProfileSnapshot.ClearRecordsByStageId, Is.Null);
            Assert.That(diagnosticClone.StageClearProfileSnapshot.ProcessedStageRunIds, Is.Null);
            Assert.That(diagnosticClone.StageClearProfileSnapshot.ProcessedClearAttemptIds, Is.Null);
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_ReportsLevelGroupSynchronizationWithoutMutation()
        {
            var provider = CreateProvider("stage-2-2");
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);
                var state = CreateState("stage-2-2", "level-5", false);

                var result = evaluator.Evaluate(state);

                Assert.That(result.Status, Is.EqualTo(CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired));
                Assert.That(result.ResolvedLevelGroupId, Is.EqualTo("level-2"));
                Assert.That(state.CurrentLevelGroupId, Is.EqualTo("level-5"));
            }
            finally
            {
                provider.Dispose();
            }
        }

        [Test]
        public void CampaignSlotLaunchEvaluator_RetiredPreReleaseStageIsNotAutoRepaired()
        {
            var provider = CreateProvider("stage-4-3");
            var saveStore = new TransientCampaignSaveSlotStore();
            saveStore.ClearAll();
            try
            {
                var evaluator = new CampaignSlotLaunchEvaluator(
                    CampaignStageSequenceTestAsset.LoadProductionResolver(),
                    provider.Provider);
                var legacySlot = new SaveSlotData
                {
                    SlotNumber = 1,
                    CurrentStageId = StageId.CreateOrThrow("stage-5-1"),
                    CurrentLevelGroupId = "level-5",
                    RemainingChances = 2,
                };

                var state = CampaignSlotRawDataMapper.ToState(legacySlot);
                var result = evaluator.Evaluate(state);

                Assert.That(result.Status, Is.EqualTo(CampaignSlotLaunchStatus.StageMissingFromSequence));
                Assert.That(result.State, Is.SameAs(state));
                Assert.That(state.CurrentStageId.Value, Is.EqualTo("stage-5-1"));
                Assert.That(state.CurrentLevelGroupId, Is.EqualTo("level-5"));
                Assert.That(state.CampaignCompleted, Is.False);

                saveStore.ImportSlotSeed(
                    CampaignSlotRawDataMapper.ToSeedImportRequest(legacySlot));
                var persisted = saveStore.LoadSlot(1);

                Assert.That(persisted.CurrentStageId.Value, Is.EqualTo("stage-5-1"));
                Assert.That(persisted.CurrentLevelGroupId, Is.EqualTo("level-5"));
                Assert.That(persisted.CampaignCompleted, Is.False);
            }
            finally
            {
                saveStore.ClearAll();
                provider.Dispose();
            }
        }

        [Test]
        public void RetiredPreReleaseSaveCompatibility_IsAbsentFromRuntimeSurface()
        {
            Assert.That(
                File.Exists(
                    "Assets/_Features/Stages/Runtime/Campaign/RetiredCampaignSaveCompatibilityPolicy.cs"),
                Is.False);
            Assert.That(
                File.Exists(
                    "Assets/_Features/Stages/Runtime/Campaign/SaveSlotValidationService.cs"),
                Is.False);
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

        private static CampaignSlotState CreateState(
            string stageId,
            string levelGroupId,
            bool campaignCompleted)
        {
            return CampaignSlotRawDataMapper.ToState(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = 2,
                CampaignCompleted = campaignCompleted,
            });
        }

        private static void AssertPolicy(
            CampaignSlotActionPolicy policy,
            bool canContinue,
            bool canRestart,
            bool canDelete)
        {
            Assert.That(policy.CanContinue, Is.EqualTo(canContinue));
            Assert.That(policy.CanRestart, Is.EqualTo(canRestart));
            Assert.That(policy.CanDelete, Is.EqualTo(canDelete));
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
