using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PausePopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Button _resumeButton;

        public event Action ResumeRequested;

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

        public void Configure(GameObject root, Text titleLabel, Text descriptionLabel, Button resumeButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _descriptionLabel = descriptionLabel;
            _resumeButton = resumeButton;

            _resumeButton.onClick.RemoveListener(ClickResume);
            _resumeButton.onClick.AddListener(ClickResume);

            RefreshView();
        }

        public void ClickResume()
        {
            if (!IsVisible)
            {
                return;
            }

            ResumeRequested?.Invoke();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "Paused";
            }

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = "Pausing modal popup";
            }
        }
    }
}
