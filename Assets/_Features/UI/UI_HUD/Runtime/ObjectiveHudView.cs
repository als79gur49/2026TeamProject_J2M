using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ObjectiveHudView : MonoBehaviour
    {
        private const string CompleteColor = "#8EE6A8";
        private const string ActiveColor = "#FFFFFF";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _objectiveLabel;
        [SerializeField] private Button _dropdownButton;
        [SerializeField] private TMP_Text _eyebrowText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _progressPillText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private RectTransform _chevronIcon;
        [SerializeField] private CanvasGroup _bodyCanvasGroup;
        [SerializeField] private RectTransform _bodyRoot;
        [SerializeField] private TMP_Text _summaryText;
        [SerializeField] private RectTransform _conditionListRoot;
        [SerializeField] private ObjectiveConditionRowView _conditionRowTemplate;
        [SerializeField] private TMP_Text _footerStatusText;
        [SerializeField] private GameObject _completeBadge;

        private readonly List<ObjectiveConditionRowView> _rowPool = new List<ObjectiveConditionRowView>();
        private ObjectiveHudViewModel _viewModel;
        private Sequence _bodySequence;
        private Tween _completePulseTween;
        private int _lastAnimationSequenceId;

        public event Action ExpandToggleRequested;

        public ObjectiveHudViewModel ViewModel => _viewModel;

        public void Bind(ObjectiveHudViewModel viewModel)
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

        public void ValidateAuthoredStructureOrThrow()
        {
            RequireReference(_root, nameof(_root));
            RequireReference(_objectiveLabel, nameof(_objectiveLabel));
            RequireReference(_dropdownButton, nameof(_dropdownButton));
            RequireReference(_eyebrowText, nameof(_eyebrowText));
            RequireReference(_titleText, nameof(_titleText));
            RequireReference(_progressPillText, nameof(_progressPillText));
            RequireReference(_progressBar, nameof(_progressBar));
            RequireReference(_chevronIcon, nameof(_chevronIcon));
            RequireReference(_bodyCanvasGroup, nameof(_bodyCanvasGroup));
            RequireReference(_bodyRoot, nameof(_bodyRoot));
            RequireReference(_summaryText, nameof(_summaryText));
            RequireReference(_conditionListRoot, nameof(_conditionListRoot));
            RequireReference(_conditionRowTemplate, nameof(_conditionRowTemplate));
            RequireReference(_footerStatusText, nameof(_footerStatusText));
            RequireReference(_completeBadge, nameof(_completeBadge));

            if (_conditionRowTemplate.transform.parent != _conditionListRoot)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} row template must be a direct child of ConditionListRoot.");
            }

            if (_conditionRowTemplate.gameObject.activeSelf)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} row template must be inactive in the authored prefab.");
            }

            _conditionRowTemplate.ValidateAuthoredStructureOrThrow();
        }

        public void ClickDropdown()
        {
            if (ExpandToggleRequested != null)
            {
                ExpandToggleRequested.Invoke();
                return;
            }

            _viewModel?.ToggleExpanded();
        }

        private void OnEnable()
        {
            RebindButton(_dropdownButton, ClickDropdown);
            RefreshView();
        }

        private void OnDisable()
        {
            KillTweens();
            UnbindButton(_dropdownButton, ClickDropdown);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_objectiveLabel, nameof(_objectiveLabel));
            ValidateSerializedReference(_dropdownButton, nameof(_dropdownButton));
        }
#endif

        private void OnDestroy()
        {
            UnbindButton(_dropdownButton, ClickDropdown);
            KillTweens();
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
            ValidateAuthoredStructureOrThrow();
            var isVisible = _viewModel != null && _viewModel.IsVisible;
            if (_root != null)
            {
                _root.SetActive(isVisible);
            }

            if (_dropdownButton != null)
            {
                _dropdownButton.interactable = isVisible && _viewModel != null && _viewModel.CanExpand;
            }

            RefreshStructuredDropdown(isVisible);
            if (_objectiveLabel == null)
            {
                return;
            }

            _objectiveLabel.textWrappingMode = _viewModel != null && _viewModel.IsExpanded
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            _objectiveLabel.overflowMode = _viewModel != null && _viewModel.IsExpanded
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Ellipsis;
            _objectiveLabel.color = _viewModel != null && _viewModel.IsComplete
                ? ParseColor(CompleteColor)
                : ParseColor(ActiveColor);
            _objectiveLabel.text = isVisible ? _viewModel.ObjectiveText : string.Empty;
        }

        private void RefreshStructuredDropdown(bool isVisible)
        {
            if (_viewModel == null)
            {
                return;
            }

            if (_eyebrowText != null)
            {
                _eyebrowText.text = "OBJECTIVE";
            }

            if (_titleText != null)
            {
                _titleText.text = isVisible ? _viewModel.Title : string.Empty;
                _titleText.textWrappingMode = TextWrappingModes.NoWrap;
                _titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (_progressPillText != null)
            {
                _progressPillText.text = _viewModel.ProgressText;
                _progressPillText.gameObject.SetActive(isVisible && !string.IsNullOrWhiteSpace(_viewModel.ProgressText));
            }

            if (_progressBar != null)
            {
                _progressBar.fillAmount = _viewModel.Progress01;
            }

            if (_summaryText != null)
            {
                _summaryText.text = _viewModel.IsExpanded ? _viewModel.Summary : string.Empty;
                _summaryText.gameObject.SetActive(_viewModel.IsExpanded && !string.IsNullOrWhiteSpace(_viewModel.Summary));
            }

            if (_footerStatusText != null)
            {
                _footerStatusText.text = _viewModel.IsComplete ? "Complete" : string.Empty;
                _footerStatusText.gameObject.SetActive(_viewModel.IsExpanded || _viewModel.IsComplete);
            }

            if (_completeBadge != null)
            {
                _completeBadge.SetActive(_viewModel.ShowCompleteBadge);
            }

            if (_chevronIcon != null)
            {
                _chevronIcon.localRotation = Quaternion.Euler(0.0f, 0.0f, _viewModel.IsExpanded ? 180.0f : 0.0f);
            }

            RefreshRows();
            RefreshBodyVisibility();
            PlayCompletePulseIfNeeded();
        }

        private void RefreshRows()
        {
            if (_conditionListRoot == null)
            {
                return;
            }

            var rows = _viewModel.Rows;
            while (_rowPool.Count < rows.Count)
            {
                _rowPool.Add(CreateRow(_rowPool.Count));
            }

            for (var i = 0; i < _rowPool.Count; i++)
            {
                var active = _viewModel.IsExpanded && i < rows.Count;
                _rowPool[i].gameObject.SetActive(active);
                if (active)
                {
                    _rowPool[i].Bind(rows[i], HudAnimationSettings.Default);
                }
            }
        }

        private ObjectiveConditionRowView CreateRow(int index)
        {
            var rowObject = Instantiate(_conditionRowTemplate.gameObject, _conditionListRoot);
            rowObject.name = $"ObjectiveConditionRowView {index}";
            rowObject.SetActive(false);
            return rowObject.GetComponent<ObjectiveConditionRowView>();
        }

        private void RefreshBodyVisibility()
        {
            if (_bodyRoot != null)
            {
                _bodyRoot.gameObject.SetActive(_viewModel.IsExpanded);
            }

            if (_bodyCanvasGroup == null)
            {
                return;
            }

            KillBodySequence();
            _bodyCanvasGroup.alpha = _viewModel.IsExpanded ? 1.0f : 0.0f;
        }

        private void PlayCompletePulseIfNeeded()
        {
            var hint = _viewModel.AnimationHint;
            if (!hint.PulseComplete ||
                hint.SequenceId <= 0 ||
                hint.SequenceId == _lastAnimationSequenceId)
            {
                return;
            }

            _lastAnimationSequenceId = hint.SequenceId;
            if (_completeBadge == null)
            {
                return;
            }

            KillCompletePulse();
            _completePulseTween = _completeBadge.transform
                .DOPunchScale(Vector3.one * 0.12f, 0.32f, 8, 0.75f)
                .SetUpdate(true)
                .SetLink(_completeBadge, LinkBehaviour.KillOnDestroy);
        }

        private void KillTweens()
        {
            KillBodySequence();
            KillCompletePulse();
        }

        private void KillBodySequence()
        {
            if (_bodySequence == null)
            {
                return;
            }

            _bodySequence.Kill(false);
            _bodySequence = null;
        }

        private void KillCompletePulse()
        {
            if (_completePulseTween == null)
            {
                return;
            }

            _completePulseTween.Kill(false);
            _completePulseTween = null;
        }

        private static Color ParseColor(string htmlString)
        {
            return ColorUtility.TryParseHtmlString(htmlString, out var color) ? color : Color.white;
        }

        private static void RequireReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{nameof(ObjectiveHudView)} is missing authored reference '{fieldName}'.");
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
                Debug.LogWarning($"{nameof(ObjectiveHudView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
