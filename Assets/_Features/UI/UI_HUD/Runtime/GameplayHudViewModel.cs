using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.UIAccess.Models;

namespace Game.Feature.UI.HUD
{
    public sealed class GameplayHudViewModel
    {
        public int PlayerEntityId { get; internal set; }

        public int CurrentHp { get; internal set; }

        public Direction Facing { get; internal set; }

        public PlayerActionKind ActiveActionKind { get; internal set; }

        public bool CanMoveThisTick { get; internal set; }

        public bool CanStartActionThisTick { get; internal set; }

        public bool IsPaused { get; internal set; }

        public bool IsStageCleared { get; internal set; }

        public CubeTopologyState CurrentTopology { get; internal set; }

        public GameplayCommandAcceptance LastCommandAcceptance { get; internal set; }
    }
}
