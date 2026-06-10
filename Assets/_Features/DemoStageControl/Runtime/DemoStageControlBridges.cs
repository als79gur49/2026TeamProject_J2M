using System;
using Game.Feature.Stages;

namespace Game.Feature.DemoStageControl
{
    public sealed class DemoStageControlCampaignBridge : IDemoStageControlCampaignBridge
    {
        private readonly ActiveSlotProvider _activeSlotProvider;
        private readonly SaveSlotStore _saveSlotStore;
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public DemoStageControlCampaignBridge(
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider,
            CampaignStageSequenceResolver sequenceResolver)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
        }

        public StageId CurrentStageId
        {
            get
            {
                if (!_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber))
                {
                    return StageId.None;
                }

                return _saveSlotStore.LoadSlot(slotNumber).CurrentStageId;
            }
        }

        public bool TrySetActiveStage(StageContentEntry entry, out string message)
        {
            if (entry == null || !entry.StageId.IsValid)
            {
                message = "Selected stage has no valid catalog entry.";
                return false;
            }

            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber))
            {
                message = "No active campaign save slot is selected.";
                return false;
            }

            var stageId = entry.StageId;
            if (!_sequenceResolver.Contains(stageId))
            {
                message = $"Stage '{stageId.Value}' is not part of the campaign sequence.";
                return false;
            }

            var levelGroupId = ResolveLevelGroupId(entry);
            _saveSlotStore.UpdateSlot(
                slotNumber,
                slot =>
                {
                    slot.CurrentStageId = stageId;
                    slot.CurrentLevelGroupId = levelGroupId;
                    slot.CampaignCompleted = false;
                    slot.LastPlayedAt = DateTimeOffset.UtcNow.ToString("O");
                    slot.StageClearProfileSnapshot ??= new StageClearProfileSnapshot();
                    if (!slot.StageClearProfileSnapshot.ClearRecordsByStageId.ContainsKey(stageId))
                    {
                        slot.StageClearProfileSnapshot.ClearRecordsByStageId[stageId] =
                            PlayerStageClearRecord.CreateEmpty(stageId);
                    }
                });

            message = $"Campaign active stage set to '{stageId.Value}'.";
            return true;
        }

        public bool IsUnlocked(StageContentEntry entry)
        {
            return entry != null && entry.IsInitiallyAvailable;
        }

        private string ResolveLevelGroupId(StageContentEntry entry)
        {
            if (_sequenceResolver.Contains(entry.StageId))
            {
                return _sequenceResolver.GetLevelGroupId(entry.StageId);
            }

            return entry.CatalogChapterId;
        }
    }

    public sealed class DemoStageControlLaunchBridge : IDemoStageControlLaunchBridge
    {
        private readonly Func<bool> _isSceneTransitionInProgress;
        private readonly IStageLaunchRouter _stageLaunchRouter;

        public DemoStageControlLaunchBridge(
            IStageLaunchRouter stageLaunchRouter,
            Func<bool> isSceneTransitionInProgress)
        {
            _stageLaunchRouter = stageLaunchRouter ?? throw new ArgumentNullException(nameof(stageLaunchRouter));
            _isSceneTransitionInProgress = isSceneTransitionInProgress ?? (() => false);
        }

        public bool IsSceneTransitionInProgress => _isSceneTransitionInProgress();

        public DemoStageControlResult Launch(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                return DemoStageControlResult.Fail("Stage launch requires a valid stage id.");
            }

            if (IsSceneTransitionInProgress)
            {
                return DemoStageControlResult.Fail("Scene transition is already in progress.");
            }

            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "demo-stage-control-start-stage",
                StageTransitionHint.ForKind(StageTransitionKind.StageRetryManual));
            try
            {
                StageLaunchContextStore.SetCurrent(stageId);
                _stageLaunchRouter.Launch(request);
                return DemoStageControlResult.Ok($"Loading stage '{stageId.Value}'.");
            }
            catch (Exception exception)
            {
                return DemoStageControlResult.Fail(exception.Message);
            }
        }
    }
}
