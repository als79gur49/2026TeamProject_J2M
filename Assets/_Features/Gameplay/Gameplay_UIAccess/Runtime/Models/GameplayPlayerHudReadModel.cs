namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayPlayerHudReadModel
    {
        public GameplayPlayerHudReadModel(
            bool isAvailable,
            int playerEntityId,
            int currentHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            GameplayUiDirection activeActionDirection,
            int activeTargetEntityId,
            bool isActionInProgress,
            bool isActionInRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick)
        {
            IsAvailable = isAvailable;
            PlayerEntityId = playerEntityId;
            CurrentHp = currentHp;
            Facing = facing;
            ActiveActionKind = activeActionKind;
            ActiveActionDirection = activeActionDirection;
            ActiveTargetEntityId = activeTargetEntityId;
            IsActionInProgress = isActionInProgress;
            IsActionInRecoveryPhase = isActionInRecoveryPhase;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
        }

        public bool IsAvailable { get; }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public GameplayUiDirection Facing { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public GameplayUiDirection ActiveActionDirection { get; }

        public int ActiveTargetEntityId { get; }

        public bool IsActionInProgress { get; }

        public bool IsActionInRecoveryPhase { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }
    }
}
