using System;

namespace Game.Feature.Gameplay.Host
{
    [Serializable]
    public struct GameplayCameraBaselineAuthoringPolicy
    {
        public bool UseAuthoredSceneCameraPose;
        public bool UseAuthoredSceneCameraLens;

        public static GameplayCameraBaselineAuthoringPolicy CreateRuntimeDefault()
        {
            return new GameplayCameraBaselineAuthoringPolicy
            {
                UseAuthoredSceneCameraPose = false,
                UseAuthoredSceneCameraLens = true,
            };
        }

        public static GameplayCameraBaselineAuthoringPolicy CreateShowcaseDefault()
        {
            return new GameplayCameraBaselineAuthoringPolicy
            {
                UseAuthoredSceneCameraPose = true,
                UseAuthoredSceneCameraLens = true,
            };
        }
    }
}
