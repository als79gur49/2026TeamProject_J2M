using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Feature.Stages
{
    public static class StageLaunchContextStore
    {
        private const string PendingEditorStageIdSessionKey =
            "Game.Feature.Stages.PendingEditorDirectPlayStageId";

        private static StageId currentStageId = StageId.None;
        private static bool hasCurrentStageId;

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

#if UNITY_EDITOR
            if (TryConsumePendingEditorDirectPlay(out stageId))
            {
                currentStageId = stageId;
                hasCurrentStageId = true;
                return true;
            }
#endif

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

#if UNITY_EDITOR
        public static void PrimePendingEditorDirectPlay(StageId stageId)
        {
            if (!stageId.IsValid)
            {
                throw new System.ArgumentException("StageId must be canonical.", nameof(stageId));
            }

            SessionState.SetString(PendingEditorStageIdSessionKey, stageId.Value);
        }

        public static bool TryPeekPendingEditorDirectPlay(out StageId stageId)
        {
            var rawStageId = SessionState.GetString(PendingEditorStageIdSessionKey, string.Empty);
            if (StageId.TryCreate(rawStageId, out stageId))
            {
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        private static bool TryConsumePendingEditorDirectPlay(out StageId stageId)
        {
            if (TryPeekPendingEditorDirectPlay(out stageId))
            {
                SessionState.EraseString(PendingEditorStageIdSessionKey);
                return true;
            }

            return false;
        }
#endif

        private static void ClearPendingEditorDirectPlayInternal()
        {
#if UNITY_EDITOR
            SessionState.EraseString(PendingEditorStageIdSessionKey);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            ClearCurrent();
        }
    }
}
