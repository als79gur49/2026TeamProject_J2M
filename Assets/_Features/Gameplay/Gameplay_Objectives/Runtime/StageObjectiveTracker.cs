using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Objectives
{
    public sealed class StageObjectiveTracker
    {
        private readonly StageObjectiveConditionRuntimeEntry[] _conditionEntries;
        private readonly StageObjectiveRuntimeDefinition _objectiveDefinition;
        private bool _hasEvaluatedObjectiveTick;
        private bool _isCleared;
        private bool _previousRequiredNonPrimaryConditionsSatisfied;
        private StageObjectiveTickResult _currentResult;

        public StageObjectiveTracker(StageObjectiveRuntimeDefinition objectiveDefinition)
        {
            _objectiveDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            _conditionEntries = _objectiveDefinition.CreateConditionRuntimeEntries();
            Reset();
        }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _objectiveDefinition;

        public StageObjectiveTickResult CurrentResult => _currentResult;

        public void Reset()
        {
            _hasEvaluatedObjectiveTick = false;
            _isCleared = false;
            _previousRequiredNonPrimaryConditionsSatisfied = false;

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                _conditionEntries[i].Runtime.Reset();
            }

            _currentResult = _objectiveDefinition.HasObjective
                ? CreateResult(
                    goalReached: false,
                    clearedThisTick: false,
                    allConditionsSatisfied: AreAllRequiredEntriesSatisfied(primaryGoalAvailable: true))
                : StageObjectiveTickResult.NoObjective;
        }

        public StageObjectiveTickResult Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
        {
            if (finalSnapshot == null)
            {
                throw new ArgumentNullException(nameof(finalSnapshot));
            }

            if (!_objectiveDefinition.HasObjective)
            {
                _currentResult = StageObjectiveTickResult.NoObjective;
                return _currentResult;
            }

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                _conditionEntries[i].Runtime.Advance(finalSnapshot, in tickFacts);
            }

            var hasRequiredNonPrimaryConditions = HasRequiredNonPrimaryConditions();
            var requiredNonPrimaryConditionsSatisfied = AreRequiredNonPrimaryConditionsSatisfied();
            var primaryGoalAvailable =
                !hasRequiredNonPrimaryConditions ||
                requiredNonPrimaryConditionsSatisfied;
            var goalReached = IsPrimaryGoalSatisfied(primaryGoalAvailable);
            var allConditionsSatisfied = AreAllRequiredEntriesSatisfied(primaryGoalAvailable);
            var requiredNonPrimaryConditionsSatisfiedThisTick =
                hasRequiredNonPrimaryConditions &&
                _hasEvaluatedObjectiveTick &&
                !_previousRequiredNonPrimaryConditionsSatisfied &&
                requiredNonPrimaryConditionsSatisfied;
            var shouldClear = ShouldClear(allConditionsSatisfied);
            var clearedThisTick = !_isCleared && shouldClear;
            if (clearedThisTick)
            {
                _isCleared = true;
            }

            _currentResult = CreateResult(
                goalReached,
                clearedThisTick,
                allConditionsSatisfied,
                primaryGoalAvailable,
                hasRequiredNonPrimaryConditions,
                requiredNonPrimaryConditionsSatisfied,
                requiredNonPrimaryConditionsSatisfiedThisTick);
            _previousRequiredNonPrimaryConditionsSatisfied = requiredNonPrimaryConditionsSatisfied;
            _hasEvaluatedObjectiveTick = true;
            return _currentResult;
        }

        private bool AreAllRequiredEntriesSatisfied(bool primaryGoalAvailable)
        {
            if (_conditionEntries.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Role == StageObjectiveConditionRole.PrimaryGoal &&
                    _conditionEntries[i].Required &&
                    !primaryGoalAvailable)
                {
                    return false;
                }

                if (_conditionEntries[i].Required &&
                    !_conditionEntries[i].Runtime.IsSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPrimaryGoalSatisfied(bool primaryGoalAvailable)
        {
            if (!primaryGoalAvailable)
            {
                return false;
            }

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    return _conditionEntries[i].Runtime.IsSatisfied;
                }
            }

            return false;
        }

        private bool HasRequiredNonPrimaryConditions()
        {
            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Required &&
                    _conditionEntries[i].Role != StageObjectiveConditionRole.PrimaryGoal)
                {
                    return true;
                }
            }

            return false;
        }

        private bool AreRequiredNonPrimaryConditionsSatisfied()
        {
            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Required &&
                    _conditionEntries[i].Role != StageObjectiveConditionRole.PrimaryGoal &&
                    !_conditionEntries[i].Runtime.IsSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldClear(bool allConditionsSatisfied)
        {
            switch (_objectiveDefinition.CompletionPolicy)
            {
                case StageCompletionPolicy.RequireAllConditions:
                    return allConditionsSatisfied;

                case StageCompletionPolicy.Disabled:
                default:
                    return false;
            }
        }

        private StageObjectiveTickResult CreateResult(
            bool goalReached,
            bool clearedThisTick,
            bool allConditionsSatisfied = false,
            bool primaryGoalAvailable = true,
            bool hasRequiredNonPrimaryConditions = false,
            bool requiredNonPrimaryConditionsSatisfied = true,
            bool requiredNonPrimaryConditionsSatisfiedThisTick = false)
        {
            return new StageObjectiveTickResult(
                hasObjective: true,
                goalReached,
                allConditionsSatisfied,
                clearedThisTick,
                isCleared: _isCleared,
                hasRequiredNonPrimaryConditions,
                requiredNonPrimaryConditionsSatisfied,
                requiredNonPrimaryConditionsSatisfiedThisTick,
                conditionStatuses: BuildConditionStatuses(primaryGoalAvailable));
        }

        private IReadOnlyList<StageConditionStatus> BuildConditionStatuses(bool primaryGoalAvailable)
        {
            if (_conditionEntries.Length == 0)
            {
                return Array.Empty<StageConditionStatus>();
            }

            var statuses = new StageConditionStatus[_conditionEntries.Length];
            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                var entry = _conditionEntries[i];
                var status = entry.Runtime.CreateStatus()
                    .WithEntryMetadata(entry.StableConditionId, entry.Role, entry.Required);
                statuses[i] = entry.Role == StageObjectiveConditionRole.PrimaryGoal && !primaryGoalAvailable
                    ? MaskLockedPrimaryGoalStatus(status)
                    : status;
            }

            return statuses;
        }

        private static StageConditionStatus MaskLockedPrimaryGoalStatus(StageConditionStatus status)
        {
            var details = string.IsNullOrWhiteSpace(status.Details)
                ? "PrimaryGoalAvailable=0|LockedByRequiredNonPrimary=1"
                : $"{status.Details}|PrimaryGoalAvailable=0|LockedByRequiredNonPrimary=1";

            return new StageConditionStatus(
                status.ConditionId,
                status.DisplayName,
                status.ConditionType,
                isSatisfied: false,
                details,
                status.Role,
                status.Required);
        }
    }
}
