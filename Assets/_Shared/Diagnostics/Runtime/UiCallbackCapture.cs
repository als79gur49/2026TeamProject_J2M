#if VECTORQUAKE_CAPTURE_BUILD
using System;
using System.Diagnostics;
using System.Threading;

namespace Game.Shared.Diagnostics
{
    public enum UiCallbackSection
    {
        Feed, FeedFrameBuild, FeedFrameDispatch, FeedTerminalClear, FeedTerminalAccepted,
        FeedTerminalRejected, UiFrameHandling, UiRefreshHandling, UiLevelFailedDispatch,
        UiRefreshInput, QuerySession, QueryStage, QueryObjectives, QueryPlayerHud,
        QuerySurfaceButtonRemainders, UiRoute, UiReduce, UiSnapshotCommit,
        UiSnapshotDispatch, HudRoot, HudObjective, HudObjectiveBuildRows,
        HudObjectiveHeaderText, HudObjectiveModel, HudObjectiveNotify, HudObjectiveView,
        HudShell, HudStageInfo, HudChancePanel, HudSurfaceBelt,
        HudPlayerStatus, // Reserved: retired display path; preserve historical sidecar IDs.
        UiTickEventsBuild, UiTickEventsDispatch, Calibration, HudObjectiveApply, HostPresentationAdvanced, HostStateChanged, HostTerminalRelease,
        QueryPlayerHudSnapshot, QueryPlayerHudChances, QueryPlayerHudChancesOverride,
        QueryPlayerHudChancesLoadSlot, QueryPlayerHudChancesDiagnostics, QueryPlayerHudActor,
        QueryPlayerHudDiagnostics, QueryPlayerHudControlState,
        // Reserved H03 retired query work: preserve names and ordinal values for historical sidecars.
        QueryPlayerHudAdmission,
        QueryPlayerHudSettled, QueryPlayerHudActionGate, QueryPlayerHudPushPreview,
        QueryPlayerHudPushDirection, QueryPlayerHudPushContact, QueryPlayerHudRecovery,
        QueryPlayerHudResult,
        QueryPlayerHudMovementGate // Reserved H03 retired query work.
    }

    public enum UiCallbackOrigin
    {
        Inherit, TickCompleted, PresentStateChanged, PresentationAdvanced,
        TerminalRelease, PauseOrInputRefresh, LocaleChanged, BindOrEnable
    }

    public enum UiCallbackCounter
    {
        InputRefresh, ObjectiveApply, BuildRows, LocalizationResolve, SetState,
        VmChanged, ViewRefresh, Conditions, Rows, Groups, JustSatisfiedRows,
        PlayerHudCompositionId, PlayerHudSourcePresent, PlayerHudSourceAbsent,
        PlayerHudOverrideHit, PlayerHudOverrideMiss, PlayerHudOverrideAbsent, PlayerHudLoadSlot,
        PlayerHudPlayerPresent, PlayerHudPlayerAbsent,
        // Reserved H03 retired branch observations: never reuse these ordinal values.
        PlayerHudActionPass, PlayerHudActionFail,
        PlayerHudPushSkipped, PlayerHudPushExecuted, PlayerHudDirectionNone, PlayerHudDirectionCardinal,
        PlayerHudMovementGate,
        PlayerHudDiagnosticsEnabled, PlayerHudDiagnosticsConsoleEnabled,
        HudQueryCacheHit, HudStorageAccess,
        HudStageResultCreated, HudObjectiveResultCreated, HudPlayerHudResultCreated, HudSurfaceResultCreated
    }

    public struct UiCallbackSpan
    {
        public int Id, ParentId, HostTickIndex, FrameTickIndex, ThreadId, OwnerGeneration;
        public UiCallbackSection Section;
        public UiCallbackOrigin RootOrigin, LocalTrigger;
        public long StartTicks, EndTicks, AllocatedBytes;
        internal long AllocationStart;
    }

    public struct UiCallbackCount
    {
        public int SpanId;
        public UiCallbackCounter Counter;
        public long Value;
    }

    public static class UiCallbackAllocationProbe
    {
        public const int ProbeBytes = 4096;

        // A callable API can still return a constant zero on a Player backend.
        // Call only before recording, and keep the allocation alive across both counter reads.
        public static bool TryGetAvailability(out long? observedDelta, Func<long> counter = null)
        {
            observedDelta = null;
            try
            {
                counter ??= GC.GetAllocatedBytesForCurrentThread;
                long before = counter();
                var allocation = new byte[ProbeBytes];
                long after = counter();
                GC.KeepAlive(allocation);
                observedDelta = checked(after - before);
                return observedDelta.Value >= ProbeBytes;
            }
            catch
            {
                // Unsupported/throwing counters are unavailable, not a zero-allocation observation.
                observedDelta = null;
                return false;
            }
        }
    }

    // One observer, no simulation/Unity dependencies, all storage allocated before the measured window.
    public sealed class UiCallbackSession
    {
        private readonly UiCallbackSpan[] _spans;
        private readonly UiCallbackCount[] _counts;
        private readonly int[] _stack;
        private readonly Func<long> _clock, _allocation;
        private readonly Func<int> _thread;
        private int _depth;
        public int Generation { get; private set; } = 1;
        public int ThreadId { get; }
        public int SpanCount { get; private set; }
        public int CounterCount { get; private set; }
        public int InvalidCount { get; private set; }
        public int OverflowCount { get; private set; }
        public bool AllocationAvailable => _allocation != null;
        public bool Recording { get; private set; }
        public int CurrentSpanId => _depth == 0 ? 0 : _stack[_depth - 1] + 1;
        public UiCallbackSpan GetSpan(int index) => _spans[index];
        public UiCallbackCount GetCounter(int index) => _counts[index];

        public UiCallbackSession(bool allocationAvailable, int capacity = 262144, int maxDepth = 64,
            Func<long> clock = null, Func<long> allocation = null, Func<int> thread = null)
        {
            if (capacity <= 0 || maxDepth <= 0) throw new ArgumentOutOfRangeException();
            _spans = new UiCallbackSpan[capacity];
            _counts = new UiCallbackCount[capacity];
            _stack = new int[maxDepth];
            _clock = clock ?? Stopwatch.GetTimestamp;
            _allocation = allocationAvailable ? allocation ?? GC.GetAllocatedBytesForCurrentThread : null;
            _thread = thread ?? (() => Thread.CurrentThread.ManagedThreadId);
            ThreadId = _thread();
        }

        public void Start()
        {
            if (Recording || _depth != 0 || _thread() != ThreadId) { Fail(); return; }
            SpanCount = CounterCount = 0;
            Generation++;
            Recording = true;
        }

        public void Stop()
        {
            if (_depth != 0 || _thread() != ThreadId) Fail();
            Recording = false;
        }

        public void Fail() => InvalidCount++;

        internal UiCallbackCapture.Scope Begin(UiCallbackSection section, UiCallbackOrigin trigger, int tick)
        {
            if (!Recording) return default;
            if (_thread() != ThreadId) { Fail(); return default; }
            if (_depth == _stack.Length || SpanCount == _spans.Length)
            { OverflowCount++; Fail(); return default; }
            int parent = CurrentSpanId;
            var origin = parent == 0
                ? (trigger == UiCallbackOrigin.Inherit ? UiCallbackOrigin.BindOrEnable : trigger)
                : _spans[parent - 1].RootOrigin;
            int hostTick = parent == 0 ? (origin == UiCallbackOrigin.TickCompleted ? tick : 0)
                : _spans[parent - 1].HostTickIndex;
            int frameTick = tick != 0 ? tick : parent == 0 ? 0 : _spans[parent - 1].FrameTickIndex;
            int index = SpanCount++;
            _stack[_depth++] = index;
            _spans[index] = new UiCallbackSpan
            {
                Id = index + 1, ParentId = parent, Section = section, RootOrigin = origin,
                LocalTrigger = trigger == UiCallbackOrigin.Inherit
                    ? (parent == 0 ? origin : _spans[parent - 1].LocalTrigger) : trigger,
                HostTickIndex = hostTick, FrameTickIndex = frameTick,
                ThreadId = ThreadId, OwnerGeneration = Generation,
                AllocationStart = _allocation == null ? 0 : _allocation(), StartTicks = _clock()
            };
            return new UiCallbackCapture.Scope(this, index + 1, Generation);
        }

        internal void End(int id, int generation)
        {
            long end = _clock();
            if (_thread() != ThreadId || generation != Generation || CurrentSpanId != id || !Recording)
            { Fail(); return; }
            ref var span = ref _spans[id - 1];
            span.EndTicks = end;
            span.AllocatedBytes = _allocation == null ? 0 : _allocation() - span.AllocationStart;
            if (span.EndTicks < span.StartTicks || span.AllocatedBytes < 0) Fail();
            _depth--;
        }

        internal void Count(UiCallbackCounter counter, int amount)
        {
            if (!Recording) return;
            if (_thread() != ThreadId || CurrentSpanId == 0 || amount < 0) { Fail(); return; }
            if (CounterCount == _counts.Length) { OverflowCount++; Fail(); return; }
            _counts[CounterCount++] = new UiCallbackCount { SpanId = CurrentSpanId, Counter = counter, Value = amount };
        }
    }

    public readonly struct PlayerHudComposition
    {
        public readonly int Id;
        public readonly string SourceType, StoreType;
        internal PlayerHudComposition(int id, object source, object store)
        { Id = id; SourceType = source.GetType().FullName; StoreType = store?.GetType().FullName; }
    }

    public static class UiCallbackCapture
    {
        private static UiCallbackSession _session;
        private static bool _playerHudDetail;
        private static readonly object[] PlayerHudSources = new object[64];
        private static readonly PlayerHudComposition[] PlayerHudCompositions = new PlayerHudComposition[64];
        public static int PlayerHudCompositionCount { get; private set; }
        public static int PlayerHudCompositionOverflowCount { get; private set; }
        public static int PlayerHudCompositionInvalidCount { get; private set; }
        public static bool PlayerHudEnabled => _playerHudDetail && Enabled;

        public static void ConfigurePlayerHud(bool enabled)
        {
            if (Enabled) { _session.Fail(); return; }
            _playerHudDetail = enabled;
        }
        public static Scope MeasurePlayerHud(UiCallbackSection section)
            => PlayerHudEnabled ? _session.Begin(section, UiCallbackOrigin.Inherit, 0) : default;
        public static void CountPlayerHud(UiCallbackCounter counter, int amount = 1)
        { if (PlayerHudEnabled) _session.Count(counter, amount); }

        // Constructors register identity before capture; no reflection or growth during recording.
        public static int RegisterPlayerHudComposition(object source, object store = null)
        {
            if (Enabled)
            { PlayerHudCompositionInvalidCount++; _session.Fail(); return 0; }
            if (source == null) return 0;
            for (int i = 0; i < PlayerHudCompositionCount; i++)
                if (ReferenceEquals(PlayerHudSources[i], source))
                {
                    if (store != null && PlayerHudCompositions[i].StoreType != store.GetType().FullName)
                    { PlayerHudCompositionInvalidCount++; return 0; }
                    return i + 1;
                }
            if (PlayerHudCompositionCount == PlayerHudSources.Length)
            { PlayerHudCompositionOverflowCount++; return 0; }
            int index = PlayerHudCompositionCount++;
            PlayerHudSources[index] = source;
            PlayerHudCompositions[index] = new PlayerHudComposition(index + 1, source, store);
            return index + 1;
        }
        public static PlayerHudComposition GetPlayerHudComposition(int index) => PlayerHudCompositions[index];
        public static void ResetPlayerHudCompositions()
        {
            if (Enabled) { _session.Fail(); return; }
            Array.Clear(PlayerHudSources, 0, PlayerHudSources.Length);
            Array.Clear(PlayerHudCompositions, 0, PlayerHudCompositions.Length);
            PlayerHudCompositionCount = PlayerHudCompositionOverflowCount = PlayerHudCompositionInvalidCount = 0;
        }
        public static bool Enabled => _session != null && _session.Recording;
        public static int CurrentSpanId => _session?.CurrentSpanId ?? 0;
        public static UiCallbackSection? CurrentSection => CurrentSpanId == 0 ? (UiCallbackSection?)null : _session.GetSpan(CurrentSpanId - 1).Section;
        public static int SessionGeneration => _session?.Generation ?? 0;
        public static void Attach(UiCallbackSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (_session != null) throw new InvalidOperationException("UI capture session is already owned.");
            _session = session;
        }
        public static void Detach(UiCallbackSession session)
        {
            if (!ReferenceEquals(session, _session)) { _session?.Fail(); return; }
            _session?.Stop();
            _session = null;
            _playerHudDetail = false;
        }
        public static Scope Measure(UiCallbackSection section,
            UiCallbackOrigin trigger = UiCallbackOrigin.Inherit, int frameTickIndex = 0)
            => _session == null ? default : _session.Begin(section, trigger, frameTickIndex);
        public static void Count(UiCallbackCounter counter, int amount = 1) => _session?.Count(counter, amount);
        public static void Fail() => _session?.Fail();

        public readonly struct Scope : IDisposable
        {
            private readonly UiCallbackSession _owner;
            private readonly int _id, _generation;
            internal Scope(UiCallbackSession owner, int id, int generation)
            { _owner = owner; _id = id; _generation = generation; }
            public void Dispose() => _owner?.End(_id, _generation);
        }
    }
}
#endif
