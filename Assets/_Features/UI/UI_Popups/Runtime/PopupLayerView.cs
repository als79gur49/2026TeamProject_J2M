using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PopupLayerView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _backdropCanvasGroup;
        [SerializeField] private Image _backdropImage;
        [SerializeField] private Button _backdropButton;
        [SerializeField] private RectTransform _contentRoot;

        private bool _isVisible;

        public event Action BackdropClicked;

        public RectTransform ContentRoot => _contentRoot;

        public bool IsDimVisible { get; private set; }

        public bool BlocksLowerLayerPointer { get; private set; }

        public PopupBackdropMode BackdropMode { get; private set; }

        public void Configure(
            GameObject root,
            CanvasGroup backdropCanvasGroup,
            Image backdropImage,
            Button backdropButton,
            RectTransform contentRoot)
        {
            _root = root;
            _backdropCanvasGroup = backdropCanvasGroup;
            _backdropImage = backdropImage;
            _backdropButton = backdropButton;
            _contentRoot = contentRoot;

            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(HandleBackdropClicked);
                _backdropButton.onClick.AddListener(HandleBackdropClicked);
            }

            RefreshView();
        }

        public T FindPopupView<T>() where T : Component
        {
            if (_contentRoot == null)
            {
                return null;
            }

            return _contentRoot.GetComponentInChildren<T>(true);
        }

        public void SetState(
            bool isVisible,
            bool showDim,
            bool blocksLowerLayerPointer,
            PopupBackdropMode backdropMode)
        {
            _isVisible = isVisible;
            IsDimVisible = showDim;
            BlocksLowerLayerPointer = blocksLowerLayerPointer;
            BackdropMode = backdropMode;
            RefreshView();
        }

        private void HandleBackdropClicked()
        {
            if (BackdropMode == PopupBackdropMode.None)
            {
                return;
            }

            BackdropClicked?.Invoke();
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(_isVisible);
            }

            if (_backdropImage != null)
            {
                _backdropImage.color = new Color(0f, 0f, 0f, IsDimVisible ? 0.58f : 0f);
                _backdropImage.raycastTarget = BlocksLowerLayerPointer || BackdropMode != PopupBackdropMode.None;
            }

            if (_backdropCanvasGroup != null)
            {
                _backdropCanvasGroup.alpha = IsDimVisible ? 1f : 0f;
                _backdropCanvasGroup.interactable = BackdropMode != PopupBackdropMode.None;
                _backdropCanvasGroup.blocksRaycasts = BlocksLowerLayerPointer || BackdropMode != PopupBackdropMode.None;
            }
        }
    }
}
