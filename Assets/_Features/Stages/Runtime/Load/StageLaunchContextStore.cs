using UnityEngine;

namespace Game.Feature.Stages
{
    public static class StageLaunchContextStore
    {
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
        }

        public static bool TryGetCurrent(out StageId stageId)
        {
            if (hasCurrentStageId)
            {
                stageId = currentStageId;
                return true;
            }

            stageId = StageId.None;
            return false;
        }

        public static void Clear()
        {
            currentStageId = StageId.None;
            hasCurrentStageId = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload()
        {
            Clear();
        }
    }
}
