using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Game.Feature.Stages
{
    internal interface ICampaignHudReadProvider
    {
        ICampaignHudReadSession OpenHudReadSession(int slotNumber);
        CampaignHudReadStore HudReadStore { get; }
    }

    internal interface ICampaignHudReadSession : IDisposable
    {
        long Revision { get; }
        long Generation { get; }
        long StorageAccessCount { get; }
        CampaignSlotEntry Read();
        CampaignSlotEntry Reload();
        CampaignHudBeforeMutationCapture CaptureBeforeMutation();
    }

    internal enum CampaignHudAvailability { Uninitialized, Invalidated, Ready, Unavailable }

    // Captured by Stages, inspected by Gameplay. No callbacks run inside a save operation.
    internal sealed class CampaignHudBeforeMutationCapture : IDisposable
    {
        private CampaignHudReadStore _store;
        internal CampaignHudBeforeMutationCapture(CampaignHudReadStore store, int slotNumber)
        {
            _store = store;
            SlotNumber = slotNumber;
        }
        internal int SlotNumber { get; }
        internal CampaignSlotState State { get; private set; }
        internal void Observe(CampaignSlotState state)
        {
            if (State == null && state?.SlotNumber == SlotNumber) State = state;
        }
        public void Dispose()
        {
            _store?.RemoveCapture(this);
            _store = null;
        }
    }

    internal sealed class CampaignHudReadStore
    {
        private CampaignSlotEntry[] _entries;
        private CampaignSaveLoadReport _report;
        private Exception _failure;
        private bool _gateKnown;
        private bool _profileValidated;
        private bool _blocked;
        private int _operationDepth;
        private long _operationId;
        private long _gateVerifiedOperation = -1;
        private readonly List<CampaignHudBeforeMutationCapture> _captures = new();

        internal long StorageAccessCount { get; private set; }
        internal void ObserveStorageAccess() => StorageAccessCount++;
        internal long Revision { get; private set; }
        internal long Generation { get; private set; }
        internal CampaignHudAvailability Availability { get; private set; }
        internal bool IsOperating => _operationDepth != 0;

        internal IDisposable BeginOperation()
        {
            if (_operationDepth == 0) _operationId++;
            _operationDepth++;
            return new Operation(this);
        }

        private sealed class Operation : IDisposable
        {
            private CampaignHudReadStore _store;
            internal Operation(CampaignHudReadStore store) { _store = store; }
            public void Dispose()
            {
                if (_store == null) return;
                _store._operationDepth--;
                _store = null;
            }
        }

        internal void Invalidate(bool gateUnknown = false, bool profileUnknown = true)
        {
            Revision++;
            Availability = CampaignHudAvailability.Invalidated;
            _failure = null;
            if (gateUnknown) _gateKnown = false;
            if (profileUnknown) _profileValidated = false;
        }

        internal void InvalidateForValidation()
        {
            // A raw profile observation cannot inherit an earlier operation's gate approval.
            Invalidate(gateUnknown: _operationDepth == 0 || _gateVerifiedOperation != _operationId);
        }

        internal void Reset()
        {
            Generation++;
            _profileValidated = false;
            Invalidate(gateUnknown: true);
        }

        internal void ObserveGate(bool blocked)
        {
            var changed = !_gateKnown || _blocked != blocked;
            if (changed) Revision++;
            _gateVerifiedOperation = _operationDepth > 0 ? _operationId : -1;
            _gateKnown = true;
            _blocked = blocked;
            if (blocked)
            {
                Availability = CampaignHudAvailability.Unavailable;
            }
            else if (_entries != null && _profileValidated)
            {
                Availability = _report.BlocksCampaignAccess || _failure != null
                    ? CampaignHudAvailability.Unavailable : CampaignHudAvailability.Ready;
            }
            else if (changed) Availability = CampaignHudAvailability.Invalidated;
        }

        internal void Observe(CampaignSaveLoadResult result)
        {
            // Arrays are copied; their entries and canonical states are immutable.
            _entries = result.Slots == null ? null : (CampaignSlotEntry[])result.Slots.Clone();
            _report = result.Report;
            _profileValidated = result.Report.Status != CampaignSaveLoadStatus.RecoveryPending;
            _failure = null;
            Revision++;
            Availability = !_gateKnown
                ? (_blocked ? CampaignHudAvailability.Unavailable : CampaignHudAvailability.Invalidated)
                : (_blocked || _report.BlocksCampaignAccess
                    ? CampaignHudAvailability.Unavailable : CampaignHudAvailability.Ready);
        }

        internal void ObserveProfile(CampaignProfileLoadResult result)
        {
            // Observation must never turn a durable save into a failed command.
            try
            {
                var report = CampaignSaveSlotStoreAdapter.ToCampaignLoadReport(
                    CampaignSaveServiceResult.Failure(CampaignSaveCommandStatus.LoadFailed,
                        result.Message, hasProfileLoadStatus: true, profileLoadStatus: result.Status));
                var entries = CampaignSaveSlotStoreAdapter.CreateEntries(result.Document?.Slots);
                Observe(new CampaignSaveLoadResult(entries, report));
            }
            catch
            {
                Invalidate();
            }
        }

        internal void ObserveFailure(Exception exception)
        {
            _failure = exception;
            _profileValidated = false;
            _entries = null;
            Availability = CampaignHudAvailability.Unavailable;
            Revision++;
        }

        internal void ObserveBeforeMutation(CampaignSlotState state)
        {
            for (var i = 0; i < _captures.Count; i++) _captures[i].Observe(state);
        }

        internal CampaignHudBeforeMutationCapture Capture(int slotNumber)
        {
            var capture = new CampaignHudBeforeMutationCapture(this, slotNumber);
            _captures.Add(capture);
            return capture;
        }

        internal void RemoveCapture(CampaignHudBeforeMutationCapture capture) => _captures.Remove(capture);

        internal ICampaignHudReadSession Open(int slotNumber, Func<CampaignSaveLoadResult> validate)
        {
            CampaignSaveSlotPolicy.ThrowIfInvalidSlotNumber(slotNumber);
            return new Session(this, slotNumber, validate);
        }

        private sealed class Session : ICampaignHudReadSession
        {
            private CampaignHudReadStore _store;
            private Func<CampaignSaveLoadResult> _validate;
            private readonly int _slotNumber;
            private bool _needsBindValidation = true;
            private CampaignHudBeforeMutationCapture _capture;
            internal Session(CampaignHudReadStore store, int slotNumber, Func<CampaignSaveLoadResult> validate)
            {
                _store = store;
                _slotNumber = slotNumber;
                _validate = validate;
            }
            public long Revision => Store.Revision;
            public long Generation => Store.Generation;
            public long StorageAccessCount => Store.StorageAccessCount;
            private CampaignHudReadStore Store => _store ?? throw new ObjectDisposedException(nameof(Session));
            public CampaignHudBeforeMutationCapture CaptureBeforeMutation()
            {
                _capture?.Dispose();
                return _capture = Store.Capture(_slotNumber);
            }
            public CampaignSlotEntry Reload()
            {
                Store.Invalidate(gateUnknown: true);
                _needsBindValidation = true;
                return Read();
            }
            public CampaignSlotEntry Read()
            {
                var store = Store;
                if (_needsBindValidation || store.Availability == CampaignHudAvailability.Invalidated ||
                    store.Availability == CampaignHudAvailability.Uninitialized)
                {
                    // Never re-enter partially executing repository/recovery work to repair a HUD cache.
                    if (store.IsOperating)
                        throw new InvalidOperationException("Campaign HUD validation is pending a save operation.");
                    _needsBindValidation = false;
                    try { store.Observe(_validate()); }
                    catch (Exception exception) { store.ObserveFailure(exception); throw; }
                }
                if (store._blocked || !store._gateKnown)
                    throw new InvalidOperationException("Campaign save access is blocked (RecoveryPending): Campaign save reset is pending.");
                if (store._failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(store._failure).Throw();
                CampaignSaveSlotStoreAdapter.ThrowIfCampaignAccessBlocked(store._report);
                if (store.Availability != CampaignHudAvailability.Ready)
                    throw new InvalidOperationException("Campaign HUD read has not been validated.");
                return store._entries?[_slotNumber - 1] ?? CampaignSlotEntry.Empty(_slotNumber);
            }
            public void Dispose()
            {
                _capture?.Dispose();
                _capture = null;
                _validate = null;
                _store = null;
            }
        }
    }

    internal static class CampaignHudReadRegistry
    {
        private static readonly Dictionary<string, WeakReference<CampaignHudReadStore>> Stores = new();
        private static readonly ConditionalWeakTable<object, BackingToken> BackingTokens = new();
        private sealed class BackingToken { internal readonly string Value = Guid.NewGuid().ToString("N"); }

        internal static string FileKey(string root) => "file:" + SavePathProviderBase.NormalizeRoot(root);
        internal static string MemoryKey(object identity) => "memory:" + BackingTokens.GetValue(identity, _ => new BackingToken()).Value;
        internal static CampaignHudReadStore Acquire(string key)
        {
            lock (Stores)
            {
                Prune();
                if (Stores.TryGetValue(key, out var weak) && weak.TryGetTarget(out var live)) return live;
                var store = new CampaignHudReadStore();
                Stores[key] = new WeakReference<CampaignHudReadStore>(store);
                return store;
            }
        }
        internal static void Reset(string key)
        {
            lock (Stores)
            {
                if (Stores.TryGetValue(key, out var weak) && weak.TryGetTarget(out var live)) live.Reset();
                Prune();
            }
        }
        private static void Prune()
        {
            var dead = new List<string>();
            foreach (var pair in Stores) if (!pair.Value.TryGetTarget(out _)) dead.Add(pair.Key);
            foreach (var key in dead) Stores.Remove(key);
        }
    }
}
