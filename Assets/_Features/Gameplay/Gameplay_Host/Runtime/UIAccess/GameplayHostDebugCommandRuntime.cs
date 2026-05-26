using System;
using Game.Feature.Gameplay.UIAccess.DebugCommands;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class CampaignDebugStageNavigationResolver : IDebugStageNavigationResolver
    {
        private readonly CampaignStageSequenceResolver _sequenceResolver;

        public CampaignDebugStageNavigationResolver(CampaignStageSequenceResolver sequenceResolver)
        {
            _sequenceResolver = sequenceResolver ?? throw new ArgumentNullException(nameof(sequenceResolver));
        }

        public bool TryResolveNextStage(StageId currentStageId, out StageId nextStageId)
        {
            return _sequenceResolver.TryGetNext(currentStageId, out nextStageId);
        }
    }

    internal sealed class GameplayHostDebugStageCommandPort : IDebugStageCommandPort, IDebugStageLaunchGatewayBinder
    {
        private const string DebugSource = "debug-command-ui";
        private readonly StageContentEntry _entry;
        private readonly GameplayHostPresentationFeed _presentationFeed;
        private readonly IDebugStageNavigationResolver _navigationResolver;
        private readonly IDebugStageLaunchConstraint _launchConstraint;
        private IDebugStageLaunchGateway _stageLaunchGateway;

        public GameplayHostDebugStageCommandPort(
            StageContentEntry entry,
            GameplayHostPresentationFeed presentationFeed,
            IDebugStageNavigationResolver navigationResolver,
            IDebugStageLaunchConstraint launchConstraint = null)
        {
            _entry = entry;
            _presentationFeed = presentationFeed ?? throw new ArgumentNullException(nameof(presentationFeed));
            _navigationResolver = navigationResolver ?? throw new ArgumentNullException(nameof(navigationResolver));
            _launchConstraint = launchConstraint ?? AllowAllDebugStageLaunchConstraint.Instance;
        }

        public void BindStageLaunchGateway(IDebugStageLaunchGateway stageLaunchGateway)
        {
            _stageLaunchGateway = stageLaunchGateway;
        }

        public DebugCommandAvailabilitySnapshot GetAvailability()
        {
            var currentStageId = ResolveCurrentStageId();
            var hasNext = currentStageId.IsValid && _navigationResolver.TryResolveNextStage(currentStageId, out var nextStageId);
            var isLocked = IsPresentationLocked(_presentationFeed.CurrentState) ||
                           _presentationFeed.HasPendingStageClearPresentation;
            var isGatewayBound = _stageLaunchGateway != null;
            var isLaunchInProgress = _stageLaunchGateway != null && _stageLaunchGateway.IsLaunchInProgress;
            var launchConstraintReason = string.Empty;
            var isLaunchAllowed = hasNext && _launchConstraint.CanLaunch(nextStageId, out launchConstraintReason);
            var reason = ResolveReason(
                currentStageId,
                hasNext,
                isLocked,
                isGatewayBound,
                isLaunchInProgress,
                isLaunchAllowed,
                launchConstraintReason);
            return new DebugCommandAvailabilitySnapshot(
                isDebugBuildEnabled: true,
                canOpenPanel: true,
                canGoNextStage: !isLocked && isGatewayBound && !isLaunchInProgress && hasNext && isLaunchAllowed,
                canForceClearResultOnly: !isLocked && currentStageId.IsValid && _presentationFeed.CurrentStageCompletion == null,
                isPresentationLocked: isLocked || isLaunchInProgress,
                reasonText: reason,
                currentStageId: currentStageId,
                nextStageId: hasNext ? nextStageId : StageId.None);
        }

        public DebugCommandResult GoToNextStage()
        {
            var availability = GetAvailability();
            if (!availability.CanGoNextStage || !availability.NextStageId.IsValid)
            {
                return DebugCommandResult.Unavailable(availability.ReasonText);
            }

            if (_stageLaunchGateway == null)
            {
                return DebugCommandResult.Unavailable("Debug stage launch gateway is not bound.");
            }

            var request = new StageNavigationRequest(
                availability.NextStageId,
                StageNavigationKind.NextStage,
                DebugSource,
                StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
            return _stageLaunchGateway.TryLaunch(request);
        }

        public DebugCommandResult ForceClearResultOnly()
        {
            var availability = GetAvailability();
            if (!availability.CanForceClearResultOnly)
            {
                return DebugCommandResult.Unavailable(availability.ReasonText);
            }

            try
            {
                var readModel = _presentationFeed.ForceClearResultOnly();
                var nextRequest = StageNavigationRequest.None;
                var canContinueToNextStage = availability.CanGoNextStage && availability.NextStageId.IsValid;
                if (canContinueToNextStage)
                {
                    nextRequest = new StageNavigationRequest(
                        availability.NextStageId,
                        StageNavigationKind.NextStage,
                        DebugSource,
                        StageTransitionHint.ForKind(StageTransitionKind.StageClearNext));
                }
                else
                {
                    readModel = BuildReadModelWithDisabledNextStage(
                        readModel,
                        availability.NextStageId.IsValid,
                        availability.ReasonText);
                }

                return DebugCommandResult.Success(
                    BuildForceClearResultMessage(canContinueToNextStage, availability.ReasonText),
                    canContinueToNextStage ? availability.NextStageId : StageId.None,
                    nextRequest,
                    readModel);
            }
            catch (Exception exception)
            {
                return DebugCommandResult.Failed(exception.Message);
            }
        }

        private StageId ResolveCurrentStageId()
        {
            return _entry != null && _entry.StageId.IsValid
                ? _entry.StageId
                : StageId.None;
        }

        private static StageCompletionReadModel BuildReadModelWithDisabledNextStage(
            StageCompletionReadModel readModel,
            bool hasNextStage,
            string reasonText)
        {
            if (readModel == null)
            {
                return null;
            }

            return new StageCompletionReadModel(
                readModel.StageId,
                readModel.DisplayName,
                readModel.ResultTitle,
                readModel.ResultSummaryText,
                AppendNextUnavailableReason(readModel.ResultDetailText, reasonText),
                hasNextStage ? "Next stage unavailable" : "No next stage",
                readModel.ClearResult,
                readModel.ClearEvaluationResult,
                readModel.RewardGrantResult,
                readModel.UpdatedProgress);
        }

        private static string BuildForceClearResultMessage(bool canContinueToNextStage, string reasonText)
        {
            const string BaseMessage = "Opened DEBUG FORCED CLEAR result only. NO SAVE / NO REWARD.";
            if (canContinueToNextStage)
            {
                return BaseMessage;
            }

            return string.IsNullOrWhiteSpace(reasonText)
                ? $"{BaseMessage} Next stage unavailable."
                : $"{BaseMessage} Next stage unavailable: {reasonText}";
        }

        private static string AppendNextUnavailableReason(string detailText, string reasonText)
        {
            var reason = string.IsNullOrWhiteSpace(reasonText)
                ? "Next stage unavailable."
                : $"Next stage unavailable: {reasonText}";
            return string.IsNullOrWhiteSpace(detailText)
                ? reason
                : $"{detailText}{Environment.NewLine}{reason}";
        }

        private static bool IsPresentationLocked(GameplayPresentationState state)
        {
            return state.IsTopologyTransitionActive ||
                   state.HasBlockingPresentation ||
                   state.IsPresentationActive;
        }

        private static string ResolveReason(
            StageId currentStageId,
            bool hasNext,
            bool isLocked,
            bool isGatewayBound,
            bool isLaunchInProgress,
            bool isLaunchAllowed,
            string launchConstraintReason)
        {
            if (!currentStageId.IsValid)
            {
                return "Current stage is unavailable.";
            }

            if (isLocked)
            {
                return "Presentation/topology lock is active.";
            }

            if (!hasNext)
            {
                return "No next stage.";
            }

            if (!isGatewayBound)
            {
                return "Debug stage launch gateway is not bound.";
            }

            if (isLaunchInProgress)
            {
                return "Scene transition is already in progress.";
            }

            if (!isLaunchAllowed)
            {
                return string.IsNullOrWhiteSpace(launchConstraintReason)
                    ? "Debug next stage launch is unavailable."
                    : launchConstraintReason;
            }

            return hasNext ? "Ready." : "No next stage.";
        }
    }

    internal sealed class CampaignActiveSlotDebugStageLaunchConstraint : IDebugStageLaunchConstraint
    {
        private readonly SaveSlotStore _saveSlotStore;
        private readonly ActiveSlotProvider _activeSlotProvider;

        public CampaignActiveSlotDebugStageLaunchConstraint(
            SaveSlotStore saveSlotStore,
            ActiveSlotProvider activeSlotProvider)
        {
            _saveSlotStore = saveSlotStore ?? throw new ArgumentNullException(nameof(saveSlotStore));
            _activeSlotProvider = activeSlotProvider ?? throw new ArgumentNullException(nameof(activeSlotProvider));
        }

        public bool CanLaunch(StageId targetStageId, out string reasonText)
        {
            if (!targetStageId.IsValid)
            {
                reasonText = "Target stage is unavailable.";
                return false;
            }

            if (!_activeSlotProvider.TryGetActiveSlotNumber(out var slotNumber))
            {
                reasonText = "Campaign active slot is unavailable.";
                return false;
            }

            var activeSlot = _saveSlotStore.LoadSlot(slotNumber);
            if (!activeSlot.CurrentStageId.Equals(targetStageId))
            {
                reasonText =
                    $"Debug Next Stage unavailable: campaign active slot stage '{activeSlot.CurrentStageId.Value}' does not match target stage '{targetStageId.Value}'.";
                return false;
            }

            reasonText = string.Empty;
            return true;
        }
    }
}
