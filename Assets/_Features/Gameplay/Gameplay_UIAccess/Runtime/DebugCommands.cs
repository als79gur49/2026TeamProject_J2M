using System;
using Game.Feature.Stages;

namespace Game.Feature.Gameplay.UIAccess.DebugCommands
{
    public enum DebugCommandStatus
    {
        Success = 0,
        Failed = 1,
        Unavailable = 2,
    }

    public readonly struct DebugCommandAvailabilitySnapshot
    {
        public DebugCommandAvailabilitySnapshot(
            bool isDebugBuildEnabled,
            bool canOpenPanel,
            bool canGoNextStage,
            bool canForceClearResultOnly,
            bool isPresentationLocked,
            string reasonText,
            StageId currentStageId,
            StageId nextStageId)
        {
            IsDebugBuildEnabled = isDebugBuildEnabled;
            CanOpenPanel = canOpenPanel;
            CanGoNextStage = canGoNextStage;
            CanForceClearResultOnly = canForceClearResultOnly;
            IsPresentationLocked = isPresentationLocked;
            ReasonText = reasonText ?? string.Empty;
            CurrentStageId = currentStageId;
            NextStageId = nextStageId;
        }

        public bool IsDebugBuildEnabled { get; }

        public bool CanOpenPanel { get; }

        public bool CanGoNextStage { get; }

        public bool CanForceClearResultOnly { get; }

        public bool IsPresentationLocked { get; }

        public string ReasonText { get; }

        public StageId CurrentStageId { get; }

        public StageId NextStageId { get; }

        public static DebugCommandAvailabilitySnapshot Disabled(string reasonText = "Debug commands are disabled.")
        {
            return new DebugCommandAvailabilitySnapshot(
                isDebugBuildEnabled: false,
                canOpenPanel: false,
                canGoNextStage: false,
                canForceClearResultOnly: false,
                isPresentationLocked: false,
                reasonText: reasonText,
                currentStageId: StageId.None,
                nextStageId: StageId.None);
        }
    }

    public readonly struct DebugCommandResult
    {
        public DebugCommandResult(
            DebugCommandStatus status,
            string message,
            StageId targetStageId = default,
            StageNavigationRequest stageNavigationRequest = default,
            StageCompletionReadModel stageResultReadModel = null)
        {
            Status = status;
            Message = message ?? string.Empty;
            TargetStageId = targetStageId;
            StageNavigationRequest = stageNavigationRequest;
            StageResultReadModel = stageResultReadModel;
        }

        public DebugCommandStatus Status { get; }

        public string Message { get; }

        public StageId TargetStageId { get; }

        public StageNavigationRequest StageNavigationRequest { get; }

        public StageCompletionReadModel StageResultReadModel { get; }

        public bool IsSuccess => Status == DebugCommandStatus.Success;

        public static DebugCommandResult Success(
            string message,
            StageId targetStageId = default,
            StageNavigationRequest stageNavigationRequest = default,
            StageCompletionReadModel stageResultReadModel = null)
        {
            return new DebugCommandResult(
                DebugCommandStatus.Success,
                message,
                targetStageId,
                stageNavigationRequest,
                stageResultReadModel);
        }

        public static DebugCommandResult Unavailable(string message)
        {
            return new DebugCommandResult(DebugCommandStatus.Unavailable, message);
        }

        public static DebugCommandResult Failed(string message)
        {
            return new DebugCommandResult(DebugCommandStatus.Failed, message);
        }
    }

    public interface IDebugStageNavigationResolver
    {
        bool TryResolveNextStage(StageId currentStageId, out StageId nextStageId);
    }

    public interface IDebugStageCommandPort
    {
        DebugCommandAvailabilitySnapshot GetAvailability();

        DebugCommandResult GoToNextStage();

        DebugCommandResult ForceClearResultOnly();
    }

    public interface IDebugStageLaunchGateway
    {
        bool IsLaunchInProgress { get; }

        DebugCommandResult TryLaunch(StageNavigationRequest request);
    }

    public interface IDebugStageLaunchGatewayBinder
    {
        void BindStageLaunchGateway(IDebugStageLaunchGateway stageLaunchGateway);
    }

    public interface IDebugStageLaunchConstraint
    {
        bool CanLaunch(StageId targetStageId, out string reasonText);
    }

    public sealed class DebugCommandAccess
    {
        private static readonly IDebugStageCommandPort DisabledPort = new DisabledDebugStageCommandPort();

        private DebugCommandAccess(bool isEnabled, IDebugStageCommandPort stageCommandPort)
        {
            IsEnabled = isEnabled;
            StageCommandPort = stageCommandPort ?? DisabledPort;
        }

        public static DebugCommandAccess Disabled { get; } = new(false, DisabledPort);

        public bool IsEnabled { get; }

        public IDebugStageCommandPort StageCommandPort { get; }

        public static DebugCommandAccess Enabled(IDebugStageCommandPort stageCommandPort)
        {
            return new DebugCommandAccess(true, stageCommandPort);
        }

        public void BindStageLaunchGateway(IDebugStageLaunchGateway stageLaunchGateway)
        {
            if (StageCommandPort is IDebugStageLaunchGatewayBinder binder)
            {
                binder.BindStageLaunchGateway(stageLaunchGateway);
            }
        }

        private sealed class DisabledDebugStageCommandPort : IDebugStageCommandPort
        {
            public DebugCommandAvailabilitySnapshot GetAvailability()
            {
                return DebugCommandAvailabilitySnapshot.Disabled();
            }

            public DebugCommandResult GoToNextStage()
            {
                return DebugCommandResult.Unavailable("Debug commands are disabled.");
            }

            public DebugCommandResult ForceClearResultOnly()
            {
                return DebugCommandResult.Unavailable("Debug commands are disabled.");
            }
        }
    }

    public static class DebugCommandBuildGate
    {
        public static bool IsRuntimeEnabled(bool isEditor, bool isDebugBuild)
        {
            return isEditor || isDebugBuild;
        }
    }

    public sealed class AllowAllDebugStageLaunchConstraint : IDebugStageLaunchConstraint
    {
        public static AllowAllDebugStageLaunchConstraint Instance { get; } = new();

        private AllowAllDebugStageLaunchConstraint()
        {
        }

        public bool CanLaunch(StageId targetStageId, out string reasonText)
        {
            reasonText = string.Empty;
            return targetStageId.IsValid;
        }
    }
}
