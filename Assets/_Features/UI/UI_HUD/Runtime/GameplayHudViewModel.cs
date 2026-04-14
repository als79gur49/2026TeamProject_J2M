using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.UI.HUD
{
    public sealed class GameplayHudViewModel
    {
        public int PlayerEntityId { get; private set; }

        public int CurrentHp { get; private set; }

        public Direction Facing { get; private set; }

        public PlayerActionKind ActiveActionKind { get; private set; }

        public bool CanMoveThisTick { get; private set; }

        public bool CanStartActionThisTick { get; private set; }

        public bool IsPaused { get; private set; }

        public bool IsStageCleared { get; private set; }

        public bool CanAcceptGameplayCommands { get; private set; }

        public CubeTopologyState CurrentTopology { get; private set; }

        public bool IsInteractive { get; private set; }

        public string FeedbackText { get; private set; } = string.Empty;

        public GameplayCommandAcceptance? LastCommandAcceptance { get; private set; }

        public void ApplyGameplayState(
            GameplaySessionReadModel session,
            GameplayPlayerHudReadModel playerHud)
        {
            PlayerEntityId = playerHud.PlayerEntityId;
            CurrentHp = playerHud.CurrentHp;
            Facing = playerHud.Facing;
            ActiveActionKind = playerHud.ActiveActionKind;
            CanMoveThisTick = playerHud.CanMoveThisTick;
            CanStartActionThisTick = playerHud.CanStartActionThisTick;
            IsPaused = session.IsPaused;
            IsStageCleared = session.IsStageCleared;
            CanAcceptGameplayCommands = session.CanAcceptGameplayCommands;
        }

        public void SetCurrentTopology(CubeTopologyState topology)
        {
            CurrentTopology = topology;
        }

        public void SetInteractivity(bool isInteractive)
        {
            IsInteractive = isInteractive;
        }

        public void SetFeedback(string feedbackText, GameplayCommandAcceptance? acceptance)
        {
            FeedbackText = feedbackText ?? string.Empty;
            LastCommandAcceptance = acceptance;
        }
    }
}
