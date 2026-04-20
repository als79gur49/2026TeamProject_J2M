using System;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Composition
{
    internal sealed class DisplayPreviewSessionHost
    {
        private readonly PopupController popupController;
        private readonly double previewTimeoutSeconds;
        private readonly DisplayPreviewTimeoutRelay timeoutRelay;
        private PopupInstanceId activePopupId;
        private Action cancelAction;
        private Action confirmAction;
        private bool hasSession;

        public DisplayPreviewSessionHost(
            PopupController popupController,
            DisplayPreviewTimeoutRelay timeoutRelay,
            double previewTimeoutSeconds = 15d)
        {
            this.popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            this.timeoutRelay = timeoutRelay ?? throw new ArgumentNullException(nameof(timeoutRelay));
            this.previewTimeoutSeconds = previewTimeoutSeconds;
        }

        public bool HasActiveSession => hasSession;

        public bool TryOpen(
            ConfirmPopupPayload payload,
            Action onConfirmed,
            Action onCancelled)
        {
            if (hasSession || payload == null)
            {
                return false;
            }

            if (!popupController.Push(
                    new PopupRequest(PopupId.Confirm, payload, HandlePopupCompletion),
                    out activePopupId))
            {
                return false;
            }

            confirmAction = onConfirmed;
            cancelAction = onCancelled;
            hasSession = true;
            timeoutRelay.Arm(previewTimeoutSeconds, HandleTimeoutElapsed);
            return true;
        }

        public bool CancelActivePreview()
        {
            if (!hasSession)
            {
                return false;
            }

            if (!popupController.Close(activePopupId, PopupCloseReason.Programmatic, PopupCompletionKind.Cancelled) &&
                hasSession)
            {
                CompleteCancelled();
            }

            return true;
        }

        private void HandleTimeoutElapsed()
        {
            if (!hasSession)
            {
                return;
            }

            if (!popupController.Close(activePopupId, PopupCloseReason.Programmatic, PopupCompletionKind.Cancelled) &&
                hasSession)
            {
                CompleteCancelled();
            }
        }

        private void HandlePopupCompletion(PopupCompletion completion)
        {
            if (!hasSession || !completion.InstanceId.Equals(activePopupId))
            {
                return;
            }

            if (completion.CompletionKind == PopupCompletionKind.Confirmed)
            {
                CompleteConfirmed();
                return;
            }

            CompleteCancelled();
        }

        private void CompleteConfirmed()
        {
            if (!hasSession)
            {
                return;
            }

            var callback = confirmAction;
            ClearSession();
            callback?.Invoke();
        }

        private void CompleteCancelled()
        {
            if (!hasSession)
            {
                return;
            }

            var callback = cancelAction;
            ClearSession();
            callback?.Invoke();
        }

        private void ClearSession()
        {
            timeoutRelay.Cancel();
            hasSession = false;
            activePopupId = default;
            confirmAction = null;
            cancelAction = null;
        }
    }
}
