namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayChanceAudioPolicy
    {
        Default = 0,
        SuppressChanceChangeCue = 1,
    }

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
            bool canStartActionThisTick,
            GameplayUiRecoveryCooldown? recoveryCooldown = null,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default)
            : this(
                isAvailable,
                playerEntityId,
                currentHp,
                currentHp,
                facing,
                activeActionKind,
                activeActionDirection,
                activeTargetEntityId,
                isActionInProgress,
                isActionInRecoveryPhase,
                canMoveThisTick,
                canStartActionThisTick,
                recoveryCooldown,
                canStartAnyActionThisTick,
                hasExplicitPushCandidateInCurrentDirection,
                hasRemainingChances,
                remainingChances,
                maxChances,
                chanceAudioPolicy)
        {
        }

        public GameplayPlayerHudReadModel(
            bool isAvailable,
            int playerEntityId,
            int currentHp,
            int maxHp,
            GameplayUiDirection facing,
            GameplayUiActionKind activeActionKind,
            GameplayUiDirection activeActionDirection,
            int activeTargetEntityId,
            bool isActionInProgress,
            bool isActionInRecoveryPhase,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            GameplayUiRecoveryCooldown? recoveryCooldown = null,
            bool canStartAnyActionThisTick = false,
            bool hasExplicitPushCandidateInCurrentDirection = false,
            bool hasRemainingChances = false,
            int remainingChances = 0,
            int maxChances = 0,
            GameplayChanceAudioPolicy chanceAudioPolicy = GameplayChanceAudioPolicy.Default)
        {
            IsAvailable = isAvailable;
            PlayerEntityId = playerEntityId;
            CurrentHp = currentHp;
            MaxHp = maxHp > 0 ? maxHp : currentHp;
            Facing = facing;
            ActiveActionKind = activeActionKind;
            ActiveActionDirection = activeActionDirection;
            ActiveTargetEntityId = activeTargetEntityId;
            IsActionInProgress = isActionInProgress;
            IsActionInRecoveryPhase = isActionInRecoveryPhase;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
            CanStartAnyActionThisTick = canStartAnyActionThisTick || canStartActionThisTick;
            HasExplicitPushCandidateInCurrentDirection = hasExplicitPushCandidateInCurrentDirection;
            HasRemainingChances = hasRemainingChances;
            RemainingChances = remainingChances;
            MaxChances = maxChances > 0
                ? maxChances
                : (hasRemainingChances ? remainingChances : 0);
            ChanceAudioPolicy = hasRemainingChances
                ? chanceAudioPolicy
                : GameplayChanceAudioPolicy.Default;
            RecoveryCooldown = recoveryCooldown;
        }

        public bool IsAvailable { get; }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public int MaxHp { get; }

        public GameplayUiDirection Facing { get; }

        public GameplayUiActionKind ActiveActionKind { get; }

        public GameplayUiDirection ActiveActionDirection { get; }

        public int ActiveTargetEntityId { get; }

        public bool IsActionInProgress { get; }

        public bool IsActionInRecoveryPhase { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }

        public bool CanStartAnyActionThisTick { get; }

        public bool HasExplicitPushCandidateInCurrentDirection { get; }

        public bool HasRemainingChances { get; }

        public int RemainingChances { get; }

        public int MaxChances { get; }

        public GameplayChanceAudioPolicy ChanceAudioPolicy { get; }

        public GameplayUiRecoveryCooldown? RecoveryCooldown { get; }
    }
}
