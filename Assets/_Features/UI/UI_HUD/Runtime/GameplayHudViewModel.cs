namespace Game.Feature.UI.HUD
{
    public enum GameplayHudCommandFailureKind
    {
        None = 0,
        Paused = 1,
        Busy = 2,
        Unavailable = 3,
    }

    public readonly struct GameplayHudCommandResult
    {
        public GameplayHudCommandResult(bool accepted, GameplayHudCommandFailureKind failureKind)
        {
            Accepted = accepted;
            FailureKind = failureKind;
        }

        public bool Accepted { get; }

        public GameplayHudCommandFailureKind FailureKind { get; }

        public static GameplayHudCommandResult Accept()
        {
            return new GameplayHudCommandResult(true, GameplayHudCommandFailureKind.None);
        }

        public static GameplayHudCommandResult Reject(GameplayHudCommandFailureKind failureKind)
        {
            return new GameplayHudCommandResult(false, failureKind);
        }
    }

    public readonly struct GameplayHudState
    {
        public GameplayHudState(
            int playerEntityId,
            int currentHp,
            string facingText,
            string activeActionText,
            string topologyText,
            bool canMoveThisTick,
            bool canStartActionThisTick,
            bool isPaused,
            bool isStageCleared,
            bool canAcceptGameplayCommands)
        {
            PlayerEntityId = playerEntityId;
            CurrentHp = currentHp;
            FacingText = facingText ?? string.Empty;
            ActiveActionText = activeActionText ?? string.Empty;
            TopologyText = topologyText ?? string.Empty;
            CanMoveThisTick = canMoveThisTick;
            CanStartActionThisTick = canStartActionThisTick;
            IsPaused = isPaused;
            IsStageCleared = isStageCleared;
            CanAcceptGameplayCommands = canAcceptGameplayCommands;
        }

        public int PlayerEntityId { get; }

        public int CurrentHp { get; }

        public string FacingText { get; }

        public string ActiveActionText { get; }

        public string TopologyText { get; }

        public bool CanMoveThisTick { get; }

        public bool CanStartActionThisTick { get; }

        public bool IsPaused { get; }

        public bool IsStageCleared { get; }

        public bool CanAcceptGameplayCommands { get; }
    }

    public sealed class GameplayHudViewModel
    {
        public event System.Action Changed;

        public int PlayerEntityId { get; private set; }

        public int CurrentHp { get; private set; }

        public string FacingText { get; private set; } = string.Empty;

        public string ActiveActionText { get; private set; } = string.Empty;

        public string TopologyText { get; private set; } = string.Empty;

        public bool CanMoveThisTick { get; private set; }

        public bool CanStartActionThisTick { get; private set; }

        public bool IsPaused { get; private set; }

        public bool IsStageCleared { get; private set; }

        public bool CanAcceptGameplayCommands { get; private set; }

        public bool IsInteractive { get; private set; }

        public string FeedbackText { get; private set; } = string.Empty;

        public GameplayHudCommandResult? LastCommandResult { get; private set; }

        public void ApplyGameplayState(GameplayHudState state)
        {
            PlayerEntityId = state.PlayerEntityId;
            CurrentHp = state.CurrentHp;
            FacingText = state.FacingText;
            ActiveActionText = state.ActiveActionText;
            TopologyText = state.TopologyText;
            CanMoveThisTick = state.CanMoveThisTick;
            CanStartActionThisTick = state.CanStartActionThisTick;
            IsPaused = state.IsPaused;
            IsStageCleared = state.IsStageCleared;
            CanAcceptGameplayCommands = state.CanAcceptGameplayCommands;
            Changed?.Invoke();
        }

        public void SetInteractivity(bool isInteractive)
        {
            IsInteractive = isInteractive;
            Changed?.Invoke();
        }

        public void SetFeedback(string feedbackText, GameplayHudCommandResult? commandResult)
        {
            FeedbackText = feedbackText ?? string.Empty;
            LastCommandResult = commandResult;
            Changed?.Invoke();
        }
    }
}
