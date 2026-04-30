using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class ObjectiveStatusScreenView : MonoBehaviour, IScreenView
    {
        private const float PreferredPanelWidth = 520f;
        private const float PreferredPanelHeight = 360f;
        private const float MinimumPanelWidth = 420f;
        private const float MinimumPanelHeight = 300f;
        private const float MaximumPanelWidth = 620f;
        private const float MaximumPanelHeight = 440f;
        private const float ScreenMargin = 64f;

        private static readonly Vector2 RootAnchor = new Vector2(0.5f, 1f);
        private static readonly Vector2 RootPivot = new Vector2(0.5f, 1f);
        private static readonly Vector2 RootOffset = new Vector2(0f, -20f);

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _badgeLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private TMP_Text _secondaryLabel;
        [SerializeField] private Button _infoButton;
        [SerializeField] private Button _backButton;

        private ObjectiveStatusScreenViewModel _viewModel;
        private RectTransform _headerRoot;
        private RectTransform _summaryRoot;
        private RectTransform _detailRoot;
        private RectTransform _secondaryRoot;
        private RectTransform _footerRoot;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action InfoRequested;

        public event Action BackRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void Bind(ObjectiveStatusScreenViewModel viewModel)
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

            EnsureResponsiveLayout();
            RefreshView();
        }

        public void ClickInfo()
        {
            if (!IsVisible)
            {
                return;
            }

            InfoRequested?.Invoke();
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        private void OnEnable()
        {
            EnsureResponsiveLayout();
            RebindButton(_infoButton, ClickInfo);
            RebindButton(_backButton, ClickBack);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_infoButton, ClickInfo);
            UnbindButton(_backButton, ClickBack);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_badgeLabel, nameof(_badgeLabel));
            ValidateSerializedReference(_summaryLabel, nameof(_summaryLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_secondaryLabel, nameof(_secondaryLabel));
            ValidateSerializedReference(_infoButton, nameof(_infoButton));
            ValidateSerializedReference(_backButton, nameof(_backButton));
        }
#endif

        private void OnDestroy()
        {
            StopRootEnterMotion();
            if (_viewModel != null)
            {
                _viewModel.Changed -= HandleViewModelChanged;
            }
        }

        private void HandleViewModelChanged()
        {
            RefreshView();
        }

        private void RefreshView()
        {
            EnsureResponsiveLayout();
            ApplyRootVisibility();

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_badgeLabel != null)
            {
                _badgeLabel.text = _viewModel.BadgeText;
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_secondaryLabel != null)
            {
                _secondaryLabel.text = _viewModel.SecondaryText;
            }
        }

        private void EnsureResponsiveLayout()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            var rootRect = _root.transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            var panelSize = CalculatePanelSize(rootRect.parent as RectTransform);
            SettingsLayoutUtility.ConfigureOverlay(rootRect, RootAnchor, RootPivot, RootOffset, panelSize);
            SettingsLayoutUtility.EnsureLayoutElement(_root, preferredWidth: panelSize.x, preferredHeight: panelSize.y);
            SettingsLayoutUtility.EnsureVerticalLayout(
                _root,
                new RectOffset(18, 18, 16, 16),
                10f,
                TextAnchor.UpperCenter);

            EnsureLayoutContainers(rootRect);
            LayoutHeader();
            LayoutTextBlock(_summaryRoot, _summaryLabel, preferredHeight: 58f);
            LayoutTextBlock(_detailRoot, _detailLabel, minHeight: 110f, preferredHeight: 142f, flexibleHeight: 1f);
            LayoutTextBlock(_secondaryRoot, _secondaryLabel, preferredHeight: 40f);
            LayoutFooter();
        }

        private static Vector2 CalculatePanelSize(RectTransform parentRect)
        {
            var parentSize = parentRect != null && parentRect.rect.size.sqrMagnitude > 0f
                ? parentRect.rect.size
                : new Vector2(PreferredPanelWidth + ScreenMargin * 2f, PreferredPanelHeight + ScreenMargin * 2f);

            return new Vector2(
                ClampPanelDimension(PreferredPanelWidth, MinimumPanelWidth, MaximumPanelWidth, parentSize.x - ScreenMargin * 2f),
                ClampPanelDimension(PreferredPanelHeight, MinimumPanelHeight, MaximumPanelHeight, parentSize.y - ScreenMargin * 2f));
        }

        private static float ClampPanelDimension(float preferred, float minimum, float maximum, float available)
        {
            if (available <= 0f)
            {
                return preferred;
            }

            if (available < minimum)
            {
                return available;
            }

            return Mathf.Min(Mathf.Clamp(preferred, minimum, maximum), available);
        }

        private void EnsureLayoutContainers(RectTransform rootRect)
        {
            _headerRoot = SettingsLayoutUtility.EnsureChildRect(rootRect, "ObjectiveHeader");
            _summaryRoot = SettingsLayoutUtility.EnsureChildRect(rootRect, "ObjectiveSummary");
            _detailRoot = SettingsLayoutUtility.EnsureChildRect(rootRect, "ObjectiveDetail");
            _secondaryRoot = SettingsLayoutUtility.EnsureChildRect(rootRect, "ObjectiveSecondary");
            _footerRoot = SettingsLayoutUtility.EnsureChildRect(rootRect, "ObjectiveFooter");

            SettingsLayoutUtility.EnsureLayoutElement(_headerRoot, preferredHeight: 32f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(_summaryRoot, preferredHeight: 58f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(_detailRoot, minHeight: 110f, preferredHeight: 142f, flexibleWidth: 1f, flexibleHeight: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(_secondaryRoot, preferredHeight: 40f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(_footerRoot, preferredHeight: 34f, flexibleWidth: 1f);

            SettingsLayoutUtility.FillLayoutChild(_headerRoot);
            SettingsLayoutUtility.FillLayoutChild(_summaryRoot);
            SettingsLayoutUtility.FillLayoutChild(_detailRoot);
            SettingsLayoutUtility.FillLayoutChild(_secondaryRoot);
            SettingsLayoutUtility.FillLayoutChild(_footerRoot);
            _headerRoot.SetSiblingIndex(0);
            _summaryRoot.SetSiblingIndex(1);
            _detailRoot.SetSiblingIndex(2);
            _secondaryRoot.SetSiblingIndex(3);
            _footerRoot.SetSiblingIndex(4);
        }

        private void LayoutHeader()
        {
            SettingsLayoutUtility.EnsureHorizontalLayout(
                _headerRoot.gameObject,
                new RectOffset(0, 0, 0, 0),
                10f,
                TextAnchor.MiddleLeft);
            SettingsLayoutUtility.MoveToParent(_titleLabel != null ? _titleLabel.rectTransform : null, _headerRoot);
            SettingsLayoutUtility.MoveToParent(_badgeLabel != null ? _badgeLabel.rectTransform : null, _headerRoot);
            SettingsLayoutUtility.EnsureLayoutElement(_titleLabel, preferredHeight: 30f, flexibleWidth: 1f);
            SettingsLayoutUtility.EnsureLayoutElement(_badgeLabel, preferredWidth: 136f, preferredHeight: 26f);
        }

        private static void LayoutTextBlock(
            RectTransform container,
            TMP_Text label,
            float minHeight = -1f,
            float preferredHeight = -1f,
            float flexibleHeight = -1f)
        {
            if (container == null)
            {
                return;
            }

            if (label != null)
            {
                SettingsLayoutUtility.MoveToParent(label.rectTransform, container);
                SettingsLayoutUtility.Stretch(label.rectTransform);
                label.textWrappingMode = TextWrappingModes.Normal;
                SettingsLayoutUtility.EnsureLayoutElement(label, flexibleWidth: 1f, flexibleHeight: flexibleHeight);
            }

            SettingsLayoutUtility.EnsureLayoutElement(
                container,
                minHeight: minHeight,
                preferredHeight: preferredHeight,
                flexibleWidth: 1f,
                flexibleHeight: flexibleHeight);
        }

        private void LayoutFooter()
        {
            SettingsLayoutUtility.EnsureHorizontalLayout(
                _footerRoot.gameObject,
                new RectOffset(0, 0, 0, 0),
                8f,
                TextAnchor.MiddleRight);
            SettingsLayoutUtility.MoveToParent(_infoButton, _footerRoot);
            SettingsLayoutUtility.MoveToParent(_backButton, _footerRoot);
            SettingsLayoutUtility.EnsureLayoutElement(_infoButton, preferredWidth: 92f, preferredHeight: 30f);
            SettingsLayoutUtility.EnsureLayoutElement(_backButton, preferredWidth: 92f, preferredHeight: 30f);
        }

        private void ApplyRootVisibility()
        {
            var becameVisible = !_lastVisibleState && IsVisible;
            var becameHidden = _lastVisibleState && !IsVisible;

            if (becameVisible)
            {
                _lastVisibleState = true;
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                PlayRootEnterMotion();
                return;
            }

            if (becameHidden || !IsVisible)
            {
                StopRootEnterMotion();
            }

            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            _lastVisibleState = IsVisible;
        }

        private void PlayRootEnterMotion()
        {
            _rootCanvasGroup = ScreenEnterTweenUtility.EnsureCanvasGroup(_root, _rootCanvasGroup);
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            _enterTween = ScreenEnterTweenUtility.PlayEnterFade(_rootCanvasGroup, out _rootRestAlpha);
            _hasRootRestAlpha = _rootCanvasGroup != null;
        }

        private void StopRootEnterMotion()
        {
            ScreenEnterTweenUtility.Kill(ref _enterTween);
            if (_hasRootRestAlpha)
            {
                ScreenEnterTweenUtility.RestoreAlpha(_rootCanvasGroup, _rootRestAlpha);
            }
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ObjectiveStatusScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
