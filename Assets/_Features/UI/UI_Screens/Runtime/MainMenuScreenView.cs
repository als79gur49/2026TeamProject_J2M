using System;
using UnityEngine;

namespace Game.Feature.UI.Screens
{
    public sealed class MainMenuScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private SaveSlotCardView[] _slotCards = Array.Empty<SaveSlotCardView>();

        private MainMenuScreenViewModel _viewModel;

        public event Action<SaveSlotIntent> IntentRequested;

        public void Bind(MainMenuScreenViewModel viewModel)
        {
            _viewModel = viewModel;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
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
            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] != null)
                {
                    _slotCards[i].IntentRequested -= HandleSlotIntentRequested;
                    _slotCards[i].IntentRequested += HandleSlotIntentRequested;
                }
            }
        }

        private void UnwireSlotCards()
        {
            for (var i = 0; i < _slotCards.Length; i++)
            {
                if (_slotCards[i] != null)
                {
                    _slotCards[i].IntentRequested -= HandleSlotIntentRequested;
                }
            }
        }

        private void HandleSlotIntentRequested(SaveSlotIntent intent)
        {
            IntentRequested?.Invoke(intent);
        }

        private void Refresh()
        {
            if (_viewModel == null)
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
