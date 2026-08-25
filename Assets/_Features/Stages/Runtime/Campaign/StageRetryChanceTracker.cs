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

        public StageRetryRouteResult ResolveDeathRoute(CampaignSlotState slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            if (!slot.CurrentStageId.IsValid)
            {
                throw new InvalidOperationException("Save slot does not contain a current campaign stage.");
            }

            var remainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
                slot.RemainingChances);

            if (remainingChances > 1)
            {
                return new StageRetryRouteResult(
                    StageRetryRouteKind.RetrySameStage,
                    slot.CurrentStageId,
                    remainingChances - 1);
            }

            var levelGroupId = _sequenceResolver.GetLevelGroupId(slot.CurrentStageId);
            if (!_sequenceResolver.TryGetFirstStageInLevelGroup(levelGroupId, out var firstStageId))
            {
                throw new InvalidOperationException(
                    $"Campaign level group '{levelGroupId}' does not have a first stage.");
            }

            return new StageRetryRouteResult(
                StageRetryRouteKind.ReturnToLevelGroupFirstStage,
                firstStageId,
                CampaignSaveSlotPolicy.DefaultRemainingChances);
        }
    }

    public readonly struct CampaignDeathTransitionPlan
    {
        internal CampaignDeathTransitionPlan(
            StageId expectedCurrentStageId,
            int expectedRemainingChances,
            string persistedLevelGroupId,
            StageRetryRouteResult route)
        {
            ExpectedCurrentStageId = expectedCurrentStageId;
            ExpectedRemainingChances = expectedRemainingChances;
            PersistedLevelGroupId = persistedLevelGroupId ?? string.Empty;
            Route = route;
        }

        public StageId ExpectedCurrentStageId { get; }

        public int ExpectedRemainingChances { get; }

        public string PersistedLevelGroupId { get; }

        public StageRetryRouteResult Route { get; }
    }

    public readonly struct CampaignStageClearTransitionPlan
    {
        internal CampaignStageClearTransitionPlan(
            StageId completedStageId,
            StageId persistedStageId,
            string completedLevelGroupId,
            string persistedLevelGroupId,
            bool isCampaignCompleted,
            bool restoresChances)
        {
            CompletedStageId = completedStageId;
            PersistedStageId = persistedStageId;
            CompletedLevelGroupId = completedLevelGroupId ?? string.Empty;
            PersistedLevelGroupId = persistedLevelGroupId ?? string.Empty;
            IsCampaignCompleted = isCampaignCompleted;
            RestoresChances = restoresChances;
        }

        public StageId CompletedStageId { get; }

        public StageId PersistedStageId { get; }

        public string CompletedLevelGroupId { get; }

        public string PersistedLevelGroupId { get; }

        public bool IsCampaignCompleted { get; }

        public bool RestoresChances { get; }
    }

    /// <summary>
    /// Pure campaign transition policy. It decides the intended transition but never writes save data.
    /// </summary>
    public sealed class CampaignProgressionTransitionPlanner
    {
        private readonly CampaignStageSequenceResolver _sequenceResolver;
        private readonly StageRetryChanceTracker _retryChanceTracker;

        public CampaignProgressionTransitionPlanner(
            CampaignStageSequenceResolver sequenceResolver)
        {
            _sequenceResolver = sequenceResolver ??
                throw new ArgumentNullException(nameof(sequenceResolver));
            _retryChanceTracker = new StageRetryChanceTracker(_sequenceResolver);
        }

        public CampaignDeathTransitionPlan PlanDeath(CampaignSlotState slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            var remainingChances = CampaignSaveSlotPolicy.RequireValidRemainingChances(
                slot.RemainingChances);
            var route = _retryChanceTracker.ResolveDeathRoute(slot);
            return new CampaignDeathTransitionPlan(
                slot.CurrentStageId,
                remainingChances,
                _sequenceResolver.GetLevelGroupId(route.NextStageId),
                route);
        }

        public CampaignStageClearTransitionPlan PlanStageClear(StageId completedStageId)
        {
            if (!completedStageId.IsValid || !_sequenceResolver.Contains(completedStageId))
            {
                throw new ArgumentException(
                    "Completed stage must belong to the campaign sequence.",
                    nameof(completedStageId));
            }

            var completedLevelGroupId = _sequenceResolver.GetLevelGroupId(completedStageId);
            if (_sequenceResolver.IsFinal(completedStageId))
            {
                return new CampaignStageClearTransitionPlan(
                    completedStageId,
                    completedStageId,
                    completedLevelGroupId,
                    completedLevelGroupId,
                    isCampaignCompleted: true,
                    restoresChances: false);
            }

            if (!_sequenceResolver.TryGetNext(completedStageId, out var nextStageId))
            {
                throw new InvalidOperationException(
                    $"Campaign sequence could not resolve a next stage for '{completedStageId.Value}'.");
            }

            var nextLevelGroupId = _sequenceResolver.GetLevelGroupId(nextStageId);
            return new CampaignStageClearTransitionPlan(
                completedStageId,
                nextStageId,
                completedLevelGroupId,
                nextLevelGroupId,
                isCampaignCompleted: false,
                restoresChances: !string.Equals(
                    completedLevelGroupId,
                    nextLevelGroupId,
                    StringComparison.Ordinal));
        }
    }
}
