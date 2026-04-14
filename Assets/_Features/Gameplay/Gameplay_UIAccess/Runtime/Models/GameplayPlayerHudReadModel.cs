using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayPlayerHudReadModel
    {
        public GameplayPlayerHudReadModel(
            bool isAvailable,
            int playerEntityId,
            int currentHp,
            Direction facing,
            PlayerActionKind activeActionKind,
            Direction activeActionDirection,
            int activeTargetEntityId,
            bool isActionInProgress,
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
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
        }

        public bool IsAvailable { get; }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public Direction Facing { get; }

        public PlayerActionKind ActiveActionKind { get; }

        public Direction ActiveActionDirection { get; }

        public int ActiveTargetEntityId { get; }

        public bool IsActionInProgress { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }
    }
}
