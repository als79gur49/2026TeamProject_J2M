using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Composition
{
    public sealed class UiArchitectureDiagnosticsOverlayView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _summaryLabel;
        [SerializeField] private GameObject _detailRoot;
        [SerializeField] private Text _detailLabel;

        private UiArchitectureDiagnosticsTracker _tracker;
        private bool _isExpanded;
        private bool _isOverlayVisible;
        private bool _isSupported;
        private UiArchitectureDiagnosticsSnapshot _snapshot;

        public string DetailText => _detailLabel != null ? _detailLabel.text : string.Empty;

        public bool IsDetailsVisible => _detailRoot != null && _detailRoot.activeSelf;

        public bool IsExpanded => _isExpanded;

        public bool IsOverlayVisible => _isOverlayVisible;

        public bool IsSupported => _isSupported;

        public string SummaryText => _summaryLabel != null ? _summaryLabel.text : string.Empty;

        public void Bind(UiArchitectureDiagnosticsTracker tracker)
        {
            if (_tracker != null)
            {
                _tracker.SnapshotChanged -= HandleSnapshotChanged;
            }

            _tracker = tracker;
            if (_tracker != null)
            {
                _tracker.SnapshotChanged += HandleSnapshotChanged;
                _snapshot = _tracker.CurrentSnapshot;
            }

            RefreshView();
        }

        public void Configure(
            GameObject root,
            CanvasGroup canvasGroup,
            Text titleLabel,
            Text summaryLabel,
            GameObject detailRoot,
            Text detailLabel)
        {
            _root = root;
            _canvasGroup = canvasGroup;
            _titleLabel = titleLabel;
            _summaryLabel = summaryLabel;
            _detailRoot = detailRoot;
            _detailLabel = detailLabel;
            RefreshView();
        }

        public void SetSupported(bool isSupported)
        {
            _isSupported = isSupported;
            if (!_isSupported)
            {
                _isOverlayVisible = false;
                _isExpanded = false;
            }

            RefreshView();
        }

        public void ToggleExpanded()
        {
            if (!_isSupported || !_isOverlayVisible)
            {
                return;
            }

            _isExpanded = !_isExpanded;
            RefreshView();
        }

        public void ToggleVisibility()
        {
            if (!_isSupported)
            {
                return;
            }

            _isOverlayVisible = !_isOverlayVisible;
            if (!_isOverlayVisible)
            {
                _isExpanded = false;
            }

            RefreshView();
        }

        private static string BuildDetailText(UiArchitectureDiagnosticsSnapshot snapshot)
        {
            var recentEvents = snapshot.RecentEvents.Count == 0
                ? "None"
                : string.Join("\n", snapshot.RecentEvents.Select(evt => $"- {evt}"));

            return string.Join("\n", new[]
            {
                $"Screen Instance: {FormatNullable(snapshot.CurrentScreenInstanceId)}",
                $"Top Popup Instance: {FormatNullable(snapshot.TopPopupInstanceId)}",
                $"Popup Policy: {snapshot.PopupPolicySummaryText}",
                $"Inventory Summary: {snapshot.InventorySummaryText}",
                "Recent Events:",
                recentEvents,
            });
        }

        private static string BuildSummaryText(UiArchitectureDiagnosticsSnapshot snapshot)
        {
            return string.Join("\n", new[]
            {
                $"Screen: {snapshot.CurrentScreenIdText}",
                $"Back Stack: {snapshot.BackStackDepth}",
                $"Popup: {snapshot.TopPopupIdText}",
                $"Popup Depth: {snapshot.PopupDepth}",
                $"Dismissibility: {snapshot.PopupDismissibilityText}",
                $"HUD: {(snapshot.IsHudVisible ? "Visible" : "Hidden")} | {(snapshot.IsHudReadOnly ? "Read Only" : "Interactive")}",
                $"Block Source: {snapshot.BlockSourceCategoryText}",
                $"Mapped Tick: {snapshot.MappedTickIndex}",
                $"Latest Event: {snapshot.LatestMappedEventSummary}",
            });
        }

        private static string FormatNullable(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "None";
        }

        private void HandleSnapshotChanged(UiArchitectureDiagnosticsSnapshot snapshot)
        {
            _snapshot = snapshot;
            RefreshView();
        }

        private void OnDestroy()
        {
            if (_tracker != null)
            {
                _tracker.SnapshotChanged -= HandleSnapshotChanged;
            }
        }

        private void RefreshView()
        {
            if (_root != null)
            {
                _root.SetActive(_isSupported && _isOverlayVisible);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isSupported && _isOverlayVisible ? 1f : 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_titleLabel != null)
            {
                _titleLabel.text = "UI Diagnostics";
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = BuildSummaryText(_snapshot);
            }

            if (_detailRoot != null)
            {
                _detailRoot.SetActive(_isSupported && _isOverlayVisible && _isExpanded);
            }

            if (_detailLabel != null)
            {
                _detailLabel.text = BuildDetailText(_snapshot);
            }
        }
    }
}
