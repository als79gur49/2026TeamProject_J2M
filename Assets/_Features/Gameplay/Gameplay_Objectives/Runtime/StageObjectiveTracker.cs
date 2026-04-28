using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Objectives
{
    public sealed class StageObjectiveTracker
    {
        private readonly StageObjectiveConditionRuntimeEntry[] _conditionEntries;
        private readonly StageObjectiveRuntimeDefinition _objectiveDefinition;
        private bool _isCleared;
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
            _isCleared = false;

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                _conditionEntries[i].Runtime.Reset();
            }

            _currentResult = _objectiveDefinition.HasObjective
                ? CreateResult(goalReached: false, clearedThisTick: false)
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

            var allConditionsSatisfied = AreAllRequiredEntriesSatisfied();
            var goalReached = IsPrimaryGoalSatisfied();
            var shouldClear = ShouldClear(allConditionsSatisfied);
            var clearedThisTick = !_isCleared && shouldClear;
            if (clearedThisTick)
            {
                _isCleared = true;
            }

            _currentResult = CreateResult(goalReached, clearedThisTick);
            return _currentResult;
        }

        private bool AreAllRequiredEntriesSatisfied()
        {
            if (_conditionEntries.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Required &&
                    !_conditionEntries[i].Runtime.IsSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPrimaryGoalSatisfied()
        {
            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                if (_conditionEntries[i].Role == StageObjectiveConditionRole.PrimaryGoal)
                {
                    return _conditionEntries[i].Runtime.IsSatisfied;
                }
            }

            return false;
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

        private StageObjectiveTickResult CreateResult(bool goalReached, bool clearedThisTick)
        {
            return new StageObjectiveTickResult(
                hasObjective: true,
                goalReached,
                allConditionsSatisfied: AreAllRequiredEntriesSatisfied(),
                clearedThisTick,
                isCleared: _isCleared,
                conditionStatuses: BuildConditionStatuses());
        }

        private IReadOnlyList<StageConditionStatus> BuildConditionStatuses()
        {
            if (_conditionEntries.Length == 0)
            {
                return Array.Empty<StageConditionStatus>();
            }

            var statuses = new StageConditionStatus[_conditionEntries.Length];
            for (var i = 0; i < _conditionEntries.Length; i++)
            {
                var entry = _conditionEntries[i];
                statuses[i] = entry.Runtime.CreateStatus()
                    .WithEntryMetadata(entry.StableConditionId, entry.Role, entry.Required);
            }

            return statuses;
        }
    }
}
