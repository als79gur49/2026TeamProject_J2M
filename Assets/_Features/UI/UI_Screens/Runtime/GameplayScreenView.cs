using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Screens
{
    public sealed class GameplayScreenView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _helpButton;
        [SerializeField] private Button _objectiveButton;
        [SerializeField] private Text _titleLabel;

        public event Action HelpRequested;

        public event Action ObjectivesRequested;

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

        public void Configure(GameObject root, Text titleLabel, Button helpButton, Button objectiveButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _helpButton = helpButton;
            _objectiveButton = objectiveButton;

            _helpButton.onClick.RemoveListener(ClickHelp);
            _objectiveButton.onClick.RemoveListener(ClickObjectives);
            _helpButton.onClick.AddListener(ClickHelp);
            _objectiveButton.onClick.AddListener(ClickObjectives);

            RefreshView();
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

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Gameplay Screen";
            }
        }
    }
}
