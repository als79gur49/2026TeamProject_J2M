namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplaySessionReadModel
    {
        public GameplaySessionReadModel(
            int nextTickIndex,
            bool isPaused,
            bool canAcceptGameplayCommands,
            bool isStageCleared)
        {
            NextTickIndex = nextTickIndex;
            IsPaused = isPaused;
            CanAcceptGameplayCommands = canAcceptGameplayCommands;
            IsStageCleared = isStageCleared;
        }

        public int NextTickIndex { get; }

        public bool IsPaused { get; }

        public bool CanAcceptGameplayCommands { get; }

        public bool IsStageCleared { get; }
    }
}
