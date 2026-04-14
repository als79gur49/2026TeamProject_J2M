using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public readonly struct GameplayPlayerPresentationSlice
    {
        public GameplayPlayerPresentationSlice(
            GameplayUiActionKind activeActionKind,
            GameplayUiDirection actionDirection,
            int targetEntityId,
            bool startedThisTick,
            bool executedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool shouldPlayWalkLoop,
            bool moveMotionGeneratedThisTick,
            bool waitingForNextMoveCadence,
            bool tookDamageThisTick,
            int damageAmount)
        {
            ActiveActionKind = activeActionKind;
            ActionDirection = actionDirection;
            TargetEntityId = targetEntityId;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            ShouldPlayWalkLoop = shouldPlayWalkLoop;
            MoveMotionGeneratedThisTick = moveMotionGeneratedThisTick;
            WaitingForNextMoveCadence = waitingForNextMoveCadence;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public GameplayUiActionKind ActiveActionKind { get; }

        public GameplayUiDirection ActionDirection { get; }

        public int TargetEntityId { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public bool ShouldPlayWalkLoop { get; }

        public bool MoveMotionGeneratedThisTick { get; }

        public bool WaitingForNextMoveCadence { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }
}
