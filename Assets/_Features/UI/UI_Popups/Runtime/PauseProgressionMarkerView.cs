using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    [DisallowMultipleComponent]
    public sealed class PauseProgressionMarkerView : MonoBehaviour
    {
        [SerializeField] private Image _visualImage;
        [SerializeField] private Button _button;
        [SerializeField] private LayoutElement _layoutElement;
        [SerializeField] private Vector2 _normalImageSize = new(72f, 40.5f);
        [SerializeField] private Vector2 _selectedImageSize = new(144f, 81f);

        private Action<int> _clicked;
        private int _index = -1;
        private bool _isSelected;

        public RectTransform RectTransform => transform as RectTransform;

        public Image VisualImage => _visualImage;

        public LayoutElement LayoutElement => _layoutElement;

        public bool IsSelected => _isSelected;

        public void Bind(
            int index,
            Sprite previewSprite,
            Action<int> clicked)
        {
            UnbindClick();
            _index = index;
            _clicked = clicked;

            if (_visualImage != null)
            {
                _visualImage.sprite = previewSprite;
                _visualImage.preserveAspect = true;
                _visualImage.color = Color.white;
            }

            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
            }
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            var targetSize = isSelected ? _selectedImageSize : _normalImageSize;
            if (_layoutElement != null)
            {
                _layoutElement.preferredWidth = targetSize.x;
                _layoutElement.preferredHeight = _selectedImageSize.y;
            }

            if (RectTransform != null)
            {
                RectTransform.sizeDelta = new Vector2(targetSize.x, _selectedImageSize.y);
            }

            if (_visualImage != null)
            {
                _visualImage.rectTransform.sizeDelta = targetSize;
            }

        }

        private void OnDestroy()
        {
            UnbindClick();
        }

        private void HandleClick()
        {
            _clicked?.Invoke(_index);
        }

        private void UnbindClick()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }

            _clicked = null;
            _index = -1;
        }
    }
}
