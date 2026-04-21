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
        [SerializeField] private Slider _primaryCooldownSlider;
        [SerializeField] private Text _primaryStateLabel;
        [SerializeField] private Text _secondaryLabel;
        [SerializeField] private Slider _secondaryCooldownSlider;
        [SerializeField] private Text _secondaryStateLabel;
        [SerializeField] private Text _outcomeLabel;
        private ActionBarViewModel _viewModel;

        public ActionBarViewModel ViewModel => _viewModel;

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_root, nameof(_root));
            ValidateSerializedReference(_titleLabel, nameof(_titleLabel));
            ValidateSerializedReference(_primaryLabel, nameof(_primaryLabel));
            ValidateSerializedReference(_primaryCooldownSlider, nameof(_primaryCooldownSlider));
            ValidateSerializedReference(_primaryStateLabel, nameof(_primaryStateLabel));
            ValidateSerializedReference(_secondaryLabel, nameof(_secondaryLabel));
            ValidateSerializedReference(_secondaryCooldownSlider, nameof(_secondaryCooldownSlider));
            ValidateSerializedReference(_secondaryStateLabel, nameof(_secondaryStateLabel));
            ValidateSerializedReference(_outcomeLabel, nameof(_outcomeLabel));
        }
#endif

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

            ApplySlot(HudActionSlotId.Primary, _primaryLabel, _primaryCooldownSlider, _primaryStateLabel);
            ApplySlot(HudActionSlotId.Secondary, _secondaryLabel, _secondaryCooldownSlider, _secondaryStateLabel);

            if (_outcomeLabel != null)
            {
                _outcomeLabel.text = string.IsNullOrEmpty(_viewModel.OutcomeText)
                    ? "Outcome: -"
                    : $"Outcome: {_viewModel.OutcomeText}";
            }
        }

        private void ApplySlot(
            HudActionSlotId slotId,
            Text label,
            Slider cooldownSlider,
            Text stateLabel)
        {
            var hasSlot = TryGetSlot(slotId, out var slotViewModel);
            if (label != null)
            {
                label.text = hasSlot ? slotViewModel.LabelText : string.Empty;
            }

            if (cooldownSlider != null)
            {
                cooldownSlider.value = hasSlot ? slotViewModel.CooldownNormalized : 0f;
            }

            if (stateLabel != null)
            {
                stateLabel.text = hasSlot ? slotViewModel.StateText : string.Empty;
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

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(ActionBarView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
