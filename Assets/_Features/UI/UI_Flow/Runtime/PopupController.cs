using System;
using System.Collections.Generic;

namespace Game.Feature.UI.Flow
{
    public sealed class PopupController
    {
        private readonly List<PopupEntry> _stack = new List<PopupEntry>();

        public event Action StateChanged;

        public int PopupCount => _stack.Count;

        public bool CanPop => _stack.Count > 0;

        public PopupEntry? TopPopup
        {
            get
            {
                if (_stack.Count == 0)
                {
                    return null;
                }

                return _stack[_stack.Count - 1];
            }
        }

        public bool Push(PopupEntry entry)
        {
            if (entry.PopupId == PopupId.None)
            {
                return false;
            }

            if (TopPopup.HasValue && TopPopup.Value.PopupId == entry.PopupId)
            {
                return false;
            }

            _stack.Add(entry);
            StateChanged?.Invoke();
            return true;
        }

        public bool Contains(PopupId popupId)
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].PopupId == popupId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool PopTop(out PopupEntry poppedEntry)
        {
            if (_stack.Count == 0)
            {
                poppedEntry = default;
                return false;
            }

            var lastIndex = _stack.Count - 1;
            poppedEntry = _stack[lastIndex];
            _stack.RemoveAt(lastIndex);
            StateChanged?.Invoke();
            return true;
        }
    }
}
