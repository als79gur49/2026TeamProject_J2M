using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Objectives
{
    public sealed class StageObjectiveTracker
    {
        private readonly IStageConditionRuntime[] _conditionRuntimes;
        private readonly StageObjectiveRuntimeDefinition _objectiveDefinition;
        private bool _isCleared;
        private StageObjectiveTickResult _currentResult;

        public StageObjectiveTracker(StageObjectiveRuntimeDefinition objectiveDefinition)
        {
            _objectiveDefinition = objectiveDefinition ?? StageObjectiveRuntimeDefinition.Disabled;
            _conditionRuntimes = _objectiveDefinition.CreateConditionRuntimes();
            Reset();
        }

        public StageObjectiveRuntimeDefinition ObjectiveDefinition => _objectiveDefinition;

        public StageObjectiveTickResult CurrentResult => _currentResult;

        public void Reset()
        {
            _isCleared = false;

            for (var i = 0; i < _conditionRuntimes.Length; i++)
            {
                _conditionRuntimes[i].Reset();
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

            for (var i = 0; i < _conditionRuntimes.Length; i++)
            {
                _conditionRuntimes[i].Advance(finalSnapshot, in tickFacts);
            }

            var goalReached = _objectiveDefinition.IsPlayerOnGoal(finalSnapshot);
            var allConditionsSatisfied = AreAllRequiredConditionsSatisfied();
            var shouldClear = ShouldClear(goalReached, allConditionsSatisfied);
            var clearedThisTick = !_isCleared && shouldClear;
            if (clearedThisTick)
            {
                _isCleared = true;
            }

            _currentResult = CreateResult(goalReached, clearedThisTick);
            return _currentResult;
        }

        private bool AreAllRequiredConditionsSatisfied()
        {
            if (_conditionRuntimes.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < _conditionRuntimes.Length; i++)
            {
                if (!_conditionRuntimes[i].IsSatisfied)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ShouldClear(bool goalReached, bool allConditionsSatisfied)
        {
            switch (_objectiveDefinition.CompletionPolicy)
            {
                case StageCompletionPolicy.RequirePlayerOnGoalWithAllConditions:
                    return goalReached && allConditionsSatisfied;

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
                allConditionsSatisfied: AreAllRequiredConditionsSatisfied(),
                clearedThisTick,
                isCleared: _isCleared,
                conditionStatuses: BuildConditionStatuses());
        }

        private IReadOnlyList<StageConditionStatus> BuildConditionStatuses()
        {
            if (_conditionRuntimes.Length == 0)
            {
                return Array.Empty<StageConditionStatus>();
            }

            var statuses = new StageConditionStatus[_conditionRuntimes.Length];
            for (var i = 0; i < _conditionRuntimes.Length; i++)
            {
                statuses[i] = _conditionRuntimes[i].CreateStatus();
            }

            return statuses;
        }
    }
}
