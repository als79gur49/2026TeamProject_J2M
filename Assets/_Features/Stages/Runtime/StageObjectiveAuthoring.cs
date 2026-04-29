using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [Serializable]
    public struct StageZoneRegionDefinition
    {
        public Vector2Int MinInclusive;
        public Vector2Int MaxInclusive;
    }

    [Serializable]
    public struct StageZoneDefinition
    {
        public string ZoneId;
        public FaceId FaceId;
        public StageZoneRegionDefinition[] Regions;

        public StageZoneRegionDefinition[] GetRegionsOrEmpty()
        {
            return Regions ?? Array.Empty<StageZoneRegionDefinition>();
        }
    }

    [Serializable]
    public struct StageObjectiveAuthoring
    {
        public StageCompletionPolicy CompletionPolicy;
        public string ObjectiveTitle;
        public string ObjectiveSummary;
        public StageObjectiveConditionEntry[] ConditionEntries;

        public StageObjectiveConditionEntry[] GetConditionEntriesOrEmpty()
        {
            return ConditionEntries ?? Array.Empty<StageObjectiveConditionEntry>();
        }

        public static StageObjectiveAuthoring CreateDefault()
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.Disabled,
                ObjectiveTitle = string.Empty,
                ObjectiveSummary = string.Empty,
                ConditionEntries = Array.Empty<StageObjectiveConditionEntry>(),
            };
        }
    }

    [Serializable]
    public struct StageObjectiveConditionEntry
    {
        public StageConditionAsset Condition;
        public bool Required;
        public StageObjectiveConditionRole Role;
        public string StableConditionId;
        public string DisplayText;
        public int SortOrder;
    }
}
