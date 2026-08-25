using System;

namespace Game.Feature.Stages
{
    public enum CampaignSlotLaunchStatus
    {
        Empty = 0,
        Ready = 1,
        Completed = 2,
        StageMissingFromSequence = 3,
        StageMissingFromCatalog = 4,
        LevelGroupSynchronizationRequired = 5,
    }

    public readonly struct CampaignSlotLaunchEvaluation
    {
        internal CampaignSlotLaunchEvaluation(
            CampaignSlotLaunchStatus status,
            CampaignSlotState state,
            StageId resolvedStageId,
            string resolvedLevelGroupId)
        {
            Status = status;
            State = state;
            ResolvedStageId = resolvedStageId;
            ResolvedLevelGroupId = resolvedLevelGroupId ?? string.Empty;
        }

        public CampaignSlotLaunchStatus Status { get; }

        public CampaignSlotState State { get; }

        public StageId ResolvedStageId { get; }

        public string ResolvedLevelGroupId { get; }

        public bool RequiresLevelGroupSynchronization =>
            Status == CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired;

        internal static CampaignSlotLaunchEvaluation ClassifyCanonical(
            CampaignSlotEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entry.IsEmpty)
            {
                return Empty();
            }

            var state = entry.State;
            return new CampaignSlotLaunchEvaluation(
                state.CampaignCompleted
                    ? CampaignSlotLaunchStatus.Completed
                    : CampaignSlotLaunchStatus.Ready,
                state,
                state.CurrentStageId,
                state.CurrentLevelGroupId);
        }

        internal static CampaignSlotLaunchEvaluation Empty()
        {
            return new CampaignSlotLaunchEvaluation(
                CampaignSlotLaunchStatus.Empty,
                null,
                StageId.None,
                string.Empty);
        }
    }

    public readonly struct CampaignSlotActionPolicy
    {
        internal CampaignSlotActionPolicy(
            bool canContinue,
            bool canRestart,
            bool canDelete)
        {
            CanContinue = canContinue;
            CanRestart = canRestart;
            CanDelete = canDelete;
        }

        public bool CanContinue { get; }

        public bool CanRestart { get; }

        public bool CanDelete { get; }

        public static CampaignSlotActionPolicy Evaluate(
            CampaignSlotLaunchEvaluation evaluation)
        {
            switch (evaluation.Status)
            {
                case CampaignSlotLaunchStatus.Ready:
                    return AvailableForLaunch();
                case CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired:
                    return evaluation.State != null && !evaluation.State.CampaignCompleted
                        ? AvailableForLaunch()
                        : UnavailableOccupied();
                case CampaignSlotLaunchStatus.Completed:
                case CampaignSlotLaunchStatus.StageMissingFromSequence:
                case CampaignSlotLaunchStatus.StageMissingFromCatalog:
                    return UnavailableOccupied();
                case CampaignSlotLaunchStatus.Empty:
                default:
                    return default;
            }
        }

        internal static CampaignSlotActionPolicy AvailableForLaunch()
        {
            return new CampaignSlotActionPolicy(
                canContinue: true,
                canRestart: true,
                canDelete: true);
        }

        internal static CampaignSlotActionPolicy UnavailableOccupied()
        {
            return new CampaignSlotActionPolicy(
                canContinue: false,
                canRestart: true,
                canDelete: true);
        }
    }

    public sealed class CampaignSlotLaunchEvaluator
    {
        private readonly StageCatalogResolver _catalogResolver;
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public CampaignSlotLaunchEvaluator(
            CampaignStageSequenceResolver sequenceResolver,
            IStageCatalogProvider stageCatalogProvider)
        {
            _sequenceResolver = sequenceResolver ??
                                throw new ArgumentNullException(nameof(sequenceResolver));
            if (stageCatalogProvider == null)
            {
                throw new ArgumentNullException(nameof(stageCatalogProvider));
            }

            _catalogResolver = new StageCatalogResolver(stageCatalogProvider);
        }

        public CampaignSlotLaunchEvaluation Evaluate(CampaignSlotEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            return entry.IsEmpty
                ? CampaignSlotLaunchEvaluation.Empty()
                : Evaluate(entry.State);
        }

        public CampaignSlotLaunchEvaluation Evaluate(CampaignSlotState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var stageId = state.CurrentStageId;
            if (!_sequenceResolver.Contains(stageId))
            {
                return new CampaignSlotLaunchEvaluation(
                    CampaignSlotLaunchStatus.StageMissingFromSequence,
                    state,
                    stageId,
                    string.Empty);
            }

            var resolvedLevelGroupId = _sequenceResolver.GetLevelGroupId(stageId);
            if (!_catalogResolver.TryResolve(stageId, out _))
            {
                return new CampaignSlotLaunchEvaluation(
                    CampaignSlotLaunchStatus.StageMissingFromCatalog,
                    state,
                    stageId,
                    resolvedLevelGroupId);
            }

            if (!string.Equals(
                    state.CurrentLevelGroupId,
                    resolvedLevelGroupId,
                    StringComparison.Ordinal))
            {
                return new CampaignSlotLaunchEvaluation(
                    CampaignSlotLaunchStatus.LevelGroupSynchronizationRequired,
                    state,
                    stageId,
                    resolvedLevelGroupId);
            }

            return new CampaignSlotLaunchEvaluation(
                state.CampaignCompleted
                    ? CampaignSlotLaunchStatus.Completed
                    : CampaignSlotLaunchStatus.Ready,
                state,
                stageId,
                resolvedLevelGroupId);
        }
    }
}
