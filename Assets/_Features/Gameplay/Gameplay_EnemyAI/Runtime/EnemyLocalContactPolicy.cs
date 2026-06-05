using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;

namespace Game.Feature.Gameplay.Entities
{
    internal static class EnemyLocalContactPolicy
    {
        public static bool TryFindLocalEngagementTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            List<EntityState> buffer,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            var sourceEntity = source;
            return TryFindSameCellTarget(
                snapshot,
                sourceEntity,
                buffer,
                EnemyTargetEligibilityPurpose.LocalEngagementHold,
                candidate => EnemyTargetEligibilityPolicy.EvaluateLocalEngagementHold(
                    snapshot,
                    sourceEntity,
                    candidate,
                    combatCapability,
                    passiveContactCapability),
                out target,
                out result);
        }

        public static bool TryFindPassiveContactCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            List<EntityState> buffer,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            var sourceEntity = source;
            return TryFindSameCellTarget(
                snapshot,
                sourceEntity,
                buffer,
                EnemyTargetEligibilityPurpose.PassiveContactCandidate,
                candidate => EnemyTargetEligibilityPolicy.EvaluatePassiveContactCandidate(
                    snapshot,
                    sourceEntity,
                    candidate,
                    passiveContactCapability),
                out target,
                out result);
        }

        private static bool TryFindSameCellTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            List<EntityState> buffer,
            EnemyTargetEligibilityPurpose missingPurpose,
            Func<EntityState, EnemyTargetEligibilityResult> evaluate,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            target = default;
            result = default;
            buffer.Clear();
            snapshot.EnumerateUnitsAt(source.position, buffer);
            buffer.Sort(EntityStateEntityIdComparer.Instance);

            for (var i = 0; i < buffer.Count; i++)
            {
                var candidate = buffer[i];
                if (candidate.entityId == source.entityId)
                {
                    continue;
                }

                var candidateResult = evaluate(candidate);
                if (!candidateResult.Eligible)
                {
                    if (result.Purpose == default &&
                        result.RejectReason == EnemyTargetEligibilityRejectReason.None)
                    {
                        result = candidateResult;
                    }

                    continue;
                }

                target = candidate;
                result = candidateResult;
                return true;
            }

            if (result.Purpose == default &&
                result.RejectReason == EnemyTargetEligibilityRejectReason.None)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    0,
                    missingPurpose,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
            }

            return false;
        }

        private sealed class EntityStateEntityIdComparer : IComparer<EntityState>
        {
            internal static readonly EntityStateEntityIdComparer Instance = new();

            public int Compare(EntityState left, EntityState right)
            {
                return left.entityId.CompareTo(right.entityId);
            }
        }
    }
}
