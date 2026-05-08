using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Campaign Level Metadata", fileName = "CampaignLevel")]
    public sealed class CampaignLevelMetadata : ScriptableObject
    {
        [SerializeField] private string levelId = string.Empty;
        [SerializeField] private string displayName = string.Empty;

        public string LevelId => levelId ?? string.Empty;

        public string DisplayName => displayName ?? string.Empty;

        public void Set(string levelIdValue, string displayNameValue)
        {
            levelId = levelIdValue ?? string.Empty;
            displayName = displayNameValue ?? string.Empty;
        }
    }
}
