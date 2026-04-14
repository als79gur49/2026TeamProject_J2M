using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class HelpScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Button _backButton;

        public event Action BackRequested;

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

        public void Configure(GameObject root, Text titleLabel, Text descriptionLabel, Button backButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _descriptionLabel = descriptionLabel;
            _backButton = backButton;

            _backButton.onClick.RemoveListener(ClickBack);
            _backButton.onClick.AddListener(ClickBack);

            RefreshView();
        }

        public void ClickBack()
        {
            if (!IsVisible)
            {
                return;
            }

            BackRequested?.Invoke();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Help Screen";
            }

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = "Flow validation screen";
            }
        }
    }
}
