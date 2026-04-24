using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class InventoryActionView : MonoBehaviour
    {
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Button _secondaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private TMP_Text _secondaryButtonLabel;
        [SerializeField] private TMP_Text _primaryStateLabel;
        [SerializeField] private TMP_Text _secondaryStateLabel;
        [SerializeField] private TMP_Text _feedbackLabel;

        private InventoryActionViewModel _viewModel;

        public event Action PrimaryActionRequested;

        public event Action SecondaryActionRequested;

        public string FeedbackText => _feedbackLabel != null ? _feedbackLabel.text : string.Empty;

        public string PrimaryStateText => _primaryStateLabel != null ? _primaryStateLabel.text : string.Empty;

        public void Bind(InventoryActionViewModel viewModel)
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

        public void ClickPrimaryAction()
        {
            PrimaryActionRequested?.Invoke();
        }

        public void ClickSecondaryAction()
        {
            SecondaryActionRequested?.Invoke();
        }

        private void OnEnable()
        {
            RebindButton(_primaryButton, ClickPrimaryAction);
            RebindButton(_secondaryButton, ClickSecondaryAction);
            RefreshView();
        }

        private void OnDisable()
        {
            ClearButton(_primaryButton);
            ClearButton(_secondaryButton);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateSerializedReference(_primaryButton, nameof(_primaryButton));
            ValidateSerializedReference(_secondaryButton, nameof(_secondaryButton));
            ValidateSerializedReference(_primaryButtonLabel, nameof(_primaryButtonLabel));
            ValidateSerializedReference(_secondaryButtonLabel, nameof(_secondaryButtonLabel));
            ValidateSerializedReference(_primaryStateLabel, nameof(_primaryStateLabel));
            ValidateSerializedReference(_secondaryStateLabel, nameof(_secondaryStateLabel));
            ValidateSerializedReference(_feedbackLabel, nameof(_feedbackLabel));
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
            if (_viewModel == null)
            {
                return;
            }

            if (_primaryButton != null)
            {
                _primaryButton.gameObject.SetActive(_viewModel.IsPrimaryVisible);
                _primaryButton.interactable = _viewModel.IsPrimaryEnabled;
            }

            if (_secondaryButton != null)
            {
                _secondaryButton.gameObject.SetActive(_viewModel.IsSecondaryVisible);
                _secondaryButton.interactable = _viewModel.IsSecondaryEnabled;
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel.PrimaryLabelText;
            }

            if (_secondaryButtonLabel != null)
            {
                _secondaryButtonLabel.text = _viewModel.SecondaryLabelText;
            }

            if (_primaryStateLabel != null)
            {
                _primaryStateLabel.text = _viewModel.PrimaryStateText;
            }

            if (_secondaryStateLabel != null)
            {
                _secondaryStateLabel.text = _viewModel.SecondaryStateText;
            }

            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = _viewModel.FeedbackText;
            }
        }

        private static void RebindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void ClearButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
        }

#if UNITY_EDITOR
        private void ValidateSerializedReference(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
            {
                Debug.LogWarning($"{nameof(InventoryActionView)} on '{name}' is missing serialized reference '{fieldName}'.", this);
            }
        }
#endif
    }
}
