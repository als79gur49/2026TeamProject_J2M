namespace Game.Feature.Gameplay.Entities
{
    internal enum EnemyTargetEligibilityPurpose
    {
        FreshAcquire,
        RetainLockedTarget,
        LocalEngagementHold,
        CombatActionValidate,
        PassiveContactCandidate,
    }

    internal enum EnemyTargetEligibilityAcceptReason
    {
        None,
        FreshAcquired,
        LockedTargetRetained,
        SameCellLocalEngagement,
        CombatTargetValidated,
        PassiveContactCandidate,
    }

    internal enum EnemyTargetEligibilityRejectReason
    {
        None,
        SourceMissing,
        SourceNotControllable,
        SourceNotOccupying,
        SourceNotParticipating,
        TargetMissing,
        TargetDead,
        TargetMarkedForDeath,
        TargetNotOccupying,
        TargetNotHostile,
        TargetNotGameplayVisible,
        TargetNotContactVisible,
        TargetNotOnParticipatingTopology,
        TargetNotSameCell,
        FreshSelectionSuppressedBySpatialState,
        CombatCapabilityMissing,
        PassiveContactCapabilityMissing,
        OutOfRange,
        BlockedByProfileRule,
    }

    internal readonly struct EnemyTargetEligibilityResult
    {
        private EnemyTargetEligibilityResult(
            bool eligible,
            int sourceEntityId,
            int targetEntityId,
            EnemyTargetEligibilityPurpose purpose,
            EnemyTargetEligibilityAcceptReason acceptReason,
            EnemyTargetEligibilityRejectReason rejectReason)
        {
            Eligible = eligible;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            Purpose = purpose;
            AcceptReason = acceptReason;
            RejectReason = rejectReason;
        }

        public bool Eligible { get; }

        public int SourceEntityId { get; }

        public int TargetEntityId { get; }

        public EnemyTargetEligibilityPurpose Purpose { get; }

        public EnemyTargetEligibilityAcceptReason AcceptReason { get; }

        public EnemyTargetEligibilityRejectReason RejectReason { get; }

        public static EnemyTargetEligibilityResult Accept(
            int sourceEntityId,
            int targetEntityId,
            EnemyTargetEligibilityPurpose purpose,
            EnemyTargetEligibilityAcceptReason reason)
        {
            return new EnemyTargetEligibilityResult(
                eligible: true,
                sourceEntityId,
                targetEntityId,
                purpose,
                reason,
                EnemyTargetEligibilityRejectReason.None);
        }

        public static EnemyTargetEligibilityResult Reject(
            int sourceEntityId,
            int targetEntityId,
            EnemyTargetEligibilityPurpose purpose,
            EnemyTargetEligibilityRejectReason reason)
        {
            return new EnemyTargetEligibilityResult(
                eligible: false,
                sourceEntityId,
                targetEntityId,
                purpose,
                EnemyTargetEligibilityAcceptReason.None,
                reason);
        }
    }
}
