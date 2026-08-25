using System;

namespace Game.Feature.Stages
{
    public sealed class CampaignSaveSlotStoreAdapter : ICampaignSaveRuntime
    {
        private readonly CampaignSaveService _campaignSaveService;
        private readonly ICampaignSaveRecoveryPort _recoveryPort;

        public CampaignSaveSlotStoreAdapter(
            CampaignSaveService campaignSaveService,
            ICampaignSaveRecoveryPort recoveryPort = null)
        {
            _campaignSaveService = campaignSaveService ?? throw new ArgumentNullException(nameof(campaignSaveService));
            _recoveryPort = recoveryPort;
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Load has not run.");
        }

        public string DiagnosticsKey => CampaignSaveServiceResultStatusToken;

        public CampaignSaveLoadReport LastCampaignLoadReport { get; private set; }

        public CampaignSlotEntry[] LoadAll()
        {
            return LoadAllEntries();
        }

        private CampaignSlotEntry[] LoadAllEntries()
        {
            var result = LoadAllWithReport();
            ThrowIfCampaignAccessBlocked(result.Report);
            return result.Slots;
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            if (IsRecoveryPending())
            {
                LastCampaignLoadReport = CreateRecoveryPendingReport();
                return new CampaignSaveLoadResult(CreateEmptyEntries(), LastCampaignLoadReport);
            }

            var result = _campaignSaveService.GetSlots();
            if (!result.Succeeded)
            {
                LastCampaignLoadReport = ToCampaignLoadReport(result);
                return new CampaignSaveLoadResult(CreateEmptyEntries(), LastCampaignLoadReport);
            }

            LastCampaignLoadReport = result.HasProfileLoadStatus
                ? ToCampaignLoadReport(result)
                : CampaignSaveLoadReport.Loaded(
                    "Campaign profile loaded successfully.",
                    CampaignSaveServiceResultStatusToken);
            return new CampaignSaveLoadResult(
                CreateEntries(result.Slots),
                LastCampaignLoadReport);
        }

        public CampaignSlotEntry LoadSlot(int slotNumber)
        {
            return LoadEntry(slotNumber);
        }

        private CampaignSlotEntry LoadEntry(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return LoadAllEntries()[slotNumber - 1];
        }

        public CampaignContinuePreparationResult PrepareContinue(
            CampaignContinuePreparationCommand command)
        {
            ThrowIfRecoveryPending();
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var result = _campaignSaveService.PrepareContinue(command);
            switch (result.Status)
            {
                case CampaignSaveCommandStatus.Succeeded:
                    return CampaignContinuePreparationResult.Prepared(
                        ParseOccupied(result.Slot),
                        !string.Equals(
                            command.ExpectedPersistedLevelGroupId,
                            command.TargetLevelGroupId,
                            StringComparison.Ordinal));
                case CampaignSaveCommandStatus.SlotNotFound:
                    return CampaignContinuePreparationResult.SlotMissing();
                case CampaignSaveCommandStatus.StalePrecondition:
                    return CampaignContinuePreparationResult.StalePrecondition();
                default:
                    ThrowIfFailed(result);
                    throw new InvalidOperationException(
                        "Campaign Continue preparation returned an unsupported result.");
            }
        }

        public CampaignSlotState InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            return InitializeNewGameState(slotNumber, sequenceResolver, lastPlayedAt);
        }

        private CampaignSlotState InitializeNewGameState(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (sequenceResolver == null)
            {
                throw new ArgumentNullException(nameof(sequenceResolver));
            }

            var firstStageId = sequenceResolver.FirstStageId;
            ThrowIfFailed(_campaignSaveService.InitializeNewGame(new CampaignNewGameRequest
            {
                SlotNumber = slotNumber,
                InitialStageId = firstStageId.Value,
                InitialLevelGroupId = sequenceResolver.GetLevelGroupId(firstStageId),
                LastPlayedAtUtc = lastPlayedAt ?? string.Empty,
            }));

            return RequireOccupied(LoadEntry(slotNumber));
        }

        public void MarkIntroComicCompleted(int slotNumber)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            ThrowIfFailed(_campaignSaveService.SetIntroComicCompleted(slotNumber));
        }

        public void MarkOutroComicCompleted(int slotNumber)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            ThrowIfFailed(_campaignSaveService.SetOutroComicCompleted(slotNumber));
        }

        public CampaignSlotState SetActiveStageForDiagnostics(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            return SetActiveStageForDiagnosticsState(slotNumber, stageId, levelGroupId);
        }

        private CampaignSlotState SetActiveStageForDiagnosticsState(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (!stageId.IsValid)
            {
                throw new ArgumentException("Diagnostic stage selection requires a valid stage id.", nameof(stageId));
            }

            var result = _campaignSaveService.SetActiveStageForDiagnostics(
                slotNumber,
                stageId,
                levelGroupId);
            ThrowIfFailed(result);
            return ParseOccupied(result.Slot);
        }

        public CampaignSlotState ImportSlotSeed(CampaignSlotSeedImportRequest request)
        {
            ThrowIfRecoveryPending();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ThrowIfFailed(_campaignSaveService.ImportSlotSeed(request));
            return RequireOccupied(LoadEntry(request.SlotNumber));
        }

        public CampaignDeathCommitResult CommitDeath(
            int slotNumber,
            CampaignDeathTransitionPlan plan)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var result = _campaignSaveService.CommitDeath(slotNumber, plan);
            ThrowIfFailed(result);
            return new CampaignDeathCommitResult(ParseOccupied(result.Slot));
        }

        public CampaignStageClearCommitResult CommitStageClear(
            int slotNumber,
            CampaignStageClearCommitRequest request)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var result = _campaignSaveService.CommitStageClear(slotNumber, request);
            ThrowIfFailed(result);
            if (!result.PreviousRemainingChances.HasValue)
            {
                throw new InvalidOperationException(
                    "Successful stage clear commit did not return the previous chance count.");
            }

            return new CampaignStageClearCommitResult(
                ParseOccupied(result.Slot),
                result.PreviousRemainingChances.Value);
        }

        public void DeleteSlot(int slotNumber)
        {
            ThrowIfRecoveryPending();
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var result = _campaignSaveService.DeleteSlot(slotNumber);
            if (result.Status == CampaignSaveCommandStatus.SlotNotFound)
            {
                return;
            }

            ThrowIfFailed(result);
        }

        public void ClearAll()
        {
            ThrowIfRecoveryPending();
            ThrowIfFailed(_campaignSaveService.ClearAll());
            LastCampaignLoadReport = CampaignSaveLoadReport.Missing("Campaign profile was cleared.");
        }

        private const string CampaignSaveServiceResultStatusToken = "CampaignProfileDocument";

        private bool IsRecoveryPending()
        {
            return _recoveryPort?.HasPendingReset == true;
        }

        private void ThrowIfRecoveryPending()
        {
            if (IsRecoveryPending())
            {
                LastCampaignLoadReport = CreateRecoveryPendingReport();
                ThrowIfCampaignAccessBlocked(LastCampaignLoadReport);
            }
        }

        private static CampaignSaveLoadReport CreateRecoveryPendingReport()
        {
            return new CampaignSaveLoadReport(
                CampaignSaveLoadStatus.RecoveryPending,
                "Campaign save reset is pending.",
                CampaignSaveServiceResultStatusToken);
        }

        private static void ThrowIfCampaignAccessBlocked(CampaignSaveLoadReport report)
        {
            if (!report.BlocksCampaignAccess)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Campaign save access is blocked ({report.Status}): {report.Reason}");
        }

        private static CampaignSaveLoadReport ToCampaignLoadReport(CampaignSaveServiceResult result)
        {
            if (result == null)
            {
                return new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.IoFailed,
                    "Campaign save command did not return a result.",
                    string.Empty);
            }

            if (!result.HasProfileLoadStatus)
            {
                return new CampaignSaveLoadReport(
                    CampaignSaveLoadStatus.IoFailed,
                    result.Message,
                    string.Empty);
            }

            switch (result.ProfileLoadStatus)
            {
                case CampaignProfileLoadStatus.Missing:
                    return CampaignSaveLoadReport.Missing(result.Message);
                case CampaignProfileLoadStatus.Loaded:
                    return CampaignSaveLoadReport.Loaded(result.Message, CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.BackupRecovered:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.BackupRecovered,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.CorruptNoFallback:
                case CampaignProfileLoadStatus.InvalidDocument:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.CorruptRepairRequired,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.UnsupportedVersion:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.SchemaInvalidRepairRequired,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.Unauthorized:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.Unauthorized,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
                case CampaignProfileLoadStatus.IoFailed:
                default:
                    return new CampaignSaveLoadReport(
                        CampaignSaveLoadStatus.IoFailed,
                        result.Message,
                        CampaignSaveServiceResultStatusToken);
            }
        }

        private static CampaignSlotEntry[] CreateEmptyEntries()
        {
            var entries = new CampaignSlotEntry[CampaignSaveSlotPolicy.SlotCount];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = CampaignSlotEntry.Empty(i + 1);
            }

            return entries;
        }

        private static CampaignSlotEntry[] CreateEntries(CampaignSlotDocument[] documents)
        {
            var entries = CreateEmptyEntries();
            var source = documents ?? Array.Empty<CampaignSlotDocument>();
            for (var index = 0; index < source.Length; index++)
            {
                var state = ParseOccupied(source[index]);
                entries[state.SlotNumber - 1] = CampaignSlotEntry.Occupied(state);
            }

            return entries;
        }

        private static CampaignSlotState ParseOccupied(CampaignSlotDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var parse = CampaignSlotParser.ParseEntry(document.SlotNumber, document);
            if (!parse.IsSuccess || parse.Entry.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Campaign save service returned a non-canonical occupied slot.");
            }

            return parse.Entry.State;
        }

        private static CampaignSlotState RequireOccupied(CampaignSlotEntry entry)
        {
            if (entry == null || entry.IsEmpty)
            {
                throw new InvalidOperationException("Campaign slot is empty or missing.");
            }

            return entry.State;
        }

        private void ThrowIfFailed(CampaignSaveServiceResult result)
        {
            if (result == null)
            {
                LastCampaignLoadReport = ToCampaignLoadReport(null);
                throw new InvalidOperationException("Campaign save command did not return a result.");
            }

            if (!result.Succeeded)
            {
                if (result.Status == CampaignSaveCommandStatus.LoadFailed)
                {
                    LastCampaignLoadReport = ToCampaignLoadReport(result);
                }

                throw new InvalidOperationException(result.Message);
            }
        }
    }

}
