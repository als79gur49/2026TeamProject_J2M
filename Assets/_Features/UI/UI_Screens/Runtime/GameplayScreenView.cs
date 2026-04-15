using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class GameplayScreenView : MonoBehaviour, IScreenView
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _helpButton;
        [SerializeField] private Button _objectiveButton;
        [SerializeField] private Button _inventoryButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _helpButtonLabel;
        [SerializeField] private Text _objectiveButtonLabel;
        [SerializeField] private Text _inventoryButtonLabel;
        [SerializeField] private Text _settingsButtonLabel;

        public event Action HelpRequested;

        public event Action ObjectivesRequested;

        public event Action InventoryRequested;

        public event Action SettingsRequested;

        private GameplayScreenViewModel _viewModel;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        private bool _isVisible;

        public void Configure(
            GameObject root,
            Text titleLabel,
            Button helpButton,
            Button objectiveButton,
            Button inventoryButton,
            Button settingsButton,
            Text helpButtonLabel,
            Text objectiveButtonLabel,
            Text inventoryButtonLabel,
            Text settingsButtonLabel)
        {
            _root = root;
            _titleLabel = titleLabel;
            _helpButton = helpButton;
            _objectiveButton = objectiveButton;
            _inventoryButton = inventoryButton;
            _settingsButton = settingsButton;
            _helpButtonLabel = helpButtonLabel;
            _objectiveButtonLabel = objectiveButtonLabel;
            _inventoryButtonLabel = inventoryButtonLabel;
            _settingsButtonLabel = settingsButtonLabel;

            _helpButton.onClick.RemoveListener(ClickHelp);
            _objectiveButton.onClick.RemoveListener(ClickObjectives);
            _inventoryButton.onClick.RemoveListener(ClickInventory);
            _settingsButton.onClick.RemoveListener(ClickSettings);
            _helpButton.onClick.AddListener(ClickHelp);
            _objectiveButton.onClick.AddListener(ClickObjectives);
            _inventoryButton.onClick.AddListener(ClickInventory);
            _settingsButton.onClick.AddListener(ClickSettings);

            RefreshView();
        }

        public void Bind(GameplayScreenViewModel viewModel)
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

        public void SetIsCurrent(bool isCurrent)
        {
            IsVisible = isCurrent;
        }

        public void ClickHelp()
        {
            if (!IsVisible)
            {
                return;
            }

            HelpRequested?.Invoke();
        }

        public void ClickObjectives()
        {
            if (!IsVisible)
            {
                return;
            }

            ObjectivesRequested?.Invoke();
        }

        public void ClickInventory()
        {
            if (!IsVisible)
            {
                return;
            }

            InventoryRequested?.Invoke();
        }

        public void ClickSettings()
        {
            if (!IsVisible)
            {
                return;
            }

            SettingsRequested?.Invoke();
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
                _root.SetActive(IsVisible);
            }

            if (_viewModel == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = _viewModel.TitleText;
            }

            if (_helpButtonLabel != null)
            {
                _helpButtonLabel.text = _viewModel.HelpLabel;
            }

            if (_objectiveButtonLabel != null)
            {
                _objectiveButtonLabel.text = _viewModel.ObjectivesLabel;
            }

            if (_inventoryButtonLabel != null)
            {
                _inventoryButtonLabel.text = _viewModel.InventoryLabel;
            }

            if (_settingsButtonLabel != null)
            {
                _settingsButtonLabel.text = _viewModel.SettingsLabel;
            }
        }
    }
}
