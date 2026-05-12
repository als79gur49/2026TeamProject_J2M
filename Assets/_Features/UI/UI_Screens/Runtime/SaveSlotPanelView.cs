using System;
using Game.Feature.UI.ViewShared;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotPanelView : MonoBehaviour
    {
        public const int RequiredSlotCardCount = 3;

        private const string MissingAuthoredStructureMessage =
            "MainMenu save slot panel is missing required authored SaveSlotCardView references. Repair MainMenuScreen.prefab so SaveSlotPanelView owns exactly three SaveSlotCardView children.";

        [SerializeField] private SaveSlotCardView[] _slotCards = Array.Empty<SaveSlotCardView>();

        private SaveSlotPanelViewModel _viewModel;
        private int _selectedCardIndex;
        private SaveSlotActionSelection _selectedAction = SaveSlotActionSelection.Primary;
        private bool _navigationFrameVisible;

        public event Action<SaveSlotIntent> SaveSlotIntentRequested;

        public SaveSlotCardView[] SlotCards => _slotCards;

        public bool HasFocusableCards => FindFirstFocusableCardIndex() >= 0;

        public int SelectedCardIndex => _selectedCardIndex;

        public SaveSlotActionSelection SelectedAction => _selectedAction;

        public void Bind(SaveSlotPanelViewModel viewModel)
        {
            _viewModel = viewModel;
            Refresh();
            RefreshNavigationAfterSlotDataChanged();
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
        }

        private void OnEnable()
        {
            WireSlotCards();
            Refresh();
        }

        private void OnDisable()
        {
            HideNavigationFrames();
            UnwireSlotCards();
        }

        public bool FocusFirstAvailableCardPrimary(bool showFrame)
        {
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
            if (!HasFocusableCards)
            {
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

        private void HandleCardIntentRequested(SaveSlotIntent intent)
        {
            SaveSlotIntentRequested?.Invoke(intent);
        }

        private void Refresh()
        {
            if (_viewModel == null || _slotCards == null)
            {
                return;
            }

            var count = Math.Min(_slotCards.Length, _viewModel.SlotCards.Count);
            for (var i = 0; i < count; i++)
            {
                if (_slotCards[i] != null)
                {
                    _slotCards[i].Bind(_viewModel.SlotCards[i]);
                }
            }
        }

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
