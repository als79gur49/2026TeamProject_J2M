using System;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public readonly struct ScreenInstanceId : IEquatable<ScreenInstanceId>
    {
        public ScreenInstanceId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ScreenInstanceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ScreenInstanceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public enum ScreenPolicyClass
    {
        GameplayRoot = 0,
        InformationalOverlay = 1,
        GameplayAdjacentOverlay = 2,
        Configuration = 3,
        TerminalResult = 4,
    }

    public enum ScreenRetentionMode
    {
        RetainMountedHistory = 0,
        DisposeOnHide = 1,
    }

    public enum ScreenBackAction
    {
        None = 0,
        Pop = 1,
        Consume = 2,
    }

    public enum HudShellMode
    {
        Visible = 0,
        Hidden = 1,
        Dimmed = 2,
    }

    public readonly struct ScreenPolicy
    {
        public ScreenPolicy(
            ScreenPolicyClass policyClass,
            ScreenRetentionMode retentionMode,
            ScreenBackAction backAction,
            HudShellMode hudShellMode,
            bool blocksUiGameplayInput)
        {
            PolicyClass = policyClass;
            RetentionMode = retentionMode;
            BackAction = backAction;
            HudShellMode = hudShellMode;
            BlocksUiGameplayInput = blocksUiGameplayInput;
        }

        public ScreenPolicyClass PolicyClass { get; }

        public ScreenRetentionMode RetentionMode { get; }

        public ScreenBackAction BackAction { get; }

        public HudShellMode HudShellMode { get; }

        public bool BlocksUiGameplayInput { get; }
    }

    public readonly struct ScreenRequest
    {
        public ScreenRequest(
            ScreenId screenId,
            IScreenPayload payload,
            string reuseKey = null)
        {
            if (screenId == ScreenId.None)
            {
                throw new ArgumentOutOfRangeException(nameof(screenId));
            }

            ScreenId = screenId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            ReuseKey = reuseKey;
        }

        public ScreenId ScreenId { get; }

        public IScreenPayload Payload { get; }

        public string ReuseKey { get; }
    }

    public readonly struct ScreenEntry
    {
        public ScreenEntry(
            ScreenInstanceId instanceId,
            ScreenId screenId,
            IScreenPayload payload,
            ScreenPolicy policy,
            string reuseKey)
        {
            if (screenId == ScreenId.None)
            {
                throw new ArgumentOutOfRangeException(nameof(screenId));
            }

            InstanceId = instanceId;
            ScreenId = screenId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Policy = policy;
            ReuseKey = reuseKey;
        }

        public ScreenInstanceId InstanceId { get; }

        public ScreenId ScreenId { get; }

        public IScreenPayload Payload { get; }

        public ScreenPolicy Policy { get; }

        public string ReuseKey { get; }
    }

    public enum ScreenTransitionKind
    {
        Show = 0,
        Push = 1,
        Replace = 2,
        Pop = 3,
    }

    public readonly struct ScreenTransitionedEvent
    {
        public ScreenTransitionedEvent(
            ScreenTransitionKind kind,
            ScreenEntry? previousEntry,
            ScreenEntry? currentEntry)
        {
            Kind = kind;
            PreviousEntry = previousEntry;
            CurrentEntry = currentEntry;
        }

        public ScreenTransitionKind Kind { get; }

        public ScreenEntry? PreviousEntry { get; }

        public ScreenEntry? CurrentEntry { get; }
    }

    public enum ScreenActionKind
    {
        None = 0,
        BackRequested = 1,
        ShowScreen = 2,
        PushScreen = 3,
        ReplaceScreen = 4,
        RequestPopup = 5,
        LaunchStage = 6,
        ReturnToMainMenu = 7,
    }

    public readonly struct ScreenAction
    {
        private ScreenAction(
            ScreenActionKind actionKind,
            ScreenRequest screenRequest,
            PopupRequest popupRequest,
            StageNavigationRequest stageNavigationRequest)
        {
            ActionKind = actionKind;
            ScreenRequest = screenRequest;
            PopupRequest = popupRequest;
            StageNavigationRequest = stageNavigationRequest;
        }

        public ScreenActionKind ActionKind { get; }

        public ScreenRequest ScreenRequest { get; }

        public PopupRequest PopupRequest { get; }

        public StageNavigationRequest StageNavigationRequest { get; }

        public static ScreenAction Back()
        {
            return new ScreenAction(ScreenActionKind.BackRequested, default, default, StageNavigationRequest.None);
        }

        public static ScreenAction Show(ScreenRequest request)
        {
            return new ScreenAction(ScreenActionKind.ShowScreen, request, default, StageNavigationRequest.None);
        }

        public static ScreenAction Push(ScreenRequest request)
        {
            return new ScreenAction(ScreenActionKind.PushScreen, request, default, StageNavigationRequest.None);
        }

        public static ScreenAction Replace(ScreenRequest request)
        {
            return new ScreenAction(ScreenActionKind.ReplaceScreen, request, default, StageNavigationRequest.None);
        }

        public static ScreenAction Popup(PopupRequest request)
        {
            return new ScreenAction(ScreenActionKind.RequestPopup, default, request, StageNavigationRequest.None);
        }

        public static ScreenAction LaunchStage(StageNavigationRequest request)
        {
            return new ScreenAction(ScreenActionKind.LaunchStage, default, default, request);
        }

        public static ScreenAction ReturnToMainMenu()
        {
            return new ScreenAction(ScreenActionKind.ReturnToMainMenu, default, default, StageNavigationRequest.None);
        }
    }

    public interface IMainMenuReturnRouter
    {
        void ReturnToMainMenu();
    }

    public sealed class NoOpMainMenuReturnRouter : IMainMenuReturnRouter
    {
        public static readonly NoOpMainMenuReturnRouter Instance = new();

        private NoOpMainMenuReturnRouter()
        {
        }

        public void ReturnToMainMenu()
        {
        }
    }

    public interface IScreenRuntime : IDisposable
    {
        event Action<ScreenAction> ActionRequested;

        void ApplyPayload(IScreenPayload payload);

        void SetIsCurrent(bool isCurrent);
    }

    public readonly struct ScreenRuntimeFactoryResult
    {
        public ScreenRuntimeFactoryResult(ScreenPolicy policy, IScreenRuntime runtime)
        {
            Policy = policy;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public ScreenPolicy Policy { get; }

        public IScreenRuntime Runtime { get; }
    }

    public interface IScreenRuntimeFactory
    {
        ScreenRuntimeFactoryResult Create(ScreenRequest request);
    }
}
