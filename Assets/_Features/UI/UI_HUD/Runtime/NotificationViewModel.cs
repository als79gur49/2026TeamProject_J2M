using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Feature.UI.HUD
{
    public readonly struct NotificationItemViewModel
    {
        public NotificationItemViewModel(string messageText)
        {
            MessageText = messageText ?? string.Empty;
        }

        public string MessageText { get; }
    }

    public sealed class NotificationViewModel
    {
        public event Action Changed;

        private readonly ReadOnlyCollection<NotificationItemViewModel> _emptyItems =
            new(new List<NotificationItemViewModel>());
        private ReadOnlyCollection<NotificationItemViewModel> _items;

        public IReadOnlyList<NotificationItemViewModel> Items => _items ?? _emptyItems;

        public void SetItems(IEnumerable<NotificationItemViewModel> items)
        {
            _items = new ReadOnlyCollection<NotificationItemViewModel>(new List<NotificationItemViewModel>(items ?? Array.Empty<NotificationItemViewModel>()));
            Changed?.Invoke();
        }
    }
}
