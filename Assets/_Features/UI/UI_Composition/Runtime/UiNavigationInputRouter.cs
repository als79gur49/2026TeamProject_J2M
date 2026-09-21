using System;
using Game.Feature.UI.Application;
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

        private Func<bool> _backRequestedFallback;
        private Func<bool> _isNavigationBlocked;
        private InputAction _cancelAction;
        private IUiNavigationTarget _currentTarget;
        private bool _currentTargetFocusRevealed;
        private bool _initialized;
        private InputAction _navigateAction;
        private IUiNavigationTargetResolver _targetResolver;
        private IUiAudioPort _uiAudioPort;
        private InputAction _submitAction;

        internal int SubmitPerformedCount { get; private set; }

        internal bool LastSubmitDispatchResult { get; private set; }

        public void Initialize(
            InputActionAsset inputActions,
            IUiNavigationTargetResolver targetResolver,
            Func<bool> backRequestedFallback,
            Func<bool> isNavigationBlocked,
            IUiAudioPort uiAudioPort = null)
        {
            UnbindActions();
            _inputActions = inputActions;
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

            var target = ResolveAndSetCurrentTarget();
            if (target == null || !target.CanHandleUiNavigation)
            {
                return false;
            }

            if (!RevealCurrentTargetFocus())
            {
                return false;
            }

            target = GetLiveCurrentTarget();
            if (target == null)
            {
                return false;
            }

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

            var target = ResolveAndSetCurrentTarget();
            if (target == null || !target.CanHandleUiNavigation)
            {
                return false;
            }

            if (!_currentTargetFocusRevealed)
            {
                return RevealCurrentTargetFocus();
            }

            target = GetLiveCurrentTarget();
            return target != null && target.HandleSubmit();
        }

        public bool DispatchCancel()
        {
            if (IsBlocked())
            {
                return false;
            }

            var target = ResolveAndSetCurrentTarget();
            if (target != null && target.CanHandleUiNavigation && target.HandleCancel())
            {
                return true;
            }

            return _backRequestedFallback != null && _backRequestedFallback();
        }

        internal void ClearNavigationFocus()
        {
            SetCurrentTarget(null);
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
            SubmitPerformedCount++;
            LastSubmitDispatchResult = DispatchSubmit();
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

        private void SetCurrentTarget(IUiNavigationTarget target)
        {
            target = NormalizeTarget(target);
            var previousTarget = _currentTarget;
            if (ReferenceEquals(previousTarget, target))
            {
                return;
            }

            _currentTarget = target;
            _currentTargetFocusRevealed = false;
            if (IsTargetAlive(previousTarget))
            {
                previousTarget.OnNavigationFocusLost();
            }
        }

        private IUiNavigationTarget ResolveAndSetCurrentTarget()
        {
            SetCurrentTarget(ResolveTarget().Target);
            return GetLiveCurrentTarget();
        }

        private IUiNavigationTarget GetLiveCurrentTarget()
        {
            if (IsTargetAlive(_currentTarget))
            {
                return _currentTarget;
            }

            _currentTarget = null;
            _currentTargetFocusRevealed = false;
            return null;
        }

        private bool RevealCurrentTargetFocus()
        {
            var target = GetLiveCurrentTarget();
            if (target == null)
            {
                return false;
            }

            if (_currentTargetFocusRevealed)
            {
                return true;
            }

            target.OnNavigationFocusGained();
            if (!ReferenceEquals(_currentTarget, target) || !IsTargetAlive(target))
            {
                if (ReferenceEquals(_currentTarget, target))
                {
                    _currentTarget = null;
                    _currentTargetFocusRevealed = false;
                }

                return false;
            }

            _currentTargetFocusRevealed = true;
            return true;
        }

        private static IUiNavigationTarget NormalizeTarget(IUiNavigationTarget target)
        {
            return IsTargetAlive(target) ? target : null;
        }

        private static bool IsTargetAlive(IUiNavigationTarget target)
        {
            if (ReferenceEquals(target, null))
            {
                return false;
            }

            return target is not UnityEngine.Object unityTarget || unityTarget != null;
        }

        private bool IsBlocked()
        {
            return _isNavigationBlocked != null && _isNavigationBlocked();
        }
    }
}
