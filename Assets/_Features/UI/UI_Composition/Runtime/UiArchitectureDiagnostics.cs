using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Feature.UI.Application;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Screens;
using UnityEngine;

namespace Game.Feature.UI.Composition
{
    public readonly struct UiArchitectureDiagnosticsSnapshot
    {
        private readonly ReadOnlyCollection<string> _recentEvents;

        public UiArchitectureDiagnosticsSnapshot(
            string currentScreenIdText,
            int? currentScreenInstanceId,
            int backStackDepth,
            string topPopupIdText,
            int? topPopupInstanceId,
            int popupDepth,
            string popupDismissibilityText,
            bool isHudVisible,
            bool isHudReadOnly,
            string blockSourceCategoryText,
            int mappedTickIndex,
            string latestMappedEventSummary,
            string popupPolicySummaryText,
            IEnumerable<string> recentEvents)
        {
            CurrentScreenIdText = currentScreenIdText ?? string.Empty;
            CurrentScreenInstanceId = currentScreenInstanceId;
            BackStackDepth = backStackDepth;
            TopPopupIdText = topPopupIdText ?? string.Empty;
            TopPopupInstanceId = topPopupInstanceId;
            PopupDepth = popupDepth;
            PopupDismissibilityText = popupDismissibilityText ?? string.Empty;
            IsHudVisible = isHudVisible;
            IsHudReadOnly = isHudReadOnly;
            BlockSourceCategoryText = blockSourceCategoryText ?? string.Empty;
            MappedTickIndex = mappedTickIndex;
            LatestMappedEventSummary = latestMappedEventSummary ?? string.Empty;
            PopupPolicySummaryText = popupPolicySummaryText ?? string.Empty;
            _recentEvents = new ReadOnlyCollection<string>(new List<string>(recentEvents ?? Array.Empty<string>()));
        }

        public string CurrentScreenIdText { get; }

        public int? CurrentScreenInstanceId { get; }

        public int BackStackDepth { get; }

        public string TopPopupIdText { get; }

        public int? TopPopupInstanceId { get; }

        public int PopupDepth { get; }

        public string PopupDismissibilityText { get; }

        public bool IsHudVisible { get; }

        public bool IsHudReadOnly { get; }

        public string BlockSourceCategoryText { get; }

        public int MappedTickIndex { get; }

        public string LatestMappedEventSummary { get; }

        public string PopupPolicySummaryText { get; }

        public IReadOnlyList<string> RecentEvents => _recentEvents != null
            ? _recentEvents
            : Array.Empty<string>();
    }

    public sealed class UiArchitectureDiagnosticsTracker : IDisposable
    {
        private const int RecentEventLimit = 8;

        private readonly Func<bool> _isHudReadOnly;
        private readonly Func<bool> _isHudVisible;
        private readonly PopupController _popupController;
        private readonly IGameplayUiPresentationSource _presentationSource;
        private readonly UIFlowCoordinator _coordinator;
        private readonly List<string> _recentEvents = new List<string>();
        private readonly ScreenController _screenController;
        private string _latestMappedEventSummary = "None";

        public UiArchitectureDiagnosticsTracker(
            IGameplayUiPresentationSource presentationSource,
            UIFlowCoordinator coordinator,
            ScreenController screenController,
            PopupController popupController,
            Func<bool> isHudVisible,
            Func<bool> isHudReadOnly)
        {
            _presentationSource = presentationSource ?? throw new ArgumentNullException(nameof(presentationSource));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _screenController = screenController ?? throw new ArgumentNullException(nameof(screenController));
            _popupController = popupController ?? throw new ArgumentNullException(nameof(popupController));
            _isHudVisible = isHudVisible ?? throw new ArgumentNullException(nameof(isHudVisible));
            _isHudReadOnly = isHudReadOnly ?? throw new ArgumentNullException(nameof(isHudReadOnly));

            _latestMappedEventSummary = BuildTickEventSummary(_presentationSource.CurrentTickEvents);

            _presentationSource.SnapshotChanged += HandlePresentationSnapshotChanged;
            _presentationSource.TickEventsApplied += HandleTickEventsApplied;
            _screenController.StateChanged += HandleScreenStateChanged;
            _popupController.StateChanged += HandlePopupStateChanged;

            RebuildSnapshot();
        }

        public static bool IsRuntimeSupported => UnityEngine.Application.isEditor || Debug.isDebugBuild;

        public event Action<UiArchitectureDiagnosticsSnapshot> SnapshotChanged;

        public UiArchitectureDiagnosticsSnapshot CurrentSnapshot { get; private set; }

        public void Dispose()
        {
            _presentationSource.SnapshotChanged -= HandlePresentationSnapshotChanged;
            _presentationSource.TickEventsApplied -= HandleTickEventsApplied;
            _screenController.StateChanged -= HandleScreenStateChanged;
            _popupController.StateChanged -= HandlePopupStateChanged;
        }

        private void HandlePopupStateChanged()
        {
            AppendEvent($"Popup -> {BuildPopupEventText()}");
            RebuildSnapshot();
        }

        private void HandlePresentationSnapshotChanged(UIPresentationSnapshot _)
        {
            RebuildSnapshot();
        }

        private void HandleScreenStateChanged()
        {
            AppendEvent($"Screen -> {BuildScreenEventText()}");
            RebuildSnapshot();
        }

        private void HandleTickEventsApplied(UITickEventBatch batch)
        {
            _latestMappedEventSummary = BuildTickEventSummary(batch);
            if (batch.HasAnyEvents)
            {
                AppendEvent($"Tick {batch.TickIndex} -> {_latestMappedEventSummary}");
            }

            RebuildSnapshot();
        }

        private void AppendEvent(string eventText)
        {
            if (string.IsNullOrWhiteSpace(eventText))
            {
                return;
            }

            _recentEvents.Insert(0, eventText);
            if (_recentEvents.Count > RecentEventLimit)
            {
                _recentEvents.RemoveAt(_recentEvents.Count - 1);
            }
        }

        private string BuildPopupEventText()
        {
            var topPopup = _popupController.TopPopup;
            return topPopup.HasValue
                ? $"{topPopup.Value.PopupId}#{topPopup.Value.InstanceId.Value} | Depth {_popupController.PopupCount}"
                : $"None | Depth {_popupController.PopupCount}";
        }

        private static string BuildPopupDismissibilityText(PopupEntry? topPopup)
        {
            if (!topPopup.HasValue)
            {
                return "None";
            }

            var segments = new List<string>();
            segments.Add(topPopup.Value.Policy.BackAction switch
            {
                PopupBackAction.Cancel => "Back: Cancel",
                PopupBackAction.Close => "Back: Close",
                _ => "Back: Consume",
            });

            if (topPopup.Value.Policy.BackdropMode != Game.Feature.UI.Popups.PopupBackdropMode.None)
            {
                segments.Add(topPopup.Value.Policy.BackdropMode == Game.Feature.UI.Popups.PopupBackdropMode.CloseTop
                    ? "Backdrop: Close"
                    : "Backdrop: Consume");
            }
            else
            {
                segments.Add("Backdrop: None");
            }

            return string.Join(" | ", segments);
        }

        private string BuildPopupPolicySummaryText(PopupEntry? topPopup)
        {
            if (!topPopup.HasValue)
            {
                return "None";
            }

            return
                $"{topPopup.Value.Policy.PolicyClass} | " +
                $"Dim={(topPopup.Value.Policy.ShowsDim ? "On" : "Off")} | " +
                $"LowerLayers={(topPopup.Value.Policy.BlocksLowerLayers ? "Blocked" : "Open")} | " +
                $"Backdrop={topPopup.Value.Policy.BackdropMode}";
        }

        private string BuildScreenEventText()
        {
            var currentEntry = _screenController.CurrentEntry;
            return currentEntry.HasValue
                ? $"{currentEntry.Value.ScreenId}#{currentEntry.Value.InstanceId.Value} | Back {_screenController.BackStackCount}"
                : $"None | Back {_screenController.BackStackCount}";
        }

        private static string BuildTickEventSummary(UITickEventBatch batch)
        {
            if (!batch.HasAnyEvents)
            {
                return "None";
            }

            if (batch.Events.Count == 1)
            {
                return batch.Events[0].EventKind.ToString();
            }

            return $"{batch.Events[0].EventKind} (+{batch.Events.Count - 1})";
        }

        private static string DetermineBlockSourceCategory(
            UIPresentationSnapshot presentationSnapshot,
            UIBlockSnapshot blockSnapshot,
            ScreenEntry? currentScreen,
            PopupEntry? topPopup)
        {
            if (topPopup.HasValue && blockSnapshot.BlocksScreenInteraction)
            {
                return "Popup Modal";
            }

            if (presentationSnapshot.Interaction.IsPaused)
            {
                return "Pause";
            }

            if (currentScreen.HasValue &&
                currentScreen.Value.Policy.BlocksUiGameplayInput &&
                blockSnapshot.BlocksUiGameplayInput)
            {
                return "Screen Policy";
            }

            if (presentationSnapshot.Interaction.IsUiGameplayInputBlocked)
            {
                return "Mapped Read Only";
            }

            if (presentationSnapshot.Interaction.HasBlockingGameplayPresentation)
            {
                return "Gameplay Presentation";
            }

            return "None";
        }

        private static string FormatTextOrNone(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "None" : TrimText(text, 80);
        }

        private void RebuildSnapshot()
        {
            var currentScreen = _screenController.CurrentEntry;
            var topPopup = _popupController.TopPopup;
            var presentationSnapshot = _presentationSource.CurrentSnapshot;
            var blockSnapshot = _coordinator.CurrentBlockSnapshot;

            CurrentSnapshot = new UiArchitectureDiagnosticsSnapshot(
                currentScreen.HasValue ? currentScreen.Value.ScreenId.ToString() : "None",
                currentScreen.HasValue ? currentScreen.Value.InstanceId.Value : (int?)null,
                _screenController.BackStackCount,
                topPopup.HasValue ? topPopup.Value.PopupId.ToString() : "None",
                topPopup.HasValue ? topPopup.Value.InstanceId.Value : (int?)null,
                _popupController.PopupCount,
                BuildPopupDismissibilityText(topPopup),
                _isHudVisible(),
                _isHudReadOnly(),
                DetermineBlockSourceCategory(presentationSnapshot, blockSnapshot, currentScreen, topPopup),
                presentationSnapshot.Tick.LastReducedTickIndex,
                _latestMappedEventSummary,
                BuildPopupPolicySummaryText(topPopup),
                _recentEvents.ToArray());
            SnapshotChanged?.Invoke(CurrentSnapshot);
        }

        private static string TrimText(string text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var trimmed = text.Trim();
            if (trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxLength - 3) + "...";
        }
    }
}
