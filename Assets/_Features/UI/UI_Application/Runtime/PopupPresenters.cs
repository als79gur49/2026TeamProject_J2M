using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Application
{
    public sealed class PausePopupPresenter
    {
        public PausePopupPresenter()
        {
            ViewModel = new PausePopupViewModel();
        }

        public PausePopupViewModel ViewModel { get; }

        public void Apply(PausePopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.DescriptionText,
                payload.ResumeLabel,
                payload.SettingsLabel,
                payload.RetryLabel,
                payload.MainMenuLabel);
        }
    }

    public sealed class ConfirmPopupPresenter
    {
        public ConfirmPopupPresenter()
        {
            ViewModel = new ConfirmPopupViewModel();
        }

        public ConfirmPopupViewModel ViewModel { get; }

        public void Apply(ConfirmPopupPayload payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            ViewModel.SetContent(
                payload.TitleText,
                payload.BodyText,
                payload.ConfirmLabel,
                payload.CancelLabel,
                payload.IsConfirmDestructive);
        }
    }

}
