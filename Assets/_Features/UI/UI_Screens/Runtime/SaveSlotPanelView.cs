using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotPanelView : MonoBehaviour
    {
        public const int RequiredSlotCardCount = 3;

        private const string MissingAuthoredStructureMessage =
            "MainMenu save slot panel is missing required authored SaveSlotCardView references. Repair MainMenuScreen.prefab so SaveSlotPanelView owns exactly three SaveSlotCardView children.";

        [SerializeField] private SaveSlotCardView[] _slotCards = Array.Empty<SaveSlotCardView>();
        [SerializeField] private GameObject _blockedStateRoot;
        [SerializeField] private TMP_Text _blockedTitleLabel;
        [SerializeField] private TMP_Text _blockedDetailLabel;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _retryButtonLabel;
        [SerializeField] private Button _resetProfileButton;
        [SerializeField] private TMP_Text _resetProfileButtonLabel;

        private SaveSlotPanelViewModel _viewModel;
        private int _selectedCardIndex;
        private SaveSlotActionSelection _selectedAction = SaveSlotActionSelection.Primary;
        private bool _navigationFrameVisible;
        private bool _interactionBlocked;
        private int _selectedRecoveryIndex;

        public event Action<SaveSlotIntent> SaveSlotIntentRequested;

        public event Action RetryBlockedSaveRequested;

        public event Action ResetBlockedSaveRequested;

        public SaveSlotCardView[] SlotCards => _slotCards;

        public bool HasFocusableCards => IsBlockedView
            ? CanFocusRecovery(0) || CanFocusRecovery(1)
            : FindFirstFocusableCardIndex() >= 0;

        public int SelectedCardIndex => _selectedCardIndex;

        public SaveSlotActionSelection SelectedAction => _selectedAction;

        public IReadOnlyList<TMP_Text> CreateTypographyTargets()
        {
            var targets = new List<TMP_Text>();
            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] != null)
                {
                    targets.AddRange(_slotCards[i].CreateTypographyTargets());
                }
            }

            if (_blockedTitleLabel != null) targets.Add(_blockedTitleLabel);
            if (_blockedDetailLabel != null) targets.Add(_blockedDetailLabel);
            if (_retryButtonLabel != null) targets.Add(_retryButtonLabel);
            if (_resetProfileButtonLabel != null) targets.Add(_resetProfileButtonLabel);

            return targets;
        }

        public void Bind(SaveSlotPanelViewModel viewModel)
        {
            _viewModel = viewModel;
            Refresh();
            RefreshNavigationAfterSlotDataChanged();
        }

        public void SetInteractionBlocked(bool blocked)
        {
            _interactionBlocked = blocked;
            if (_slotCards != null)
            {
                for (var i = 0; i < _slotCards.Length; i++)
                {
                    _slotCards[i]?.SetInteractionBlocked(blocked);
                }
            }

            if (_retryButton != null) _retryButton.interactable = !blocked && CanShowRetry;
            if (_resetProfileButton != null) _resetProfileButton.interactable = !blocked && CanShowReset;

            if (blocked)
            {
                HideNavigationFrames();
            }
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_slotCards == null || _slotCards.Length != RequiredSlotCardCount)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            var childCards = GetComponentsInChildren<SaveSlotCardView>(true);
            if (childCards.Length != RequiredSlotCardCount)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                var card = _slotCards[i];
                if (card == null || !card.transform.IsChildOf(transform))
                {
                    throw new InvalidOperationException(MissingAuthoredStructureMessage);
                }

                card.ValidateAuthoredStructureOrThrow();
            }

            if (_blockedStateRoot == null ||
                _blockedTitleLabel == null ||
                _blockedDetailLabel == null ||
                _retryButton == null ||
                _retryButtonLabel == null ||
                _resetProfileButton == null ||
                _resetProfileButtonLabel == null)
            {
                throw new InvalidOperationException(
                    "MainMenu save slot panel is missing required blocked-save recovery references.");
            }
        }

        private void OnEnable()
        {
            WireSlotCards();
            WireRecoveryButtons();
            Refresh();
        }

        private void OnDisable()
        {
            HideNavigationFrames();
            UnwireSlotCards();
            UnwireRecoveryButtons();
        }

        public bool FocusFirstAvailableCardPrimary(bool showFrame)
        {
            if (IsBlockedView)
            {
                return FocusFirstRecoveryAction();
            }

            var index = FindFirstFocusableCardIndex();
            if (index < 0)
            {
                HideNavigationFrames();
                return false;
            }

            SelectCard(index, SaveSlotActionSelection.Primary, showFrame);
            return true;
        }

        public bool HandleNavigate(UiNavigationCommand command)
        {
            if (_interactionBlocked || !HasFocusableCards)
            {
                return false;
            }

            if (IsBlockedView)
            {
                if (command == UiNavigationCommand.Up ||
                    command == UiNavigationCommand.Left)
                {
                    MoveRecovery(-1);
                    return true;
                }

                if (command == UiNavigationCommand.Down ||
                    command == UiNavigationCommand.Right)
                {
                    MoveRecovery(1);
                    return true;
                }

                return false;
            }

            switch (command)
            {
                case UiNavigationCommand.Up:
                    MoveCard(-1);
                    return true;

                case UiNavigationCommand.Down:
                    MoveCard(1);
                    return true;

                case UiNavigationCommand.Left:
                    GetSelectedCard()?.MoveActionLeft();
                    SyncSelectedActionFromCard();
                    return true;

                case UiNavigationCommand.Right:
                    GetSelectedCard()?.MoveActionRight();
                    SyncSelectedActionFromCard();
                    return true;

                default:
                    return false;
            }
        }

        public bool HandleSubmit()
        {
            if (_interactionBlocked)
            {
                return false;
            }

            WireSlotCards();
            if (IsBlockedView)
            {
                var button = _selectedRecoveryIndex == 1 ? _resetProfileButton : _retryButton;
                if (button == null || !button.IsActive() || !button.IsInteractable())
                {
                    return false;
                }

                button.onClick.Invoke();
                return true;
            }

            var card = GetSelectedCard();
            return card != null && card.SubmitSelectedAction();
        }

        public bool HandleCancel()
        {
            HideNavigationFrames();
            return true;
        }

        public void HideNavigationFrames()
        {
            _navigationFrameVisible = false;
            if (_slotCards == null)
            {
                return;
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                _slotCards[i]?.HideNavigationFrames();
            }
        }

        public bool RefreshNavigationAfterSlotDataChanged()
        {
            if (IsBlockedView)
            {
                return FocusFirstRecoveryAction();
            }

            if (_slotCards == null || _slotCards.Length == 0)
            {
                HideNavigationFrames();
                return false;
            }

            var card = GetSelectedCard();
            if (card != null && card.HasAnyFocusableAction)
            {
                SelectCard(_selectedCardIndex, _selectedAction, _navigationFrameVisible);
                return true;
            }

            var fallbackIndex = FindNearestFocusableCardIndex(_selectedCardIndex);
            if (fallbackIndex < 0)
            {
                HideNavigationFrames();
                return false;
            }

            SelectCard(fallbackIndex, SaveSlotActionSelection.Primary, _navigationFrameVisible);
            return true;
        }

        public bool RestorePreviousFocusOrFallback()
        {
            return RefreshNavigationAfterSlotDataChanged();
        }

        public bool RestorePreviousFocusOrFallback(bool showFrame)
        {
            var previousFrameVisible = _navigationFrameVisible;
            _navigationFrameVisible = showFrame;
            if (RefreshNavigationAfterSlotDataChanged())
            {
                return true;
            }

            _navigationFrameVisible = previousFrameVisible;
            return false;
        }

        public void CaptureNavigationFocus()
        {
            SyncSelectedActionFromCard();
        }

        private void WireSlotCards()
        {
            if (_slotCards == null)
            {
                return;
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                var card = _slotCards[i];
                if (card == null)
                {
                    continue;
                }

                card.IntentRequested -= HandleCardIntentRequested;
                card.IntentRequested += HandleCardIntentRequested;
            }
        }

        private void UnwireSlotCards()
        {
            if (_slotCards == null)
            {
                return;
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                var card = _slotCards[i];
                if (card != null)
                {
                    card.IntentRequested -= HandleCardIntentRequested;
                }
            }
        }

        private void WireRecoveryButtons()
        {
            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveListener(HandleRetryClicked);
                _retryButton.onClick.AddListener(HandleRetryClicked);
            }

            if (_resetProfileButton != null)
            {
                _resetProfileButton.onClick.RemoveListener(HandleResetClicked);
                _resetProfileButton.onClick.AddListener(HandleResetClicked);
            }
        }

        private void UnwireRecoveryButtons()
        {
            _retryButton?.onClick.RemoveListener(HandleRetryClicked);
            _resetProfileButton?.onClick.RemoveListener(HandleResetClicked);
        }

        private void HandleRetryClicked()
        {
            if (!_interactionBlocked && CanShowRetry)
            {
                RetryBlockedSaveRequested?.Invoke();
            }
        }

        private void HandleResetClicked()
        {
            if (!_interactionBlocked && CanShowReset)
            {
                ResetBlockedSaveRequested?.Invoke();
            }
        }

        private void HandleCardIntentRequested(SaveSlotIntent intent)
        {
            if (_interactionBlocked)
            {
                return;
            }

            SaveSlotIntentRequested?.Invoke(intent);
        }

        private void Refresh()
        {
            if (_viewModel == null || _slotCards == null)
            {
                return;
            }

            var blocked = IsBlockedView;
            if (_blockedStateRoot != null)
            {
                _blockedStateRoot.SetActive(blocked);
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] != null)
                {
                    _slotCards[i].gameObject.SetActive(!blocked);
                }
            }

            if (blocked)
            {
                var blockedState = _viewModel.BlockedState;
                if (_blockedTitleLabel != null) _blockedTitleLabel.text = blockedState.TitleText;
                if (_blockedDetailLabel != null) _blockedDetailLabel.text = blockedState.DetailText;
                if (_retryButtonLabel != null) _retryButtonLabel.text = blockedState.RetryActionText;
                if (_resetProfileButtonLabel != null) _resetProfileButtonLabel.text = blockedState.ResetProfileActionText;
                if (_retryButton != null)
                {
                    _retryButton.gameObject.SetActive(blockedState.ShowRetry);
                    _retryButton.interactable = !_interactionBlocked && blockedState.ShowRetry;
                }

                if (_resetProfileButton != null)
                {
                    _resetProfileButton.gameObject.SetActive(blockedState.ShowResetProfile);
                    _resetProfileButton.interactable = !_interactionBlocked && blockedState.ShowResetProfile;
                }

                return;
            }

            var count = Math.Min(_slotCards.Length, _viewModel.SlotCards.Count);
            for (var i = 0; i < count; i++)
            {
                if (_slotCards[i] != null)
                {
                    _slotCards[i].Bind(_viewModel.SlotCards[i]);
                    _slotCards[i].SetInteractionBlocked(_interactionBlocked);
                }
            }
        }

        private bool FocusFirstRecoveryAction()
        {
            if (CanFocusRecovery(0))
            {
                SelectRecovery(0);
                return true;
            }

            if (CanFocusRecovery(1))
            {
                SelectRecovery(1);
                return true;
            }

            return false;
        }

        private void MoveRecovery(int delta)
        {
            var candidate = _selectedRecoveryIndex + Math.Sign(delta);
            if (CanFocusRecovery(candidate))
            {
                SelectRecovery(candidate);
            }
        }

        private void SelectRecovery(int index)
        {
            _selectedRecoveryIndex = index;
            var button = index == 1 ? _resetProfileButton : _retryButton;
            button?.Select();
        }

        private bool CanFocusRecovery(int index)
        {
            var button = index == 0 ? _retryButton : index == 1 ? _resetProfileButton : null;
            return button != null && button.IsActive() && button.IsInteractable();
        }

        private bool IsBlockedView => _viewModel?.BlockedState != null;

        private bool CanShowRetry => _viewModel?.BlockedState?.ShowRetry == true;

        private bool CanShowReset => _viewModel?.BlockedState?.ShowResetProfile == true;

        private void MoveCard(int delta)
        {
            if (delta == 0 || _slotCards == null || _slotCards.Length == 0)
            {
                return;
            }

            var nextIndex = FindNextFocusableCardIndex(_selectedCardIndex, delta);
            if (nextIndex < 0)
            {
                SelectCard(_selectedCardIndex, _selectedAction, _navigationFrameVisible);
                return;
            }

            SelectCard(nextIndex, _selectedAction, _navigationFrameVisible);
        }

        private void SelectCard(int index, SaveSlotActionSelection desiredAction, bool showFrame)
        {
            if (_slotCards == null || index < 0 || index >= _slotCards.Length || _slotCards[index] == null)
            {
                HideNavigationFrames();
                return;
            }

            _selectedCardIndex = index;
            _navigationFrameVisible = showFrame;

            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] == null)
                {
                    continue;
                }

                if (i == _selectedCardIndex)
                {
                    _slotCards[i].SetActionSelection(desiredAction, showFrame);
                    _selectedAction = _slotCards[i].CurrentSelection;
                }
                else
                {
                    _slotCards[i].HideNavigationFrames();
                }
            }
        }

        private SaveSlotCardView GetSelectedCard()
        {
            if (_slotCards == null || _selectedCardIndex < 0 || _selectedCardIndex >= _slotCards.Length)
            {
                return null;
            }

            return _slotCards[_selectedCardIndex];
        }

        private void SyncSelectedActionFromCard()
        {
            var card = GetSelectedCard();
            if (card != null)
            {
                _selectedAction = card.CurrentSelection;
            }
        }

        private int FindFirstFocusableCardIndex()
        {
            if (_slotCards == null)
            {
                return -1;
            }

            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] != null && _slotCards[i].HasAnyFocusableAction)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FindNearestFocusableCardIndex(int origin)
        {
            if (_slotCards == null || _slotCards.Length == 0)
            {
                return -1;
            }

            var clamped = Mathf.Clamp(origin, 0, _slotCards.Length - 1);
            if (_slotCards[clamped] != null && _slotCards[clamped].HasAnyFocusableAction)
            {
                return clamped;
            }

            for (var distance = 1; distance < _slotCards.Length; distance++)
            {
                var lower = clamped - distance;
                if (lower >= 0 && _slotCards[lower] != null && _slotCards[lower].HasAnyFocusableAction)
                {
                    return lower;
                }

                var upper = clamped + distance;
                if (upper < _slotCards.Length && _slotCards[upper] != null && _slotCards[upper].HasAnyFocusableAction)
                {
                    return upper;
                }
            }

            return -1;
        }

        private int FindNextFocusableCardIndex(int origin, int delta)
        {
            var index = Mathf.Clamp(origin, 0, _slotCards.Length - 1);
            while (true)
            {
                index += delta;
                if (index < 0 || index >= _slotCards.Length)
                {
                    return -1;
                }

                if (_slotCards[index] != null && _slotCards[index].HasAnyFocusableAction)
                {
                    return index;
                }
            }
        }
    }
}
