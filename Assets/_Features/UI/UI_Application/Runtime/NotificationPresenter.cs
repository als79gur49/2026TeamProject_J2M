using System.Collections.Generic;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.UI.HUD;

namespace Game.Feature.UI.Application
{
    public sealed class NotificationPresenter
    {
        private const int MaxNotificationCount = 3;

        public NotificationViewModel ViewModel { get; } = new();

        public void Apply(UINotificationLedgerSlice notifications)
        {
            var sorted = new List<UINotificationRecord>(notifications.ActiveNotifications);
            sorted.Sort(CompareNotifications);

            var items = new List<NotificationItemViewModel>(MaxNotificationCount);
            for (var i = 0; i < sorted.Count && i < MaxNotificationCount; i++)
            {
                items.Add(new NotificationItemViewModel(MapMessage(sorted[i])));
            }

            ViewModel.SetItems(items);
        }

        private static int CompareNotifications(UINotificationRecord left, UINotificationRecord right)
        {
            var tickComparison = right.TickIndex.CompareTo(left.TickIndex);
            if (tickComparison != 0)
            {
                return tickComparison;
            }

            var eventComparison = right.EventKind.CompareTo(left.EventKind);
            if (eventComparison != 0)
            {
                return eventComparison;
            }

            return right.ActionSequence.CompareTo(left.ActionSequence);
        }

        private static string MapMessage(UINotificationRecord record)
        {
            switch (record.EventKind)
            {
                case UITickEventKind.PlayerActionStarted:
                    return $"T{record.TickIndex}: {FormatAction(record.ActionKind)} started.";
                case UITickEventKind.PlayerActionResolved:
                    return $"T{record.TickIndex}: {FormatAction(record.ActionKind)} {FormatResolution(record.ResolutionKind)}.";
                case UITickEventKind.PlayerActionCompleted:
                    return $"T{record.TickIndex}: {FormatAction(record.ActionKind)} completed.";
                case UITickEventKind.PlayerActionCanceled:
                    return $"T{record.TickIndex}: {FormatAction(record.ActionKind)} canceled.";
                case UITickEventKind.PlayerDamaged:
                    return $"T{record.TickIndex}: Took {record.DamageAmount} damage.";
                case UITickEventKind.StageCleared:
                    return $"T{record.TickIndex}: Stage cleared.";
                default:
                    return $"T{record.TickIndex}: Topology shifted.";
            }
        }

        private static string FormatAction(GameplayUiActionKind actionKind)
        {
            return actionKind == GameplayUiActionKind.None
                ? "Action"
                : actionKind.ToString();
        }

        private static string FormatResolution(GameplayUiActionResolutionKind resolutionKind)
        {
            switch (resolutionKind)
            {
                case GameplayUiActionResolutionKind.Impact:
                    return "impacted";
                case GameplayUiActionResolutionKind.Blocked:
                    return "blocked";
                case GameplayUiActionResolutionKind.Success:
                    return "succeeded";
                default:
                    return "resolved";
            }
        }
    }
}
