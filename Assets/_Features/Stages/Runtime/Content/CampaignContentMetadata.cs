using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(menuName = "Gameplay/Stages/Campaign Content Metadata", fileName = "CampaignContent")]
    public sealed class CampaignContentMetadata : ScriptableObject
    {
        [SerializeField] private string campaignId = string.Empty;
        [SerializeField] private string displayName = string.Empty;

        public string CampaignId => campaignId ?? string.Empty;

        public string DisplayName => displayName ?? string.Empty;

        public void Set(string campaignIdValue, string displayNameValue)
        {
            campaignId = campaignIdValue ?? string.Empty;
            displayName = displayNameValue ?? string.Empty;
        }
    }
}
