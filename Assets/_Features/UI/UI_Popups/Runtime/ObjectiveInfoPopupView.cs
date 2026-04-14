using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class ObjectiveInfoPopupView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _bodyLabel;
        [SerializeField] private Button _closeButton;

        private bool _isVisible;

        public event Action CloseRequested;

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                RefreshView();
            }
        }

        public string TitleText { get; private set; } = string.Empty;

        public string BodyText { get; private set; } = string.Empty;

        public void Configure(GameObject root, Text titleLabel, Text bodyLabel, Button closeButton)
        {
            _root = root;
            _titleLabel = titleLabel;
            _bodyLabel = bodyLabel;
            _closeButton = closeButton;

            _closeButton.onClick.RemoveListener(ClickClose);
            _closeButton.onClick.AddListener(ClickClose);

            RefreshView();
        }

        public void SetContent(string titleText, string bodyText)
        {
            TitleText = titleText ?? string.Empty;
            BodyText = bodyText ?? string.Empty;
            RefreshView();
        }

        public void ClickClose()
        {
            if (!IsVisible)
            {
                return;
            }

            CloseRequested?.Invoke();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(IsVisible);
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = TitleText;
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = BodyText;
            }
        }
    }
}
