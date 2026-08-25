using System;

namespace Game.Feature.Stages
{
    public interface IActiveSlotStorage
    {
        string DiagnosticsKey { get; }

        bool TryGetActiveSlot(out int slotNumber);

        void SetActiveSlot(int slotNumber);

        void ClearActiveSlot();
    }

    public sealed class LocalStateActiveSlotStorage : IActiveSlotStorage
    {
        private readonly ICampaignLocalLaunchStateRepository _repository;
        private readonly ICampaignSaveQuery _profileSlots;

        public LocalStateActiveSlotStorage(
            ICampaignLocalLaunchStateRepository repository,
            ICampaignSaveQuery profileSlots)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _profileSlots = profileSlots ?? throw new ArgumentNullException(nameof(profileSlots));
        }

        public string DiagnosticsKey => CampaignLocalLaunchStateRepository.FileName;

        public bool TryGetActiveSlot(out int slotNumber)
        {
            var loadResult = _repository.Load();
            if (loadResult.Status == CampaignLocalLaunchStateLoadStatus.Loaded)
            {
                return TryUseLoadedDocument(loadResult.Document, out slotNumber);
            }

            slotNumber = 0;
            return false;
        }

        public void SetActiveSlot(int slotNumber)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var availability = ResolveProfileSlotAvailability(slotNumber, out var loadStatus);
            if (availability == ProfileSlotAvailability.DefinitelyAbsent)
            {
                throw new InvalidOperationException(
                    $"Cannot set active campaign slot '{slotNumber}' because the profile slot is empty or missing.");
            }

            if (availability == ProfileSlotAvailability.Indeterminate)
            {
                throw new InvalidOperationException(
                    $"Cannot set active campaign slot '{slotNumber}' because profile availability could not be verified " +
                    $"(status={loadStatus}).");
            }

            _repository.SaveActiveSlot(slotNumber);
        }

        public void ClearActiveSlot()
        {
            _repository.ClearActiveSlot();
        }

        private bool TryUseLoadedDocument(
            CampaignLocalLaunchStateDocument document,
            out int slotNumber)
        {
            slotNumber = document?.campaign?.activeSlotNumber ?? 0;
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                slotNumber = 0;
                return false;
            }

            var availability = ResolveProfileSlotAvailability(slotNumber, out _);
            if (availability == ProfileSlotAvailability.Available)
            {
                return true;
            }

            if (availability == ProfileSlotAvailability.DefinitelyAbsent)
            {
                _repository.ClearActiveSlot();
            }

            slotNumber = 0;
            return false;
        }

        private ProfileSlotAvailability ResolveProfileSlotAvailability(
            int slotNumber,
            out CampaignSaveLoadStatus loadStatus)
        {
            loadStatus = CampaignSaveLoadStatus.IoFailed;
            if (!CampaignSaveSlotPolicy.IsValidSlotNumber(slotNumber))
            {
                return ProfileSlotAvailability.DefinitelyAbsent;
            }

            try
            {
                var result = _profileSlots.LoadAllWithReport();
                loadStatus = result.Report.Status;
                switch (result.Report.Status)
                {
                    case CampaignSaveLoadStatus.Loaded:
                    case CampaignSaveLoadStatus.BackupRecovered:
                        if (result.Slots == null || result.Slots.Length < slotNumber)
                        {
                            return ProfileSlotAvailability.Indeterminate;
                        }

                        var slot = result.Slots[slotNumber - 1];
                        return slot == null || slot.IsEmpty
                            ? ProfileSlotAvailability.DefinitelyAbsent
                            : ProfileSlotAvailability.Available;
                    case CampaignSaveLoadStatus.Missing:
                        return ProfileSlotAvailability.DefinitelyAbsent;
                    default:
                        return ProfileSlotAvailability.Indeterminate;
                }
            }
            catch
            {
                return ProfileSlotAvailability.Indeterminate;
            }
        }

        private enum ProfileSlotAvailability
        {
            Available,
            DefinitelyAbsent,
            Indeterminate,
        }
    }

    public sealed class CampaignLaunchStateRepairingCampaignSaveSlotStore : ICampaignSaveRuntime
    {
        private readonly ICampaignSaveRuntime _inner;
        private readonly IActiveSlotStorage _activeSlotStorage;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;

        public CampaignLaunchStateRepairingCampaignSaveSlotStore(
            ICampaignSaveRuntime inner,
            IActiveSlotStorage activeSlotStorage,
            ICampaignLaunchHandoffStore launchHandoffStore)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _activeSlotStorage = activeSlotStorage ?? throw new ArgumentNullException(nameof(activeSlotStorage));
            _launchHandoffStore = launchHandoffStore ?? throw new ArgumentNullException(nameof(launchHandoffStore));
        }

        public string DiagnosticsKey => _inner.DiagnosticsKey;

        public CampaignSaveLoadReport LastCampaignLoadReport => _inner.LastCampaignLoadReport;

        public CampaignSlotEntry[] LoadAll()
        {
            return _inner.LoadAll();
        }

        public CampaignSaveLoadResult LoadAllWithReport()
        {
            return _inner.LoadAllWithReport();
        }

        public CampaignSlotEntry LoadSlot(int slotNumber)
        {
            return _inner.LoadSlot(slotNumber);
        }

        public CampaignContinuePreparationResult PrepareContinue(
            CampaignContinuePreparationCommand command)
        {
            return _inner.PrepareContinue(command);
        }

        public CampaignSlotState InitializeNewGame(
            int slotNumber,
            CampaignStageSequenceResolver sequenceResolver,
            string lastPlayedAt)
        {
            return _inner.InitializeNewGame(
                slotNumber,
                sequenceResolver,
                lastPlayedAt);
        }

        public void MarkIntroComicCompleted(int slotNumber)
        {
            _inner.MarkIntroComicCompleted(slotNumber);
        }

        public void MarkOutroComicCompleted(int slotNumber)
        {
            _inner.MarkOutroComicCompleted(slotNumber);
        }

        public CampaignSlotState SetActiveStageForDiagnostics(
            int slotNumber,
            StageId stageId,
            string levelGroupId)
        {
            return _inner.SetActiveStageForDiagnostics(
                slotNumber,
                stageId,
                levelGroupId);
        }

        public CampaignSlotState ImportSlotSeed(CampaignSlotSeedImportRequest request)
        {
            return _inner.ImportSlotSeed(request);
        }

        public CampaignDeathCommitResult CommitDeath(
            int slotNumber,
            CampaignDeathTransitionPlan plan)
        {
            return _inner.CommitDeath(slotNumber, plan);
        }

        public CampaignStageClearCommitResult CommitStageClear(
            int slotNumber,
            CampaignStageClearCommitRequest request)
        {
            return _inner.CommitStageClear(slotNumber, request);
        }

        public void DeleteSlot(int slotNumber)
        {
            var deletedSlotWasActive =
                _activeSlotStorage.TryGetActiveSlot(out var activeSlotNumber) &&
                activeSlotNumber == slotNumber;
            var deletedSlotWasPending =
                _launchHandoffStore.TryPeek(out var pendingHandoff) &&
                pendingHandoff.SlotNumber == slotNumber;
            _inner.DeleteSlot(slotNumber);
            if (deletedSlotWasPending)
            {
                _launchHandoffStore.TryClear(pendingHandoff.Token);
            }

            if (deletedSlotWasActive)
            {
                _activeSlotStorage.ClearActiveSlot();
            }
        }

        public void ClearAll()
        {
            var hadPendingLaunch = _launchHandoffStore.TryPeek(out var pendingHandoff);
            _inner.ClearAll();
            if (hadPendingLaunch)
            {
                _launchHandoffStore.TryClear(pendingHandoff.Token);
            }

            _activeSlotStorage.ClearActiveSlot();
        }

    }

    public sealed class CampaignLaunchStateRepairingCampaignSaveRecoveryPort : ICampaignSaveRecoveryPort
    {
        private readonly ICampaignSaveRecoveryPort _inner;
        private readonly IActiveSlotStorage _activeSlotStorage;
        private readonly ICampaignLaunchHandoffStore _launchHandoffStore;

        public CampaignLaunchStateRepairingCampaignSaveRecoveryPort(
            ICampaignSaveRecoveryPort inner,
            IActiveSlotStorage activeSlotStorage,
            ICampaignLaunchHandoffStore launchHandoffStore)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _activeSlotStorage = activeSlotStorage ?? throw new ArgumentNullException(nameof(activeSlotStorage));
            _launchHandoffStore = launchHandoffStore ?? throw new ArgumentNullException(nameof(launchHandoffStore));
        }

        public CampaignSaveResetResult ResetBlockedProfile(CampaignSaveLoadStatus expectedStatus)
        {
            var result = _inner.ResetBlockedProfile(expectedStatus);
            if (result == CampaignSaveResetResult.Completed)
            {
                ClearLaunchState();
            }

            return result;
        }

        public bool HasPendingReset => _inner.HasPendingReset;

        public CampaignSaveResetResult RetryPendingReset()
        {
            var result = _inner.RetryPendingReset();
            if (result == CampaignSaveResetResult.Completed)
            {
                ClearLaunchState();
            }

            return result;
        }

        public void ClearLaunchState()
        {
            if (_launchHandoffStore.TryPeek(out var pendingHandoff))
            {
                _launchHandoffStore.TryClear(pendingHandoff.Token);
            }

            _activeSlotStorage.ClearActiveSlot();
        }
    }
}
