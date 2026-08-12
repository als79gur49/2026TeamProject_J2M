using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignStageSequenceEntry
    {
        [SerializeField] private StageId stageId;
        [SerializeField] private string levelGroupId;

        public StageId StageId => stageId;

        public string LevelGroupId => levelGroupId ?? string.Empty;

        public void Set(StageId stageIdValue, string levelGroupIdValue)
        {
            stageId = stageIdValue;
            levelGroupId = levelGroupIdValue ?? string.Empty;
        }
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Campaign Stage Sequence", fileName = "CampaignStageSequence")]
    public sealed class CampaignStageSequenceDefinition : ScriptableObject
    {
        [SerializeField] private CampaignStageSequenceEntry[] entries = Array.Empty<CampaignStageSequenceEntry>();

        public IReadOnlyList<CampaignStageSequenceEntry> Entries => entries ?? Array.Empty<CampaignStageSequenceEntry>();

        public void SetEntries(CampaignStageSequenceEntry[] value)
        {
            entries = value ?? Array.Empty<CampaignStageSequenceEntry>();
        }
    }
}
