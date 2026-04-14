using System;
using System.Collections.Generic;

namespace Game.Feature.UI.Flow
{
    public sealed class ScreenController
    {
        private readonly List<ScreenId> _backStack = new List<ScreenId>();

        public event Action StateChanged;

        public ScreenId CurrentScreenId { get; private set; }

        public int BackStackCount => _backStack.Count;

        public bool CanPop => _backStack.Count > 0;

        public void SetRoot(ScreenId screenId)
        {
            _backStack.Clear();
            CurrentScreenId = screenId;
            StateChanged?.Invoke();
        }

        public bool Push(ScreenId screenId)
        {
            if (screenId == ScreenId.None || CurrentScreenId == screenId)
            {
                return false;
            }

            if (CurrentScreenId != ScreenId.None)
            {
                _backStack.Add(CurrentScreenId);
            }

            CurrentScreenId = screenId;
            StateChanged?.Invoke();
            return true;
        }

        public bool Replace(ScreenId screenId)
        {
            if (screenId == ScreenId.None || CurrentScreenId == screenId)
            {
                return false;
            }

            CurrentScreenId = screenId;
            StateChanged?.Invoke();
            return true;
        }

        public bool Pop()
        {
            if (_backStack.Count == 0)
            {
                return false;
            }

            var lastIndex = _backStack.Count - 1;
            CurrentScreenId = _backStack[lastIndex];
            _backStack.RemoveAt(lastIndex);
            StateChanged?.Invoke();
            return true;
        }
    }
}
