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
                Resolve(payload.ResumeLabelDescriptor),
                Resolve(payload.SettingsLabelDescriptor),
                Resolve(payload.RetryLabelDescriptor),
                Resolve(payload.MainMenuLabelDescriptor),
                MapProgression(payload.Progression));
        }

        internal static PauseProgressionViewModel MapProgression(PauseProgressionSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsAvailable || snapshot.Stages.Count == 0)
            {
                return PauseProgressionViewModel.Hidden;
            }

            var currentIndex = -1;
            for (var i = 0; i < snapshot.Stages.Count; i++)
            {
                if (string.Equals(snapshot.Stages[i].StageKey, snapshot.CurrentStageKey, StringComparison.Ordinal))
                {
                    currentIndex = i;
                    break;
                }
            }

            var markers = new PauseProgressionMarkerModel[snapshot.Stages.Count];
            for (var i = 0; i < snapshot.Stages.Count; i++)
            {
                var stage = snapshot.Stages[i];
                markers[i] = new PauseProgressionMarkerModel(
                    stage.StageKey,
                    stage.DisplayNameDescriptor);
            }

            return new PauseProgressionViewModel(
                isVisible: true,
                markers,
                currentIndex);
        }

        private string Resolve(LocalizedTextDescriptor descriptor)
        {
            return _localizedTextResolver.Resolve(descriptor);
        }
    }

    public sealed class ConfirmPopupPresenter : IDisposable
    {
        private readonly ILocalizedTextResolver _localizedTextResolver;
        private ConfirmPopupPayload _payload;
        private bool _isDisposed;

        public ConfirmPopupPresenter(ILocalizedTextResolver localizedTextResolver = null)
        {
            _localizedTextResolver = localizedTextResolver;
            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged += HandleLocaleChanged;
            }

            ViewModel = new ConfirmPopupViewModel();
        }

        public ConfirmPopupViewModel ViewModel { get; }

        public void Apply(ConfirmPopupPayload payload)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(ConfirmPopupPresenter));
            }

            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            _payload = payload;
            ViewModel.SetContent(
                Resolve(payload.TitleTextDescriptor, payload.TitleText),
                Resolve(payload.BodyTextDescriptor, payload.BodyText),
                Resolve(payload.WarningTextDescriptor, payload.WarningText),
                Resolve(payload.ConfirmLabelDescriptor, payload.ConfirmLabel),
                Resolve(payload.CancelLabelDescriptor, payload.CancelLabel),
                payload.IsConfirmDestructive);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_localizedTextResolver != null)
            {
                _localizedTextResolver.LocaleChanged -= HandleLocaleChanged;
            }

            _payload = null;
            _isDisposed = true;
        }

        private string Resolve(LocalizedTextDescriptor descriptor, string fallback)
        {
            if (string.IsNullOrEmpty(descriptor.Table) && string.IsNullOrEmpty(descriptor.Key))
            {
                return fallback ?? string.Empty;
            }

            if (_localizedTextResolver == null)
            {
                throw new InvalidOperationException(
                    "Localized ConfirmPopupPayload requires an explicit localized text resolver.");
            }

            return _localizedTextResolver.Resolve(descriptor);
        }

        private void HandleLocaleChanged()
        {
            if (!_isDisposed && _payload != null)
            {
                Apply(_payload);
            }
        }
    }

}
