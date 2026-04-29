using System;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    public sealed class StageAuthoringNormalizedGameplaySnapshot
    {
        public StageAuthoringNormalizedGameplaySnapshot(
            StageBoardDefinition board,
            StageAuthoringNormalizedSpawn[] spawns,
            StageAuthoringNormalizedZone[] zones,
            StageAuthoringNormalizedObjective objective)
        {
            Board = board;
            Spawns = spawns ?? Array.Empty<StageAuthoringNormalizedSpawn>();
            Zones = zones ?? Array.Empty<StageAuthoringNormalizedZone>();
            Objective = objective ?? StageAuthoringNormalizedObjective.Empty;
        }

        public StageBoardDefinition Board { get; }

        public StageAuthoringNormalizedSpawn[] Spawns { get; }

        public StageAuthoringNormalizedZone[] Zones { get; }

        public StageAuthoringNormalizedObjective Objective { get; }
    }

    public readonly struct StageAuthoringNormalizedZone
    {
        public StageAuthoringNormalizedZone(
            string zoneId,
            FaceId faceId,
            StageAuthoringNormalizedZoneRegion[] regions)
        {
            ZoneId = zoneId ?? string.Empty;
            FaceId = faceId;
            Regions = regions ?? Array.Empty<StageAuthoringNormalizedZoneRegion>();
        }

        public string ZoneId { get; }

        public FaceId FaceId { get; }

        public StageAuthoringNormalizedZoneRegion[] Regions { get; }
    }

    public readonly struct StageAuthoringNormalizedZoneRegion
    {
        public StageAuthoringNormalizedZoneRegion(Vector2Int minInclusive, Vector2Int maxInclusive)
        {
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
        }

        public Vector2Int MinInclusive { get; }

        public Vector2Int MaxInclusive { get; }
    }

    public sealed class StageAuthoringNormalizedObjective
    {
        public static readonly StageAuthoringNormalizedObjective Empty = new(
            StageCompletionPolicy.Disabled,
            Array.Empty<StageAuthoringNormalizedObjectiveCondition>());

        public StageAuthoringNormalizedObjective(
            StageCompletionPolicy completionPolicy,
            StageAuthoringNormalizedObjectiveCondition[] conditions)
        {
            CompletionPolicy = completionPolicy;
            Conditions = conditions ?? Array.Empty<StageAuthoringNormalizedObjectiveCondition>();
        }

        public StageCompletionPolicy CompletionPolicy { get; }

        public StageAuthoringNormalizedObjectiveCondition[] Conditions { get; }
    }

    public readonly struct StageAuthoringNormalizedObjectiveCondition
    {
        public StageAuthoringNormalizedObjectiveCondition(
            StageConditionAsset condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId)
        {
            Condition = condition;
            Required = required;
            Role = role;
            StableConditionId = stableConditionId ?? string.Empty;
        }

        public StageConditionAsset Condition { get; }

        public bool Required { get; }

        public StageObjectiveConditionRole Role { get; }

        public string StableConditionId { get; }
    }
}
