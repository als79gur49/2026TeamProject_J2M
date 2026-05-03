using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class StageResultScreenView : MonoBehaviour, IScreenView
    {
        private static readonly Vector2 PreferredPanelSize = new Vector2(460f, 220f);
        private static readonly Vector2 MinimumPanelSize = new Vector2(360f, 190f);
        private static readonly Vector2 MaximumPanelSize = new Vector2(560f, 280f);

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _detailLabel;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _continueButtonLabel;

        private StageResultScreenViewModel _viewModel;
        private bool _isVisible;
        private Tween _enterTween;
        private CanvasGroup _rootCanvasGroup;
        private bool _lastVisibleState;
        private bool _hasRootRestAlpha;
        private float _rootRestAlpha = 1f;

        public event Action ContinueRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public void Bind(StageResultScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickContinue()
        {
            if (!IsVisible)
            {
                return;
            }

            ContinueRequested?.Invoke();
        }

        private void OnEnable()
        {
            EnsureResponsiveLayout();
            RebindButton(_continueButton, ClickContinue);
            RefreshView();
        }

        private void OnDisable()
        {
            StopRootEnterMotion();
            UnbindButton(_continueButton, ClickContinue);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_summaryLabel, nameof(_summaryLabel));
            ValidateSerializedReference(_detailLabel, nameof(_detailLabel));
            ValidateSerializedReference(_continueButton, nameof(_continueButton));
            ValidateSerializedReference(_continueButtonLabel, nameof(_continueButtonLabel));
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

            if (_summaryLabel != null)
            {
                _summaryLabel.text = _viewModel.SummaryText;
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = _viewModel.DetailText;
            }

            if (_continueButtonLabel != null)
            {
                _continueButtonLabel.text = _viewModel.ContinueLabel;
            }
        }

        private void EnsureResponsiveLayout()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            var rootRect = TerminalResultScreenLayoutUtility.ConfigureRoot(
                _root,
                PreferredPanelSize,
                MinimumPanelSize,
                MaximumPanelSize);
            if (rootRect == null)
            {
                return;
            }

            var header = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultHeader", 0, preferredHeight: 30f);
            var summary = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultSummary", 1, preferredHeight: 30f);
            var detail = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultDetail", 2, minHeight: 44f, preferredHeight: 56f, flexibleHeight: 1f);
            var footer = TerminalResultScreenLayoutUtility.EnsureContainer(rootRect, "ResultFooter", 3, preferredHeight: 32f);

            TerminalResultScreenLayoutUtility.LayoutText(_titleLabel, header, preferredHeight: 30f);
            TerminalResultScreenLayoutUtility.LayoutText(_summaryLabel, summary, preferredHeight: 30f);
            TerminalResultScreenLayoutUtility.LayoutText(_detailLabel, detail, flexibleHeight: 1f);
            TerminalResultScreenLayoutUtility.LayoutFooter(footer);
            TerminalResultScreenLayoutUtility.LayoutButton(_continueButton, footer, preferredWidth: 112f, preferredHeight: 30f);
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
                Debug.LogWarning($"{nameof(StageResultScreenView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }

    internal static class TerminalResultScreenLayoutUtility
    {
        private const float ScreenMargin = 64f;

        private static readonly Vector2 CenterAnchor = new Vector2(0.5f, 0.5f);

        public static RectTransform ConfigureRoot(
            GameObject root,
            Vector2 preferredSize,
            Vector2 minimumSize,
            Vector2 maximumSize)
        {
            if (root == null)
            {
                return null;
            }

            var rootRect = root.GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = root.AddComponent<RectTransform>();
            }

            var panelSize = CalculatePanelSize(rootRect.parent as RectTransform, preferredSize, minimumSize, maximumSize);
            SettingsLayoutUtility.ConfigureOverlay(rootRect, CenterAnchor, CenterAnchor, Vector2.zero, panelSize);
            SettingsLayoutUtility.EnsureLayoutElement(root, preferredWidth: panelSize.x, preferredHeight: panelSize.y);
            SettingsLayoutUtility.EnsureVerticalLayout(
                root,
                new RectOffset(24, 24, 20, 20),
                10f,
                TextAnchor.UpperCenter);
            EnsurePanelBackground(root);
            return rootRect;
        }

        public static RectTransform EnsureContainer(
            RectTransform root,
            string name,
            int siblingIndex,
            float minHeight = -1f,
            float preferredHeight = -1f,
            float flexibleHeight = -1f)
        {
            var container = SettingsLayoutUtility.EnsureChildRect(root, name);
            SettingsLayoutUtility.FillLayoutChild(container);
            SettingsLayoutUtility.EnsureLayoutElement(
                container,
                minHeight: minHeight,
                preferredHeight: preferredHeight,
                flexibleWidth: 1f,
                flexibleHeight: flexibleHeight);
            container.SetSiblingIndex(siblingIndex);
            return container;
        }

        public static void LayoutText(
            TMP_Text label,
            RectTransform parent,
            float preferredHeight = -1f,
            float flexibleHeight = -1f)
        {
            if (label == null || parent == null)
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(label.rectTransform, parent);
            SettingsLayoutUtility.Stretch(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.Normal;
            SettingsLayoutUtility.EnsureLayoutElement(
                label,
                preferredHeight: preferredHeight,
                flexibleWidth: 1f,
                flexibleHeight: flexibleHeight);
        }

        public static void LayoutFooter(RectTransform footer)
        {
            if (footer == null)
            {
                return;
            }

            SettingsLayoutUtility.EnsureHorizontalLayout(
                footer.gameObject,
                new RectOffset(0, 0, 0, 0),
                10f,
                TextAnchor.MiddleCenter);
        }

        public static void LayoutButton(Button button, RectTransform footer, float preferredWidth, float preferredHeight)
        {
            if (button == null || footer == null)
            {
                return;
            }

            SettingsLayoutUtility.MoveToParent(button, footer);
            SettingsLayoutUtility.EnsureLayoutElement(button, preferredWidth: preferredWidth, preferredHeight: preferredHeight);
        }

        private static Vector2 CalculatePanelSize(
            RectTransform parentRect,
            Vector2 preferredSize,
            Vector2 minimumSize,
            Vector2 maximumSize)
        {
            var parentSize = parentRect != null && parentRect.rect.size.sqrMagnitude > 0f
                ? parentRect.rect.size
                : preferredSize + new Vector2(ScreenMargin * 2f, ScreenMargin * 2f);

            return new Vector2(
                ClampPanelDimension(preferredSize.x, minimumSize.x, maximumSize.x, parentSize.x - ScreenMargin * 2f),
                ClampPanelDimension(preferredSize.y, minimumSize.y, maximumSize.y, parentSize.y - ScreenMargin * 2f));
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

        private static void EnsurePanelBackground(GameObject root)
        {
            var image = root.GetComponent<Image>();
            if (image == null)
            {
                image = root.AddComponent<Image>();
            }

            image.color = new Color(0.11f, 0.12f, 0.16f, 0.92f);
        }
    }
}
