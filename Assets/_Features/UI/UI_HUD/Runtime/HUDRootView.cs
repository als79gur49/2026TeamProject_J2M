using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class HUDRootView : MonoBehaviour
    {
        private const float HudNormalAlpha = 1.0f;
        private const float HudDimmedAlpha = 0.82f;
        private const float HudDimTweenDurationSeconds = 0.12f;
        private const Ease HudDimTweenEase = Ease.OutQuad;
        private const bool HudDimUseUnscaledTime = true;

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _shellCanvasGroup;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private TMP_Text _stageNameLabel;
        [SerializeField] private ObjectiveHudView _objectiveHudView;
        [SerializeField] private ChancePanelView _chancePanelView;
        [SerializeField] private TopologyBeltView _topologyBeltView;
        [SerializeField] private PlayerStatusView _playerStatusView;
        [SerializeField] private NotificationView _notificationView;

        private HUDRootViewModel _viewModel;
        private StageInfoViewModel _stageInfoViewModel;
        private bool _isVisible = true;
        private Tween _shellTween;
        private bool _lastRootVisibleState;
        private bool _hasShellAlphaTarget;
        private float _lastShellAlphaTarget = HudNormalAlpha;

        public event Action PauseRequested;

        public PlayerStatusView PlayerStatusView => _playerStatusView;

        public ObjectiveHudView ObjectiveHudView => _objectiveHudView;

        public ChancePanelView ChancePanelView
        {
            get
            {
                EnsureHudModules();
                return _chancePanelView;
            }
        }

        public TopologyBeltView TopologyBeltView
        {
            get
            {
                EnsureHudModules();
                return _topologyBeltView;
            }
        }

        public NotificationView NotificationView => _notificationView;

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

        private void OnEnable()
        {
            EnsureHudModules();
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
            ValidateSerializedReference(_playerStatusView, nameof(_playerStatusView));
            ValidateSerializedReference(_notificationView, nameof(_notificationView));
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
            EnsureHudModules();
            var isRootVisible = IsVisible && (_viewModel == null || _viewModel.IsVisible);
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

        private void EnsureHudModules()
        {
            if (_chancePanelView == null)
            {
                _chancePanelView = GetComponentInChildren<ChancePanelView>(true);
            }

            if (_topologyBeltView == null)
            {
                _topologyBeltView = GetComponentInChildren<TopologyBeltView>(true);
            }

            if (!Application.isPlaying && !gameObject.scene.IsValid())
            {
                return;
            }

            _chancePanelView = ResolveSingleChancePanelView(_chancePanelView);

            if (_chancePanelView == null)
            {
                _chancePanelView = CreateRuntimeHudModule<ChancePanelView>("ChancePanel");
            }

            if (_topologyBeltView == null)
            {
                _topologyBeltView = CreateRuntimeHudModule<TopologyBeltView>("TopologyBelt");
            }
        }

        private T CreateRuntimeHudModule<T>(string moduleName)
            where T : Component
        {
            var module = new GameObject(moduleName, typeof(RectTransform), typeof(T));
            var parent = _root != null ? _root.transform : transform;
            module.transform.SetParent(parent, false);
            var rect = (RectTransform)module.transform;
            if (typeof(T) == typeof(ChancePanelView))
            {
                rect.anchorMin = new Vector2(0.0f, 1.0f);
                rect.anchorMax = new Vector2(0.0f, 1.0f);
                rect.pivot = new Vector2(0.0f, 1.0f);
                rect.anchoredPosition = new Vector2(32.0f, -32.0f);
                rect.sizeDelta = new Vector2(220.0f, 72.0f);
            }
            else if (typeof(T) == typeof(TopologyBeltView))
            {
                rect.anchorMin = new Vector2(1.0f, 1.0f);
                rect.anchorMax = new Vector2(1.0f, 1.0f);
                rect.pivot = new Vector2(1.0f, 1.0f);
                rect.anchoredPosition = new Vector2(-32.0f, -32.0f);
                rect.sizeDelta = new Vector2(360.0f, 72.0f);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return module.GetComponent<T>();
        }

        private ChancePanelView ResolveSingleChancePanelView(ChancePanelView preferred)
        {
            var modules = GetComponentsInChildren<ChancePanelView>(true);
            if (modules == null || modules.Length == 0)
            {
                return null;
            }

            var selected = preferred;
            var hasSelected = false;
            if (selected != null)
            {
                for (var i = 0; i < modules.Length; i++)
                {
                    if (ReferenceEquals(modules[i], selected))
                    {
                        hasSelected = true;
                        break;
                    }
                }
            }

            if (!hasSelected)
            {
                selected = modules[0];
            }

            for (var i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                if (module == null || ReferenceEquals(module, selected))
                {
                    continue;
                }

                DestroyDuplicateChancePanelView(module);
            }

            return selected;
        }

        private static void DestroyDuplicateChancePanelView(ChancePanelView module)
        {
            module.Bind(null);

            var target = module.gameObject == null || module.gameObject.GetComponent<HUDRootView>() != null
                ? (UnityEngine.Object)module
                : module.gameObject;

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
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
