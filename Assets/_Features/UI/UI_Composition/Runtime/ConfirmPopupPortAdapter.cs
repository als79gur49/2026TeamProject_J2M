using Game.Feature.Stages;
using Game.Feature.UI.ViewShared;
using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Composition
{
    public sealed class ConfirmPopupPortAdapter : IConfirmPopupPort, ICampaignModeSelectionPort, ICampaignSaveFailureDialogPort
    {
        private readonly PopupController _popupController;

        public ConfirmPopupPortAdapter(PopupController popupController)
        {
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
        }

        public void Request(ConfirmPopupPayload payload, Action<CampaignSaveFailureChoice> completion)
        {
            _popupController.Push(new PopupRequest(PopupId.Confirm, payload, result =>
            {
                var choice = CampaignSaveFailureChoice.Dismissed;
                if (result.CloseReason == PopupCloseReason.UserAction)
                {
                    if (result.CompletionKind == PopupCompletionKind.Confirmed) choice = CampaignSaveFailureChoice.Quit;
                    else if (result.CompletionKind == PopupCompletionKind.Cancelled) choice = CampaignSaveFailureChoice.Menu;
                }
                completion?.Invoke(choice);
            }), out _);
        }

        public void RequestMode(Action<GameMode?> completion)
        {
            var payload = new ConfirmPopupPayload(
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.title", LocalizedTextRole.Title),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.casual_detail"),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.hardcore_detail"),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.casual", LocalizedTextRole.Button),
                new LocalizedTextDescriptor("UI", "ui.campaign.mode.hardcore", LocalizedTextRole.Button), false)
            { SecondaryIsAlternative = true, IsCampaignModeSelection = true };
            _popupController.Push(new PopupRequest(PopupId.Confirm, payload, result =>
                completion?.Invoke(result.CompletionKind == PopupCompletionKind.Confirmed ? GameMode.Casual :
                    result.CompletionKind == PopupCompletionKind.AlternativeSelected ? GameMode.Hardcore : (GameMode?)null)), out _);
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
