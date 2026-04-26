using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class SaveSlotCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _stageLabel;
        [SerializeField] private TMP_Text _chancesLabel;
        [SerializeField] private TMP_Text _deathsLabel;
        [SerializeField] private TMP_Text _lastPlayedLabel;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private TMP_Text _primaryButtonLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _deleteButton;

        private SaveSlotCardViewModel _viewModel;

        public event Action<SaveSlotIntent> IntentRequested;

        public void Bind(SaveSlotCardViewModel viewModel)
        {
            _viewModel = viewModel;
            Refresh();
        }

        private void OnEnable()
        {
            Rebind(_primaryButton, HandlePrimaryClicked);
            Rebind(_restartButton, HandleRestartClicked);
            Rebind(_deleteButton, HandleDeleteClicked);
        }

        private void OnDisable()
        {
            Unbind(_primaryButton, HandlePrimaryClicked);
            Unbind(_restartButton, HandleRestartClicked);
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

        private void HandleRestartClicked()
        {
            if (_viewModel == null)
            {
                return;
            }

            IntentRequested?.Invoke(new SaveSlotIntent(_viewModel.SlotNumber, SaveSlotIntentKind.Restart));
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

            if (_stageLabel != null)
            {
                _stageLabel.text = _viewModel?.StageText ?? string.Empty;
            }

            if (_chancesLabel != null)
            {
                _chancesLabel.text = _viewModel?.ChancesText ?? string.Empty;
            }

            if (_deathsLabel != null)
            {
                _deathsLabel.text = _viewModel?.DeathsText ?? string.Empty;
            }

            if (_lastPlayedLabel != null)
            {
                _lastPlayedLabel.text = _viewModel?.LastPlayedText ?? string.Empty;
            }

            if (_primaryButtonLabel != null)
            {
                _primaryButtonLabel.text = _viewModel?.PrimaryActionText ?? string.Empty;
            }

            if (_restartButton != null)
            {
                _restartButton.gameObject.SetActive(_viewModel != null && _viewModel.ShowRestart);
            }

            if (_deleteButton != null)
            {
                _deleteButton.gameObject.SetActive(_viewModel != null && _viewModel.ShowDelete);
            }
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
