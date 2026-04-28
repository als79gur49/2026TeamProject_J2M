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
        // TODO(goal-zone-condition-followup): remove after StageContentEntry assets are migrated to PrimaryGoal PlayerAtAnyZone condition entries.
        [Obsolete("Goal zone objective role is now represented by a PrimaryGoal PlayerAtAnyZone condition. Kept for serialized compatibility.", false)]
        [Tooltip("Deprecated. Use ConditionEntries with Role=PrimaryGoal and PlayerAtAnyZoneConditionAsset. Kept for serialized compatibility.")]
        public string[] GoalZoneIds;
        [Tooltip("Legacy required conditions. New authoring should use ConditionEntries.")]
        public StageConditionAsset[] RequiredConditions;
        public StageObjectiveConditionEntry[] ConditionEntries;

        public string[] GetGoalZoneIdsOrEmpty()
        {
            return GoalZoneIds ?? Array.Empty<string>();
        }

        public StageConditionAsset[] GetRequiredConditionsOrEmpty()
        {
            return RequiredConditions ?? Array.Empty<StageConditionAsset>();
        }

        public StageObjectiveConditionEntry[] GetConditionEntriesOrEmpty()
        {
            return ConditionEntries ?? Array.Empty<StageObjectiveConditionEntry>();
        }

        public static StageObjectiveAuthoring CreateDefault()
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.Disabled,
                GoalZoneIds = Array.Empty<string>(),
                RequiredConditions = Array.Empty<StageConditionAsset>(),
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
    }
}
