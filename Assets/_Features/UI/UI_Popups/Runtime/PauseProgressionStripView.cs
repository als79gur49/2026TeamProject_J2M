using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PauseProgressionStripView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private PauseProgressionMarkerView _groupStartMarkerTemplate;
        [SerializeField] private PauseProgressionMarkerView _stageMarkerTemplate;
        [SerializeField] private Color _neutralMarkerColor = new(0.18f, 0.585f, 0.784f, 0.75f);
        [SerializeField] private Color _previousMarkerColor = new(0.181f, 0.718f, 0.783f, 1f);
        [SerializeField] private Color _currentMarkerColor = new(0.266f, 0.896f, 0.855f, 1f);
        [SerializeField] private Color _upcomingMarkerColor = new(0.2f, 0.25f, 0.34f, 0.58f);
        [SerializeField, Range(0f, 1f)] private float _focusViewportRatio = 0.4f;

        private readonly List<PauseProgressionMarkerView> _markers = new();
        private PauseProgressionViewModel _viewModel;
        private int _currentIndex = -1;

        public int CurrentIndex => _currentIndex;

        public int MarkerCount => _markers.Count;

        public void Bind(PauseProgressionViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            _viewModel = viewModel;
            ClearMarkers();

            var isVisible = viewModel != null && viewModel.IsVisible && viewModel.Markers.Count > 0;
            if (_root != null)
            {
                _root.gameObject.SetActive(isVisible);
            }

            if (!isVisible ||
                _content == null ||
                _groupStartMarkerTemplate == null ||
                _stageMarkerTemplate == null)
            {
                _currentIndex = -1;
                return;
            }

            for (var i = 0; i < viewModel.Markers.Count; i++)
            {
                var markerModel = viewModel.Markers[i];
                var template = markerModel.Kind == PauseProgressionMarkerKind.GroupStart
                    ? _groupStartMarkerTemplate
                    : _stageMarkerTemplate;
                var marker = Instantiate(template, _content, worldPositionStays: false);
                marker.name = $"Marker_{i}_{markerModel.Kind}";
                marker.gameObject.SetActive(true);
                marker.ApplyState(
                    markerModel.State,
                    _neutralMarkerColor,
                    _previousMarkerColor,
                    _currentMarkerColor,
                    _upcomingMarkerColor);
                _markers.Add(marker);
            }

            _currentIndex = viewModel.CurrentIndex >= 0 && viewModel.CurrentIndex < _markers.Count
                ? viewModel.CurrentIndex
                : -1;
            RebuildLayout();
            ScrollToCurrentIndex();
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void ScrollToCurrentIndex()
        {
            if (_content == null || _markers.Count == 0 || _currentIndex < 0)
            {
                return;
            }

            var position = _content.anchoredPosition;
            position.x = CalculateTargetContentX(_currentIndex);
            _content.anchoredPosition = position;
        }

        private float CalculateTargetContentX(int markerIndex)
        {
            var viewport = ResolveViewport();
            if (viewport == null || _content == null)
            {
                return 0f;
            }

            var marker = _markers[Mathf.Clamp(markerIndex, 0, _markers.Count - 1)].RectTransform;
            var viewportWidth = viewport.rect.width;
            var contentWidth = _content.rect.width;
            var minX = Mathf.Min(0f, viewportWidth - contentWidth);
            var desiredX = viewportWidth * Mathf.Clamp01(_focusViewportRatio) - marker.anchoredPosition.x;
            return Mathf.Clamp(desiredX, minX, 0f);
        }

        private RectTransform ResolveViewport()
        {
            return _scrollRect != null && _scrollRect.viewport != null
                ? _scrollRect.viewport
                : transform as RectTransform;
        }

        private void ClearMarkers()
        {
            for (var i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                if (marker == null)
                {
                    continue;
                }

                marker.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(marker.gameObject);
                }
                else
                {
                    DestroyImmediate(marker.gameObject);
                }
            }

            _markers.Clear();
            _currentIndex = -1;
        }
    }
}
