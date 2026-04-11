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
        public string[] GoalZoneIds;
        public StageConditionAsset[] RequiredConditions;

        public string[] GetGoalZoneIdsOrEmpty()
        {
            return GoalZoneIds ?? Array.Empty<string>();
        }

        public StageConditionAsset[] GetRequiredConditionsOrEmpty()
        {
            return RequiredConditions ?? Array.Empty<StageConditionAsset>();
        }

        public static StageObjectiveAuthoring CreateDefault()
        {
            return new StageObjectiveAuthoring
            {
                CompletionPolicy = StageCompletionPolicy.Disabled,
                GoalZoneIds = Array.Empty<string>(),
                RequiredConditions = Array.Empty<StageConditionAsset>(),
            };
        }
    }
}
