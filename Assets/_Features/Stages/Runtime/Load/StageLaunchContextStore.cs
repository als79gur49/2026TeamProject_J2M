using UnityEngine;

namespace Game.Feature.Stages
{
    public static class StageLaunchContextStore
    {
        public delegate bool TryGetPendingStageId(out StageId stageId);

        private static StageId currentStageId = StageId.None;
        private static bool hasCurrentStageId;
        private static System.Action<StageId> primePendingEditorDirectPlay;
        private static TryGetPendingStageId tryPeekPendingEditorDirectPlay;
        private static TryGetPendingStageId tryConsumePendingEditorDirectPlay;
        private static System.Action clearPendingEditorDirectPlay;
        private static StageId fallbackPendingEditorStageId = StageId.None;
        private static bool hasFallbackPendingEditorStageId;

        public static StageId CurrentStageId => hasCurrentStageId ? currentStageId : StageId.None;

        public static void SetCurrent(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new System.ArgumentException("StageId must be canonical.", nameof(stageId));
            }

            currentStageId = stageId;
            hasCurrentStageId = true;
            ClearPendingEditorDirectPlayInternal();
        }

        public static bool TryGetCurrent(out StageId stageId)
        {
            if (hasCurrentStageId)
            {
                stageId = currentStageId;
                return true;
            }

            if (tryConsumePendingEditorDirectPlay != null &&
                tryConsumePendingEditorDirectPlay(out stageId))
            {
                currentStageId = stageId;
                hasCurrentStageId = true;
                return true;
            }

            if (tryConsumePendingEditorDirectPlay == null &&
                hasFallbackPendingEditorStageId)
            {
                stageId = fallbackPendingEditorStageId;
                fallbackPendingEditorStageId = StageId.None;
                hasFallbackPendingEditorStageId = false;
                currentStageId = stageId;
                hasCurrentStageId = true;
                return true;
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
            currentStageId = StageId.None;
            hasCurrentStageId = false;
        }

        public static bool TryClearCurrent(StageId expectedStageId)
        {
            if (!hasCurrentStageId || !currentStageId.Equals(expectedStageId))
            {
                return false;
            }

            ClearCurrent();
            return true;
        }

        public static void ConfigurePendingEditorDirectPlayStore(
            System.Action<StageId> primePending,
            TryGetPendingStageId tryPeekPending,
            TryGetPendingStageId tryConsumePending,
            System.Action clearPending)
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
                throw new System.ArgumentException("StageId must be canonical.", nameof(stageId));
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            ClearCurrent();
        }
    }
}
