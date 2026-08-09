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
            string source,
            EditorDirectPlayContext editorDirectPlayContext = default)
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
            EditorDirectPlayContext = editorDirectPlayContext.ForStage(stageId);
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

        public EditorDirectPlayContext EditorDirectPlayContext { get; }

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
                request.Source,
                request.EditorDirectPlayContext);
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
                   string.Equals(Source, request.Source, StringComparison.Ordinal) &&
                   EditorDirectPlayContext.Equals(request.EditorDirectPlayContext);
        }

        public bool Equals(StageLaunchContext other)
        {
            return other != null &&
                   Token == other.Token &&
                   SlotNumber == other.SlotNumber &&
                   StageId.Equals(other.StageId) &&
                   NavigationKind == other.NavigationKind &&
                   string.Equals(Source, other.Source, StringComparison.Ordinal) &&
                   EditorDirectPlayContext.Equals(other.EditorDirectPlayContext);
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
                hash = (hash * 397) ^ EditorDirectPlayContext.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return
                $"StageLaunchContext(slot={SlotNumber}, stage={StageId.Value}, navigation={NavigationKind}, source={Source}, directPlay={EditorDirectPlayContext.Mode}, token={Token:N})";
        }
    }

    public static class StageLaunchContextStore
    {
        public delegate bool TryGetPendingStageLaunchContext(out StageLaunchContext context);

        private static readonly object Sync = new();
        private static StageLaunchContext current;
        private static Action<StageLaunchContext> primePendingEditorDirectPlay;
        private static TryGetPendingStageLaunchContext tryPeekPendingEditorDirectPlay;
        private static TryGetPendingStageLaunchContext tryConsumePendingEditorDirectPlay;
        private static Func<StageLaunchContext, bool> tryClearPendingEditorDirectPlay;
        private static Action clearPendingEditorDirectPlay;
        private static StageLaunchContext fallbackPendingEditorDirectPlay;

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
                tryConsumePendingEditorDirectPlay(out var pendingContext))
            {
                stageId = pendingContext.StageId;
                return TrySetCurrent(pendingContext);
            }

            if (tryConsumePendingEditorDirectPlay == null &&
                fallbackPendingEditorDirectPlay != null)
            {
                var fallbackContext = fallbackPendingEditorDirectPlay;
                fallbackPendingEditorDirectPlay = null;
                stageId = fallbackContext.StageId;
                return TrySetCurrent(fallbackContext);
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
            Action<StageLaunchContext> primePending,
            TryGetPendingStageLaunchContext tryPeekPending,
            TryGetPendingStageLaunchContext tryConsumePending,
            Func<StageLaunchContext, bool> tryClearPending,
            Action clearPending)
        {
            primePendingEditorDirectPlay = primePending;
            tryPeekPendingEditorDirectPlay = tryPeekPending;
            tryConsumePendingEditorDirectPlay = tryConsumePending;
            tryClearPendingEditorDirectPlay = tryClearPending;
            clearPendingEditorDirectPlay = clearPending;
        }

        public static StageLaunchContext PrimePendingEditorDirectPlay(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new ArgumentException("StageId must be canonical.", nameof(stageId));
            }

            var context = StageLaunchContext.CreateDirectPlay(stageId);
            PrimePendingEditorDirectPlay(context);
            return context;
        }

        internal static void PrimePendingEditorDirectPlay(StageLaunchContext context)
        {
            ThrowIfNotDirectPlayContext(context);

            if (primePendingEditorDirectPlay != null)
            {
                primePendingEditorDirectPlay(context);
                return;
            }

            fallbackPendingEditorDirectPlay = context;
        }

        public static bool TryPeekPendingEditorDirectPlay(out StageId stageId)
        {
            if (TryPeekPendingEditorDirectPlayContext(out var context))
            {
                stageId = context.StageId;
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        public static bool TryPeekPendingEditorDirectPlayContext(out StageLaunchContext context)
        {
            if (tryPeekPendingEditorDirectPlay != null)
            {
                return tryPeekPendingEditorDirectPlay(out context);
            }

            context = fallbackPendingEditorDirectPlay;
            return context != null;
        }

        public static bool TryClearPendingEditorDirectPlay(StageLaunchContext expected)
        {
            if (expected == null)
            {
                return false;
            }

            if (tryClearPendingEditorDirectPlay != null)
            {
                return tryClearPendingEditorDirectPlay(expected);
            }

            if (fallbackPendingEditorDirectPlay == null ||
                !fallbackPendingEditorDirectPlay.Equals(expected))
            {
                return false;
            }

            fallbackPendingEditorDirectPlay = null;
            return true;
        }

        private static void ClearPendingEditorDirectPlayInternal()
        {
            if (clearPendingEditorDirectPlay != null)
            {
                clearPendingEditorDirectPlay();
                return;
            }

            fallbackPendingEditorDirectPlay = null;
        }

        private static void ThrowIfNotDirectPlayContext(StageLaunchContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.SlotNumber != 0 ||
                context.NavigationKind != StageNavigationKind.Continue ||
                !string.Equals(context.Source, "editor-direct-play", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Pending Editor DirectPlay requires the canonical direct-play context identity.",
                    nameof(context));
            }
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
