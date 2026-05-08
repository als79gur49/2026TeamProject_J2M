using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotCardView : MonoBehaviour
    {
        private const string MissingAuthoredStructureMessage =
            "MainMenu save slot card is missing required authored UI references. Repair MainMenuScreen.prefab so each SaveSlotCardView owns its labels and action buttons.";

        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _stageLabel;
        [SerializeField] private TMP_Text _chancesLabel;
        [SerializeField] private TMP_Text _deathsLabel;
        [SerializeField] private TMP_Text _lastPlayedLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private Button _deleteButton;

        private SaveSlotCardViewModel _viewModel;

        public event Action<SaveSlotIntent> IntentRequested;

        public void Bind(SaveSlotCardViewModel viewModel)
        {
            _viewModel = viewModel;
            ValidateAuthoredStructureOrThrow();
            Refresh();
        }

        public void ValidateAuthoredStructureOrThrow()
        {
            if (_titleLabel == null ||
                _statusLabel == null ||
                _stageLabel == null ||
                _chancesLabel == null ||
                _deathsLabel == null ||
                _lastPlayedLabel == null ||
                _primaryButton == null ||
                _primaryButtonLabel == null ||
                _deleteButton == null)
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }

            if (!IsOwnedByCard(_titleLabel.transform) ||
                !IsOwnedByCard(_statusLabel.transform) ||
                !IsOwnedByCard(_stageLabel.transform) ||
                !IsOwnedByCard(_chancesLabel.transform) ||
                !IsOwnedByCard(_deathsLabel.transform) ||
                !IsOwnedByCard(_lastPlayedLabel.transform) ||
                !IsOwnedByCard(_primaryButton.transform) ||
                !IsOwnedByCard(_primaryButtonLabel.transform) ||
                !IsOwnedByCard(_deleteButton.transform))
            {
                throw new InvalidOperationException(MissingAuthoredStructureMessage);
            }
        }

        private void OnEnable()
        {
            ValidateAuthoredStructureOrThrow();
            Rebind(_primaryButton, HandlePrimaryClicked);
            Rebind(_deleteButton, HandleDeleteClicked);
        }

        private void OnDisable()
        {
            Unbind(_primaryButton, HandlePrimaryClicked);
            Unbind(_deleteButton, HandleDeleteClicked);
        }

        private void HandlePrimaryClicked()
        {
            if (_viewModel == null || _viewModel.PrimaryIntentKind == SaveSlotIntentKind.None)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, _viewModel.PrimaryIntentKind));
        }

        private void HandleDeleteClicked()
        {
            if (_viewModel == null)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, SaveSlotIntentKind.Delete));
        }

        private void Refresh()
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel?.TitleText ?? string.Empty;
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = _viewModel?.StatusText ?? string.Empty;
            }

            SetOptionalLabel(_stageLabel, _viewModel?.StageText);
            SetOptionalLabel(_chancesLabel, _viewModel?.ChancesText);
            SetOptionalLabel(_deathsLabel, _viewModel?.DeathsText);
            SetOptionalLabel(_lastPlayedLabel, _viewModel?.LastPlayedText);

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel?.PrimaryActionText ?? string.Empty;
            }

            if (_primaryButton != null)
            {
                var hasPrimaryIntent = _viewModel != null && _viewModel.PrimaryIntentKind != SaveSlotIntentKind.None;
                _primaryButton.gameObject.SetActive(true);
                _primaryButton.interactable = hasPrimaryIntent;
            }

            if (_deleteButton != null)
            {
                _deleteButton.gameObject.SetActive(true);
                _deleteButton.interactable = _viewModel != null && _viewModel.ShowDelete;
            }
        }

        private static void SetOptionalLabel(TMP_Text label, string text)
        {
            if (label == null)
            {
                return;
            }

            label.text = text ?? string.Empty;
            label.gameObject.SetActive(true);
        }

        private bool IsOwnedByCard(Transform child)
        {
            if (child == null)
            {
                return false;
            }

            return child == transform || child.IsChildOf(transform);
        }

        private static void Rebind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
        }
    }
}
