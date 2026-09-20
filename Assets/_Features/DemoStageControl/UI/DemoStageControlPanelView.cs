using System;
using System.Collections.Generic;
using Game.Feature.Stages;
using Game.Feature.UI.Popups;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.DemoStageControl.UI
{
    public sealed class DemoStageControlPanelView : MonoBehaviour, IPopupView, IUiNavigationTarget
    {
        private const int PreviousIndex = 0;
        private const int NextIndex = 1;
        private const int StartIndex = 2;
        private const int ForceClearIndex = 3;
        private const int InvincibleIndex = 4;
        private const int CloseIndex = 5;
        private const int RequiredActionCount = 6;

        private static readonly int[] RecoveryPriority =
        {
            StartIndex, ForceClearIndex, InvincibleIndex, CloseIndex, PreviousIndex, NextIndex,
        };

        [SerializeField] private RectTransform _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _currentText;
        [SerializeField] private TextMeshProUGUI _campaignText;
        [SerializeField] private TextMeshProUGUI _selectedStageText;
        [SerializeField] private TextMeshProUGUI _lastResultText;
        [SerializeField] private Button _previousButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _forceClearButton;
        [SerializeField] private Button _playerInvincibleButton;
        [SerializeField] private TextMeshProUGUI _playerInvincibleButtonLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private UiSelectableButtonGroup _navigationGroup = new();
        [SerializeField] private TMP_Text[] _typographyTargets = Array.Empty<TMP_Text>();

        private DemoStageControlPanelViewModel _viewModel;
        private bool _navigationFocusVisible;
        private int _lastSelectorIndex = PreviousIndex;

        public event Action<PopupCompletionKind> CompletionRequested;
        public event Action<StageId> SelectedStageChanged;
        public event Action<StageId> StartStageClicked;
        public event Action ForceClearClicked;
        public event Action<bool> PlayerInvincibleToggled;

        public bool CanHandleUiNavigation =>
            IsVisible && isActiveAndEnabled && _canvasGroup != null && _canvasGroup.interactable;

        public bool IsVisible
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public IReadOnlyList<TMP_Text> CreateTypographyTargets() => _typographyTargets;

        public void ValidateAuthoredReferences()
        {
            if (_root == null || _root != transform || _canvasGroup == null ||
                _currentText == null || _campaignText == null ||
                _selectedStageText == null || _lastResultText == null ||
                _previousButton == null || _nextButton == null ||
                _startButton == null || _forceClearButton == null ||
                _playerInvincibleButton == null || _playerInvincibleButtonLabel == null ||
                _closeButton == null)
            {
                throw new InvalidOperationException(
                    "DemoStageControlPanel prefab has missing or malformed authored references.");
            }

            _navigationGroup?.ValidateOrThrow(
                "DemoStageControlPanel requires six authored navigation slots and a visual profile.");
            if (_navigationGroup == null || _navigationGroup.SlotCount != RequiredActionCount)
            {
                throw new InvalidOperationException(
                    "DemoStageControlPanel requires exactly six authored navigation slots.");
            }

            var expectedButtons = GetButtons();
            for (var i = 0; i < RequiredActionCount; i++)
            {
                if (_navigationGroup.GetSlot(i)?.Button != expectedButtons[i] ||
                    _navigationGroup.GetSlot(i)?.SelectionFrame == null)
                {
                    throw new InvalidOperationException(
                        $"DemoStageControlPanel navigation slot {i} is malformed.");
                }
            }

            if (_typographyTargets == null || _typographyTargets.Length == 0)
            {
                throw new InvalidOperationException(
                    "DemoStageControlPanel requires authored typography targets.");
            }
        }

        public void Bind(DemoStageControlPanelViewModel viewModel)
        {
            if (_viewModel != null) _viewModel.Changed -= Refresh;
            _viewModel = viewModel;
            if (_viewModel != null) _viewModel.Changed += Refresh;
            if (_viewModel != null) BindButtonListeners();
            else UnbindButtonListeners();
            Refresh();
        }

        public void SetIsTopmost(bool isTopmost)
        {
            _canvasGroup.interactable = isTopmost;
            _canvasGroup.blocksRaycasts = isTopmost;
            if (!isTopmost) OnNavigationFocusLost();
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (!CanHandleUiNavigation) return false;
            if (!_navigationFocusVisible) OnNavigationFocusGained();

            var selected = _navigationGroup.SelectedIndex;
            var next = ResolveDirectionalTarget(selected, command);
            if (next < 0 || next == selected) return false;
            if (next == PreviousIndex || next == NextIndex) _lastSelectorIndex = next;
            _navigationGroup.SetSelectedIndex(next);
            return true;
        }

        public bool HandleSubmit()
        {
            if (!CanHandleUiNavigation) return false;
            if (!_navigationFocusVisible)
            {
                OnNavigationFocusGained();
                return true;
            }

            var selected = _navigationGroup.GetSelectedButton();
            if (selected == null || !selected.interactable)
            {
                RecoverFocus();
                return false;
            }

            _navigationGroup.PlaySelectedSubmitFeedback();
            InvokeAction(_navigationGroup.SelectedIndex);
            return true;
        }

        public bool HandleCancel()
        {
            if (!CanHandleUiNavigation) return false;
            ClickClose();
            return true;
        }

        public void OnNavigationFocusGained()
        {
            if (!CanHandleUiNavigation) return;
            _navigationFocusVisible = true;
            _navigationGroup.SetSelectedIndex(StartIndex);
            RecoverFocus();
        }

        public void OnNavigationFocusLost()
        {
            _navigationFocusVisible = false;
            _navigationGroup?.HideAllFrames();
        }

        private void Awake() => ValidateAuthoredReferences();

        private void OnEnable()
        {
            BindButtonListeners();
        }

        private void BindButtonListeners()
        {
            Rebind(_previousButton, ClickPrevious);
            Rebind(_nextButton, ClickNext);
            Rebind(_startButton, ClickStart);
            Rebind(_forceClearButton, ClickForceClear);
            Rebind(_playerInvincibleButton, ClickPlayerInvincible);
            Rebind(_closeButton, ClickClose);
        }

        private void OnDisable()
        {
            UnbindButtonListeners();
            OnNavigationFocusLost();
        }

        private void UnbindButtonListeners()
        {
            Unbind(_previousButton, ClickPrevious);
            Unbind(_nextButton, ClickNext);
            Unbind(_startButton, ClickStart);
            Unbind(_forceClearButton, ClickForceClear);
            Unbind(_playerInvincibleButton, ClickPlayerInvincible);
            Unbind(_closeButton, ClickClose);
        }

        private void OnDestroy()
        {
            if (_viewModel != null) _viewModel.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_viewModel == null) return;
            _currentText.text = _viewModel.CurrentStageText;
            _campaignText.text = _viewModel.CampaignActiveStageText;
            _selectedStageText.text = _viewModel.SelectedStageText;
            _lastResultText.text = string.IsNullOrWhiteSpace(_viewModel.LastResultText)
                ? "Last result: none"
                : $"Last result: {_viewModel.LastResultText}";
            _playerInvincibleButtonLabel.text = _viewModel.PlayerInvincibleText;
            _playerInvincibleButton.interactable = true;
            RefreshPlayerInvincibleButtonColors();
            _startButton.interactable = _viewModel.CanStartSelectedStage;
            _forceClearButton.interactable = _viewModel.CanForceClearCurrentStage;
            _previousButton.interactable = _viewModel.Stages.Count > 1;
            _nextButton.interactable = _viewModel.Stages.Count > 1;
            if (_navigationFocusVisible) RecoverFocus();
        }

        private int ResolveDirectionalTarget(int selected, UiNavigationCommand command)
        {
            if (selected == PreviousIndex || selected == NextIndex)
            {
                if (command == UiNavigationCommand.Left && selected == NextIndex && IsEnabled(PreviousIndex))
                    return PreviousIndex;
                if (command == UiNavigationCommand.Right && selected == PreviousIndex && IsEnabled(NextIndex))
                    return NextIndex;
                return command == UiNavigationCommand.Down ? FindEnabledVertical(StartIndex, 1) : -1;
            }

            if (command == UiNavigationCommand.Up)
            {
                if (selected == StartIndex)
                    return IsEnabled(_lastSelectorIndex) ? _lastSelectorIndex : FindEnabledSelector();
                return FindEnabledVertical(selected - 1, -1);
            }

            return command == UiNavigationCommand.Down
                ? FindEnabledVertical(selected + 1, 1)
                : -1;
        }

        private int FindEnabledVertical(int origin, int delta)
        {
            for (var index = origin; index >= StartIndex && index <= CloseIndex; index += delta)
                if (IsEnabled(index)) return index;
            return -1;
        }

        private int FindEnabledSelector()
        {
            if (IsEnabled(PreviousIndex)) return PreviousIndex;
            return IsEnabled(NextIndex) ? NextIndex : -1;
        }

        private void RecoverFocus()
        {
            var selected = _navigationGroup.SelectedIndex;
            if (IsEnabled(selected))
            {
                _navigationGroup.RefreshVisuals();
                return;
            }

            var bestIndex = CloseIndex;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < RecoveryPriority.Length; i++)
            {
                var candidate = RecoveryPriority[i];
                if (!IsEnabled(candidate)) continue;
                var distance = GraphDistance(selected, candidate);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = candidate;
            }

            _navigationGroup.SetSelectedIndex(bestIndex);
        }

        private static int GraphDistance(int from, int to)
        {
            if (from == to) return 0;
            var fromVertical = from >= StartIndex;
            var toVertical = to >= StartIndex;
            if (fromVertical && toVertical) return Math.Abs(from - to);
            if (!fromVertical && !toVertical) return 1;
            var vertical = fromVertical ? from : to;
            return 1 + Math.Abs(vertical - StartIndex);
        }

        private bool IsEnabled(int index)
        {
            var button = _navigationGroup.GetSlot(index)?.Button;
            return button != null && button.gameObject.activeInHierarchy && button.interactable;
        }

        private Button[] GetButtons() => new[]
        {
            _previousButton, _nextButton, _startButton,
            _forceClearButton, _playerInvincibleButton, _closeButton,
        };

        private void InvokeAction(int index)
        {
            switch (index)
            {
                case PreviousIndex: ClickPrevious(); break;
                case NextIndex: ClickNext(); break;
                case StartIndex: ClickStart(); break;
                case ForceClearIndex: ClickForceClear(); break;
                case InvincibleIndex: ClickPlayerInvincible(); break;
                case CloseIndex: ClickClose(); break;
            }
        }

        private void ClickPrevious()
        {
            if (!CanInvoke(_previousButton) || _viewModel.Stages.Count == 0) return;
            _navigationGroup.SetSelectedIndex(PreviousIndex);
            _lastSelectorIndex = PreviousIndex;
            var index = _viewModel.SelectedStageIndex <= 0
                ? _viewModel.Stages.Count - 1
                : _viewModel.SelectedStageIndex - 1;
            _viewModel.SelectIndex(index);
            SelectedStageChanged?.Invoke(_viewModel.SelectedStageId);
        }

        private void ClickNext()
        {
            if (!CanInvoke(_nextButton) || _viewModel.Stages.Count == 0) return;
            _navigationGroup.SetSelectedIndex(NextIndex);
            _lastSelectorIndex = NextIndex;
            var index = _viewModel.SelectedStageIndex >= _viewModel.Stages.Count - 1
                ? 0
                : _viewModel.SelectedStageIndex + 1;
            _viewModel.SelectIndex(index);
            SelectedStageChanged?.Invoke(_viewModel.SelectedStageId);
        }

        private void ClickStart()
        {
            if (!CanInvoke(_startButton) || !_viewModel.SelectedStageId.IsValid) return;
            _navigationGroup.SetSelectedIndex(StartIndex);
            StartStageClicked?.Invoke(_viewModel.SelectedStageId);
        }

        private void ClickForceClear()
        {
            if (!CanInvoke(_forceClearButton)) return;
            _navigationGroup.SetSelectedIndex(ForceClearIndex);
            ForceClearClicked?.Invoke();
        }

        private void ClickPlayerInvincible()
        {
            if (!CanInvoke(_playerInvincibleButton)) return;
            _navigationGroup.SetSelectedIndex(InvincibleIndex);
            PlayerInvincibleToggled?.Invoke(!_viewModel.PlayerInvincible);
        }

        private void ClickClose()
        {
            if (!CanInvoke(_closeButton)) return;
            _navigationGroup.SetSelectedIndex(CloseIndex);
            CompletionRequested?.Invoke(PopupCompletionKind.Closed);
        }

        private bool CanInvoke(Button button) =>
            _viewModel != null && CanHandleUiNavigation && button != null && button.interactable;

        private void RefreshPlayerInvincibleButtonColors()
        {
            var normal = _viewModel.PlayerInvincible
                ? new Color(0.18f, 0.42f, 0.34f, 1f)
                : new Color(0.18f, 0.22f, 0.25f, 1f);
            var colors = _playerInvincibleButton.colors;
            colors.normalColor = normal;
            colors.highlightedColor = _viewModel.PlayerInvincible
                ? new Color(0.23f, 0.50f, 0.41f, 1f)
                : new Color(0.26f, 0.31f, 0.35f, 1f);
            colors.pressedColor = _viewModel.PlayerInvincible
                ? new Color(0.13f, 0.30f, 0.24f, 1f)
                : new Color(0.12f, 0.15f, 0.18f, 1f);
            _playerInvincibleButton.colors = colors;
        }

        private static void Rebind(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }
    }
}
