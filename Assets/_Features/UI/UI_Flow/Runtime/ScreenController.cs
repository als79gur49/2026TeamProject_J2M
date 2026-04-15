using System;
using System.Collections.Generic;
using Game.Feature.UI.Screens;

namespace Game.Feature.UI.Flow
{
    public sealed class ScreenController : IDisposable
    {
        private readonly List<ScreenRuntimeRecord> _backStack = new List<ScreenRuntimeRecord>();
        private readonly IScreenRuntimeFactory _runtimeFactory;
        private ScreenRuntimeRecord _current;
        private int _nextInstanceId = 1;

        public ScreenController(IScreenRuntimeFactory runtimeFactory)
        {
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
        }

        public event Action StateChanged;

        public event Action<ScreenAction> ActionRequested;

        public ScreenId CurrentScreenId => CurrentEntry.HasValue ? CurrentEntry.Value.ScreenId : ScreenId.None;

        public ScreenEntry? CurrentEntry => _current != null ? _current.Entry : null;

        public int BackStackCount => _backStack.Count;

        public bool CanPop =>
            _backStack.Count > 0 &&
            CurrentEntry.HasValue &&
            CurrentEntry.Value.Policy.BackAction == ScreenBackAction.Pop;

        public void SetRoot(ScreenRequest request)
        {
            ClearInternal();
            _current = CreateRuntimeRecord(request);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
        }

        public bool Show(ScreenRequest request)
        {
            if (CurrentEntry.HasValue &&
                CurrentEntry.Value.ScreenId == request.ScreenId &&
                string.Equals(CurrentEntry.Value.ReuseKey, request.ReuseKey, StringComparison.Ordinal))
            {
                UpdateCurrentPayload(request.Payload);
                return true;
            }

            RemoveMatchingHistoryEntries(request.ReuseKey);

            if (_current != null)
            {
                DetachAndDispose(_current);
            }

            _current = CreateRuntimeRecord(request);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool Push(ScreenRequest request)
        {
            if (_current == null)
            {
                SetRoot(request);
                return true;
            }

            if (TryRestoreReusableEntry(request))
            {
                return true;
            }

            var previousCurrent = _current;
            if (previousCurrent.Entry.Policy.RetentionMode == ScreenRetentionMode.RetainMountedHistory)
            {
                previousCurrent.Runtime.SetIsCurrent(false);
                _backStack.Add(previousCurrent);
            }
            else
            {
                var disposedRecord = previousCurrent.DetachRuntime();
                DetachAndDispose(previousCurrent);
                _backStack.Add(disposedRecord);
            }

            _current = CreateRuntimeRecord(request);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool Replace(ScreenRequest request)
        {
            if (CurrentEntry.HasValue &&
                CurrentEntry.Value.ScreenId == request.ScreenId &&
                string.Equals(CurrentEntry.Value.ReuseKey, request.ReuseKey, StringComparison.Ordinal))
            {
                UpdateCurrentPayload(request.Payload);
                return true;
            }

            RemoveMatchingHistoryEntries(request.ReuseKey);

            if (_current != null)
            {
                DetachAndDispose(_current);
            }

            _current = CreateRuntimeRecord(request);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool Pop()
        {
            if (_backStack.Count == 0)
            {
                return false;
            }

            if (_current != null)
            {
                DetachAndDispose(_current);
            }

            var lastIndex = _backStack.Count - 1;
            var restoredRecord = _backStack[lastIndex];
            _backStack.RemoveAt(lastIndex);
            if (restoredRecord.Runtime == null)
            {
                restoredRecord = CreateRuntimeRecord(new ScreenRequest(
                    restoredRecord.Entry.ScreenId,
                    restoredRecord.Entry.Payload,
                    restoredRecord.Entry.ReuseKey),
                    restoredRecord.Entry.InstanceId);
            }

            _current = restoredRecord;
            _current.Runtime.ApplyPayload(_current.Entry.Payload);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
            return true;
        }

        public bool PopTo(ScreenId screenId)
        {
            for (var i = _backStack.Count - 1; i >= 0; i--)
            {
                if (_backStack[i].Entry.ScreenId != screenId)
                {
                    continue;
                }

                return RestoreHistoryEntry(i, _backStack[i].Entry.Payload);
            }

            return false;
        }

        public bool HandleBackRequested()
        {
            if (!CurrentEntry.HasValue)
            {
                return false;
            }

            switch (CurrentEntry.Value.Policy.BackAction)
            {
                case ScreenBackAction.Pop:
                    return Pop();

                case ScreenBackAction.Consume:
                    return true;

                default:
                    return false;
            }
        }

        public void Clear()
        {
            if (_current == null && _backStack.Count == 0)
            {
                return;
            }

            ClearInternal();
            StateChanged?.Invoke();
        }

        public void Dispose()
        {
            ClearInternal();
        }

        private void UpdateCurrentPayload(IScreenPayload payload)
        {
            _current.Entry = new ScreenEntry(
                _current.Entry.InstanceId,
                _current.Entry.ScreenId,
                payload,
                _current.Entry.Policy,
                _current.Entry.ReuseKey);
            _current.Runtime.ApplyPayload(payload);
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
        }

        private bool TryRestoreReusableEntry(ScreenRequest request)
        {
            if (string.IsNullOrEmpty(request.ReuseKey))
            {
                return false;
            }

            if (CurrentEntry.HasValue &&
                CurrentEntry.Value.ScreenId == request.ScreenId &&
                string.Equals(CurrentEntry.Value.ReuseKey, request.ReuseKey, StringComparison.Ordinal))
            {
                UpdateCurrentPayload(request.Payload);
                return true;
            }

            for (var i = _backStack.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_backStack[i].Entry.ReuseKey, request.ReuseKey, StringComparison.Ordinal))
                {
                    continue;
                }

                return RestoreHistoryEntry(i, request.Payload);
            }

            return false;
        }

        private bool RestoreHistoryEntry(int historyIndex, IScreenPayload payload)
        {
            if (historyIndex < 0 || historyIndex >= _backStack.Count)
            {
                return false;
            }

            if (_current != null)
            {
                DetachAndDispose(_current);
            }

            for (var i = _backStack.Count - 1; i > historyIndex; i--)
            {
                DetachAndDispose(_backStack[i]);
                _backStack.RemoveAt(i);
            }

            var restoredRecord = _backStack[historyIndex];
            _backStack.RemoveAt(historyIndex);
            if (restoredRecord.Runtime == null)
            {
                restoredRecord = CreateRuntimeRecord(new ScreenRequest(
                    restoredRecord.Entry.ScreenId,
                    payload,
                    restoredRecord.Entry.ReuseKey),
                    restoredRecord.Entry.InstanceId);
            }
            else
            {
                restoredRecord.Entry = new ScreenEntry(
                    restoredRecord.Entry.InstanceId,
                    restoredRecord.Entry.ScreenId,
                    payload,
                    restoredRecord.Entry.Policy,
                    restoredRecord.Entry.ReuseKey);
                restoredRecord.Runtime.ApplyPayload(payload);
            }

            _current = restoredRecord;
            _current.Runtime.SetIsCurrent(true);
            StateChanged?.Invoke();
            return true;
        }

        private void RemoveMatchingHistoryEntries(string reuseKey)
        {
            if (string.IsNullOrEmpty(reuseKey))
            {
                return;
            }

            for (var i = _backStack.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_backStack[i].Entry.ReuseKey, reuseKey, StringComparison.Ordinal))
                {
                    continue;
                }

                DetachAndDispose(_backStack[i]);
                _backStack.RemoveAt(i);
            }
        }

        private void ClearInternal()
        {
            if (_current != null)
            {
                DetachAndDispose(_current);
                _current = null;
            }

            for (var i = _backStack.Count - 1; i >= 0; i--)
            {
                DetachAndDispose(_backStack[i]);
            }

            _backStack.Clear();
        }

        private ScreenRuntimeRecord CreateRuntimeRecord(ScreenRequest request, ScreenInstanceId? existingInstanceId = null)
        {
            var runtimeResult = _runtimeFactory.Create(request);
            var instanceId = existingInstanceId ?? new ScreenInstanceId(_nextInstanceId++);
            var entry = new ScreenEntry(
                instanceId,
                request.ScreenId,
                request.Payload,
                runtimeResult.Policy,
                request.ReuseKey);
            var record = new ScreenRuntimeRecord
            {
                Entry = entry,
                Runtime = runtimeResult.Runtime,
            };
            record.ActionHandler = action => HandleRuntimeActionRequested(action);
            record.Runtime.ActionRequested += record.ActionHandler;
            record.Runtime.ApplyPayload(request.Payload);
            return record;
        }

        private void HandleRuntimeActionRequested(ScreenAction action)
        {
            ActionRequested?.Invoke(action);
        }

        private static void DetachAndDispose(ScreenRuntimeRecord record)
        {
            if (record == null || record.Runtime == null)
            {
                return;
            }

            if (record.ActionHandler != null)
            {
                record.Runtime.ActionRequested -= record.ActionHandler;
            }

            record.Runtime.Dispose();
        }

        private sealed class ScreenRuntimeRecord
        {
            public ScreenEntry Entry { get; set; }

            public IScreenRuntime Runtime { get; set; }

            public Action<ScreenAction> ActionHandler { get; set; }

            public ScreenRuntimeRecord DetachRuntime()
            {
                return new ScreenRuntimeRecord
                {
                    Entry = Entry,
                    Runtime = null,
                    ActionHandler = null,
                };
            }
        }
    }
}
