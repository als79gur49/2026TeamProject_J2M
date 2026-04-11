using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Objectives
{
    public enum StageCompletionPolicy
    {
        Disabled = 0,
        RequirePlayerOnGoalWithAllConditions = 1,
    }

    public readonly struct StageZoneRuntimeRegion
    {
        public StageZoneRuntimeRegion(UnityEngine.Vector2Int minInclusive, UnityEngine.Vector2Int maxInclusive)
        {
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
        }

        public UnityEngine.Vector2Int MinInclusive { get; }

        public UnityEngine.Vector2Int MaxInclusive { get; }

        public bool Contains(UnityEngine.Vector2Int planarPosition)
        {
            return planarPosition.x >= MinInclusive.x &&
                   planarPosition.x <= MaxInclusive.x &&
                   planarPosition.y >= MinInclusive.y &&
                   planarPosition.y <= MaxInclusive.y;
        }
    }

    public readonly struct StageZoneRuntimeDefinition
    {
        public StageZoneRuntimeDefinition(
            string zoneId,
            FaceId faceId,
            StageZoneRuntimeRegion[] regions)
        {
            ZoneId = string.IsNullOrWhiteSpace(zoneId)
                ? throw new ArgumentException("Zone id cannot be null or whitespace.", nameof(zoneId))
                : zoneId.Trim();
            FaceId = faceId;
            Regions = regions ?? Array.Empty<StageZoneRuntimeRegion>();
        }

        public string ZoneId { get; }

        public FaceId FaceId { get; }

        public IReadOnlyList<StageZoneRuntimeRegion> Regions { get; }

        public bool Contains(SurfaceCell cell)
        {
            if (cell.face != FaceId)
            {
                return false;
            }

            var planarPosition = cell.PlanarPosition;
            for (var i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Contains(planarPosition))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public readonly struct StageObjectiveTickFacts
    {
        private static readonly IReadOnlyList<int> EmptyRemovedEntityIds = Array.Empty<int>();
        private static readonly IReadOnlyList<Loop.DamageResolutionRecord> EmptyDamageResolutions = Array.Empty<Loop.DamageResolutionRecord>();
        private static readonly IReadOnlyDictionary<Type, object> EmptyExtensions = new Dictionary<Type, object>();

        public static readonly StageObjectiveTickFacts Empty = new(
            0,
            Loop.PlayerTickCommand.None,
            EmptyRemovedEntityIds,
            EmptyDamageResolutions);

        public StageObjectiveTickFacts(
            int tickIndex,
            Loop.PlayerTickCommand playerCommand,
            IReadOnlyList<int> cleanupRemovedEntityIds,
            IReadOnlyList<Loop.DamageResolutionRecord> attackDamageResolutions,
            IReadOnlyDictionary<Type, object> extensions = null)
        {
            TickIndex = tickIndex;
            PlayerCommand = playerCommand;
            CleanupRemovedEntityIds = cleanupRemovedEntityIds ?? EmptyRemovedEntityIds;
            AttackDamageResolutions = attackDamageResolutions ?? EmptyDamageResolutions;
            Extensions = extensions ?? EmptyExtensions;
        }

        public int TickIndex { get; }

        public Loop.PlayerTickCommand PlayerCommand { get; }

        public IReadOnlyList<int> CleanupRemovedEntityIds { get; }

        public IReadOnlyList<Loop.DamageResolutionRecord> AttackDamageResolutions { get; }

        public IReadOnlyDictionary<Type, object> Extensions { get; }

        public bool TryGetExtension<TExtension>(out TExtension extension)
        {
            if (Extensions.TryGetValue(typeof(TExtension), out var boxedExtension) &&
                boxedExtension is TExtension typedExtension)
            {
                extension = typedExtension;
                return true;
            }

            extension = default;
            return false;
        }
    }

    public readonly struct StageConditionStatus
    {
        public StageConditionStatus(
            string conditionId,
            string displayName,
            string conditionType,
            bool isSatisfied,
            string details = "")
        {
            ConditionId = conditionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ConditionType = conditionType ?? string.Empty;
            IsSatisfied = isSatisfied;
            Details = details ?? string.Empty;
        }

        public string ConditionId { get; }

        public string DisplayName { get; }

        public string ConditionType { get; }

        public bool IsSatisfied { get; }

        public string Details { get; }
    }

    public interface IStageConditionRuntime
    {
        void Reset();

        void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts);

        bool IsSatisfied { get; }

        StageConditionStatus CreateStatus();
    }

    public abstract class StageConditionRuntimeDefinition
    {
        protected StageConditionRuntimeDefinition(string conditionId, string displayName)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Condition"
                : displayName.Trim();
            ConditionId = string.IsNullOrWhiteSpace(conditionId)
                ? DisplayName
                : conditionId.Trim();
        }

        public string ConditionId { get; }

        public string DisplayName { get; }

        public abstract IStageConditionRuntime CreateRuntime();
    }

    public sealed class StageObjectiveRuntimeDefinition
    {
        public static readonly StageObjectiveRuntimeDefinition Disabled = new(
            StageCompletionPolicy.Disabled,
            0,
            Array.Empty<StageZoneRuntimeDefinition>(),
            Array.Empty<StageZoneRuntimeDefinition>(),
            Array.Empty<StageConditionRuntimeDefinition>());

        public StageObjectiveRuntimeDefinition(
            StageCompletionPolicy completionPolicy,
            int playerEntityId,
            StageZoneRuntimeDefinition[] zones,
            StageZoneRuntimeDefinition[] goalZones,
            StageConditionRuntimeDefinition[] requiredConditions)
        {
            CompletionPolicy = completionPolicy;
            PlayerEntityId = playerEntityId;
            Zones = zones ?? Array.Empty<StageZoneRuntimeDefinition>();
            GoalZones = goalZones ?? Array.Empty<StageZoneRuntimeDefinition>();
            RequiredConditions = requiredConditions ?? Array.Empty<StageConditionRuntimeDefinition>();
        }

        public StageCompletionPolicy CompletionPolicy { get; }

        public int PlayerEntityId { get; }

        public IReadOnlyList<StageZoneRuntimeDefinition> Zones { get; }

        public IReadOnlyList<StageZoneRuntimeDefinition> GoalZones { get; }

        public IReadOnlyList<StageConditionRuntimeDefinition> RequiredConditions { get; }

        public bool HasObjective =>
            CompletionPolicy != StageCompletionPolicy.Disabled &&
            PlayerEntityId > 0 &&
            GoalZones.Count > 0;

        public bool IsPlayerOnGoal(WorldSnapshot finalSnapshot)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (!HasObjective ||
                !finalSnapshot.TryGetEntity(PlayerEntityId, out var player) ||
                player.hp <= 0 ||
                player.markedForDeath)
            {
                return false;
            }

            for (var i = 0; i < GoalZones.Count; i++)
            {
                if (GoalZones[i].Contains(player.position))
                {
                    return true;
                }
            }

            return false;
        }

        public StageObjectiveTracker CreateTracker()
        {
            return new StageObjectiveTracker(this);
        }

        internal IStageConditionRuntime[] CreateConditionRuntimes()
        {
            if (RequiredConditions.Count == 0)
            {
                return Array.Empty<IStageConditionRuntime>();
            }

            var runtimes = new IStageConditionRuntime[RequiredConditions.Count];
            for (var i = 0; i < RequiredConditions.Count; i++)
            {
                runtimes[i] = RequiredConditions[i]?.CreateRuntime() ??
                              throw new InvalidOperationException(
                                  $"Stage condition definition at index {i} compiled a null runtime.");
            }

            return runtimes;
        }
    }

    public sealed class StageObjectiveTickResult
    {
        public static readonly StageObjectiveTickResult NoObjective = new(
            hasObjective: false,
            goalReached: false,
            allConditionsSatisfied: false,
            clearedThisTick: false,
            isCleared: false,
            Array.Empty<StageConditionStatus>());

        public StageObjectiveTickResult(
            bool hasObjective,
            bool goalReached,
            bool allConditionsSatisfied,
            bool clearedThisTick,
            bool isCleared,
            IReadOnlyList<StageConditionStatus> conditionStatuses)
        {
            HasObjective = hasObjective;
            GoalReached = goalReached;
            AllConditionsSatisfied = allConditionsSatisfied;
            ClearedThisTick = clearedThisTick;
            IsCleared = isCleared;
            ConditionStatuses = conditionStatuses ?? Array.Empty<StageConditionStatus>();
        }

        public bool HasObjective { get; }

        public bool GoalReached { get; }

        public bool AllConditionsSatisfied { get; }

        public bool ClearedThisTick { get; }

        public bool IsCleared { get; }

        public IReadOnlyList<StageConditionStatus> ConditionStatuses { get; }
    }
}
