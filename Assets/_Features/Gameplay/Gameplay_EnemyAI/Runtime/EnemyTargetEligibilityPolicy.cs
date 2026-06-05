using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyTargetEligibilityPolicy
    {
        public static EnemyTargetEligibilityResult EvaluateFreshAcquire(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            in DetectionSettings detectionSettings)
        {
            var baseResult = EvaluateBaseTarget(
                snapshot,
                source,
                target,
                EnemyTargetEligibilityPurpose.FreshAcquire,
                requireControllableSource: false);
            if (!baseResult.Eligible)
            {
                return baseResult;
            }

            if (snapshot.CanBeTargetedForNewSelection(target.entityId))
            {
                return EnemyTargetEligibilityResult.Accept(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityAcceptReason.FreshAcquired);
            }

            if (snapshot.TryGetResolvedSpatialState(target.entityId, out var spatialState) &&
                spatialState.Kind == SpatialState.Phased &&
                spatialState.IsGameplayVisible)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState);
            }

            return EnemyTargetEligibilityResult.Reject(
                source.entityId,
                target.entityId,
                EnemyTargetEligibilityPurpose.FreshAcquire,
                EnemyTargetEligibilityRejectReason.TargetNotGameplayVisible);
        }

        public static EnemyTargetEligibilityResult EvaluateRetainLockedTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target)
        {
            return EvaluateVisibleOccupyingTarget(
                snapshot,
                source,
                target,
                EnemyTargetEligibilityPurpose.RetainLockedTarget,
                EnemyTargetEligibilityAcceptReason.LockedTargetRetained,
                requireSameCell: false,
                requireControllableSource: false);
        }

        public static EnemyTargetEligibilityResult EvaluateLocalEngagementHold(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability)
        {
            if (combatCapability == null &&
                passiveContactCapability == null)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.LocalEngagementHold,
                    EnemyTargetEligibilityRejectReason.BlockedByProfileRule);
            }

            return EvaluateVisibleOccupyingTarget(
                snapshot,
                source,
                target,
                EnemyTargetEligibilityPurpose.LocalEngagementHold,
                EnemyTargetEligibilityAcceptReason.SameCellLocalEngagement,
                requireSameCell: true,
                requireControllableSource: true);
        }

        public static EnemyTargetEligibilityResult EvaluateCombatActionValidate(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyCombatCapabilityRuntime combatCapability)
        {
            if (combatCapability == null)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.CombatActionValidate,
                    EnemyTargetEligibilityRejectReason.CombatCapabilityMissing);
            }

            var retainResult = EvaluateVisibleOccupyingTarget(
                snapshot,
                source,
                target,
                EnemyTargetEligibilityPurpose.CombatActionValidate,
                EnemyTargetEligibilityAcceptReason.CombatTargetValidated,
                requireSameCell: false,
                requireControllableSource: false);
            if (!retainResult.Eligible)
            {
                return retainResult;
            }

            return combatCapability.AttackDecisionStrategy.IsTargetInRange(source, target, combatCapability.AttackDecisionSettings)
                ? retainResult
                : EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.CombatActionValidate,
                    EnemyTargetEligibilityRejectReason.OutOfRange);
        }

        public static EnemyTargetEligibilityResult EvaluatePassiveContactCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability)
        {
            if (passiveContactCapability == null)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    EnemyTargetEligibilityPurpose.PassiveContactCandidate,
                    EnemyTargetEligibilityRejectReason.PassiveContactCapabilityMissing);
            }

            return EvaluateVisibleOccupyingTarget(
                snapshot,
                source,
                target,
                EnemyTargetEligibilityPurpose.PassiveContactCandidate,
                EnemyTargetEligibilityAcceptReason.PassiveContactCandidate,
                requireSameCell: true,
                requireControllableSource: false);
        }

        private static EnemyTargetEligibilityResult EvaluateVisibleOccupyingTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyTargetEligibilityPurpose purpose,
            EnemyTargetEligibilityAcceptReason acceptReason,
            bool requireSameCell,
            bool requireControllableSource)
        {
            var baseResult = EvaluateBaseTarget(snapshot, source, target, purpose, requireControllableSource);
            if (!baseResult.Eligible)
            {
                return baseResult;
            }

            if (!snapshot.TryGetResolvedSpatialState(target.entityId, out var spatialState) ||
                !spatialState.IsGameplayVisible)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    requireSameCell
                        ? EnemyTargetEligibilityRejectReason.TargetNotContactVisible
                        : EnemyTargetEligibilityRejectReason.TargetNotGameplayVisible);
            }

            if (!snapshot.Topology.IsFaceActive(target.position.face))
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetNotOnParticipatingTopology);
            }

            if (requireSameCell &&
                !source.position.Equals(target.position))
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetNotSameCell);
            }

            return EnemyTargetEligibilityResult.Accept(source.entityId, target.entityId, purpose, acceptReason);
        }

        private static EnemyTargetEligibilityResult EvaluateBaseTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState target,
            EnemyTargetEligibilityPurpose purpose,
            bool requireControllableSource)
        {
            if (snapshot == null)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.SourceMissing);
            }

            if (source.entityId <= 0)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.SourceMissing);
            }

            if (requireControllableSource &&
                !EnemyParticipationPolicy.IsControllableParticipant(snapshot, source))
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    source.boardPresence == EntityBoardPresence.Occupying
                        ? EnemyTargetEligibilityRejectReason.SourceNotParticipating
                        : EnemyTargetEligibilityRejectReason.SourceNotOccupying);
            }

            if (target.entityId <= 0 ||
                target.type != EntityType.Unit)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
            }

            if (target.entityId == source.entityId ||
                target.teamId == source.teamId)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetNotHostile);
            }

            if (target.hp <= 0)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetDead);
            }

            if (target.markedForDeath)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetMarkedForDeath);
            }

            if (target.boardPresence != EntityBoardPresence.Occupying)
            {
                return EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    target.entityId,
                    purpose,
                    EnemyTargetEligibilityRejectReason.TargetNotOccupying);
            }

            return EnemyTargetEligibilityResult.Accept(
                source.entityId,
                target.entityId,
                purpose,
                EnemyTargetEligibilityAcceptReason.None);
        }
    }
}
