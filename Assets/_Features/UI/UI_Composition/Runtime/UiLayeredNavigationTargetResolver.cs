using System;
using Game.Feature.UI.Flow;
using Game.Feature.UI.ViewShared;

namespace Game.Feature.UI.Composition
{
    public sealed class UiLayeredNavigationTargetResolver : IUiNavigationTargetResolver
    {
        private readonly PopupController _popupController;
        private readonly ScreenController _screenController;
        private readonly IUiNavigationTargetProvider _screenProvider;
        private readonly IUiNavigationTargetProvider _modalOverlayProvider;
        private readonly IUiNavigationTargetProvider _hudProvider;
        private readonly Func<bool> _isHudFocusEnabled;

        public UiLayeredNavigationTargetResolver(
            PopupController popupController,
            ScreenController screenController,
            IUiNavigationTargetProvider screenProvider = null,
            IUiNavigationTargetProvider modalOverlayProvider = null,
            IUiNavigationTargetProvider hudProvider = null,
            Func<bool> isHudFocusEnabled = null)
        {
            _popupController = popupController;
            _screenController = screenController;
            _screenProvider = screenProvider;
            _modalOverlayProvider = modalOverlayProvider;
            _hudProvider = hudProvider;
            _isHudFocusEnabled = isHudFocusEnabled;
        }

        public UiNavigationTargetResolution Resolve()
        {
            if (_popupController != null && _popupController.TopPopup.HasValue)
            {
                var popup = _popupController.TopPopup.Value;
                if (_popupController is IUiNavigationTargetProvider popupProvider &&
                    popupProvider.TryGetNavigationTarget(out var popupTarget))
                {
                    return new UiNavigationTargetResolution(popupTarget, popup.Policy.BlocksLowerLayers);
                }

                if (popup.Policy.BlocksLowerLayers)
                {
                    return UiNavigationTargetResolution.Blocking(null);
                }

                // Non-blocking popups fall through only because their policy explicitly leaves lower layers open.
            }

            if (_modalOverlayProvider != null &&
                _modalOverlayProvider.TryGetNavigationTarget(out var overlayTarget) &&
                overlayTarget != null)
            {
                return UiNavigationTargetResolution.Blocking(overlayTarget);
            }

            if (_screenController is IUiNavigationTargetProvider controllerScreenProvider &&
                controllerScreenProvider.TryGetNavigationTarget(out var screenTarget))
            {
                return UiNavigationTargetResolution.Open(screenTarget);
            }

            if (_screenProvider != null &&
                _screenProvider.TryGetNavigationTarget(out var providerScreenTarget) &&
                providerScreenTarget != null)
            {
                return UiNavigationTargetResolution.Open(providerScreenTarget);
            }

            if (_isHudFocusEnabled != null &&
                _isHudFocusEnabled() &&
                _hudProvider != null &&
                _hudProvider.TryGetNavigationTarget(out var hudTarget) &&
                hudTarget != null)
            {
                return UiNavigationTargetResolution.Open(hudTarget);
            }

            return UiNavigationTargetResolution.Open(null);
        }
    }
}
