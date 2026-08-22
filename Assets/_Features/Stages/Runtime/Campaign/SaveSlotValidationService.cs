using System;

namespace Game.Feature.Stages
{
    public enum SaveSlotValidationStatus
    {
        Empty = 0,
        Valid = 1,
        Completed = 2,
        Corrupted = 3,
        UnsupportedVersion = 4,
        StageMissingFromSequence = 5,
        StageMissingFromCatalog = 6,
    }

    public readonly struct SaveSlotValidationResult
    {
        public SaveSlotValidationResult(
            SaveSlotData slot,
            SaveSlotValidationStatus status,
            string derivedLevelGroupId,
            bool levelGroupWasSynced,
            bool requiresSaveSync = false)
        {
            Slot = slot?.Clone() ?? SaveSlotData.CreateEmpty(1);
            Status = status;
            DerivedLevelGroupId = derivedLevelGroupId ?? string.Empty;
            LevelGroupWasSynced = levelGroupWasSynced;
            RequiresSaveSync = requiresSaveSync || levelGroupWasSynced;
        }

        public SaveSlotData Slot { get; }

        public SaveSlotValidationStatus Status { get; }

        public string DerivedLevelGroupId { get; }

        public bool LevelGroupWasSynced { get; }

        public bool RequiresSaveSync { get; }

        public bool CanContinue => Status == SaveSlotValidationStatus.Valid;

        public bool CanRestart => Status != SaveSlotValidationStatus.Empty;

        public bool CanDelete => Status != SaveSlotValidationStatus.Empty;

        public bool IsCorruptedOrUnsupported =>
            Status == SaveSlotValidationStatus.Corrupted ||
            Status == SaveSlotValidationStatus.UnsupportedVersion ||
            Status == SaveSlotValidationStatus.StageMissingFromSequence ||
            Status == SaveSlotValidationStatus.StageMissingFromCatalog;
    }

    public sealed class SaveSlotValidationService
    {
        private readonly StageCatalogResolver _catalogResolver;
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public SaveSlotValidationService(
            CampaignStageSequenceResolver sequenceResolver,
            IStageCatalogProvider stageCatalogProvider)
        {
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
            if (stageCatalogProvider == null)
            {
                throw new ArgumentNullException(nameof(stageCatalogProvider));
            }

            _catalogResolver = new StageCatalogResolver(stageCatalogProvider);
        }

        public SaveSlotValidationResult Validate(SaveSlotData slot)
        {
            if (slot == null)
            {
                return new SaveSlotValidationResult(
                    SaveSlotData.CreateEmpty(1),
                    SaveSlotValidationStatus.Corrupted,
                    string.Empty,
                    levelGroupWasSynced: false);
            }

            var mutableSlot = slot.Clone();
            if (mutableSlot.IsEmpty)
            {
                return new SaveSlotValidationResult(
                    mutableSlot,
                    SaveSlotValidationStatus.Empty,
                    string.Empty,
                    levelGroupWasSynced: false);
            }

            if (!mutableSlot.CurrentStageId.IsValid)
            {
                return new SaveSlotValidationResult(
                    mutableSlot,
                    SaveSlotValidationStatus.Corrupted,
                    string.Empty,
                    levelGroupWasSynced: false);
            }

            if (!_sequenceResolver.Contains(mutableSlot.CurrentStageId))
            {
                if (RetiredCampaignSaveCompatibilityPolicy.IsRetiredCompletedStageId(mutableSlot.CurrentStageId))
                {
                    return ValidateRetiredCompletedStage(mutableSlot);
                }

                return new SaveSlotValidationResult(
                    mutableSlot,
                    SaveSlotValidationStatus.StageMissingFromSequence,
                    string.Empty,
                    levelGroupWasSynced: false);
            }

            if (!_catalogResolver.TryResolve(mutableSlot.CurrentStageId, out _))
            {
                return new SaveSlotValidationResult(
                    mutableSlot,
                    SaveSlotValidationStatus.StageMissingFromCatalog,
                    _sequenceResolver.GetLevelGroupId(mutableSlot.CurrentStageId),
                    levelGroupWasSynced: false);
            }

            var derivedLevelGroupId = _sequenceResolver.GetLevelGroupId(mutableSlot.CurrentStageId);
            var levelGroupWasSynced = !string.Equals(
                mutableSlot.CurrentLevelGroupId ?? string.Empty,
                derivedLevelGroupId,
                StringComparison.Ordinal);
            if (levelGroupWasSynced)
            {
                mutableSlot.CurrentLevelGroupId = derivedLevelGroupId;
            }

            return new SaveSlotValidationResult(
                mutableSlot,
                mutableSlot.CampaignCompleted
                    ? SaveSlotValidationStatus.Completed
                    : SaveSlotValidationStatus.Valid,
                derivedLevelGroupId,
                levelGroupWasSynced);
        }

        private SaveSlotValidationResult ValidateRetiredCompletedStage(SaveSlotData mutableSlot)
        {
            var completionStageId = _sequenceResolver.FinalStageId;
            var completionLevelGroupId = _sequenceResolver.GetLevelGroupId(completionStageId);
            mutableSlot.CurrentStageId = completionStageId;
            mutableSlot.CurrentLevelGroupId = completionLevelGroupId;
            mutableSlot.CampaignCompleted = true;

            if (!_catalogResolver.TryResolve(completionStageId, out _))
            {
                return new SaveSlotValidationResult(
                    mutableSlot,
                    SaveSlotValidationStatus.StageMissingFromCatalog,
                    completionLevelGroupId,
                    levelGroupWasSynced: false,
                    requiresSaveSync: true);
            }

            return new SaveSlotValidationResult(
                mutableSlot,
                SaveSlotValidationStatus.Completed,
                completionLevelGroupId,
                levelGroupWasSynced: false,
                requiresSaveSync: true);
        }

        public SaveSlotValidationResult ValidateAndSync(ICampaignSaveSlotStore saveSlotStore, int slotNumber)
        {
            if (saveSlotStore == null)
            {
                throw new ArgumentNullException(nameof(saveSlotStore));
            }

            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            var result = Validate(saveSlotStore.LoadSlot(slotNumber));
            if (result.RequiresSaveSync)
            {
                saveSlotStore.SaveSlot(result.Slot);
                result = Validate(result.Slot);
            }

            return result;
        }
    }
}
