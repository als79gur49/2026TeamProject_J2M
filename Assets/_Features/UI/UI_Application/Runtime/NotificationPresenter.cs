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
            for (var i = 0; i < sorted.Count && items.Count < MaxNotificationCount; i++)
            {
                if (TryMapMessage(sorted[i], out var message))
                {
                    items.Add(new NotificationItemViewModel(message));
                }
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

        private static bool TryMapMessage(UINotificationRecord record, out string message)
        {
            switch (record.EventKind)
            {
                case UITickEventKind.PlayerActionStarted:
                case UITickEventKind.PlayerActionResolved:
                case UITickEventKind.PlayerActionCompleted:
                case UITickEventKind.PlayerActionCanceled:
                    message = string.Empty;
                    return false;
                case UITickEventKind.PlayerDamaged:
                    message = $"T{record.TickIndex}: Took {record.DamageAmount} damage.";
                    return true;
                case UITickEventKind.StageCleared:
                    message = $"T{record.TickIndex}: Stage cleared.";
                    return true;
                default:
                    message = $"T{record.TickIndex}: Topology shifted.";
                    return true;
            }
        }
    }
}
