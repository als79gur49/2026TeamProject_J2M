using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Composition
{
    public sealed class ConfirmPopupPortAdapter : IConfirmPopupPort
    {
        private readonly PopupController _popupController;

        public ConfirmPopupPortAdapter(PopupController popupController)
        {
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
        }

        public void Request(ConfirmPopupPayload payload, Action<bool> completion)
        {
            _popupController.Push(
                new PopupRequest(
                    PopupId.Confirm,
                    payload,
                    popupCompletion => completion?.Invoke(
                        popupCompletion.CompletionKind == PopupCompletionKind.Confirmed)),
                out _);
        }
    }
}
