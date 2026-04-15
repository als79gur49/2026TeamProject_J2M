using System;
using System.Collections.Generic;
using Game.Feature.UI.Popups;

namespace Game.Feature.UI.Flow
{
    public sealed class PopupController : IDisposable
    {
        private readonly IPopupRuntimeFactory _runtimeFactory;
        private readonly List<PopupRuntimeRecord> _stack = new List<PopupRuntimeRecord>();
        private int _nextInstanceId = 1;

        public PopupController(IPopupRuntimeFactory runtimeFactory)
        {
            _runtimeFactory = runtimeFactory ?? throw new ArgumentNullException(nameof(runtimeFactory));
        }

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

                return _stack[_stack.Count - 1].Entry;
            }
        }

        public bool Push(PopupRequest request, out PopupInstanceId instanceId)
        {
            var newInstanceId = new PopupInstanceId(_nextInstanceId++);
            instanceId = newInstanceId;
            var runtimeResult = _runtimeFactory.Create(request);
            var entry = new PopupEntry(
                newInstanceId,
                request.PopupId,
                request.Payload,
                runtimeResult.Policy,
                request.CompletionCallback);
            var completionHandler = new Action<PopupCompletionKind>(completionKind =>
                HandleRuntimeCompletionRequested(newInstanceId, completionKind));
            var record = new PopupRuntimeRecord(entry, runtimeResult.Runtime, completionHandler);
            runtimeResult.Runtime.CompletionRequested += completionHandler;
            _stack.Add(record);
            ApplyTopmostState();
            StateChanged?.Invoke();
            return true;
        }

        public bool Contains(PopupId popupId)
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Entry.PopupId == popupId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HandleBackRequested()
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            var topEntry = _stack[_stack.Count - 1].Entry;
            switch (topEntry.Policy.BackAction)
            {
                case PopupBackAction.Close:
                    return CloseTop(PopupCloseReason.Back, PopupCompletionKind.Closed);

                case PopupBackAction.Cancel:
                    return CloseTop(PopupCloseReason.Back, PopupCompletionKind.Cancelled);

                case PopupBackAction.Consume:
                    return true;

                default:
                    return false;
            }
        }

        public bool HandleBackdropClicked()
        {
            if (_stack.Count == 0)
            {
                return false;
            }

            var topEntry = _stack[_stack.Count - 1].Entry;
            switch (topEntry.Policy.BackdropMode)
            {
                case Game.Feature.UI.Popups.PopupBackdropMode.CloseTop:
                    return CloseTop(PopupCloseReason.BackdropClick, PopupCompletionKind.Closed);

                case Game.Feature.UI.Popups.PopupBackdropMode.Consume:
                    return true;

                default:
                    return false;
            }
        }

        public bool PopTop(out PopupEntry poppedEntry)
        {
            return TryCloseTop(PopupCloseReason.Programmatic, PopupCompletionKind.Closed, out poppedEntry);
        }

        public bool Close(PopupInstanceId instanceId, PopupCloseReason closeReason)
        {
            return Close(instanceId, closeReason, PopupCompletionKind.Closed);
        }

        public bool Close(PopupInstanceId instanceId, PopupCloseReason closeReason, PopupCompletionKind completionKind)
        {
            var index = FindIndex(instanceId);
            if (index < 0)
            {
                return false;
            }

            var closedRecord = _stack[index];
            _stack.RemoveAt(index);
            DetachAndDispose(closedRecord);
            ApplyTopmostState();
            StateChanged?.Invoke();
            NotifyCompletion(closedRecord.Entry, completionKind, closeReason);
            return true;
        }

        public bool CloseTop(PopupCloseReason closeReason)
        {
            return CloseTop(closeReason, PopupCompletionKind.Closed);
        }

        public bool CloseTop(PopupCloseReason closeReason, PopupCompletionKind completionKind)
        {
            return TryCloseTop(closeReason, completionKind, out _);
        }

        public void CloseAll(PopupCloseReason closeReason)
        {
            if (_stack.Count == 0)
            {
                return;
            }

            var closedRecords = new List<PopupRuntimeRecord>(_stack);
            _stack.Clear();

            for (var i = closedRecords.Count - 1; i >= 0; i--)
            {
                DetachAndDispose(closedRecords[i]);
            }

            StateChanged?.Invoke();

            for (var i = closedRecords.Count - 1; i >= 0; i--)
            {
                NotifyCompletion(
                    closedRecords[i].Entry,
                    PopupCompletionKind.Closed,
                    closeReason);
            }
        }

        public void Dispose()
        {
            CloseAll(PopupCloseReason.Dispose);
        }

        private void ApplyTopmostState()
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                _stack[i].Runtime.SetIsTopmost(i == _stack.Count - 1);
            }
        }

        private void DetachAndDispose(PopupRuntimeRecord record)
        {
            record.Runtime.CompletionRequested -= record.CompletionHandler;
            record.Runtime.Dispose();
        }

        private int FindIndex(PopupInstanceId instanceId)
        {
            for (var i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].Entry.InstanceId.Equals(instanceId))
                {
                    return i;
                }
            }

            return -1;
        }

        private void HandleRuntimeCompletionRequested(PopupInstanceId instanceId, PopupCompletionKind completionKind)
        {
            Close(instanceId, PopupCloseReason.UserAction, completionKind);
        }

        private void NotifyCompletion(
            PopupEntry entry,
            PopupCompletionKind completionKind,
            PopupCloseReason closeReason)
        {
            entry.CompletionCallback?.Invoke(new PopupCompletion(
                entry.InstanceId,
                entry.PopupId,
                completionKind,
                closeReason));
        }

        private bool TryCloseTop(
            PopupCloseReason closeReason,
            PopupCompletionKind completionKind,
            out PopupEntry poppedEntry)
        {
            if (_stack.Count == 0)
            {
                poppedEntry = default;
                return false;
            }

            var lastIndex = _stack.Count - 1;
            var closedRecord = _stack[lastIndex];
            poppedEntry = closedRecord.Entry;
            _stack.RemoveAt(lastIndex);
            DetachAndDispose(closedRecord);
            ApplyTopmostState();
            StateChanged?.Invoke();
            NotifyCompletion(closedRecord.Entry, completionKind, closeReason);
            return true;
        }

        private sealed class PopupRuntimeRecord
        {
            public PopupRuntimeRecord(
                PopupEntry entry,
                IPopupRuntime runtime,
                Action<PopupCompletionKind> completionHandler)
            {
                Entry = entry;
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
                CompletionHandler = completionHandler ?? throw new ArgumentNullException(nameof(completionHandler));
            }

            public PopupEntry Entry { get; }

            public IPopupRuntime Runtime { get; }

            public Action<PopupCompletionKind> CompletionHandler { get; }
        }
    }
}
