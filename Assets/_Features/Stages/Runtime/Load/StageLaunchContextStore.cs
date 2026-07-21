using System;
using UnityEngine;

namespace Game.Feature.Stages
{
    /// <summary>
    /// Immutable application-session identity for one stage launch operation.
    /// It is deliberately not serialized; persistent campaign state owns only the committed active slot.
    /// </summary>
    public sealed class StageLaunchContext : IEquatable<StageLaunchContext>
    {
        public StageLaunchContext(
            Guid token,
            int slotNumber,
            StageId stageId,
            StageNavigationKind navigationKind,
            string source)
        {
            if (token == Guid.Empty)
            {
                throw new ArgumentException("Stage launch context requires a unique token.", nameof(token));
            }

            if (slotNumber < 0 || slotNumber > SaveSlotStore.SlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), slotNumber, "Stage launch context slot is out of range.");
            }

            if (!stageId.IsValid)
            {
                throw new ArgumentException("Stage launch context requires a canonical StageId.", nameof(stageId));
            }

            if (navigationKind == StageNavigationKind.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(navigationKind),
                    navigationKind,
                    "Stage launch context requires a navigation kind.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("Stage launch context requires a source.", nameof(source));
            }

            Token = token;
            SlotNumber = slotNumber;
            StageId = stageId;
            NavigationKind = navigationKind;
            Source = source;
        }

        public Guid Token { get; }

        /// <summary>
        /// Campaign handoffs carry 1..SlotCount. Explicit pending-less reload and DirectPlay
        /// contexts use 0 and bind to their already-committed/runtime namespace at validation.
        /// </summary>
        public int SlotNumber { get; }

        public StageId StageId { get; }

        public StageNavigationKind NavigationKind { get; }

        public string Source { get; }

        public bool HasCampaignSlot => SaveSlotStore.IsValidSlotNumber(SlotNumber);

        public static StageLaunchContext FromHandoff(CampaignLaunchHandoff handoff)
        {
            if (handoff == null)
            {
                throw new ArgumentNullException(nameof(handoff));
            }

            return new StageLaunchContext(
                handoff.Token,
                handoff.SlotNumber,
                handoff.StageId,
                handoff.NavigationKind,
                handoff.Source);
        }

        public static StageLaunchContext CreatePendinglessReload(StageNavigationRequest request)
        {
            if (!request.IsValid)
            {
                throw new ArgumentException("Pending-less reload requires a valid request.", nameof(request));
            }

            return new StageLaunchContext(
                Guid.NewGuid(),
                0,
                request.StageId,
                request.NavigationKind,
                request.Source);
        }

        internal static StageLaunchContext CreateDirectPlay(StageId stageId)
        {
            return new StageLaunchContext(
                Guid.NewGuid(),
                0,
                stageId,
                StageNavigationKind.Continue,
                "editor-direct-play");
        }

        public bool Matches(CampaignLaunchHandoff handoff)
        {
            return handoff != null &&
                   Token == handoff.Token &&
                   SlotNumber == handoff.SlotNumber &&
                   StageId.Equals(handoff.StageId) &&
                   NavigationKind == handoff.NavigationKind &&
                   string.Equals(Source, handoff.Source, StringComparison.Ordinal);
        }

        public bool Matches(StageNavigationRequest request)
        {
            return request.IsValid &&
                   StageId.Equals(request.StageId) &&
                   NavigationKind == request.NavigationKind &&
                   string.Equals(Source, request.Source, StringComparison.Ordinal);
        }

        public bool Equals(StageLaunchContext other)
        {
            return other != null &&
                   Token == other.Token &&
                   SlotNumber == other.SlotNumber &&
                   StageId.Equals(other.StageId) &&
                   NavigationKind == other.NavigationKind &&
                   string.Equals(Source, other.Source, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StageLaunchContext);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Token.GetHashCode();
                hash = (hash * 397) ^ SlotNumber;
                hash = (hash * 397) ^ StageId.GetHashCode();
                hash = (hash * 397) ^ (int)NavigationKind;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Source);
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"StageLaunchContext(slot={SlotNumber}, stage={StageId.Value}, navigation={NavigationKind}, source={Source}, token={Token:N})";
        }
    }

    public static class StageLaunchContextStore
    {
        public delegate bool TryGetPendingStageId(out StageId stageId);

        private static readonly object Sync = new();
        private static StageLaunchContext current;
        private static Action<StageId> primePendingEditorDirectPlay;
        private static TryGetPendingStageId tryPeekPendingEditorDirectPlay;
        private static TryGetPendingStageId tryConsumePendingEditorDirectPlay;
        private static Action clearPendingEditorDirectPlay;
        private static StageId fallbackPendingEditorStageId = StageId.None;
        private static bool hasFallbackPendingEditorStageId;

        public static StageId CurrentStageId =>
            TryPeek(out var context) ? context.StageId : StageId.None;

        public static bool TrySetCurrent(StageLaunchContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            lock (Sync)
            {
                if (current != null)
                {
                    return false;
                }

                current = context;
            }

            ClearPendingEditorDirectPlayInternal();
            return true;
        }

        public static bool TryPeek(out StageLaunchContext context)
        {
            lock (Sync)
            {
                context = current;
                return context != null;
            }
        }

        public static bool IsCurrent(StageLaunchContext expected)
        {
            if (expected == null)
            {
                return false;
            }

            lock (Sync)
            {
                return current != null && current.Equals(expected);
            }
        }

        public static bool TryClear(StageLaunchContext expected)
        {
            if (expected == null)
            {
                return false;
            }

            lock (Sync)
            {
                if (current == null || !current.Equals(expected))
                {
                    return false;
                }

                current = null;
                return true;
            }
        }

        public static bool TryConsume(StageLaunchContext expected, out StageLaunchContext consumed)
        {
            if (expected == null)
            {
                consumed = null;
                return false;
            }

            lock (Sync)
            {
                if (current == null || !current.Equals(expected))
                {
                    consumed = null;
                    return false;
                }

                consumed = current;
                current = null;
                return true;
            }
        }

        /// <summary>
        /// Compatibility entry point for explicit DirectPlay/test launchers.
        /// Production navigation must use a full StageLaunchContext and TrySetCurrent.
        /// </summary>
        public static void SetCurrent(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("StageId must be canonical.", nameof(stageId));
            }

            if (!TrySetCurrent(StageLaunchContext.CreateDirectPlay(stageId)))
            {
                throw new InvalidOperationException("A different stage launch context is already current.");
            }
        }

        public static bool TryGetCurrent(out StageId stageId)
        {
            if (TryPeek(out var context))
            {
                stageId = context.StageId;
                return true;
            }

            if (tryConsumePendingEditorDirectPlay != null &&
                tryConsumePendingEditorDirectPlay(out stageId))
            {
                return TrySetCurrent(StageLaunchContext.CreateDirectPlay(stageId));
            }

            if (tryConsumePendingEditorDirectPlay == null &&
                hasFallbackPendingEditorStageId)
            {
                stageId = fallbackPendingEditorStageId;
                fallbackPendingEditorStageId = StageId.None;
                hasFallbackPendingEditorStageId = false;
                return TrySetCurrent(StageLaunchContext.CreateDirectPlay(stageId));
            }

            stageId = StageId.None;
            return false;
        }

        public static void Clear()
        {
            ClearCurrent();
            ClearPendingEditorDirectPlayInternal();
        }

        public static void ClearCurrent()
        {
            lock (Sync)
            {
                current = null;
            }
        }

        public static bool TryClearCurrent(StageId expectedStageId)
        {
            lock (Sync)
            {
                if (current == null || !current.StageId.Equals(expectedStageId))
                {
                    return false;
                }

                // Compatibility only. Production failure paths use TryClear(expected)
                // so a same-stage newer operation cannot be cleared.
                current = null;
                return true;
            }
        }

        public static void ConfigurePendingEditorDirectPlayStore(
            Action<StageId> primePending,
            TryGetPendingStageId tryPeekPending,
            TryGetPendingStageId tryConsumePending,
            Action clearPending)
        {
            primePendingEditorDirectPlay = primePending;
            tryPeekPendingEditorDirectPlay = tryPeekPending;
            tryConsumePendingEditorDirectPlay = tryConsumePending;
            clearPendingEditorDirectPlay = clearPending;
        }

        public static void PrimePendingEditorDirectPlay(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("StageId must be canonical.", nameof(stageId));
            }

            if (primePendingEditorDirectPlay != null)
            {
                primePendingEditorDirectPlay(stageId);
                return;
            }

            fallbackPendingEditorStageId = stageId;
            hasFallbackPendingEditorStageId = true;
        }

        public static bool TryPeekPendingEditorDirectPlay(out StageId stageId)
        {
            if (tryPeekPendingEditorDirectPlay != null)
            {
                return tryPeekPendingEditorDirectPlay(out stageId);
            }

            if (hasFallbackPendingEditorStageId)
            {
                stageId = fallbackPendingEditorStageId;
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        private static void ClearPendingEditorDirectPlayInternal()
        {
            if (clearPendingEditorDirectPlay != null)
            {
                clearPendingEditorDirectPlay();
                return;
            }

            fallbackPendingEditorStageId = StageId.None;
            hasFallbackPendingEditorStageId = false;
        }

        internal static void ResetForTests()
        {
            Clear();
        }

        internal static void ResetRuntimeStateForTests()
        {
            ResetRuntimeLaunchContext();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            ResetRuntimeLaunchContext();
        }

        private static void ResetRuntimeLaunchContext()
        {
            ClearCurrent();
        }
    }
}
