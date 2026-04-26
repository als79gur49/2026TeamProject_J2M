using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public sealed class CampaignStageSequenceEntry
    {
        [SerializeField] private StageId stageId;
        [SerializeField] private string displayName;
        [SerializeField] private string levelGroupId;

        public StageId StageId => stageId;

        public string DisplayName => displayName ?? string.Empty;

        public string LevelGroupId => levelGroupId ?? string.Empty;

        public void Set(StageId stageIdValue, string displayNameValue, string levelGroupIdValue)
        {
            stageId = stageIdValue;
            displayName = displayNameValue ?? string.Empty;
            levelGroupId = levelGroupIdValue ?? string.Empty;
        }
    }

    [CreateAssetMenu(menuName = "Gameplay/Stages/Campaign Stage Sequence", fileName = "CampaignStageSequence")]
    public sealed class CampaignStageSequenceDefinition : ScriptableObject
    {
        public const string Level0GroupId = "level-0";
        public const string Level1GroupId = "level-1";
        public const string Level2GroupId = "level-2";
        public const string Level3GroupId = "level-3";
        public const string Level4GroupId = "level-4";
        public const string Level5GroupId = "level-5";

        public static readonly string[] CanonicalStageIdValues =
        {
            "stage-0-1",
            "stage-1-1",
            "stage-2-1",
            "stage-2-2",
            "stage-3-1",
            "stage-3-2",
            "stage-4-1",
            "stage-4-2",
            "stage-5-1",
        };

        public static readonly string[] CanonicalDisplayNames =
        {
            "0-1",
            "1-1",
            "2-1",
            "2-2",
            "3-1",
            "3-2",
            "4-1",
            "4-2",
            "5-1",
        };

        public static readonly string[] CanonicalLevelGroupIds =
        {
            Level0GroupId,
            Level1GroupId,
            Level2GroupId,
            Level2GroupId,
            Level3GroupId,
            Level3GroupId,
            Level4GroupId,
            Level4GroupId,
            Level5GroupId,
        };

        [SerializeField] private CampaignStageSequenceEntry[] entries = CreateCanonicalEntries();

        public IReadOnlyList<CampaignStageSequenceEntry> Entries => entries ?? Array.Empty<CampaignStageSequenceEntry>();

        public static CampaignStageSequenceDefinition CreateCanonicalRuntimeInstance()
        {
            var definition = CreateInstance<CampaignStageSequenceDefinition>();
            definition.entries = CreateCanonicalEntries();
            return definition;
        }

        public void SetEntries(CampaignStageSequenceEntry[] value)
        {
            entries = value ?? Array.Empty<CampaignStageSequenceEntry>();
        }

        private static CampaignStageSequenceEntry[] CreateCanonicalEntries()
        {
            var result = new CampaignStageSequenceEntry[CanonicalStageIdValues.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var entry = new CampaignStageSequenceEntry();
                entry.Set(
                    StageId.CreateOrThrow(CanonicalStageIdValues[i]),
                    CanonicalDisplayNames[i],
                    CanonicalLevelGroupIds[i]);
                result[i] = entry;
            }

            return result;
        }
    }
}
