using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Application
{
    public sealed class PausePopupPresenter
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;

        public PausePopupPresenter(ILocalizedTextResolver localizedTextResolver = null)
        {
            _localizedTextResolver = localizedTextResolver
                ?? throw new ArgumentNullException(nameof(localizedTextResolver));
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
                Resolve(payload.TitleTextDescriptor),
                Resolve(payload.DescriptionTextDescriptor),
                Resolve(payload.ResumeLabelDescriptor),
                Resolve(payload.SettingsLabelDescriptor),
                Resolve(payload.RetryLabelDescriptor),
                Resolve(payload.MainMenuLabelDescriptor));
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return _localizedTextResolver.Resolve(descriptor);
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
