using System;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class UiNavigationInputRouter : MonoBehaviour
    {
        private const string UiMapName = "UI";
        private const string NavigateActionName = "Navigate";
        private const string SubmitActionName = "Submit";
        private const string CancelActionName = "Cancel";
        private const float NavigateDeadzone = 0.5f;

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private PopupLayerView _popupLayerView;
        [SerializeField] private MainMenuScreenView _mainMenuScreenView;

        private Func<bool> _backRequestedFallback;
        private Func<bool> _isNavigationBlocked;
        private InputAction _cancelAction;
        private IUiNavigationTarget _currentTarget;
        private bool _currentTargetFocusRevealed;
        private bool _initialized;
        private InputAction _navigateAction;
        private PopupController _popupController;
        private IUiNavigationTargetResolver _targetResolver;
        private IUiAudioPort _uiAudioPort;
        private InputAction _submitAction;

        public void Initialize(
            InputActionAsset inputActions,
            PopupController popupController,
            PopupLayerView popupLayerView,
            MainMenuScreenView mainMenuScreenView,
            Func<bool> backRequestedFallback,
            Func<bool> isNavigationBlocked,
            IUiAudioPort uiAudioPort = null)
        {
            UnbindActions();
            _inputActions = inputActions;
            _popupController = popupController;
            _popupLayerView = popupLayerView;
            _mainMenuScreenView = mainMenuScreenView;
            _targetResolver = new LegacyNavigationTargetResolver(popupController, popupLayerView, mainMenuScreenView);
            _backRequestedFallback = backRequestedFallback;
            _isNavigationBlocked = isNavigationBlocked;
            _uiAudioPort = uiAudioPort;
            _initialized = true;
            BindActions();
            SetCurrentTarget(ResolveTarget().Target);
        }

        public void Initialize(
            InputActionAsset inputActions,
            IUiNavigationTargetResolver targetResolver,
            Func<bool> backRequestedFallback,
            Func<bool> isNavigationBlocked,
            IUiAudioPort uiAudioPort = null)
        {
            UnbindActions();
            _inputActions = inputActions;
            _popupController = null;
            _popupLayerView = null;
            _mainMenuScreenView = null;
            _targetResolver = targetResolver;
            _backRequestedFallback = backRequestedFallback;
            _isNavigationBlocked = isNavigationBlocked;
            _uiAudioPort = uiAudioPort;
            _initialized = true;
            BindActions();
            SetCurrentTarget(ResolveTarget().Target);
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            BindActions();
            SetCurrentTarget(ResolveTarget().Target);
        }

        private void OnDisable()
        {
            UnbindActions();
            SetCurrentTarget(null);
        }

        private void OnDestroy()
        {
            UnbindActions();
            SetCurrentTarget(null);
        }

        public bool DispatchNavigate(UiNavigationCommand command)
        {
            if (IsBlocked())
            {
                return false;
            }

            var resolution = ResolveTarget();
            var target = resolution.Target;
            SetCurrentTarget(target);
            if (target == null || !target.CanHandleUiNavigation)
            {
                return false;
            }

            RevealCurrentTargetFocus();
            var handled = target.HandleNavigate(command);
            if (handled)
            {
                _uiAudioPort?.Play(UiAudioCueId.KeyboardMove);
            }

            return handled;
        }

        public bool DispatchSubmit()
        {
            if (IsBlocked())
            {
                return false;
            }

            var resolution = ResolveTarget();
            var target = resolution.Target;
            SetCurrentTarget(target);
            if (target == null || !target.CanHandleUiNavigation)
            {
                return false;
            }

            if (!_currentTargetFocusRevealed)
            {
                RevealCurrentTargetFocus();
                return true;
            }

            return target.HandleSubmit();
        }

        public bool DispatchCancel()
        {
            if (IsBlocked())
            {
                return false;
            }

            var resolution = ResolveTarget();
            var target = resolution.Target;
            SetCurrentTarget(target);
            if (target != null && target.CanHandleUiNavigation && target.HandleCancel())
            {
                return true;
            }

            return _backRequestedFallback != null && _backRequestedFallback();
        }

        public static bool TryConvertNavigateVector(Vector2 value, out UiNavigationCommand command)
        {
            command = UiNavigationCommand.Down;
            if (value.sqrMagnitude < NavigateDeadzone * NavigateDeadzone)
            {
                return false;
            }

            var absX = Mathf.Abs(value.x);
            var absY = Mathf.Abs(value.y);
            if (absX > absY)
            {
                command = value.x > 0f ? UiNavigationCommand.Right : UiNavigationCommand.Left;
                return true;
            }

            command = value.y > 0f ? UiNavigationCommand.Up : UiNavigationCommand.Down;
            return true;
        }

        private void BindActions()
        {
            if (_inputActions == null || _navigateAction != null)
            {
                return;
            }

            var uiMap = _inputActions.FindActionMap(UiMapName, throwIfNotFound: false);
            if (uiMap == null)
            {
                return;
            }

            _navigateAction = uiMap.FindAction(NavigateActionName, throwIfNotFound: false);
            _submitAction = uiMap.FindAction(SubmitActionName, throwIfNotFound: false);
            _cancelAction = uiMap.FindAction(CancelActionName, throwIfNotFound: false);

            if (_navigateAction != null)
            {
                _navigateAction.performed += HandleNavigatePerformed;
                _navigateAction.Enable();
            }

            if (_submitAction != null)
            {
                _submitAction.performed += HandleSubmitPerformed;
                _submitAction.Enable();
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed += HandleCancelPerformed;
                _cancelAction.Enable();
            }
        }

        private void UnbindActions()
        {
            if (_navigateAction != null)
            {
                _navigateAction.performed -= HandleNavigatePerformed;
            }

            if (_submitAction != null)
            {
                _submitAction.performed -= HandleSubmitPerformed;
            }

            if (_cancelAction != null)
            {
                _cancelAction.performed -= HandleCancelPerformed;
            }

            _navigateAction = null;
            _submitAction = null;
            _cancelAction = null;
        }

        private void HandleNavigatePerformed(InputAction.CallbackContext context)
        {
            if (TryConvertNavigateVector(context.ReadValue<Vector2>(), out var command))
            {
                DispatchNavigate(command);
            }
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            DispatchSubmit();
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            DispatchCancel();
        }

        private UiNavigationTargetResolution ResolveTarget()
        {
            return _targetResolver != null
                ? _targetResolver.Resolve()
                : UiNavigationTargetResolution.Open(null);
        }

        private sealed class LegacyNavigationTargetResolver : IUiNavigationTargetResolver
        {
            private readonly MainMenuScreenView _mainMenuScreenView;
            private readonly PopupController _popupController;
            private readonly PopupLayerView _popupLayerView;

            public LegacyNavigationTargetResolver(
                PopupController popupController,
                PopupLayerView popupLayerView,
                MainMenuScreenView mainMenuScreenView)
            {
                _popupController = popupController;
                _popupLayerView = popupLayerView;
                _mainMenuScreenView = mainMenuScreenView;
            }

            public UiNavigationTargetResolution Resolve()
            {
                if (_popupController != null && _popupController.TopPopup.HasValue)
                {
                    var popup = _popupController.TopPopup.Value;
                    if (_popupController is IUiNavigationTargetProvider popupProvider &&
                        popupProvider.TryGetNavigationTarget(out var runtimeTarget))
                    {
                        return new UiNavigationTargetResolution(runtimeTarget, popup.Policy.BlocksLowerLayers);
                    }

                    if (_popupLayerView != null && _popupLayerView.TryFindNavigationTarget(out var layerTarget))
                    {
                        return new UiNavigationTargetResolution(layerTarget, popup.Policy.BlocksLowerLayers);
                    }

                    if (popup.Policy.BlocksLowerLayers)
                    {
                        return UiNavigationTargetResolution.Blocking(null);
                    }
                }

                return UiNavigationTargetResolution.Open(_mainMenuScreenView);
            }
        }

        private void SetCurrentTarget(IUiNavigationTarget target)
        {
            if (ReferenceEquals(_currentTarget, target))
            {
                return;
            }

            _currentTarget?.OnNavigationFocusLost();
            _currentTarget = target;
            _currentTargetFocusRevealed = false;
        }

        private void RevealCurrentTargetFocus()
        {
            if (_currentTarget == null || _currentTargetFocusRevealed)
            {
                return;
            }

            _currentTarget.OnNavigationFocusGained();
            _currentTargetFocusRevealed = true;
        }

        private bool IsBlocked()
        {
            return _isNavigationBlocked != null && _isNavigationBlocked();
        }
    }
}
