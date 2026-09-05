using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Popups
{
    public readonly struct PauseStagePreviewSelection
    {
        public PauseStagePreviewSelection(
            string stageKey,
            LocalizedTextDescriptor displayNameDescriptor,
            Sprite previewSprite)
        {
            StageKey = stageKey ?? string.Empty;
            DisplayNameDescriptor = displayNameDescriptor;
            PreviewSprite = previewSprite;
        }

        public string StageKey { get; }

        public LocalizedTextDescriptor DisplayNameDescriptor { get; }

        public Sprite PreviewSprite { get; }
    }

    public sealed class PauseProgressionStripView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _content;
        [SerializeField] private TMP_Text _stageNameLabel;
        [SerializeField] private PauseStagePreviewCatalog _previewCatalog;
        [SerializeField] private PauseProgressionMarkerView _stageMarkerTemplate;
        [SerializeField, Range(0f, 1f)] private float _focusViewportRatio = 0.5f;

        private readonly List<PauseProgressionMarkerView> _markers = new();
        private PauseProgressionViewModel _viewModel;
        private int _currentIndex = -1;
        private int _selectedIndex = -1;

        public event Action ProgressionInteracted;

        public event Action<PauseStagePreviewSelection> PreviewRequested;

        public event Action<LocalizedTextDescriptor> SelectedStageDescriptorChanged;

        public int CurrentIndex => _currentIndex;

        public int SelectedIndex => _selectedIndex;

        public int MarkerCount => _markers.Count;

        public string SelectedStageName => _stageNameLabel != null ? _stageNameLabel.text : string.Empty;

        public TMP_Text StageNameLabel => _stageNameLabel;

        public LocalizedTextDescriptor SelectedStageDescriptor => HasSelection
            ? _viewModel.Markers[_selectedIndex].DisplayNameDescriptor
            : default;

        public bool HasSelection =>
            _viewModel != null &&
            _selectedIndex >= 0 &&
            _selectedIndex < _markers.Count &&
            _selectedIndex < _viewModel.Markers.Count;

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
                _stageMarkerTemplate == null)
            {
                ClearStageName();
                SelectedStageDescriptorChanged?.Invoke(default);
                return;
            }

            _currentIndex = viewModel.CurrentIndex >= 0 && viewModel.CurrentIndex < viewModel.Markers.Count
                ? viewModel.CurrentIndex
                : -1;
            _selectedIndex = _currentIndex >= 0 ? _currentIndex : 0;

            for (var i = 0; i < viewModel.Markers.Count; i++)
            {
                var markerModel = viewModel.Markers[i];
                var marker = Instantiate(_stageMarkerTemplate, _content, worldPositionStays: false);
                marker.name = $"StageImage_{i}";
                marker.gameObject.SetActive(true);
                var authoredFallback = marker.VisualImage != null ? marker.VisualImage.sprite : null;
                var previewSprite = _previewCatalog != null
                    ? _previewCatalog.ResolveOrPlaceholder(markerModel.StageKey)
                    : null;
                marker.Bind(
                    i,
                    previewSprite != null ? previewSprite : authoredFallback,
                    HandleMarkerClicked);
                marker.SetSelected(i == _selectedIndex);
                _markers.Add(marker);
            }

            RebuildLayout();
            ClearStageName();
            ScrollToSelectedIndex();
            SelectedStageDescriptorChanged?.Invoke(SelectedStageDescriptor);
        }

        public bool TryMoveSelectedIndex(int delta)
        {
            if (delta == 0 || _markers.Count == 0 || _selectedIndex < 0)
            {
                return false;
            }

            var next = Mathf.Clamp(_selectedIndex + Math.Sign(delta), 0, _markers.Count - 1);
            return next != _selectedIndex && SelectIndex(next);
        }

        public bool SelectIndex(int index)
        {
            if (_markers.Count == 0 || index < 0 || index >= _markers.Count || index == _selectedIndex)
            {
                return false;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _markers.Count)
            {
                _markers[_selectedIndex].SetSelected(false);
            }

            _selectedIndex = index;
            _markers[_selectedIndex].SetSelected(true);
            RebuildLayout();
            ClearStageName();
            ScrollToSelectedIndex();
            SelectedStageDescriptorChanged?.Invoke(SelectedStageDescriptor);
            return true;
        }

        public bool TryOpenSelectedPreview()
        {
            if (!HasSelection)
            {
                return false;
            }

            var markerModel = _viewModel.Markers[_selectedIndex];
            var previewSprite = _markers[_selectedIndex].VisualImage != null
                ? _markers[_selectedIndex].VisualImage.sprite
                : null;
            PreviewRequested?.Invoke(new PauseStagePreviewSelection(
                markerModel.StageKey,
                markerModel.DisplayNameDescriptor,
                previewSprite));
            return true;
        }

        private void HandleMarkerClicked(int index)
        {
            ProgressionInteracted?.Invoke();
            if (index == _selectedIndex)
            {
                TryOpenSelectedPreview();
                return;
            }

            SelectIndex(index);
        }

        private void RebuildLayout()
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        private void ScrollToSelectedIndex()
        {
            if (_content == null || _markers.Count == 0 || _selectedIndex < 0)
            {
                return;
            }

            var position = _content.anchoredPosition;
            position.x = CalculateTargetContentX(_selectedIndex);
            _content.anchoredPosition = position;
            _scrollRect?.StopMovement();
        }

        private float CalculateTargetContentX(int markerIndex)
        {
            var viewport = ResolveViewport();
            if (viewport == null || _content == null)
            {
                return 0f;
            }

            var marker = _markers[Mathf.Clamp(markerIndex, 0, _markers.Count - 1)].RectTransform;
            var markerCenterX = RectTransformUtility
                .CalculateRelativeRectTransformBounds(_content, marker)
                .center.x;
            var viewportWidth = viewport.rect.width;
            var contentWidth = _content.rect.width;
            var minX = Mathf.Min(0f, viewportWidth - contentWidth);
            var desiredX = viewportWidth * Mathf.Clamp01(_focusViewportRatio) - markerCenterX;
            return Mathf.Clamp(desiredX, minX, 0f);
        }

        private RectTransform ResolveViewport()
        {
            return _scrollRect != null && _scrollRect.viewport != null
                ? _scrollRect.viewport
                : transform as RectTransform;
        }

        private void ClearStageName()
        {
            if (_stageNameLabel != null)
            {
                _stageNameLabel.text = string.Empty;
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
            _selectedIndex = -1;
            ClearStageName();
        }
    }
}
