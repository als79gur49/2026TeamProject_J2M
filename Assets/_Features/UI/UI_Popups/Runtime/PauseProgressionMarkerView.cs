using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    [DisallowMultipleComponent]
    public sealed class PauseProgressionMarkerView : MonoBehaviour
    {
        [SerializeField] private Image _visualImage;
        [SerializeField] private GameObject _currentFrame;

        public RectTransform RectTransform => transform as RectTransform;

        public Image VisualImage => _visualImage;

        public GameObject CurrentFrame => _currentFrame;

        public PauseProgressionMarkerState State { get; private set; } = PauseProgressionMarkerState.Neutral;

        public void ApplyState(
            PauseProgressionMarkerState state,
            Color neutralColor,
            Color previousColor,
            Color currentColor,
            Color upcomingColor)
        {
            State = state;
            if (_visualImage != null)
            {
                _visualImage.color = state switch
                {
                    PauseProgressionMarkerState.Previous => previousColor,
                    PauseProgressionMarkerState.Current => currentColor,
                    PauseProgressionMarkerState.Upcoming => upcomingColor,
                    _ => neutralColor,
                };
            }

            if (_currentFrame != null)
            {
                _currentFrame.SetActive(state == PauseProgressionMarkerState.Current);
            }
        }
    }
}
