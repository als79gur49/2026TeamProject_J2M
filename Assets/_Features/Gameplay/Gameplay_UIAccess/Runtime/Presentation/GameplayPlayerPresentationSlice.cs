using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.Gameplay.UIAccess.Presentation
{
    public readonly struct GameplayPlayerPresentationSlice
    {
        public GameplayPlayerPresentationSlice(
            int playerEntityId,
            GameplayUiActionKind activeActionKind,
            int activeActionSequence,
            GameplayUiDirection actionDirection,
            int targetEntityId,
            bool startedThisTick,
            bool executedThisTick,
            bool completedThisTick,
            bool canceledThisTick,
            bool isRecoveryPhase,
            GameplayUiActionResolutionKind resolutionKind,
            bool shouldPlayWalkLoop,
            bool moveMotionGeneratedThisTick,
            bool waitingForNextMoveCadence,
            bool tookDamageThisTick,
            int damageAmount)
        {
            PlayerEntityId = playerEntityId;
            ActiveActionKind = activeActionKind;
            ActiveActionSequence = activeActionSequence;
            ActionDirection = actionDirection;
            TargetEntityId = targetEntityId;
            StartedThisTick = startedThisTick;
            ExecutedThisTick = executedThisTick;
            CompletedThisTick = completedThisTick;
            CanceledThisTick = canceledThisTick;
            IsRecoveryPhase = isRecoveryPhase;
            ResolutionKind = resolutionKind;
            ShouldPlayWalkLoop = shouldPlayWalkLoop;
            MoveMotionGeneratedThisTick = moveMotionGeneratedThisTick;
            WaitingForNextMoveCadence = waitingForNextMoveCadence;
            TookDamageThisTick = tookDamageThisTick;
            DamageAmount = damageAmount;
        }

        public int PlayerEntityId { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public int ActiveActionSequence { get; }

        public GameplayUiDirection ActionDirection { get; }

        public int TargetEntityId { get; }

        public bool StartedThisTick { get; }

        public bool ExecutedThisTick { get; }

        public bool CompletedThisTick { get; }

        public bool CanceledThisTick { get; }

        public bool IsRecoveryPhase { get; }

        public GameplayUiActionResolutionKind ResolutionKind { get; }

        public bool ShouldPlayWalkLoop { get; }

        public bool MoveMotionGeneratedThisTick { get; }

        public bool WaitingForNextMoveCadence { get; }

        public bool TookDamageThisTick { get; }

        public int DamageAmount { get; }
    }
}
