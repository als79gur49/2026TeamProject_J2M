using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.Objectives
{
    public enum StageCompletionPolicy
    {
        Disabled = 0,
        // TODO(goal-zone-condition-followup): alias or remove this legacy policy after migrated assets use PrimaryGoal condition entries.
        [Obsolete("Goal zone objective role is now represented by a PrimaryGoal PlayerAtAnyZone condition. Kept for serialized compatibility.", false)]
        RequirePlayerOnGoalWithAllConditions = 1,
        RequireAllConditions = 2,
    }

    public enum StageObjectiveConditionRole
    {
        None = 0,
        PrimaryGoal = 1,
        SecondaryGoal = 2,
        Challenge = 3,
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

    public readonly struct StageObjectiveDamageFact
    {
        public StageObjectiveDamageFact(
            int sourceId,
            AttackSourceKind sourceKind,
            int targetId,
            int amount,
            bool accepted,
            DamageRejectReason rejectReason,
            int localActionIndex = 0,
            bool hasPlayerDamageState = false,
            PlayerDamageState playerDamageState = default)
        {
            SourceId = sourceId;
            SourceKind = sourceKind;
            TargetId = targetId;
            Amount = amount;
            Accepted = accepted;
            RejectReason = rejectReason;
            LocalActionIndex = localActionIndex;
            HasPlayerDamageState = hasPlayerDamageState;
            PlayerDamageState = playerDamageState;
        }

        public int SourceId { get; }

        public AttackSourceKind SourceKind { get; }

        public int TargetId { get; }

        public int Amount { get; }

        public bool Accepted { get; }

        public DamageRejectReason RejectReason { get; }

        public int LocalActionIndex { get; }

        public bool HasPlayerDamageState { get; }

        public PlayerDamageState PlayerDamageState { get; }
    }

    public readonly struct StageObjectiveTickFacts
    {
        private static readonly IReadOnlyList<int> EmptyRemovedEntityIds = Array.Empty<int>();
        private static readonly IReadOnlyList<StageObjectiveDamageFact> EmptyDamageResolutions = Array.Empty<StageObjectiveDamageFact>();

        public static readonly StageObjectiveTickFacts Empty = new(
            0,
            Loop.PlayerTickCommand.None,
            EmptyRemovedEntityIds,
            EmptyDamageResolutions);

        public StageObjectiveTickFacts(
            int tickIndex,
            Loop.PlayerTickCommand playerCommand,
            IReadOnlyList<int> cleanupRemovedEntityIds,
            IReadOnlyList<StageObjectiveDamageFact> attackDamageResolutions)
        {
            TickIndex = tickIndex;
            PlayerCommand = playerCommand;
            CleanupRemovedEntityIds = cleanupRemovedEntityIds ?? EmptyRemovedEntityIds;
            AttackDamageResolutions = attackDamageResolutions ?? EmptyDamageResolutions;
        }

        public int TickIndex { get; }

        public Loop.PlayerTickCommand PlayerCommand { get; }

        public IReadOnlyList<int> CleanupRemovedEntityIds { get; }

        public IReadOnlyList<StageObjectiveDamageFact> AttackDamageResolutions { get; }
    }

    public readonly struct StageConditionStatus
    {
        public StageConditionStatus(
            string conditionId,
            string displayName,
            string conditionType,
            bool isSatisfied,
            string details = "",
            StageObjectiveConditionRole role = StageObjectiveConditionRole.None,
            bool required = true)
        {
            ConditionId = conditionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ConditionType = conditionType ?? string.Empty;
            IsSatisfied = isSatisfied;
            Details = details ?? string.Empty;
            Role = role;
            Required = required;
        }

        public string ConditionId { get; }

        public string DisplayName { get; }

        public string ConditionType { get; }

        public bool IsSatisfied { get; }

        public string Details { get; }

        public StageObjectiveConditionRole Role { get; }

        public bool Required { get; }

        public StageConditionStatus WithEntryMetadata(
            string stableConditionId,
            StageObjectiveConditionRole role,
            bool required)
        {
            return new StageConditionStatus(
                string.IsNullOrWhiteSpace(stableConditionId) ? ConditionId : stableConditionId.Trim(),
                DisplayName,
                ConditionType,
                IsSatisfied,
                Details,
                role,
                required);
        }
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

    public sealed class PlayerAtAnyZoneConditionRuntimeDefinition : StageConditionRuntimeDefinition
    {
        public const string RuntimeConditionType = "PlayerAtAnyZoneConditionAsset";

        private readonly int _playerEntityId;
        private readonly bool _requireAlive;
        private readonly StageZoneRuntimeDefinition[] _targetZones;

        public PlayerAtAnyZoneConditionRuntimeDefinition(
            string conditionId,
            string displayName,
            int playerEntityId,
            StageZoneRuntimeDefinition[] targetZones,
            bool requireAlive)
            : base(conditionId, displayName)
        {
            _playerEntityId = playerEntityId;
            _targetZones = targetZones ?? Array.Empty<StageZoneRuntimeDefinition>();
            _requireAlive = requireAlive;
        }

        public override IStageConditionRuntime CreateRuntime()
        {
            return new PlayerAtAnyZoneConditionRuntime(
                ConditionId,
                DisplayName,
                _playerEntityId,
                _targetZones,
                _requireAlive);
        }

        private sealed class PlayerAtAnyZoneConditionRuntime : IStageConditionRuntime
        {
            private readonly string _conditionId;
            private readonly string _displayName;
            private readonly int _playerEntityId;
            private readonly bool _requireAlive;
            private readonly StageZoneRuntimeDefinition[] _targetZones;
            private bool _isSatisfied;
            private string _matchedZoneId = string.Empty;

            public PlayerAtAnyZoneConditionRuntime(
                string conditionId,
                string displayName,
                int playerEntityId,
                StageZoneRuntimeDefinition[] targetZones,
                bool requireAlive)
            {
                _conditionId = conditionId ?? string.Empty;
                _displayName = displayName ?? string.Empty;
                _playerEntityId = playerEntityId;
                _targetZones = targetZones ?? Array.Empty<StageZoneRuntimeDefinition>();
                _requireAlive = requireAlive;
            }

            public bool IsSatisfied => _isSatisfied;

            public void Reset()
            {
                _isSatisfied = false;
                _matchedZoneId = string.Empty;
            }

            public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
            {
                if (finalSnapshot == null)
                {
                    throw new ArgumentNullException(nameof(finalSnapshot));
                }

                _isSatisfied = false;
                _matchedZoneId = string.Empty;

                if (_playerEntityId <= 0 ||
                    _targetZones.Length == 0 ||
                    !finalSnapshot.TryGetEntity(_playerEntityId, out var player) ||
                    player.boardPresence != EntityBoardPresence.Occupying)
                {
                    return;
                }

                if (_requireAlive && (player.hp <= 0 || player.markedForDeath))
                {
                    return;
                }

                for (var i = 0; i < _targetZones.Length; i++)
                {
                    if (!_targetZones[i].Contains(player.position))
                    {
                        continue;
                    }

                    _isSatisfied = true;
                    _matchedZoneId = _targetZones[i].ZoneId;
                    return;
                }
            }

            public StageConditionStatus CreateStatus()
            {
                return new StageConditionStatus(
                    _conditionId,
                    _displayName,
                    RuntimeConditionType,
                    _isSatisfied,
                    $"PlayerEntityId={_playerEntityId}|RequireAlive={(_requireAlive ? 1 : 0)}|MatchedZoneId={_matchedZoneId}|InZone={(_isSatisfied ? 1 : 0)}");
            }
        }
    }

    public sealed class StageObjectiveConditionRuntimeDefinitionEntry
    {
        public StageObjectiveConditionRuntimeDefinitionEntry(
            StageConditionRuntimeDefinition condition,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Required = required;
            Role = role;
            StableConditionId = string.IsNullOrWhiteSpace(stableConditionId)
                ? condition.ConditionId
                : stableConditionId.Trim();
        }

        public StageConditionRuntimeDefinition Condition { get; }

        public bool Required { get; }

        public StageObjectiveConditionRole Role { get; }

        public string StableConditionId { get; }
    }

    public sealed class StageObjectiveRuntimeDefinition
    {
        public static readonly StageObjectiveRuntimeDefinition Disabled = new(
            StageCompletionPolicy.Disabled,
            0,
            Array.Empty<StageZoneRuntimeDefinition>(),
            Array.Empty<StageZoneRuntimeDefinition>(),
            Array.Empty<StageObjectiveConditionRuntimeDefinitionEntry>());

        public StageObjectiveRuntimeDefinition(
            StageCompletionPolicy completionPolicy,
            int playerEntityId,
            StageZoneRuntimeDefinition[] zones,
            StageZoneRuntimeDefinition[] goalZones,
            StageConditionRuntimeDefinition[] requiredConditions)
            : this(
                completionPolicy,
                playerEntityId,
                zones,
                goalZones,
                BuildCompatibilityEntries(completionPolicy, playerEntityId, goalZones, requiredConditions))
        {
        }

        public StageObjectiveRuntimeDefinition(
            StageCompletionPolicy completionPolicy,
            int playerEntityId,
            StageZoneRuntimeDefinition[] zones,
            StageZoneRuntimeDefinition[] goalZones,
            StageObjectiveConditionRuntimeDefinitionEntry[] conditionEntries)
        {
            CompletionPolicy = completionPolicy;
            PlayerEntityId = playerEntityId;
            Zones = zones ?? Array.Empty<StageZoneRuntimeDefinition>();
            // TODO(goal-zone-condition-followup): remove GoalZones after content assets finish migrating to PrimaryGoal condition entries.
            GoalZones = goalZones ?? Array.Empty<StageZoneRuntimeDefinition>();
            ConditionEntries = ValidateConditionEntries(conditionEntries);
            RequiredConditions = ExtractLegacyRequiredConditionDefinitions(ConditionEntries);
        }

        public StageCompletionPolicy CompletionPolicy { get; }

        public int PlayerEntityId { get; }

        public IReadOnlyList<StageZoneRuntimeDefinition> Zones { get; }

        [Obsolete("Goal zone objective role is now represented by a PrimaryGoal PlayerAtAnyZone condition. Kept for serialized compatibility.", false)]
        public IReadOnlyList<StageZoneRuntimeDefinition> GoalZones { get; }

        public IReadOnlyList<StageConditionRuntimeDefinition> RequiredConditions { get; }

        public IReadOnlyList<StageObjectiveConditionRuntimeDefinitionEntry> ConditionEntries { get; }

        public bool HasObjective
        {
            get
            {
                switch (CompletionPolicy)
                {
                    case StageCompletionPolicy.Disabled:
                        return false;

                    case StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions:
                        return PlayerEntityId > 0 && HasRequiredPrimaryGoalEntry();

                    case StageCompletionPolicy.RequireAllConditions:
                        return HasRequiredConditionEntries();

                    default:
                        return false;
                }
            }
        }

        // TODO(goal-zone-condition-followup): delete this helper after no callers need legacy GoalZones compatibility.
        [Obsolete("Goal zone objective role is now represented by a PrimaryGoal PlayerAtAnyZone condition. Kept for serialized compatibility.", false)]
        public bool IsPlayerOnGoal(WorldSnapshot finalSnapshot)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (GoalZones.Count == 0 ||
                PlayerEntityId <= 0 ||
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
            var entries = CreateConditionRuntimeEntries();
            if (entries.Length == 0)
            {
                return Array.Empty<IStageConditionRuntime>();
            }

            var runtimes = new IStageConditionRuntime[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                runtimes[i] = entries[i].Runtime;
            }

            return runtimes;
        }

        internal StageObjectiveConditionRuntimeEntry[] CreateConditionRuntimeEntries()
        {
            if (ConditionEntries.Count == 0)
            {
                return Array.Empty<StageObjectiveConditionRuntimeEntry>();
            }

            var entries = new StageObjectiveConditionRuntimeEntry[ConditionEntries.Count];
            for (var i = 0; i < ConditionEntries.Count; i++)
            {
                var definitionEntry = ConditionEntries[i];
                entries[i] = new StageObjectiveConditionRuntimeEntry(
                    definitionEntry.Condition?.CreateRuntime() ??
                    throw new InvalidOperationException(
                        $"Stage condition definition at index {i} compiled a null runtime."),
                    definitionEntry.Required,
                    definitionEntry.Role,
                    definitionEntry.StableConditionId);
            }

            return entries;
        }

        private bool HasRequiredConditionEntries()
        {
            for (var i = 0; i < ConditionEntries.Count; i++)
            {
                if (ConditionEntries[i].Required)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasRequiredPrimaryGoalEntry()
        {
            for (var i = 0; i < ConditionEntries.Count; i++)
            {
                if (ConditionEntries[i].Required &&
                    ConditionEntries[i].Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    return true;
                }
            }

            return false;
        }

        private static StageObjectiveConditionRuntimeDefinitionEntry[] BuildCompatibilityEntries(
            StageCompletionPolicy completionPolicy,
            int playerEntityId,
            StageZoneRuntimeDefinition[] goalZones,
            StageConditionRuntimeDefinition[] requiredConditions)
        {
            var entries = new List<StageObjectiveConditionRuntimeDefinitionEntry>();
            var effectiveRequiredConditions = requiredConditions ?? Array.Empty<StageConditionRuntimeDefinition>();
            for (var i = 0; i < effectiveRequiredConditions.Length; i++)
            {
                if (effectiveRequiredConditions[i] == null)
                {
                    continue;
                }

                entries.Add(new StageObjectiveConditionRuntimeDefinitionEntry(
                    effectiveRequiredConditions[i],
                    required: true,
                    StageObjectiveConditionRole.None,
                    effectiveRequiredConditions[i].ConditionId));
            }

            if (completionPolicy != StageCompletionPolicy.Disabled &&
                goalZones != null &&
                goalZones.Length > 0)
            {
                entries.Add(new StageObjectiveConditionRuntimeDefinitionEntry(
                    new PlayerAtAnyZoneConditionRuntimeDefinition(
                        "legacy-primary-goal",
                        "Primary Goal",
                        playerEntityId,
                        goalZones,
                        requireAlive: true),
                    required: completionPolicy == StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions,
                    StageObjectiveConditionRole.PrimaryGoal,
                    "legacy-primary-goal"));
            }

            return entries.ToArray();
        }

        private static StageObjectiveConditionRuntimeDefinitionEntry[] ValidateConditionEntries(
            StageObjectiveConditionRuntimeDefinitionEntry[] conditionEntries)
        {
            if (conditionEntries == null || conditionEntries.Length == 0)
            {
                return Array.Empty<StageObjectiveConditionRuntimeDefinitionEntry>();
            }

            var primaryGoalCount = 0;
            var stableConditionIds = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new StageObjectiveConditionRuntimeDefinitionEntry[conditionEntries.Length];
            for (var i = 0; i < conditionEntries.Length; i++)
            {
                var entry = conditionEntries[i] ??
                            throw new InvalidOperationException(
                                $"Stage objective condition runtime entry at index {i} cannot be null.");
                if (entry.Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    primaryGoalCount++;
                }

                if (!stableConditionIds.Add(entry.StableConditionId))
                {
                    throw new InvalidOperationException(
                        $"Stage objective runtime definition contains duplicate condition stable id '{entry.StableConditionId}'.");
                }

                normalized[i] = entry;
            }

            if (primaryGoalCount > 1)
            {
                throw new InvalidOperationException("Stage objective runtime definition cannot contain more than one PrimaryGoal condition entry.");
            }

            return normalized;
        }

        private static StageConditionRuntimeDefinition[] ExtractLegacyRequiredConditionDefinitions(
            IReadOnlyList<StageObjectiveConditionRuntimeDefinitionEntry> conditionEntries)
        {
            if (conditionEntries == null || conditionEntries.Count == 0)
            {
                return Array.Empty<StageConditionRuntimeDefinition>();
            }

            var requiredConditions = new List<StageConditionRuntimeDefinition>();
            for (var i = 0; i < conditionEntries.Count; i++)
            {
                var entry = conditionEntries[i];
                if (entry.Required && entry.Role != StageObjectiveConditionRole.PrimaryGoal)
                {
                    requiredConditions.Add(entry.Condition);
                }
            }

            return requiredConditions.ToArray();
        }
    }

    internal readonly struct StageObjectiveConditionRuntimeEntry
    {
        public StageObjectiveConditionRuntimeEntry(
            IStageConditionRuntime runtime,
            bool required,
            StageObjectiveConditionRole role,
            string stableConditionId)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Required = required;
            Role = role;
            StableConditionId = stableConditionId ?? string.Empty;
        }

        public IStageConditionRuntime Runtime { get; }

        public bool Required { get; }

        public StageObjectiveConditionRole Role { get; }

        public string StableConditionId { get; }
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
