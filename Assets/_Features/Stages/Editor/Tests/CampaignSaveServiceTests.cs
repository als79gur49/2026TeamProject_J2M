using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Stages.Editor.Tests
{
    public sealed class CampaignSaveServiceTests
    {
        private const string FixedNowUtc = "2026-07-07T00:00:00Z";
        private const string ProfileId = "campaign-save-service-test-profile";
        private const string ProductVersion = "campaign-save-service-test-product";
        private PlayerPrefsTestStateScope _playerPrefsState;

        [SetUp]
        public void SetUp()
        {
            _playerPrefsState = PlayerPrefsTestStateScope.Capture(
                PlayerPrefsKeySpec.String(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey),
                PlayerPrefsKeySpec.Int(RemovedCampaignPlayerPrefsKeys.ActiveSaveSlotKey),
                PlayerPrefsKeySpec.String(RemovedCampaignPlayerPrefsKeys.LegacySaveSlotsKey),
                PlayerPrefsKeySpec.Int(RemovedCampaignPlayerPrefsKeys.LegacyActiveSaveSlotKey),
                PlayerPrefsKeySpec.Float("settings.audio.master.volume"),
                PlayerPrefsKeySpec.Int("settings.audio.master.muted"),
                PlayerPrefsKeySpec.Int("settings.display.width"),
                PlayerPrefsKeySpec.Int("settings.display.height"),
                PlayerPrefsKeySpec.String("Game.Feature.Input.KeyboardMovementScheme"));
        }

        [TearDown]
        public void TearDown()
        {
            _playerPrefsState?.Dispose();
            _playerPrefsState = null;
        }

        [Test]
        public void InitializeNewGame_WritesDocument()
        {
            var repository = new RecordingRepository();
            var service = CreateService(repository);

            var result = service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(repository.SavedDocument.SavedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(repository.SavedDocument.Slots[0].LevelGroupId, Is.EqualTo("level-1"));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(CampaignSaveSlotPolicy.DefaultRemainingChances));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void GetSlots_ReturnsDocumentSlots()
        {
            var document = CreateDocument(
                CreateSlot(2, "stage-2-1", "level-2", remainingChances: 1));
            var service = CreateService(new RecordingRepository(document));

            var result = service.GetSlots();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Slots, Has.Length.EqualTo(1));
            Assert.That(result.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(result.Slots[0].StageId, Is.EqualTo("stage-2-1"));
            Assert.That(result.PreviousRemainingChances, Is.Null);
        }

        [TestCase("schema", CampaignProfileLoadStatus.UnsupportedVersion)]
        [TestCase("profile-id", CampaignProfileLoadStatus.InvalidDocument)]
        [TestCase("remaining-chances", CampaignProfileLoadStatus.InvalidDocument)]
        public void LoadedStatusWithInvalidDocument_FailsClosedWithoutRepairOrWrite(
            string malformedCase,
            CampaignProfileLoadStatus expectedProfileStatus)
        {
            var document = CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3));
            switch (malformedCase)
            {
                case "schema":
                    document.SchemaVersion = 0;
                    break;
                case "profile-id":
                    document.ProfileId = " ";
                    break;
                case "remaining-chances":
                    document.Slots[0].RemainingChances = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(malformedCase));
            }

            var repository = new RecordingRepository(document);
            var service = CreateService(repository);

            var result = service.SetIntroComicCompleted(1);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.LoadFailed));
            Assert.That(result.HasProfileLoadStatus, Is.True);
            Assert.That(result.ProfileLoadStatus, Is.EqualTo(expectedProfileStatus));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument, Is.SameAs(document));
        }

        [Test]
        public void ImportSlotSeed_ReplacesSlotWithNarrowCleanState()
        {
            var current = CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3, totalDeaths: 7);
            current.IntroComicCompleted = true;
            var repository = new RecordingRepository(CreateDocument(current));
            var service = CreateService(repository);
            var result = service.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                StageId.CreateOrThrow("stage-1-2"),
                "level-1",
                2,
                string.Empty));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-1-2"));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(2));
            Assert.That(repository.SavedDocument.Slots[0].TotalDeaths, Is.Zero);
            Assert.That(repository.SavedDocument.Slots[0].IntroComicCompleted, Is.False);
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
            Assert.That(repository.SavedDocument.SavedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void PrepareContinue_SynchronizesOnlyLevelGroupAndReturnsCommittedState()
        {
            var current = CreateSlot(
                1,
                "stage-2-2",
                "level-5",
                remainingChances: 2,
                totalDeaths: 7);
            current.CampaignCompleted = false;
            current.IntroComicCompleted = true;
            current.OutroComicCompleted = true;
            current.LastPlayedAtUtc = "2026-07-01T00:00:00Z";
            var repository = new RecordingRepository(CreateDocument(current));
            var service = CreateService(repository);

            var result = service.PrepareContinue(new CampaignContinuePreparationCommand(
                1,
                StageId.CreateOrThrow("stage-2-2"),
                "level-5",
                "level-2"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.SavedDocument.Slots[0].LevelGroupId, Is.EqualTo("level-2"));
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-2-2"));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(2));
            Assert.That(repository.SavedDocument.Slots[0].TotalDeaths, Is.EqualTo(7));
            Assert.That(repository.SavedDocument.Slots[0].IntroComicCompleted, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].OutroComicCompleted, Is.True);
            Assert.That(
                repository.SavedDocument.Slots[0].LastPlayedAtUtc,
                Is.EqualTo("2026-07-01T00:00:00Z"));
            Assert.That(result.Slot.LevelGroupId, Is.EqualTo("level-2"));
        }

        [Test]
        public void PrepareContinue_WhenLevelGroupIsCurrent_ReturnsStateWithoutWriting()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-2-2", "level-2", remainingChances: 2)));
            var service = CreateService(repository);

            var result = service.PrepareContinue(new CampaignContinuePreparationCommand(
                1,
                StageId.CreateOrThrow("stage-2-2"),
                "level-2",
                "level-2"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Slot.LevelGroupId, Is.EqualTo("level-2"));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].LevelGroupId, Is.EqualTo("level-2"));
        }

        [TestCase("stage-2-1", "level-5")]
        [TestCase("stage-2-2", "level-4")]
        public void PrepareContinue_WhenExpectedIdentityIsStale_FailsWithoutWriting(
            string expectedStageId,
            string expectedLevelGroupId)
        {
            var document = CreateDocument(
                CreateSlot(1, "stage-2-2", "level-5", remainingChances: 2));
            var repository = new RecordingRepository(document);
            var service = CreateService(repository);

            var result = service.PrepareContinue(new CampaignContinuePreparationCommand(
                1,
                StageId.CreateOrThrow(expectedStageId),
                expectedLevelGroupId,
                "level-2"));

            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.StalePrecondition));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument, Is.SameAs(document));
            Assert.That(repository.CurrentDocument.Slots[0].LevelGroupId, Is.EqualTo("level-5"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(CampaignSaveSlotPolicy.MaxRemainingChances + 1)]
        public void ImportSlotSeed_RejectsInvalidChancesBeforeWritingProfile(
            int invalidChances)
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CampaignSlotSeedImportRequest(
                    1,
                    StageId.CreateOrThrow("stage-1-2"),
                    "level-1",
                    invalidChances,
                    FixedNowUtc));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(repository.CurrentDocument.Slots[0].RemainingChances, Is.EqualTo(3));
        }

        [Test]
        public void ImportSlotSeed_RequestDoesNotExposeUnrelatedMutableCounters()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3, totalDeaths: 5)));
            Assert.That(typeof(CampaignSlotSeedImportRequest).GetProperty("TotalDeaths"), Is.Null);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(repository.CurrentDocument.Slots[0].TotalDeaths, Is.EqualTo(5));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ChancePolicy_RequiresValidValuesWithoutConversion(int chances)
        {
            Assert.That(
                CampaignSaveSlotPolicy.RequireValidRemainingChances(chances),
                Is.EqualTo(chances));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(4)]
        public void ChancePolicy_RejectsValuesOutsideContract(int chances)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                CampaignSaveSlotPolicy.RequireValidRemainingChances(chances));
        }

        [Test]
        public void ImportSlotSeed_CreatesReceiptAbsentState()
        {
            var sourceSlot = CreateSlot(
                1,
                "stage-1-1",
                "level-1",
                remainingChances: 3);
            sourceSlot.HasNormalCampaignCompletionReceipt = false;
            sourceSlot.NormalCampaignCompletionReceipt = null;
            var repository = new RecordingRepository(CreateDocument(sourceSlot));
            var service = CreateService(repository);

            var result = service.ImportSlotSeed(new CampaignSlotSeedImportRequest(
                1,
                StageId.CreateOrThrow("stage-1-1"),
                "level-1",
                2,
                FixedNowUtc));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].HasNormalCampaignCompletionReceipt, Is.False);
            Assert.That(repository.SavedDocument.Slots[0].NormalCampaignCompletionReceipt, Is.Null);
        }

        [Test]
        public void CommitDeath_AppliesPlannedRouteAndIncrementsTotalDeaths()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 2, totalDeaths: 4)));
            var service = CreateService(repository);

            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var result = service.CommitDeath(
                1,
                planner.PlanDeath(CampaignSlotRawDataMapper.ToState(
                    CampaignSlotRawDataMapper.FromDocument(repository.CurrentDocument.Slots[0]))));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].TotalDeaths, Is.EqualTo(5));
            Assert.That(repository.SavedDocument.Slots[0].RemainingChances, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void CommitDeath_RejectsStalePlanWithoutWritingProfile()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 2)));
            var service = CreateService(repository);
            var planner = new CampaignProgressionTransitionPlanner(
                CampaignStageSequenceTestAsset.LoadProductionResolver());
            var plan = planner.PlanDeath(
                CampaignSlotRawDataMapper.ToState(
                    CampaignSlotRawDataMapper.FromDocument(repository.CurrentDocument.Slots[0])));
            repository.CurrentDocument.Slots[0].RemainingChances = 1;

            var result = service.CommitDeath(1, plan);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].TotalDeaths, Is.Zero);
        }

        [Test]
        public void CommitDeath_MaxDeathCounterPreservesExistingExceptionAndNoWriteSurface()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(
                    1,
                    stageId.Value,
                    "level-1",
                    remainingChances: 2,
                    totalDeaths: int.MaxValue)));
            var service = CreateService(repository);
            var plan = new CampaignDeathTransitionPlan(
                stageId,
                expectedRemainingChances: 2,
                persistedLevelGroupId: "level-1",
                new StageRetryRouteResult(
                    StageRetryRouteKind.RetrySameStage,
                    stageId,
                    remainingChances: 1));

            Assert.Throws<ArgumentException>(() => service.CommitDeath(1, plan));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].TotalDeaths,
                Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void CommitDeath_ReadsClockOnceAndUsesSameSlotAndProfileTimestamp()
        {
            var stageId = StageId.CreateOrThrow("stage-1-1");
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, stageId.Value, "level-1", remainingChances: 2)));
            var clockReadCount = 0;
            var service = new CampaignSaveService(
                repository,
                () =>
                {
                    clockReadCount++;
                    return $"clock-read-{clockReadCount}";
                },
                ProfileId,
                ProductVersion);
            var plan = new CampaignDeathTransitionPlan(
                stageId,
                expectedRemainingChances: 2,
                persistedLevelGroupId: "level-1",
                new StageRetryRouteResult(
                    StageRetryRouteKind.RetrySameStage,
                    stageId,
                    remainingChances: 1));

            var result = service.CommitDeath(1, plan);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(clockReadCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc,
                Is.EqualTo("clock-read-1"));
            Assert.That(repository.SavedDocument.SavedAtUtc,
                Is.EqualTo("clock-read-1"));
        }

        [Test]
        public void ComicCompletionFlags_Persist()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);

            var intro = service.SetIntroComicCompleted(1);
            var outro = service.SetOutroComicCompleted(1);

            Assert.That(intro.Succeeded, Is.True);
            Assert.That(outro.Succeeded, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].IntroComicCompleted, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].OutroComicCompleted, Is.True);
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-1-1"));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc, Is.EqualTo(FixedNowUtc));
        }

        [Test]
        public void CommitStageClear_AdvancesCursorAndPreservesProcessedIds()
        {
            var sourceSlot = CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3);
            sourceSlot.StageClearProfileSnapshot = new CampaignStageClearProfileDocument
            {
                Version = 4,
                ProcessedStageRunIds = new[] { "preserved-run" },
                ProcessedClearAttemptIds = new[] { "preserved-attempt" },
            };
            var repository = new RecordingRepository(CreateDocument(sourceSlot));
            var service = CreateService(repository);
            var resolver = CampaignStageSequenceTestAsset.LoadProductionResolver();
            var planner = new CampaignProgressionTransitionPlanner(resolver);
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
            };

            var result = service.CommitStageClear(1, request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.PreviousRemainingChances, Is.EqualTo(3));
            var slot = repository.SavedDocument.Slots[0];
            Assert.That(slot.StageId, Is.EqualTo("stage-1-2"));
            Assert.That(slot.StageClearProfileSnapshot.Version, Is.EqualTo(4));
            Assert.That(slot.StageClearProfileSnapshot.ProcessedStageRunIds,
                Is.EqualTo(new[] { "preserved-run" }));
            Assert.That(slot.StageClearProfileSnapshot.ProcessedClearAttemptIds,
                Is.EqualTo(new[] { "preserved-attempt" }));
        }

        [Test]
        public void CommitStageClear_RejectsStalePlanWithoutWritingProfile()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 2)));
            var service = CreateService(repository);
            var planner = new CampaignProgressionTransitionPlanner(
                CampaignStageSequenceTestAsset.LoadProductionResolver());
            var request = new CampaignStageClearCommitRequest
            {
                Plan = planner.PlanStageClear(StageId.CreateOrThrow("stage-1-1")),
            };
            repository.CurrentDocument.Slots[0].StageId = "stage-1-2";

            var result = service.CommitStageClear(1, request);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveCommandStatus.InvalidRequest));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.Zero);
            Assert.That(repository.CurrentDocument.Slots[0].StageId, Is.EqualTo("stage-1-2"));
        }

        [Test]
        public void CommitStageClear_ReadsClockOnceAndUsesSameSlotAndProfileTimestamp()
        {
            var completedStageId = StageId.CreateOrThrow("stage-1-1");
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, completedStageId.Value, "level-1", remainingChances: 2)));
            var clockReadCount = 0;
            var service = new CampaignSaveService(
                repository,
                () =>
                {
                    clockReadCount++;
                    return $"clock-read-{clockReadCount}";
                },
                ProfileId,
                ProductVersion);
            var request = new CampaignStageClearCommitRequest
            {
                Plan = new CampaignStageClearTransitionPlan(
                    completedStageId,
                    StageId.CreateOrThrow("stage-1-2"),
                    completedLevelGroupId: "level-1",
                    persistedLevelGroupId: "level-1",
                    isCampaignCompleted: false,
                    restoresChances: false),
            };

            var result = service.CommitStageClear(1, request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(clockReadCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].LastPlayedAtUtc,
                Is.EqualTo("clock-read-1"));
            Assert.That(repository.SavedDocument.SavedAtUtc,
                Is.EqualTo("clock-read-1"));
        }

        [Test]
        public void DeleteSlot_RemovesSlotAndPreservesOtherSlots()
        {
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3),
                CreateSlot(2, "stage-2-1", "level-2", remainingChances: 1)));
            repository.CurrentDocument.LastPlayedSlotNumber = 1;
            var service = CreateService(repository);

            var result = service.DeleteSlot(1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots, Has.Length.EqualTo(1));
            Assert.That(repository.SavedDocument.Slots[0].SlotNumber, Is.EqualTo(2));
            Assert.That(repository.SavedDocument.Slots[0].StageId, Is.EqualTo("stage-2-1"));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void ClearAll_WritesEmptyValidProfileWithoutTouchingPlayerPrefs()
        {
            PlayerPrefs.SetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey, "unsupported-source-stays");
            PlayerPrefs.Save();
            var repository = new RecordingRepository(CreateDocument(
                CreateSlot(1, "stage-1-1", "level-1", remainingChances: 3)));
            var service = CreateService(repository);

            var result = service.ClearAll();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.EqualTo(1));
            Assert.That(repository.SavedDocument.SchemaVersion, Is.EqualTo(CampaignProfileDocument.CurrentSchemaVersion));
            Assert.That(repository.SavedDocument.ProfileId, Is.EqualTo(ProfileId));
            Assert.That(repository.SavedDocument.LastPlayedSlotNumber, Is.Zero);
            Assert.That(repository.SavedDocument.Slots, Is.Empty);
            Assert.That(PlayerPrefs.GetString(RemovedCampaignPlayerPrefsKeys.SaveSlotsKey),
                Is.EqualTo("unsupported-source-stays"));
        }

        [Test]
        public void Service_UsesRepositoryAndDoesNotTouchSettingsPlayerPrefs()
        {
            PlayerPrefs.SetFloat("settings.audio.master.volume", 0.25f);
            PlayerPrefs.SetInt("settings.audio.master.muted", 1);
            PlayerPrefs.SetInt("settings.display.width", 1600);
            PlayerPrefs.SetInt("settings.display.height", 900);
            PlayerPrefs.SetString("Game.Feature.Input.KeyboardMovementScheme", "wasd");
            PlayerPrefs.Save();
            var repository = new RecordingRepository();
            var service = CreateService(repository);

            var result = service.InitializeNewGame(1, "stage-1-1", "level-1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(repository.LoadCount, Is.EqualTo(1));
            Assert.That(repository.SaveCount, Is.Zero);
            Assert.That(repository.DestructiveSaveCount, Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetFloat("settings.audio.master.volume"), Is.EqualTo(0.25f));
            Assert.That(PlayerPrefs.GetInt("settings.audio.master.muted"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("settings.display.width"), Is.EqualTo(1600));
            Assert.That(PlayerPrefs.GetInt("settings.display.height"), Is.EqualTo(900));
            Assert.That(PlayerPrefs.GetString("Game.Feature.Input.KeyboardMovementScheme"), Is.EqualTo("wasd"));
        }

        [Test]
        public void ProductionComposition_DoesNotReferenceCampaignSaveService()
        {
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveService"));
            Assert.That(
                File.ReadAllText("Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs"),
                Does.Not.Contain("CampaignSaveService"));
            Assert.That(
                File.ReadAllText("Assets/_Features/Gameplay/Gameplay_Host/Runtime/StageBackedGameplaySceneInstallerBase.cs"),
                Does.Not.Contain("CampaignSaveService"));
        }

        [Test]
        public void ServiceSource_DoesNotDeletePlayerPrefsOrCallSteamApis()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");

            Assert.That(source, Does.Not.Contain("PlayerPrefs.DeleteKey"));
            Assert.That(source, Does.Not.Contain("Steamworks"));
            Assert.That(source, Does.Not.Contain("ISteamRemoteStorage"));
            Assert.That(source, Does.Not.Contain("SteamRemoteStorage"));
        }

        [Test]
        public void CommitTransitions_UseEngineInsideMutationWithoutInlineBusinessAssignments()
        {
            var source = File.ReadAllText(
                "Assets/_Features/Stages/Runtime/Campaign/Save/CampaignSaveService.cs");
            var deathSource = GetMethodSource(
                source,
                "public CampaignSaveServiceResult CommitDeath(",
                "public CampaignSaveServiceResult CommitStageClear(");
            var clearSource = GetMethodSource(
                source,
                "public CampaignSaveServiceResult CommitStageClear(",
                "public CampaignSaveServiceResult SetIntroComicCompleted(");

            AssertEngineCallInsideMutation(
                deathSource,
                "CampaignSlotTransitionEngine.ApplyDeath");
            Assert.That(deathSource, Does.Not.Contain("domainSlot.CurrentStageId ="));
            Assert.That(deathSource, Does.Not.Contain("domainSlot.CurrentLevelGroupId ="));
            Assert.That(deathSource, Does.Not.Contain("domainSlot.RemainingChances ="));
            Assert.That(deathSource, Does.Not.Contain("domainSlot.TotalDeaths +="));
            Assert.That(deathSource, Does.Not.Contain("plan.ExpectedCurrentStageId.IsValid"));

            AssertEngineCallInsideMutation(
                clearSource,
                "CampaignSlotTransitionEngine.ApplyStageClear");
            Assert.That(clearSource, Does.Not.Contain("domainSlot.CurrentStageId ="));
            Assert.That(clearSource, Does.Not.Contain("domainSlot.CurrentLevelGroupId ="));
            Assert.That(clearSource, Does.Not.Contain("domainSlot.CampaignCompleted ="));
            Assert.That(clearSource, Does.Not.Contain("domainSlot.RemainingChances ="));
            Assert.That(clearSource, Does.Not.Contain(
                "domainSlot.NormalCampaignCompletionReceipt ="));
            Assert.That(clearSource, Does.Not.Contain(
                "domainSlot.NormalStagePerformanceRecords ="));
            Assert.That(clearSource, Does.Not.Contain("request.Plan.CompletedStageId.IsValid"));
        }

        private static void AssertEngineCallInsideMutation(
            string methodSource,
            string engineCall)
        {
            var mutationIndex = methodSource.IndexOf(
                "return Mutate(document =>",
                StringComparison.Ordinal);
            var engineIndex = methodSource.IndexOf(engineCall, StringComparison.Ordinal);
            Assert.That(mutationIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(engineIndex, Is.GreaterThan(mutationIndex));
        }

        private static string GetMethodSource(
            string source,
            string methodSignature,
            string nextMethodSignature)
        {
            var start = source.IndexOf(methodSignature, StringComparison.Ordinal);
            var end = source.IndexOf(nextMethodSignature, start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }

        private static CampaignSaveService CreateService(RecordingRepository repository)
        {
            return new CampaignSaveService(
                repository,
                () => FixedNowUtc,
                ProfileId,
                ProductVersion);
        }

        private static CampaignProfileDocument CreateDocument(params CampaignSlotDocument[] slots)
        {
            return new CampaignProfileDocument
            {
                SchemaVersion = CampaignProfileDocument.CurrentSchemaVersion,
                ProductVersion = ProductVersion,
                SavedAtUtc = "2026-07-06T00:00:00Z",
                ProfileId = ProfileId,
                LastPlayedSlotNumber = slots.Length > 0 ? slots[0].SlotNumber : 0,
                Slots = slots,
            };
        }

        private static CampaignSlotDocument CreateSlot(
            int slotNumber,
            string stageId,
            string levelGroupId,
            int remainingChances,
            int totalDeaths = 0)
        {
            return new CampaignSlotDocument
            {
                SlotNumber = slotNumber,
                StageId = stageId,
                LevelGroupId = levelGroupId,
                RemainingChances = remainingChances,
                TotalDeaths = totalDeaths,
                LastPlayedAtUtc = "2026-07-06T00:00:00Z",
                StageClearProfileSnapshot = new CampaignStageClearProfileDocument(),
            };
        }

        private sealed class RecordingRepository : ICampaignProfileRepository
        {
            public RecordingRepository(CampaignProfileDocument currentDocument = null)
            {
                CurrentDocument = currentDocument;
            }

            public CampaignProfileDocument CurrentDocument { get; set; }

            public CampaignProfileDocument SavedDocument { get; private set; }

            public int LoadCount { get; private set; }

            public int SaveCount { get; private set; }

            public int DestructiveSaveCount { get; private set; }

            public CampaignProfileLoadResult Load()
            {
                LoadCount++;
                return CurrentDocument == null
                    ? new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Missing,
                        null,
                        "missing")
                    : new CampaignProfileLoadResult(
                        CampaignProfileLoadStatus.Loaded,
                        CurrentDocument,
                        "loaded");
            }

            public void Save(CampaignProfileDocument document)
            {
                SaveCount++;
                SavedDocument = document;
                CurrentDocument = document;
            }

            public void SaveDestructive(CampaignProfileDocument document)
            {
                DestructiveSaveCount++;
                SavedDocument = document;
                CurrentDocument = document;
            }
        }

    }
}
