using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Gameplay.UIAccess.Queries;

namespace Game.Feature.Gameplay.Host.UIAccess
{
    internal sealed class GameplayHostObjectiveQuery : IGameplayObjectiveQuery
    {
        private readonly TickRunner _tickRunner;

        public GameplayHostObjectiveQuery(TickRunner tickRunner)
        {
            _tickRunner = tickRunner;
        }

        public GameplayObjectiveReadModel Read()
        {
            var objectiveResult = _tickRunner?.CurrentObjectiveResult;
            if (objectiveResult == null)
            {
                return default;
            }

            return new GameplayObjectiveReadModel(
                objectiveResult.HasObjective,
                objectiveResult.GoalReached,
                objectiveResult.AllConditionsSatisfied,
                objectiveResult.IsCleared);
        }
    }
}
