using System;

namespace Game.Feature.Gameplay.UIAccess.Models
{
    public enum GameplayUiDirection
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 3,
        Left = 4,
    }

    public enum GameplayUiActionKind
    {
        None = 0,
        Push = 1,
        Flip = 2,
    }

    public enum GameplayUiActionResolutionKind
    {
        None = 0,
        Success = 1,
        Impact = 2,
        Blocked = 3,
    }

    public enum GameplayUiFace
    {
        Floor = 0,
        Front = 1,
        Ceiling = 2,
        Back = 3,
    }

    public readonly struct GameplayUiRecoveryCooldown
    {
        public GameplayUiRecoveryCooldown(
            GameplayUiActionKind actionKind,
            int remainingRecoveryTicks,
            int totalRecoveryTicks)
        {
            ActionKind = actionKind;
            RemainingRecoveryTicks = remainingRecoveryTicks;
            TotalRecoveryTicks = totalRecoveryTicks;
        }

        public GameplayUiActionKind ActionKind { get; }

        public int RemainingRecoveryTicks { get; }

        public int TotalRecoveryTicks { get; }
    }

    public enum GameplayUiRotationKind
    {
        None = 0,
        Forward = 1,
        Backward = 2,
    }

    public readonly struct GameplayUiTopology : IEquatable<GameplayUiTopology>
    {
        public GameplayUiTopology(GameplayUiFace bottomFace)
        {
            BottomFace = bottomFace;
        }

        public GameplayUiFace BottomFace { get; }

        public bool Equals(GameplayUiTopology other)
        {
            return BottomFace == other.BottomFace;
        }

        public override bool Equals(object obj)
        {
            return obj is GameplayUiTopology other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (int)BottomFace;
        }

        public override string ToString()
        {
            return BottomFace.ToString();
        }
    }
}
