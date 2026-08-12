using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveMigrationCoordinatorTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";
        private const string ProfileId = "coordinator-test-profile";
        private const string ProductVersion = "coordinator-test-product";

        [TearDown]
        public void TearDown()
        {
            StageLaunchContextStore.Clear();
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.SaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.ActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacySaveSlotsKey);
            PlayerPrefs.DeleteKey(SaveSlotPrefsKeys.LegacyActiveSaveSlotKey);
            PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempSaveSlotStoreKey);
            PlayerPrefs.DeleteKey(EditorDirectPlayContextStore.TempActiveSlotProviderKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportDisabledKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ImportedSourceHashKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.ResetTombstoneUtcKey);
            PlayerPrefs.DeleteKey(CampaignLegacyImportMarkerStore.DeletedSlotGuardsKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void LoadedProfile_FileWinsAndImporterIsNotCalled()
        {
            var document = CreateDocument("file-profile");
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Loaded, document));
            var importer = new RecordingImporter(Importable("legacy-hash"));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.FileLoaded));
            Assert.That(result.Document, Is.SameAs(document));
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void BackupRecovered_FileWinsAndImporterIsNotCalled()
        {
            var document = CreateDocument("backup-profile");
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.BackupRecovered, document));
            var importer = new RecordingImporter(Importable("legacy-hash"));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.FileBackupRecovered));
            Assert.That(result.Document, Is.SameAs(document));
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingProfileAndNoLegacy_ReturnsLegacyMissingWithoutWrite()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(LegacyResult(CampaignLegacyImportStatus.Missing, null, string.Empty));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.LegacyMissing));
            Assert.That(result.ProfileWriteAttempted, Is.False);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingProfileAndImportDisabled_ReturnsImportBlockedWithoutImporter()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(Importable("legacy-hash"));
            var marker = new RecordingMarkerStore { ImportDisabled = true };

            var result = CreateCoordinator(repository, importer, marker).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportBlocked));
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingProfileAndResetTombstone_ReturnsImportBlockedWithoutImporter()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(Importable("legacy-hash"));
            var marker = new RecordingMarkerStore { ResetTombstoneUtc = "2026-07-07T01:00:00Z" };

            var result = CreateCoordinator(repository, importer, marker).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportBlocked));
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingProfileAndInvalidLegacy_DoesNotWriteFileAndDoesNotDeletePlayerPrefs()
        {
            const string invalidPayload = "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":";
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, invalidPayload);
            PlayerPrefs.Save();
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(
                LegacyResult(CampaignLegacyImportStatus.InvalidPayload, null, "invalid-hash"));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.LegacyInvalid));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(PlayerPrefs.HasKey(SaveSlotPrefsKeys.SaveSlotsKey), Is.True);
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo(invalidPayload));
        }

        [Test]
        public void MissingProfileAndValidLegacyWithWriteDisabled_ReturnsDeferredCandidateOnly()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(Importable("legacy-hash"));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.MigrationDeferred));
            Assert.That(result.HasImportCandidate, Is.True);
            Assert.That(result.ProfileWriteAttempted, Is.False);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void MissingProfileAndValidLegacyWithExplicitWriteEnabled_SavesProfileJson()
        {
            using var harness = new RepositoryHarness();
            WriteAllowlistPayload(CreateSlot(1, "stage-1-1", "level-1"));
            var importer = new LegacyPlayerPrefsCampaignImporter(
                new CampaignLegacySourceReader(),
                new CampaignLegacyImportMarkerStore(),
                () => FixedNowUtc,
                ProfileId,
                ProductVersion);
            var coordinator = new CampaignSaveMigrationCoordinator(
                harness.Repository,
                importer,
                new CampaignLegacyImportMarkerStore(),
                new CampaignSaveMigrationOptions { EnableProfileWrite = true });

            var result = coordinator.Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(File.Exists(harness.ProfilePath), Is.True);
            Assert.That(harness.Repository.Load().Document.ProfileId, Is.EqualTo(ProfileId));
        }

        [Test]
        public void ImportCandidateClone_PreservesNormalCampaignCompletionReceipt()
        {
            var importedDocument = CreateDocument("legacy-profile-with-receipt");
            var sourceReceipt = new NormalCampaignCompletionReceiptDocument
            {
                Version = NormalCampaignCompletionReceipt.CurrentVersion,
                CompletedStageId = "stage-5-1",
                StageRunId = string.Empty,
                ClearSource = NormalCampaignCompletionReceipt.LegacyClearSourceAbsent,
            };
            importedDocument.Slots[0].HasNormalCampaignCompletionReceipt = true;
            importedDocument.Slots[0].NormalCampaignCompletionReceipt = sourceReceipt;
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(
                LegacyResult(CampaignLegacyImportStatus.Importable, importedDocument, "source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    options: new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            var savedSlot = repository.SavedDocument.Slots[0];
            Assert.That(savedSlot.HasNormalCampaignCompletionReceipt, Is.True);
            Assert.That(savedSlot.NormalCampaignCompletionReceipt, Is.Not.Null);
            Assert.That(savedSlot.NormalCampaignCompletionReceipt, Is.Not.SameAs(sourceReceipt));
            Assert.That(savedSlot.NormalCampaignCompletionReceipt.Version, Is.EqualTo(sourceReceipt.Version));
            Assert.That(savedSlot.NormalCampaignCompletionReceipt.CompletedStageId, Is.EqualTo(sourceReceipt.CompletedStageId));
            Assert.That(savedSlot.NormalCampaignCompletionReceipt.StageRunId, Is.EqualTo(sourceReceipt.StageRunId));
            Assert.That(savedSlot.NormalCampaignCompletionReceipt.ClearSource, Is.EqualTo(sourceReceipt.ClearSource));
        }

        [Test]
        public void ImportSuccess_RecordsLocalImportedSourceHashMarker()
        {
            var marker = new RecordingMarkerStore();
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    marker,
                    new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportSucceeded));
            Assert.That(marker.ImportedSourceHash, Is.EqualTo("source-hash"));
            Assert.That(marker.SetImportedSourceHashCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.LegacyImport.ImportedSourceHash, Is.EqualTo("source-hash"));
        }

        [Test]
        public void ImportWriteFailure_PreservesLegacyAndDoesNotRecordMarker()
        {
            const string legacyPayload = "{\"SchemaId\":\"StageClearSaveSlots\",\"SchemaVersion\":2}";
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, legacyPayload);
            PlayerPrefs.Save();
            var marker = new RecordingMarkerStore();
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing))
            {
                SaveException = new IOException("write failed"),
            };
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    marker,
                    new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.ImportWriteFailed));
            Assert.That(PlayerPrefs.GetString(SaveSlotPrefsKeys.SaveSlotsKey), Is.EqualTo(legacyPayload));
            Assert.That(marker.ImportedSourceHash, Is.Empty);
            Assert.That(marker.SetImportedSourceHashCount, Is.Zero);
        }

        [Test]
        public void SameImportedSourceHash_BlocksNormalRemigration()
        {
            var marker = new RecordingMarkerStore { ImportedSourceHash = "source-hash" };
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.Missing));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    marker,
                    new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.AlreadyImported));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(marker.SetImportedSourceHashCount, Is.Zero);
        }

        [Test]
        public void SchemaInvalid_DoesNotRawOverwriteFromLegacy()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.SchemaInvalid));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    options: new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.SchemaInvalid));
            Assert.That(result.RequiresRepair, Is.True);
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [TestCase(CampaignProfileLoadStatus.Unauthorized, CampaignSaveMigrationStatus.Unauthorized)]
        [TestCase(CampaignProfileLoadStatus.IoFailed, CampaignSaveMigrationStatus.IoFailed)]
        public void RepositoryReadFailure_DoesNotConsultLegacyOrWrite(
            CampaignProfileLoadStatus loadStatus,
            CampaignSaveMigrationStatus expectedStatus)
        {
            var repository = new RecordingRepository(LoadResult(loadStatus));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    options: new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void CorruptQuarantinedAndValidLegacy_DoesNotConsultLegacyOrFallback()
        {
            var marker = new RecordingMarkerStore();
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.CorruptQuarantined));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    marker,
                    new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.RepairRequired));
            Assert.That(result.HasImportCandidate, Is.False);
            Assert.That(result.RequiresRepair, Is.True);
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(marker.SetImportedSourceHashCount, Is.Zero);
        }

        [Test]
        public void CorruptNoFallbackAndNoLegacy_ReturnsRepairRequired()
        {
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.CorruptNoFallback));
            var importer = new RecordingImporter(LegacyResult(CampaignLegacyImportStatus.Missing, null, string.Empty));

            var result = CreateCoordinator(repository, importer).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.RepairRequired));
            Assert.That(result.RequiresRepair, Is.True);
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void CorruptNoFallbackAndValidLegacy_DoesNotConsultLegacyOrFallback()
        {
            var marker = new RecordingMarkerStore();
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.CorruptNoFallback));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(
                    repository,
                    importer,
                    marker,
                    new CampaignSaveMigrationOptions { EnableProfileWrite = true })
                .Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.RepairRequired));
            Assert.That(result.HasImportCandidate, Is.False);
            Assert.That(result.RequiresRepair, Is.True);
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(marker.SetImportedSourceHashCount, Is.Zero);
        }

        [TestCase(true, "")]
        [TestCase(false, "2026-07-07T01:00:00Z")]
        public void CorruptNoFallbackAndBlockedMarker_ReturnsRepairRequiredWithoutImport(
            bool importDisabled,
            string resetTombstoneUtc)
        {
            var marker = new RecordingMarkerStore
            {
                ImportDisabled = importDisabled,
                ResetTombstoneUtc = resetTombstoneUtc,
            };
            var repository = new RecordingRepository(LoadResult(CampaignProfileLoadStatus.CorruptNoFallback));
            var importer = new RecordingImporter(Importable("source-hash"));

            var result = CreateCoordinator(repository, importer, marker).Run();

            Assert.That(result.Status, Is.EqualTo(CampaignSaveMigrationStatus.RepairRequired));
            Assert.That(result.RequiresRepair, Is.True);
            Assert.That(importer.CallCount, Is.Zero);
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void ProductionComposition_DoesNotReferenceCampaignSaveMigrationCoordinator()
        {
            Assert.That(
                ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveMigrationCoordinator"));
            Assert.That(
                ReadRepoFile("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveMigrationCoordinator"));
            Assert.That(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("CampaignSaveMigrationCoordinator"));
        }

        [Test]
        public void SaveSlotStorePublicConstructor_StillUsesPlayerPrefsBackendByDefault()
        {
            using var harness = new RepositoryHarness();
            var store = new SaveSlotStore();

            store.SaveSlot(new SaveSlotData
            {
                SlotNumber = 1,
                CurrentStageId = StageId.CreateOrThrow("stage-1-1"),
                CurrentLevelGroupId = "level-1",
            });

            Assert.That(PlayerPrefs.HasKey(SaveSlotStore.DefaultPlayerPrefsKey), Is.True);
            Assert.That(File.Exists(harness.ProfilePath), Is.False);
            Assert.That(Directory.Exists(harness.SaveRootPath), Is.False);
        }

        [Test]
        public void CoordinatorSource_DoesNotDeletePlayerPrefsOrCallSteamApis()
        {
            var source = ReadRepoFile(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveMigrationCoordinator.cs");

            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(source, Does.Not.Contain("Steamworks"));
            Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"));
            Assert.That(source, Does.Not.Contain("SteamRemoteStorage"));
        }

        private static CampaignSaveMigrationCoordinator CreateCoordinator(
            RecordingRepository repository,
            RecordingImporter importer,
            RecordingMarkerStore marker = null,
            CampaignSaveMigrationOptions options = null)
        {
            return new CampaignSaveMigrationCoordinator(
                repository,
                importer,
                marker ?? new RecordingMarkerStore(),
                options ?? new CampaignSaveMigrationOptions());
        }

        private static CampaignProfileLoadResult LoadResult(
            CampaignProfileLoadStatus status,
            CampaignProfileDocument document = null)
        {
            return new CampaignProfileLoadResult(status, document, status.ToString());
        }

        private static CampaignLegacyImportResult Importable(string importedSourceHash)
        {
            return LegacyResult(
                CampaignLegacyImportStatus.Importable,
                CreateDocument("legacy-profile"),
                importedSourceHash);
        }

        private static CampaignLegacyImportResult LegacyResult(
            CampaignLegacyImportStatus status,
            CampaignProfileDocument document,
            string importedSourceHash)
        {
            return new CampaignLegacyImportResult(
                status,
                document,
                importedSourceHash,
                status.ToString(),
                status != CampaignLegacyImportStatus.Missing,
                status == CampaignLegacyImportStatus.ImportDisabled);
        }

        private static CampaignProfileDocument CreateDocument(string profileId)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = 1,
                ProductVersion = ProductVersion,
                SavedAtUtc = FixedNowUtc,
                ProfileId = profileId,
                LastPlayedSlotNumber = 1,
                LegacyImport = new CampaignLegacyImportDocument(),
                Slots = new[]
                {
                    new CampaignSlotDocument
                    {
                        SlotNumber = 1,
                        StageId = "stage-1-1",
                        LevelGroupId = "level-1",
                        RemainingChances = 2,
                        LastPlayedAtUtc = FixedNowUtc,
                        StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
                    },
                },
            };
        }

        private static SaveSlotData CreateSlot(int slotNumber, string stageId, string levelGroupId)
        {
            return new SaveSlotData
            {
                SlotNumber = slotNumber,
                CurrentStageId = StageId.CreateOrThrow(stageId),
                CurrentLevelGroupId = levelGroupId,
                RemainingChances = 2,
                LastPlayedAt = FixedNowUtc,
                StageClearProfileSnapshot = new StageClearProfileSnapshot { Version = 1 },
            };
        }

        private static void WriteAllowlistPayload(params SaveSlotData[] slots)
        {
            PlayerPrefs.SetString(SaveSlotPrefsKeys.SaveSlotsKey, JsonUtility.ToJson(SaveSlotDtoMapper.ToDto(slots)));
            PlayerPrefs.Save();
        }

        private static string ReadRepoFile(string relativePath)
        {
            return File.ReadAllText(relativePath);
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            private readonly CampaignProfileLoadResult _loadResult;

            public RecordingRepository(CampaignProfileLoadResult loadResult)
            {
                _loadResult = loadResult;
            }

            public int SaveCount { get; private set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public Exception SaveException { get; set; }

            public CampaignProfileLoadResult Load()
            {
                return _loadResult;
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                if (SaveException != null)
                {
                    throw SaveException;
                }

                SavedDocument = document;
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

        private sealed class RecordingMarkerStore : ICampaignLegacyImportMarkerStore
        {
            public bool ImportDisabled { get; set; }

            public string ImportedSourceHash { get; set; } = string.Empty;

            public string ResetTombstoneUtc { get; set; } = string.Empty;

            public CampaignLegacyDeletedSlotGuardDocument[] DeletedSlotGuards { get; set; } =
                Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();

            public int SetImportedSourceHashCount { get; private set; }

            public bool IsImportDisabled()
            {
                return ImportDisabled;
            }

            public string GetImportedSourceHash()
            {
                return ImportedSourceHash;
            }

            public void SetImportedSourceHash(string importedSourceHash)
            {
                SetImportedSourceHashCount++;
                ImportedSourceHash = importedSourceHash ?? string.Empty;
            }

            public string GetResetTombstoneUtc()
            {
                return ResetTombstoneUtc;
            }

            public bool HasResetTombstone()
            {
                return !string.IsNullOrWhiteSpace(ResetTombstoneUtc);
            }

            public void RecordDeletedSlotGuard(
                int slotNumber,
                string importedSourceHash,
                string deletedAtUtc,
                string reason)
            {
                DeletedSlotGuards = new[]
                {
                    new CampaignLegacyDeletedSlotGuardDocument
                    {
                        SlotNumber = slotNumber,
                        ImportedSourceHash = importedSourceHash ?? string.Empty,
                        DeletedAtUtc = deletedAtUtc ?? string.Empty,
                        Reason = reason ?? string.Empty,
                    },
                };
            }

            public CampaignLegacyDeletedSlotGuardDocument[] ReadDeletedSlotGuards()
            {
                return DeletedSlotGuards ?? Array.Empty<CampaignLegacyDeletedSlotGuardDocument>();
            }
        }

        private sealed class RepositoryHarness : IDisposable
        {
            private readonly string _testRootPath;

            public RepositoryHarness()
            {
                _testRootPath = Path.Combine(
                    "Temp",
                    "CampaignSaveMigrationCoordinatorTests",
                    Guid.NewGuid().ToString("N"));
                SaveRootPath = Path.Combine(_testRootPath, "Saves");
                Repository = new FileCampaignProfileRepository(new AtomicTextFileStore(SaveRootPath));
            }

            public string SaveRootPath { get; }

            public FileCampaignProfileRepository Repository { get; }

            public string ProfilePath => Path.Combine(SaveRootPath, FileCampaignProfileRepository.ProfileFileName);

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(_testRootPath))
                    {
                        Directory.Delete(_testRootPath, recursive: true);
                    }
                }
                catch
                {
                }
            }
        }
    }
}
