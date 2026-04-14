namespace Game.Feature.Gameplay.UIAccess.Models
{
    public readonly struct GameplayCommandAcceptance
    {
        public GameplayCommandAcceptance(bool accepted, GameplayCommandRejectionReason rejectionReason = GameplayCommandRejectionReason.None)
        {
            if (accepted)
            {
                rejectionReason = GameplayCommandRejectionReason.None;
            }
            else if (rejectionReason == GameplayCommandRejectionReason.None)
            {
                rejectionReason = GameplayCommandRejectionReason.Other;
            }

            Accepted = accepted;
            RejectionReason = rejectionReason;
        }

        public bool Accepted { get; }

        public GameplayCommandRejectionReason RejectionReason { get; }

        public static GameplayCommandAcceptance Accept()
        {
            return new GameplayCommandAcceptance(true);
        }

        public static GameplayCommandAcceptance Reject(GameplayCommandRejectionReason rejectionReason)
        {
            return new GameplayCommandAcceptance(false, rejectionReason);
        }
    }
}
