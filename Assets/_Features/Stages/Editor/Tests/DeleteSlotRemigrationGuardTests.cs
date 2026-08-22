using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class DeleteSlotRemigrationGuardTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";
        private const string ProfileId = "delete-slot-remigration-guard-profile";
        private const string ProductVersion = "delete-slot-remigration-guard-product";

        [SetUp]
        public void SetUp()
        {
            CleanupPlayerPrefsState();
            AssertPlayerPrefsStateClean();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupPlayerPrefsState();
        }

        private static void CleanupPlayerPrefsState()
        {
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        private static void AssertPlayerPrefsStateClean()
        {
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.ActiveSaveSlotKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacySaveSlotsKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CampaignLegacyImportMarkerStore.ImportDisabledKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey), Is.False);
            Assert.That(PlayerPrefs.HasKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey), Is.False);
        }

        [Test]
        public void DeleteSlot_WritesDeletedSlotGuardForImportedLegacySource()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            var repository = new RecordingRepository(
                CampaignProfileLoadStatus.Loaded,
                CreateDocument(
                    "profile",
                    "source-hash",
                    CreateSlot(1, "stage-1-1"),
                    CreateSlot(2, "stage-2-1")));
            repository.Document.LastPlayedSlotNumber = 2;
            var service = CreateService(repository, markerStore);

            var result = service.DeleteSlot(2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(ContainsSlot(repository.SavedDocument, 1), Is.True);
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.False);
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(1));
            var guard = FindGuard(repository.SavedDocument.LegacyImport.DeletedSlotGuards, 2, "source-hash");
            Assert.That(guard, Is.Not.Null);
            Assert.That(guard.DeletedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(guard.Reason, Is.EqualTo("DeleteSlot"));
        }

        [Test]
        public void DeleteSlot_MirrorsDeletedSlotGuardIntoLocalMarker()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            var repository = new RecordingRepository(
                CampaignProfileLoadStatus.Loaded,
                CreateDocument("profile", "source-hash", CreateSlot(2, "stage-2-1")));
            var service = CreateService(repository, markerStore);

            var result = service.DeleteSlot(2);

            Assert.That(result.Succeeded, Is.True);
            var guards = markerStore.ReadDeletedSlotGuards();
            var guard = FindGuard(guards, 2, "source-hash");
            Assert.That(guard, Is.Not.Null);
            Assert.That(guard.DeletedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(guard.Reason, Is.EqualTo("DeleteSlot"));
        }

        [Test]
        public void DeleteSlot_WithEmptyImportedSourceHashWritesSourceAgnosticGuard()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            var repository = new RecordingRepository(
                CampaignProfileLoadStatus.Loaded,
                CreateDocument("profile", string.Empty, CreateSlot(2, "stage-2-1")));
            var service = CreateService(repository, markerStore);

            var result = service.DeleteSlot(2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                FindGuard(repository.SavedDocument.LegacyImport.DeletedSlotGuards, 2, string.Empty),
                Is.Not.Null);
            Assert.That(FindGuard(markerStore.ReadDeletedSlotGuards(), 2, string.Empty), Is.Not.Null);
        }

        [Test]
        public void SameSourceHash_CannotResurrectDeletedSlotAndImportsUnguardedSlots()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.SetImportedSourceHash("source-hash");
            markerStore.RecordDeletedSlotGuard(2, "source-hash", FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument(
                "legacy",
                "source-hash",
                CreateSlot(1, "stage-1-1"),
                CreateSlot(2, "stage-2-1"));
            legacyDocument.LastPlayedSlotNumber = 2;
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(ContainsSlot(repository.SavedDocument, 1), Is.True);
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.False);
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(1));
        }

        [Test]
        public void ChangedSourceContainingGuardedSlot_ReturnsDeferredNoWrite()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, "old-source-hash", FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument(
                "legacy",
                "new-source-hash",
                CreateSlot(1, "stage-1-1"),
                CreateSlot(2, "stage-2-1"));
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "new-source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(result.ProfileWriteAttempted, Is.False);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(result.Message, Does.Contain("changed"));
            Assert.That(result.Message, Does.Contain("guarded deleted slot"));
        }

        [Test]
        public void AllGuardedImportableSlots_ReturnsDeferredNoWrite()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, "source-hash", FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument("legacy", "source-hash", CreateSlot(2, "stage-2-1"));
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(result.ProfileWriteAttempted, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void EmptySourceHashGuard_BlocksAnyAutomaticResurrectionForThatSlot()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, string.Empty, FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument(
                "legacy",
                "changed-source-hash",
                CreateSlot(1, "stage-1-1"),
                CreateSlot(2, "stage-2-1"));
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "changed-source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(ContainsSlot(repository.SavedDocument, 1), Is.True);
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.False);
        }

        [Test]
        public void ProfileMissing_LocalMarkerPreventsSameSourceSlotResurrection()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, "source-hash", FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument(
                "legacy",
                "source-hash",
                CreateSlot(1, "stage-1-1"),
                CreateSlot(2, "stage-2-1"));
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.False);
        }

        [Test]
        public void DeleteSlotThenInitializeNewGameOnSameSlot_RetainsOldSourceGuard()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            var repository = new RecordingRepository(
                CampaignProfileLoadStatus.Loaded,
                CreateDocument("profile", "source-hash", CreateSlot(2, "stage-2-1")));
            var service = CreateService(repository, markerStore);

            var deleteResult = service.DeleteSlot(2);
            var newGameResult = service.InitializeNewGame(2, "stage-1-1", "level-1");

            Assert.That(deleteResult.Succeeded, Is.True);
            Assert.That(newGameResult.Succeeded, Is.True);
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.True);
            Assert.That(
                FindGuard(repository.SavedDocument.LegacyImport.DeletedSlotGuards, 2, "source-hash"),
                Is.Not.Null);
            Assert.That(FindGuard(markerStore.ReadDeletedSlotGuards(), 2, "source-hash"), Is.Not.Null);
        }

        [Test]
        public void ClearAll_SupersedesSlotGuardsWithImportDisabledResetTombstone()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            var document = CreateDocument("profile", "source-hash", CreateSlot(2, "stage-2-1"));
            document.LegacyImport.DeletedSlotGuards = new[]
            {
                new CampaignLegacyDeletedSlotGuardDocument
                {
                    SlotNumber = 2,
                    ImportedSourceHash = "source-hash",
                    DeletedAtUtc = "2026-07-06T00:00:00Z",
                    Reason = "DeleteSlot",
                },
            };
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Loaded, document);
            var service = new CampaignSaveService(
                repository,
                new CampaignLegacyImportResetMarkerPort(markerStore),
                () => FixedNowUtc,
                ProfileId,
                ProductVersion,
                new CampaignLegacyDeletedSlotGuardMarkerPort(markerStore));

            var clearResult = service.ClearAll();
            var importer = new RecordingImporter(Importable(
                CreateDocument("legacy", "source-hash", CreateSlot(2, "stage-2-1")),
                "source-hash"));
            var migrationResult = CreateCoordinator(
                    new RecordingRepository(CampaignProfileLoadStatus.Missing),
                    importer,
                    markerStore,
                    enableProfileWrite: true)
                .Run();

            Assert.That(clearResult.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.LegacyImport.ImportDisabled, Is.True);
            Assert.That(repository.SavedDocument.LegacyImport.ResetTombstoneUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(repository.SavedDocument.LegacyImport.DeletedSlotGuards, Is.Empty);
            Assert.That(migrationResult.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportBlocked));
            Assert.That(importer.CallCount, Is.Zero);
        }

        [Test]
        public void LegacyActiveSlotPointingToGuardedSlot_DoesNotBecomeLastPlayedSlotNumber()
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, "source-hash", FixedNowUtc, "DeleteSlot");
            var legacyDocument = CreateDocument(
                "legacy",
                "source-hash",
                CreateSlot(1, "stage-1-1"),
                CreateSlot(2, "stage-2-1"));
            legacyDocument.LastPlayedSlotNumber = 2;
            var repository = new RecordingRepository(CampaignProfileLoadStatus.Missing);
            var importer = new RecordingImporter(Importable(legacyDocument, "source-hash"));

            var result = CreateCoordinator(repository, importer, markerStore, enableProfileWrite: true).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(1));
            Assert.That(ContainsSlot(repository.SavedDocument, 2), Is.False);
        }

        [TestCase(CampaignProfileLoadStatus.Loaded, CampaignSaveMigrationStatus.FileLoaded)]
        [TestCase(CampaignProfileLoadStatus.BackupRecovered, CampaignSaveMigrationStatus.FileBackupRecovered)]
        public void LoadedOrBackupRecoveredProfileWinsAndIgnoresLegacy(
            CampaignProfileLoadStatus loadStatus,
            CampaignSaveMigrationStatus expectedStatus)
        {
            var markerStore = new CampaignLegacyImportMarkerStore();
            markerStore.RecordDeletedSlotGuard(2, "source-hash", FixedNowUtc, "DeleteSlot");
            var fileDocument = CreateDocument("file", "source-hash", CreateSlot(1, "stage-1-1"));
            var importer = new RecordingImporter(Importable(
                CreateDocument("legacy", "source-hash", CreateSlot(2, "stage-2-1")),
                "source-hash"));

            var result = CreateCoordinator(
                    new RecordingRepository(loadStatus, fileDocument),
                    importer,
                    markerStore,
                    enableProfileWrite: true)
                .Run();

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.Document, Is.SameAs(fileDocument));
            Assert.That(importer.CallCount, Is.Zero);
        }

        [Test]
        public void ProductionComposition_UsesProviderWithoutDirectProfileInternals()
        {
            var paths = new[]
            {
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs",
            };

            for (var i = 0; i < paths.Length; i++)
            {
                var source = File.ReadAllText(paths[i]);
                Assert.That(source, Does.Not.Contain("CampaignSaveServiceFactory"), paths[i]);
                Assert.That(source, Does.Not.Contain("CampaignSaveService"), paths[i]);
                Assert.That(source, Does.Not.Contain("CampaignSaveMigrationCoordinator"), paths[i]);
                Assert.That(source, Does.Not.Contain("profile.json"), paths[i]);
            }

            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Contain("CampaignSaveCompositionProvider.CreateProductionProfileBacked()"));
        }

        private static CampaignSaveService CreateService(
            RecordingRepository repository,
            CampaignLegacyImportMarkerStore markerStore)
        {
            return new CampaignSaveService(
                repository,
                null,
                () => FixedNowUtc,
                ProfileId,
                ProductVersion,
                new CampaignLegacyDeletedSlotGuardMarkerPort(markerStore));
        }

        private static CampaignSaveMigrationCoordinator CreateCoordinator(
            RecordingRepository repository,
            RecordingImporter importer,
            CampaignLegacyImportMarkerStore markerStore,
            bool enableProfileWrite)
        {
            return new CampaignSaveMigrationCoordinator(
                repository,
                importer,
                markerStore,
                new CampaignSaveMigrationOptions { EnableProfileWrite = enableProfileWrite });
        }

        private static CampaignLegacyImportResult Importable(
            CampaignProfileDocument document,
            string importedSourceHash)
        {
            return new CampaignLegacyImportResult(
                CampaignLegacyImportStatus.Importable,
                document,
                importedSourceHash,
                "importable",
                sourceFound: true,
                importDisabled: false);
        }

        private static CampaignProfileDocument CreateDocument(
            string profileId,
            string importedSourceHash,
            params CampaignSlotDocument[] slots)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = ProductVersion,
                SavedAtUtc = FixedNowUtc,
                ProfileId = profileId,
                LastPlayedSlotNumber = slots.Length > 0 ? slots[0].SlotNumber : 0,
                LegacyImport = new CampaignLegacyImportDocument
                {
                    ImportedSourceHash = importedSourceHash ?? string.Empty,
                },
                Slots = slots,
            };
        }

        private static CampaignSlotDocument CreateSlot(int slotNumber, string stageId)
        {
            return new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                StageId = stageId,
                LevelGroupId = "level-1",
                RemainingChances = 2,
                LastPlayedAtUtc = FixedNowUtc,
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private static bool ContainsSlot(CampaignProfileDocument document, int slotNumber)
        {
            var slots = document?.Slots ?? Array.Empty<CampaignSlotDocument>();
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotNumber == slotNumber)
                {
                    return true;
                }
            }

            return false;
        }

        private static CampaignLegacyDeletedSlotGuardDocument FindGuard(
            CampaignLegacyDeletedSlotGuardDocument[] guards,
            int slotNumber,
            string importedSourceHash)
        {
            guards ??= Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
            for (var i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (guard != null &&
                    guard.SlotNumber == slotNumber &&
                    string.Equals(
                        guard.ImportedSourceHash ?? string.Empty,
                        importedSourceHash ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    return guard;
                }
            }

            return null;
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            private readonly CampaignProfileLoadStatus _status;

            public RecordingRepository(
                CampaignProfileLoadStatus status,
                CampaignProfileDocument document = null)
            {
                _status = status;
                Document = document;
            }

            public CampaignProfileDocument Document { get; private set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public int SaveCount { get; private set; }

            public CampaignProfileLoadResult Load()
            {
                return new CampaignProfileLoadResult(_status, Document, _status.ToString());
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                SavedDocument = document;
                Document = document;
            }
        }

        private sealed class RecordingImporter : ICampaignLegacyImportCandidateSource
        {
            private readonly CampaignLegacyImportResult _result;

            public RecordingImporter(CampaignLegacyImportResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public CampaignLegacyImportResult BuildImportCandidate()
            {
                CallCount++;
                return _result;
            }
        }
    }
}
