using System;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Composition
{
    internal sealed class MainMenuLogoFeedbackController : IDisposable
    {
        private const float AcceptedVisualTailSeconds = 0.45f;
        private static readonly string QuitBodyKey =
            MainMenuLocalization.Descriptor(MainMenuLocalizationEntryId.QuitBody).Key;
        private static readonly string PrepareParticipantBodyKey =
            MainMenuLocalization.Descriptor(MainMenuLocalizationEntryId.ParticipantResetBody).Key;

        private readonly MainMenuScreenView _screenView;
        private readonly MainMenuLogoEffectView _effectView;
        private readonly PopupController _popupController;
        private readonly MainMenuSettingsOverlayController _settingsOverlayController;
        private PopupInstanceId? _lastAcceptedPopup;
        private SceneEntrySessionToken _lastAcceptedGameplayEntryToken;
        private MainMenuLogoImpactKind? _acceptedTailKind;
        private float _acceptedTailRemaining;
        private bool _applicationFocused;
        private bool _popupActive;
        private bool _settingsOpen;
        private bool _saveSlotsActive;
        private bool _transitionBlocked;
        private bool _disposed;

        public MainMenuLogoFeedbackController(
            MainMenuScreenView screenView,
            MainMenuLogoEffectView effectView,
            PopupController popupController,
            MainMenuSettingsOverlayController settingsOverlayController,
            bool applicationFocused)
        {
            _screenView = screenView ?? throw new ArgumentNullException(nameof(screenView));
            _effectView = effectView ?? throw new ArgumentNullException(nameof(effectView));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _settingsOverlayController = settingsOverlayController;
            _applicationFocused = applicationFocused;
            _popupActive = popupController.PopupCount > 0;
            _settingsOpen = settingsOverlayController?.IsOpen == true;
            _saveSlotsActive = screenView.ActiveSection == MainMenuSectionId.SaveSlots;

            _screenView.CommandFocusChanged += HandleCommandFocusChanged;
            _screenView.SectionChanged += HandleSectionChanged;
            _popupController.PopupOpened += HandlePopupOpened;
            _popupController.PopupCompleted += HandlePopupCompleted;
            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened += HandleSettingsOpened;
                _settingsOverlayController.Closed += HandleSettingsClosed;
            }

            ReconcileBlock(preserveAcceptedTail: false);
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (_disposed)
            {
                return;
            }

            if (!IsScreenAvailable())
            {
                CancelAcceptedTail();
                ReconcileBlock(preserveAcceptedTail: false);
                return;
            }

            if (_acceptedTailRemaining <= 0f)
            {
                return;
            }

            _acceptedTailRemaining = Math.Max(0f, _acceptedTailRemaining - Math.Max(0f, unscaledDeltaTime));
            if (_acceptedTailRemaining <= 0f)
            {
                _acceptedTailKind = null;
                ReconcileBlock(preserveAcceptedTail: false);
            }
        }

        public void SetApplicationFocused(bool focused)
        {
            if (_disposed || _applicationFocused == focused)
            {
                return;
            }

            _applicationFocused = focused;
            if (!focused)
            {
                CancelAcceptedTail();
            }

            ReconcileBlock(preserveAcceptedTail: false);
        }

        public void SetTransitionBlocked(bool blocked)
        {
            if (_disposed || _transitionBlocked == blocked)
            {
                return;
            }

            _transitionBlocked = blocked;
            var preserveStageLaunchTail = blocked &&
                                          _acceptedTailRemaining > 0f &&
                                          _acceptedTailKind == MainMenuLogoImpactKind.StageLaunch;
            ReconcileBlock(preserveStageLaunchTail);
        }

        public void NotifyGameplayLaunchAccepted(SceneEntrySessionToken token)
        {
            if (_disposed ||
                token == default ||
                token == _lastAcceptedGameplayEntryToken ||
                !_applicationFocused ||
                !IsScreenAvailable() ||
                _popupActive ||
                _settingsOpen ||
                _transitionBlocked)
            {
                return;
            }

            _lastAcceptedGameplayEntryToken = token;
            BeginAcceptedTail(MainMenuLogoImpactKind.StageLaunch);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _screenView.CommandFocusChanged -= HandleCommandFocusChanged;
            _screenView.SectionChanged -= HandleSectionChanged;
            _popupController.PopupOpened -= HandlePopupOpened;
            _popupController.PopupCompleted -= HandlePopupCompleted;
            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened -= HandleSettingsOpened;
                _settingsOverlayController.Closed -= HandleSettingsClosed;
            }

            _screenView.SetCommandFeedbackBlocked(true);
            _effectView.SetInteractionBlocked(true);
        }

        private void HandleCommandFocusChanged(MainMenuCommandFocusChanged changed)
        {
            if (!_disposed && !IsLogicallyBlocked() && changed.Current.HasFocus)
            {
                _effectView.PlayFocusShine(changed.Current);
            }
        }

        private void HandleSectionChanged(MainMenuSectionId sectionId)
        {
            var wasBlocked = IsLogicallyBlocked();
            _saveSlotsActive = sectionId == MainMenuSectionId.SaveSlots;
            if (_saveSlotsActive && !wasBlocked)
            {
                BeginAcceptedTail(MainMenuLogoImpactKind.Start);
                return;
            }

            ReconcileBlock(preserveAcceptedTail: false);
        }

        private void HandleSettingsOpened()
        {
            var wasBlocked = IsLogicallyBlocked();
            _settingsOpen = true;
            if (!wasBlocked)
            {
                BeginAcceptedTail(MainMenuLogoImpactKind.Settings);
                return;
            }

            ReconcileBlock(preserveAcceptedTail: false);
        }

        private void HandleSettingsClosed()
        {
            _settingsOpen = false;
            ReconcileBlock(preserveAcceptedTail: false);
        }

        private void HandlePopupOpened(PopupOpenedEvent openedEvent)
        {
            var wasBlocked = IsLogicallyBlocked();
            _popupActive = true;
            var impactKind = ResolveAcceptedPopupImpact(openedEvent);
            if (!wasBlocked &&
                impactKind.HasValue &&
                (!_lastAcceptedPopup.HasValue || !_lastAcceptedPopup.Value.Equals(openedEvent.Entry.InstanceId)))
            {
                _lastAcceptedPopup = openedEvent.Entry.InstanceId;
                BeginAcceptedTail(impactKind.Value);
                return;
            }

            ReconcileBlock(preserveAcceptedTail: false);
        }

        private void HandlePopupCompleted(PopupCompletedEvent completedEvent)
        {
            _popupActive = _popupController.PopupCount > 0;
            ReconcileBlock(preserveAcceptedTail: false);
        }

        private MainMenuLogoImpactKind? ResolveAcceptedPopupImpact(PopupOpenedEvent openedEvent)
        {
            if (openedEvent.Entry.PopupId != PopupId.Confirm ||
                openedEvent.Entry.Payload is not ConfirmPopupPayload payload)
            {
                return null;
            }

            var bodyKey = payload.BodyTextDescriptor.Key;
            if (string.Equals(bodyKey, QuitBodyKey, StringComparison.Ordinal))
            {
                return MainMenuLogoImpactKind.Quit;
            }

            return string.Equals(bodyKey, PrepareParticipantBodyKey, StringComparison.Ordinal)
                ? MainMenuLogoImpactKind.PrepareParticipant
                : null;
        }

        private void BeginAcceptedTail(MainMenuLogoImpactKind kind)
        {
            _screenView.SetCommandFeedbackBlocked(true);
            _effectView.SetInteractionBlocked(true);
            _effectView.SetInteractionBlocked(false);
            _effectView.PlayAccepted(kind);
            _acceptedTailKind = kind;
            _acceptedTailRemaining = AcceptedVisualTailSeconds;
        }

        private void ReconcileBlock(bool preserveAcceptedTail)
        {
            var blocked = IsLogicallyBlocked();
            _screenView.SetCommandFeedbackBlocked(blocked);
            if (!blocked)
            {
                CancelAcceptedTail();
                _effectView.SetInteractionBlocked(false);
                return;
            }

            if (preserveAcceptedTail && _acceptedTailRemaining > 0f)
            {
                return;
            }

            CancelAcceptedTail();
            _effectView.SetInteractionBlocked(true);
        }

        private bool IsLogicallyBlocked()
        {
            return !_applicationFocused ||
                   !IsScreenAvailable() ||
                   _popupActive ||
                   _settingsOpen ||
                   _saveSlotsActive ||
                   _transitionBlocked;
        }

        private bool IsScreenAvailable()
        {
            return _screenView != null &&
                   _screenView.isActiveAndEnabled &&
                   _screenView.gameObject.activeInHierarchy;
        }

        private void CancelAcceptedTail()
        {
            _acceptedTailRemaining = 0f;
            _acceptedTailKind = null;
        }
    }
}
