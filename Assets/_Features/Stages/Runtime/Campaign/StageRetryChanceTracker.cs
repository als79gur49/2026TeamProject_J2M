using System;

namespace Game.Feature.Stages
{
    public enum StageRetryRouteKind
    {
        None = 0,
        RetrySameStage = 1,
        ReturnToLevelGroupFirstStage = 2,
    }

    public readonly struct StageRetryRouteResult
    {
        public StageRetryRouteResult(
            StageRetryRouteKind routeKind,
            StageId nextStageId,
            int remainingChances)
        {
            RouteKind = routeKind;
            NextStageId = nextStageId;
            RemainingChances = remainingChances;
        }

        public StageRetryRouteKind RouteKind { get; }

        public StageId NextStageId { get; }

        public int RemainingChances { get; }
    }

    public sealed class StageRetryChanceTracker
    {
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public StageRetryChanceTracker(CampaignStageSequenceResolver sequenceResolver)
        {
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
        }

        public StageRetryRouteResult ResolveDeathRoute(SaveSlotData slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (!slot.CurrentStageId.IsValid)
            {
                throw new InvalidOperationException("Save slot does not contain a current campaign stage.");
            }

            var remainingChances = slot.RemainingChances <= 0
                ? SaveSlotStore.DefaultRemainingChances
                : slot.RemainingChances;

            if (remainingChances > 1)
            {
                return new StageRetryRouteResult(
                    StageRetryRouteKind.RetrySameStage,
                    slot.CurrentStageId,
                    remainingChances - 1);
            }

            var levelGroupId = !string.IsNullOrWhiteSpace(slot.CurrentLevelGroupId)
                ? slot.CurrentLevelGroupId
                : _sequenceResolver.GetLevelGroupId(slot.CurrentStageId);
            if (!_sequenceResolver.TryGetFirstStageInLevelGroup(levelGroupId, out var firstStageId))
            {
                throw new InvalidOperationException(
                    $"Campaign level group '{levelGroupId}' does not have a first stage.");
            }

            return new StageRetryRouteResult(
                StageRetryRouteKind.ReturnToLevelGroupFirstStage,
                firstStageId,
                SaveSlotStore.DefaultRemainingChances);
        }
    }
}
