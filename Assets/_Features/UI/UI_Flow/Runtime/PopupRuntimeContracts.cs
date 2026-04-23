using System;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Flow
{
    public readonly struct PopupInstanceId : IEquatable<PopupInstanceId>
    {
        public PopupInstanceId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(PopupInstanceId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PopupInstanceId other && Equals(other);
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

    public enum PopupPolicyClass
    {
        ModalBlocking = 0,
        NonModalInformational = 1,
        AnchoredEphemeral = 2,
        ExplicitCloseRewardResult = 3,
    }

    public enum PopupLifetimeScope
    {
        CurrentScreen = 0,
        Application = 1,
    }

    public enum PopupBackAction
    {
        Close = 0,
        Cancel = 1,
        Consume = 2,
    }

    public enum PopupCloseReason
    {
        UserAction = 0,
        Back = 1,
        BackdropClick = 2,
        ScreenTransition = 3,
        Dispose = 4,
        Programmatic = 5,
    }

    public readonly struct PopupPolicy
    {
        public PopupPolicy(
            PopupPolicyClass policyClass,
            PopupLifetimeScope lifetimeScope,
            PopupBackAction backAction,
            PopupBackdropMode backdropMode,
            bool showsDim,
            bool blocksLowerLayers)
        {
            PolicyClass = policyClass;
            LifetimeScope = lifetimeScope;
            BackAction = backAction;
            BackdropMode = backdropMode;
            ShowsDim = showsDim;
            BlocksLowerLayers = blocksLowerLayers;
        }

        public PopupPolicyClass PolicyClass { get; }

        public PopupLifetimeScope LifetimeScope { get; }

        public PopupBackAction BackAction { get; }

        public PopupBackdropMode BackdropMode { get; }

        public bool ShowsDim { get; }

        public bool BlocksLowerLayers { get; }
    }

    public readonly struct PopupCompletion
    {
        public PopupCompletion(
            PopupInstanceId instanceId,
            PopupId popupId,
            PopupCompletionKind completionKind,
            PopupCloseReason closeReason)
        {
            InstanceId = instanceId;
            PopupId = popupId;
            CompletionKind = completionKind;
            CloseReason = closeReason;
        }

        public PopupInstanceId InstanceId { get; }

        public PopupId PopupId { get; }

        public PopupCompletionKind CompletionKind { get; }

        public PopupCloseReason CloseReason { get; }
    }

    public readonly struct PopupOpenedEvent
    {
        public PopupOpenedEvent(PopupEntry entry)
        {
            Entry = entry;
        }

        public PopupEntry Entry { get; }
    }

    public readonly struct PopupCompletedEvent
    {
        public PopupCompletedEvent(PopupEntry entry, PopupCompletion completion)
        {
            Entry = entry;
            Completion = completion;
        }

        public PopupEntry Entry { get; }

        public PopupCompletion Completion { get; }
    }

    public readonly struct PopupRequest
    {
        public PopupRequest(
            PopupId popupId,
            IPopupPayload payload,
            Action<PopupCompletion> completionCallback = null)
        {
            if (popupId == PopupId.None)
            {
                throw new ArgumentOutOfRangeException(nameof(popupId));
            }

            PopupId = popupId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            CompletionCallback = completionCallback;
        }

        public PopupId PopupId { get; }

        public IPopupPayload Payload { get; }

        public Action<PopupCompletion> CompletionCallback { get; }
    }

    public interface IPopupRuntime : IDisposable
    {
        event Action<PopupCompletionKind> CompletionRequested;

        void SetIsTopmost(bool isTopmost);
    }

    public readonly struct PopupRuntimeFactoryResult
    {
        public PopupRuntimeFactoryResult(PopupPolicy policy, IPopupRuntime runtime)
        {
            Policy = policy;
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public PopupPolicy Policy { get; }

        public IPopupRuntime Runtime { get; }
    }

    public interface IPopupRuntimeFactory
    {
        PopupRuntimeFactoryResult Create(PopupRequest request);
    }
}
