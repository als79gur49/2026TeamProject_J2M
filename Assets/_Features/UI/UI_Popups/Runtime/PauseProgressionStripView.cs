using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public sealed class PauseProgressionStripView : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private PauseProgressionMarkerView _groupStartMarkerTemplate;
        [SerializeField] private PauseProgressionMarkerView _stageMarkerTemplate;
        [SerializeField] private Color _currentMarkerColor = new(0.266f, 0.896f, 0.855f, 1f);
        [SerializeField, Range(0f, 1f)] private float _focusViewportRatio = 0.4f;
        [SerializeField, Min(0f)] private float _scrollDurationSeconds = 0.12f;

        private readonly List<PauseProgressionMarkerView> _markers = new();
        private PauseProgressionViewModel _viewModel;
        private Tween _scrollTween;
        private int _currentIndex = -1;
        private int _viewedIndex = -1;
        private bool _hasNavigationFocus;

        public int CurrentIndex => _currentIndex;

        public int ViewedIndex => _viewedIndex;

        public int MarkerCount => _markers.Count;

        private void OnDisable()
        {
            StopScrollTween();
            SetNavigationFocus(false);
        }

        private void OnDestroy()
        {
            StopScrollTween();
        }

        public void Bind(PauseProgressionViewModel viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            _viewModel = viewModel;
            StopScrollTween();
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
                _viewedIndex = -1;
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
                marker.SetCurrent(i == viewModel.CurrentIndex, _currentMarkerColor);
                marker.SetViewed(false);
                _markers.Add(marker);
            }

            _currentIndex = viewModel.CurrentIndex >= 0 && viewModel.CurrentIndex < _markers.Count
                ? viewModel.CurrentIndex
                : -1;
            SetViewedIndex(_currentIndex >= 0 ? _currentIndex : 0);
            RebuildLayout();
            ScrollToViewedIndex(immediate: true);
        }

        public bool TryMoveViewedIndex(int delta)
        {
            if (delta == 0 || _markers.Count == 0)
            {
                return false;
            }

            var next = Mathf.Clamp(_viewedIndex + Math.Sign(delta), 0, _markers.Count - 1);
            if (next == _viewedIndex)
            {
                return false;
            }

            SetViewedIndex(next);
            ScrollToViewedIndex(immediate: false);
            return true;
        }

        public void SetNavigationFocus(bool hasFocus)
        {
            _hasNavigationFocus = hasFocus;
            if (_viewedIndex >= 0 && _viewedIndex < _markers.Count)
            {
                _markers[_viewedIndex].SetViewed(hasFocus);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            StopScrollTween();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_scrollRect != null)
            {
                _scrollRect.StopMovement();
            }

            if (_markers.Count == 0 || !CanScroll())
            {
                return;
            }

            SetViewedIndex(FindNearestMarkerIndex());
            ScrollToViewedIndex(immediate: false);
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void ScrollToViewedIndex(bool immediate)
        {
            if (_content == null || _markers.Count == 0 || _viewedIndex < 0)
            {
                return;
            }

            var targetX = CalculateTargetContentX(_viewedIndex);
            StopScrollTween();
            if (immediate || _scrollDurationSeconds <= 0f || !isActiveAndEnabled)
            {
                var position = _content.anchoredPosition;
                position.x = targetX;
                _content.anchoredPosition = position;
                return;
            }

            _scrollTween = _content
                .DOAnchorPosX(targetX, _scrollDurationSeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
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

        private int FindNearestMarkerIndex()
        {
            var viewport = ResolveViewport();
            if (viewport == null || _content == null || _markers.Count == 0)
            {
                return 0;
            }

            var focusInContent = -_content.anchoredPosition.x +
                                 viewport.rect.width * Mathf.Clamp01(_focusViewportRatio);
            var nearestIndex = 0;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i].RectTransform;
                var distance = Mathf.Abs(marker.anchoredPosition.x - focusInContent);
                if (distance < nearestDistance)
                {
                    nearestIndex = i;
                    nearestDistance = distance;
                }
            }

            return nearestIndex;
        }

        private bool CanScroll()
        {
            var viewport = ResolveViewport();
            return viewport != null && _content != null && _content.rect.width > viewport.rect.width + 0.01f;
        }

        private RectTransform ResolveViewport()
        {
            return _scrollRect != null && _scrollRect.viewport != null
                ? _scrollRect.viewport
                : transform as RectTransform;
        }

        private void SetViewedIndex(int index)
        {
            if (_viewedIndex >= 0 && _viewedIndex < _markers.Count)
            {
                _markers[_viewedIndex].SetViewed(false);
            }

            _viewedIndex = _markers.Count == 0
                ? -1
                : Mathf.Clamp(index, 0, _markers.Count - 1);
            if (_viewedIndex >= 0)
            {
                _markers[_viewedIndex].SetViewed(_hasNavigationFocus);
            }
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
            _viewedIndex = -1;
        }

        private void StopScrollTween()
        {
            _scrollTween?.Kill();
            _scrollTween = null;
        }
    }
}
