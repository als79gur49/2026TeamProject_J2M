using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.HUD
{
    public sealed class ActionBarView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _primaryLabel;
        [SerializeField] private Text _primaryStateLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Text _secondaryLabel;
        [SerializeField] private Text _secondaryStateLabel;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private Text _outcomeLabel;
        [SerializeField] private Text _feedbackLabel;

        private ActionBarViewModel _viewModel;

        public event Action<HudActionSlotId> SlotRequested;

        public ActionBarViewModel ViewModel => _viewModel;

        public void Configure(
            GameObject root,
            Text titleLabel,
            Text primaryLabel,
            Text primaryStateLabel,
            Button primaryButton,
            Text secondaryLabel,
            Text secondaryStateLabel,
            Button secondaryButton,
            Text outcomeLabel,
            Text feedbackLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _primaryLabel = primaryLabel;
            _primaryStateLabel = primaryStateLabel;
            _primaryButton = primaryButton;
            _secondaryLabel = secondaryLabel;
            _secondaryStateLabel = secondaryStateLabel;
            _secondaryButton = secondaryButton;
            _outcomeLabel = outcomeLabel;
            _feedbackLabel = feedbackLabel;

            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(ClickPrimary);
                _primaryButton.onClick.AddListener(ClickPrimary);
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.onClick.RemoveListener(ClickSecondary);
                _secondaryButton.onClick.AddListener(ClickSecondary);
            }

            RefreshView();
        }

        public void Bind(ActionBarViewModel viewModel)
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

        public void ClickSlot(HudActionSlotId slotId)
        {
            if (_viewModel == null)
            {
                return;
            }

            var slot = TryGetSlot(slotId, out var slotViewModel)
                ? slotViewModel
                : default;

            if (slot.SlotId != slotId || !slot.IsInteractive)
            {
                return;
            }

            SlotRequested?.Invoke(slotId);
        }

        public void ClickPrimary()
        {
            ClickSlot(HudActionSlotId.Primary);
        }

        public void ClickSecondary()
        {
            ClickSlot(HudActionSlotId.Secondary);
        }

        private void OnDestroy()
        {
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
            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Action Bar";
            }

            if (_viewModel == null)
            {
                return;
            }

            ApplySlot(HudActionSlotId.Primary, _primaryLabel, _primaryStateLabel, _primaryButton);
            ApplySlot(HudActionSlotId.Secondary, _secondaryLabel, _secondaryStateLabel, _secondaryButton);

            if (_outcomeLabel != null)
            {
                _outcomeLabel.text = string.IsNullOrEmpty(_viewModel.OutcomeText)
                    ? "Outcome: -"
                    : $"Outcome: {_viewModel.OutcomeText}";
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = string.IsNullOrEmpty(_viewModel.FeedbackText)
                    ? string.Empty
                    : $"Feedback: {_viewModel.FeedbackText}";
            }
        }

        private void ApplySlot(
            HudActionSlotId slotId,
            Text label,
            Text stateLabel,
            Button button)
        {
            var hasSlot = TryGetSlot(slotId, out var slotViewModel);
            if (label != null)
            {
                label.text = hasSlot ? slotViewModel.LabelText : string.Empty;
            }

            if (stateLabel != null)
            {
                stateLabel.text = hasSlot ? slotViewModel.StateText : string.Empty;
            }

            if (button != null)
            {
                button.interactable = hasSlot && slotViewModel.IsInteractive;

                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    image.color = hasSlot && slotViewModel.IsHighlighted
                        ? new Color(0.32f, 0.42f, 0.22f, 1f)
                        : new Color(0.20f, 0.25f, 0.34f, 1f);
                }
            }
        }

        private bool TryGetSlot(HudActionSlotId slotId, out ActionSlotViewModel slotViewModel)
        {
            var slots = _viewModel?.Slots;
            if (slots != null)
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i].SlotId == slotId)
                    {
                        slotViewModel = slots[i];
                        return true;
                    }
                }
            }

            slotViewModel = default;
            return false;
        }
    }
}
