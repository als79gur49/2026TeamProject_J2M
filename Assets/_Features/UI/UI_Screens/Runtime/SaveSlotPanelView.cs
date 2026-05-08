using System;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotPanelView : MonoBehaviour
    {
        public const int RequiredSlotCardCount = 3;

        private const string MissingAuthoredStructureMessage =
            "MainMenu save slot panel is missing required authored SaveSlotCardView references. Repair MainMenuScreen.prefab so SaveSlotPanelView owns exactly three direct SaveSlotCardView children.";

        [SerializeField] private SaveSlotCardView[] _slotCards = Array.Empty<SaveSlotCardView>();

        private SaveSlotPanelViewModel _viewModel;

        public event Action<SaveSlotIntent> SaveSlotIntentRequested;

        public SaveSlotCardView[] SlotCards => _slotCards;

        public void Bind(SaveSlotPanelViewModel viewModel)
        {
            _viewModel = viewModel;
            Refresh();
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
                if (card == null || card.transform.parent != transform)
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
            UnwireSlotCards();
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
    }
}
