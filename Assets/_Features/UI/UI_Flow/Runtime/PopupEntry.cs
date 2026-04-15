using System;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Flow
{
    public readonly struct PopupEntry
    {
        public PopupEntry(
            PopupInstanceId instanceId,
            PopupId popupId,
            IPopupPayload payload,
            PopupPolicy policy,
            Action<PopupCompletion> completionCallback)
        {
            InstanceId = instanceId;
            PopupId = popupId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            Policy = policy;
            CompletionCallback = completionCallback;
        }

        public PopupInstanceId InstanceId { get; }

        public PopupId PopupId { get; }

        public IPopupPayload Payload { get; }

        public PopupPolicy Policy { get; }

        internal Action<PopupCompletion> CompletionCallback { get; }
    }
}
