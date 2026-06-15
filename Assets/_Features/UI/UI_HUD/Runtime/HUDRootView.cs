using System;
using DG.Tweening;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootView : MonoBehaviour, IUiNavigationTarget, IUiNavigationTargetProvider
    {
        private const float HudNormalAlpha = 1.0f;
        private const float HudDimmedAlpha = 0.82f;
        private const float HudDimTweenDurationSeconds = 0.12f;
        private const Ease HudDimTweenEase = Ease.OutQuad;
        private const bool HudDimUseUnscaledTime = true;
        private const string HudTopLeftStackName = "HudTopLeftStack";
        private const string HudTopCenterStackName = "HudTopCenterStack";
        private const string HudTopRightStackName = "HudTopRightStack";
        private const string HudBottomRightStackName = "HudBottomRightStack";

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _shellCanvasGroup;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();
        [SerializeField] private TMP_Text _stageNameLabel;
        [SerializeField] private ObjectiveHudView _objectiveHudView;
        [SerializeField] private ChancePanelView _chancePanelView;
        [FormerlySerializedAs("_surfaceIndicatorView")]
        [FormerlySerializedAs("_topologyBeltView")]
        [SerializeField] private SurfaceBeltIndicatorView _surfaceBeltIndicatorView;
        [SerializeField] private PlayerStatusView _playerStatusView;

        private HUDRootViewModel _viewModel;
        private StageInfoViewModel _stageInfoViewModel;
        private bool _isVisible = true;
        private Tween _shellTween;
        private bool _lastRootVisibleState;
        private bool _hasShellAlphaTarget;
        private float _lastShellAlphaTarget = HudNormalAlpha;
        private bool _isKeyboardFocusEnabled;

        public event Action PauseRequested;

        public bool CanHandleUiNavigation =>
            _isKeyboardFocusEnabled &&
            IsVisible &&
            isActiveAndEnabled &&
            _viewModel != null &&
            _viewModel.IsPauseButtonEnabled;

        public PlayerStatusView PlayerStatusView => _playerStatusView;

        public ObjectiveHudView ObjectiveHudView => _objectiveHudView;

        public ChancePanelView ChancePanelView => _chancePanelView;

        public SurfaceBeltIndicatorView SurfaceBeltIndicatorView => _surfaceBeltIndicatorView;

        public HUDRootViewModel ViewModel => _viewModel;

        public StageInfoViewModel StageInfoViewModel => _stageInfoViewModel;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(HUDRootViewModel viewModel)
        {
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            _viewModel = viewModel;
            if (_viewModel != null)
            {
                _viewModel.Changed += HandleViewModelChanged;
            }

            RefreshView();
        }

        public void BindStageInfo(StageInfoViewModel viewModel)
        {
            if (_stageInfoViewModel != null)
            {
                _stageInfoViewModel.Changed -= HandleStageInfoViewModelChanged;
            }

            _stageInfoViewModel = viewModel;
            if (_stageInfoViewModel != null)
            {
                _stageInfoViewModel.Changed += HandleStageInfoViewModelChanged;
            }

            RefreshStageName();
        }

        public void ClickPause()
        {
            if (!IsVisible || _viewModel == null || !_viewModel.IsPauseButtonEnabled)
            {
                return;
            }

            PauseRequested?.Invoke();
        }

        public void SetKeyboardFocusEnabled(bool isEnabled)
        {
            _isKeyboardFocusEnabled = isEnabled;
            if (!isEnabled)
            {
                _navigationGroup?.HideAllFrames();
            }
        }

        public bool TryGetNavigationTarget(out IUiNavigationTarget target)
        {
            target = CanHandleUiNavigation ? this : null;
            return target != null;
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            return false;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation)
            {
                return false;
            }

            _navigationGroup?.PlaySelectedSubmitFeedback();
            ClickPause();
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void OnNavigationFocusGained()
        {
            _navigationGroup?.SetSelectedIndex(0);
        }

        public void OnNavigationFocusLost()
        {
            _navigationGroup?.HideAllFrames();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_shellCanvasGroup, nameof(_shellCanvasGroup));
            RequireReference(_pauseButton, nameof(_pauseButton));
            RequireReference(_stageNameLabel, nameof(_stageNameLabel));
            RequireReference(_objectiveHudView, nameof(_objectiveHudView));
            RequireReference(_chancePanelView, nameof(_chancePanelView));
            RequireReference(_surfaceBeltIndicatorView, nameof(_surfaceBeltIndicatorView));
            RequireReference(_playerStatusView, nameof(_playerStatusView));
            RequireSingleChildView<ChancePanelView>(nameof(ChancePanelView));
            RequireSingleChildView<SurfaceBeltIndicatorView>(nameof(SurfaceBeltIndicatorView));

            var authoredRoot = _root != null ? _root.transform : transform;
            var topLeftStack = RequireStack(authoredRoot, HudTopLeftStackName);
            var topCenterStack = RequireStack(authoredRoot, HudTopCenterStackName);
            var topRightStack = RequireStack(authoredRoot, HudTopRightStackName);
            var bottomRightStack = RequireStack(authoredRoot, HudBottomRightStackName);

            RequireOwnedByStack(_objectiveHudView.transform, topLeftStack, nameof(_objectiveHudView));
            RequireOwnedByStack(_chancePanelView.transform, topCenterStack, nameof(_chancePanelView));
            RequireOwnedByStack(_stageNameLabel.transform, topRightStack, nameof(_stageNameLabel));
            RequireOwnedByStack(_pauseButton.transform, topRightStack, nameof(_pauseButton));
            RequireOwnedByStack(_surfaceBeltIndicatorView.transform, topRightStack, nameof(_surfaceBeltIndicatorView));

            _objectiveHudView.ValidateAuthoredStructureOrThrow();
            _chancePanelView.ValidateAuthoredStructureOrThrow();
            _surfaceBeltIndicatorView.ValidateAuthoredStructureOrThrow();
            _playerStatusView.ValidateAuthoredStructureOrThrow();
        }

        private void OnEnable()
        {
            ValidateAuthoredStructureOrThrow();
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
                _pauseButton.onClick.AddListener(ClickPause);
            }

            RefreshView();
        }

        private void OnDisable()
        {
            KillShellTween();
            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_shellCanvasGroup, nameof(_shellCanvasGroup));
            ValidateSerializedReference(_pauseButton, nameof(_pauseButton));
            ValidateSerializedReference(_stageNameLabel, nameof(_stageNameLabel));
            ValidateSerializedReference(_objectiveHudView, nameof(_objectiveHudView));
            ValidateSerializedReference(_chancePanelView, nameof(_chancePanelView));
            ValidateSerializedReference(_surfaceBeltIndicatorView, nameof(_surfaceBeltIndicatorView));
            ValidateSerializedReference(_playerStatusView, nameof(_playerStatusView));
        }
#endif

        private void OnDestroy()
        {
            KillShellTween();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }

            if (_stageInfoViewModel != null)
            {
                _stageInfoViewModel.Changed -= HandleStageInfoViewModelChanged;
            }

            if (_pauseButton != null)
            {
                _pauseButton.onClick.RemoveListener(ClickPause);
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void HandleStageInfoViewModelChanged()
        {
            RefreshStageName();
        }

        private void RefreshView()
        {
            var isRootVisible = IsVisible;
            var targetShellAlpha = _viewModel != null && _viewModel.IsDimmed ? HudDimmedAlpha : HudNormalAlpha;
            var becameVisible = !_lastRootVisibleState && isRootVisible;
            var becameHidden = _lastRootVisibleState && !isRootVisible;

            if (becameVisible)
            {
                _lastRootVisibleState = true;
                _hasShellAlphaTarget = true;
                _lastShellAlphaTarget = targetShellAlpha;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                ApplyShellAlphaImmediate(targetShellAlpha);
            }
            else
            {
                if (becameHidden || !isRootVisible)
                {
                    KillShellTween();
                    ApplyShellAlphaImmediate(targetShellAlpha);
                }

                if (_root != null)
                {
                    _root.SetActive(isRootVisible);
                }

                if (isRootVisible)
                {
                    if (!_hasShellAlphaTarget)
                    {
                        ApplyShellAlphaImmediate(targetShellAlpha);
                    }
                    else if (!Mathf.Approximately(_lastShellAlphaTarget, targetShellAlpha))
                    {
                        PlayShellAlphaTween(targetShellAlpha);
                    }

                    _hasShellAlphaTarget = true;
                    _lastShellAlphaTarget = targetShellAlpha;
                }
                else
                {
                    _hasShellAlphaTarget = true;
                    _lastShellAlphaTarget = targetShellAlpha;
                }

                _lastRootVisibleState = isRootVisible;
            }

            if (_pauseButton != null)
            {
                _pauseButton.interactable = _viewModel != null && _viewModel.IsPauseButtonEnabled;
            }

            RefreshStageName();
        }

        private void RefreshStageName()
        {
            if (_stageNameLabel == null)
            {
                return;
            }

            var hasStageName = _stageInfoViewModel != null && _stageInfoViewModel.HasStageName;
            _stageNameLabel.gameObject.SetActive(hasStageName);
            _stageNameLabel.text = hasStageName ? _stageInfoViewModel.StageName : string.Empty;
        }

        private void ApplyShellAlphaImmediate(float alpha)
        {
            if (_shellCanvasGroup == null)
            {
                return;
            }

            _shellCanvasGroup.alpha = alpha;
        }

        private void PlayShellAlphaTween(float targetAlpha)
        {
            if (_shellCanvasGroup == null)
            {
                return;
            }

            KillShellTween();
            _shellTween = _shellCanvasGroup
                .DOFade(targetAlpha, HudDimTweenDurationSeconds)
                .SetEase(HudDimTweenEase)
                .SetUpdate(HudDimUseUnscaledTime);
        }

        private void KillShellTween()
        {
            if (_shellTween == null)
            {
                return;
            }

            _shellTween.Kill();
            _shellTween = null;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(HUDRootView)} is missing authored reference '{fieldName}'.");
            }
        }

        private void RequireSingleChildView<TView>(string viewName) where TView : Component
        {
            var views = GetComponentsInChildren<TView>(true);
            if (views.Length != 1)
            {
                throw new InvalidOperationException($"{nameof(HUDRootView)} requires exactly one authored {viewName}; found {views.Length}.");
            }
        }

        private static RectTransform RequireStack(Transform root, string stackName)
        {
            var stack = root != null ? root.Find(stackName) as RectTransform : null;
            if (stack == null)
            {
                throw new InvalidOperationException($"{nameof(HUDRootView)} is missing authored HUD stack '{stackName}'.");
            }

            if (stack.GetComponent<VerticalLayoutGroup>() == null)
            {
                throw new InvalidOperationException($"Authored HUD stack '{stackName}' is missing VerticalLayoutGroup.");
            }

            return stack;
        }

        private static void RequireOwnedByStack(Transform child, Transform stack, string fieldName)
        {
            if (child == null || stack == null || !child.IsChildOf(stack))
            {
                throw new InvalidOperationException($"Authored HUD reference '{fieldName}' must be under '{stack.name}'.");
            }
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(HUDRootView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
