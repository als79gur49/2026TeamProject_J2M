using System.Collections;
using Game.Feature.UI.Screens;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;

namespace Game.Feature.UI.Composition
{
    [DisallowMultipleComponent]
    public sealed class MainMenuCameraPresentationController : MonoBehaviour
    {
        private enum CameraPresentationState
        {
            Idle,
            SaveSlots,
            Settings,
        }

        [SerializeField] private CinemachineCamera _cinemachineCamera;
        [SerializeField] private CinemachineSplineDolly _splineDolly;
        [SerializeField] private Transform _lookAtProxy;
        [SerializeField] private Transform _settingsTarget;
        [SerializeField] private Transform _idleTarget;
        [SerializeField] private Transform _saveSlotsTarget;
        [SerializeField] private int _settingsKnotIndex;
        [SerializeField] private int _idleKnotIndex = 1;
        [SerializeField] private int _saveSlotsKnotIndex = 4;
        [SerializeField] private float _transitionDurationSeconds = 1f;
        [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private MainMenuScreenView _mainMenuScreenView;
        private MainMenuSettingsOverlayController _settingsOverlayController;
        private Coroutine _transitionCoroutine;
        private bool _settingsOpen;

        internal Transform CurrentLookAtTarget => _cinemachineCamera != null ? _cinemachineCamera.LookAt : null;

        internal float CurrentCameraPosition => _splineDolly != null ? _splineDolly.CameraPosition : 0f;

        internal int SettingsKnotIndex => _settingsKnotIndex;

        internal int IdleKnotIndex => _idleKnotIndex;

        internal int SaveSlotsKnotIndex => _saveSlotsKnotIndex;

        internal void Attach(MainMenuScreenView mainMenuScreenView, MainMenuSettingsOverlayController settingsOverlayController)
        {
            Detach();
            _mainMenuScreenView = mainMenuScreenView;
            _settingsOverlayController = settingsOverlayController;

            if (_mainMenuScreenView != null)
            {
                _mainMenuScreenView.SectionChanged += HandleMainMenuSectionChanged;
            }

            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened += HandleSettingsOpened;
                _settingsOverlayController.Closed += HandleSettingsClosed;
                _settingsOpen = _settingsOverlayController.IsOpen;
            }
            else
            {
                _settingsOpen = false;
            }

            ApplyCurrentState(immediate: true);
        }

        internal void Detach()
        {
            StopActiveTransition();

            if (_mainMenuScreenView != null)
            {
                _mainMenuScreenView.SectionChanged -= HandleMainMenuSectionChanged;
            }

            if (_settingsOverlayController != null)
            {
                _settingsOverlayController.Opened -= HandleSettingsOpened;
                _settingsOverlayController.Closed -= HandleSettingsClosed;
            }

            _mainMenuScreenView = null;
            _settingsOverlayController = null;
            _settingsOpen = false;
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void HandleMainMenuSectionChanged(MainMenuSectionId sectionId)
        {
            ApplyCurrentState(immediate: false);
        }

        private void HandleSettingsOpened()
        {
            _settingsOpen = true;
            ApplyCurrentState(immediate: false);
        }

        private void HandleSettingsClosed()
        {
            _settingsOpen = false;
            ApplyCurrentState(immediate: false);
        }

        private void ApplyCurrentState(bool immediate)
        {
            if (_cinemachineCamera == null || _splineDolly == null)
            {
                return;
            }

            var state = ResolveState();
            var target = ResolveTarget(state);
            var knotIndex = ResolveKnotIndex(state);

            _cinemachineCamera.LookAt = _lookAtProxy != null ? _lookAtProxy : target;
            _splineDolly.PositionUnits = PathIndexUnit.Knot;

            StopActiveTransition();
            if (immediate || _transitionDurationSeconds <= 0f || !isActiveAndEnabled)
            {
                _splineDolly.CameraPosition = knotIndex;
                SnapLookAtProxy(target);
                return;
            }

            _transitionCoroutine = StartCoroutine(AnimateCameraPresentation(knotIndex, target));
        }

        private CameraPresentationState ResolveState()
        {
            if (_settingsOpen)
            {
                return CameraPresentationState.Settings;
            }

            return _mainMenuScreenView != null && _mainMenuScreenView.ActiveSection == MainMenuSectionId.SaveSlots
                ? CameraPresentationState.SaveSlots
                : CameraPresentationState.Idle;
        }

        private Transform ResolveTarget(CameraPresentationState state)
        {
            switch (state)
            {
                case CameraPresentationState.Settings:
                    return _settingsTarget;

                case CameraPresentationState.SaveSlots:
                    return _saveSlotsTarget;

                case CameraPresentationState.Idle:
                default:
                    return _idleTarget;
            }
        }

        private int ResolveKnotIndex(CameraPresentationState state)
        {
            switch (state)
            {
                case CameraPresentationState.Settings:
                    return _settingsKnotIndex;

                case CameraPresentationState.SaveSlots:
                    return _saveSlotsKnotIndex;

                case CameraPresentationState.Idle:
                default:
                    return _idleKnotIndex;
            }
        }

        private IEnumerator AnimateCameraPresentation(float targetPosition, Transform lookAtTarget)
        {
            var startPosition = _splineDolly.CameraPosition;
            var hasLookAtProxyTarget = _lookAtProxy != null && lookAtTarget != null;
            var startLookAtPosition = hasLookAtProxyTarget ? _lookAtProxy.position : Vector3.zero;
            var targetLookAtPosition = hasLookAtProxyTarget ? lookAtTarget.position : Vector3.zero;
            var elapsed = 0f;

            while (elapsed < _transitionDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / _transitionDurationSeconds);
                var easedProgress = _transitionCurve != null ? _transitionCurve.Evaluate(progress) : progress;
                _splineDolly.CameraPosition = Mathf.Lerp(startPosition, targetPosition, easedProgress);
                if (hasLookAtProxyTarget)
                {
                    _lookAtProxy.position = Vector3.Lerp(startLookAtPosition, targetLookAtPosition, easedProgress);
                }

                yield return null;
            }

            _splineDolly.CameraPosition = targetPosition;
            SnapLookAtProxy(lookAtTarget);
            _transitionCoroutine = null;
        }

        private void SnapLookAtProxy(Transform target)
        {
            if (_lookAtProxy == null || target == null)
            {
                return;
            }

            _lookAtProxy.position = target.position;
        }

        private void StopActiveTransition()
        {
            if (_transitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }
    }
}
