using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    [DisallowMultipleComponent]
    public sealed class PauseProgressionMarkerView : MonoBehaviour
    {
        [SerializeField] private Image _visualImage;
        [SerializeField] private GameObject _selectionFrame;

        private Color _authoredVisualColor;
        private bool _hasAuthoredVisualColor;

        public RectTransform RectTransform => transform as RectTransform;

        public Image VisualImage => _visualImage;

        public GameObject SelectionFrame => _selectionFrame;

        public bool IsViewed => _selectionFrame != null && _selectionFrame.activeSelf;

        public void SetCurrent(bool isCurrent, Color currentColor)
        {
            if (_visualImage == null)
            {
                return;
            }

            if (!_hasAuthoredVisualColor)
            {
                _authoredVisualColor = _visualImage.color;
                _hasAuthoredVisualColor = true;
            }

            _visualImage.color = isCurrent
                ? currentColor
                : _authoredVisualColor;
        }

        public void SetViewed(bool isViewed)
        {
            if (_selectionFrame != null)
            {
                _selectionFrame.SetActive(isViewed);
            }
        }
    }
}
