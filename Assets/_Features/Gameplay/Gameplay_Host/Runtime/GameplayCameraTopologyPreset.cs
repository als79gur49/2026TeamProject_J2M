using UnityEngine;

namespace Game.Feature.Gameplay.Host
{
    [CreateAssetMenu(menuName = "Gameplay/Camera/Topology Preset", fileName = "GameplayCameraTopologyPreset")]
    public sealed class GameplayCameraTopologyPreset : ScriptableObject
    {
        [Tooltip("Stage-scoped shared tuning payload.")]
        [SerializeField] private GameplayCameraTopologySharedTuning sharedTuning =
            GameplayCameraTopologySharedTuning.CreateShowcaseDefault();

        public GameplayCameraTopologySharedTuning CreateSnapshot()
        {
            Validate();
            return sharedTuning.Clone();
        }

        public void Validate()
        {
            sharedTuning ??= GameplayCameraTopologySharedTuning.CreateShowcaseDefault();
            sharedTuning.Validate();
        }
    }
}
