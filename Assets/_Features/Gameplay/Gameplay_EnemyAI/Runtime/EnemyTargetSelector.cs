using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using UnityEngine;

namespace Game.Feature.Gameplay.Entities
{
    [Serializable]
    public struct DetectionSettings
    {
        [SerializeField] private int senseRange;
        [SerializeField] private bool requireSameFace;
        [SerializeField] private bool canTargetMarkedForDeath;

        public DetectionSettings(
            int senseRange,
            bool requireSameFace,
            bool canTargetMarkedForDeath)
        {
            this.senseRange = senseRange;
            this.requireSameFace = requireSameFace;
            this.canTargetMarkedForDeath = canTargetMarkedForDeath;
        }

        public int SenseRange => senseRange;

        public bool RequireSameFace => requireSameFace;

        public bool CanTargetMarkedForDeath => canTargetMarkedForDeath;

        public void Validate(string paramName)
        {
            if (senseRange <= 0)
            {
                throw new ArgumentException("Enemy detection settings require a positive sense range.", paramName);
            }
        }

        public static DetectionSettings CreateStandardEnemyDetection()
        {
            return new DetectionSettings(
                senseRange: 8,
                requireSameFace: true,
                canTargetMarkedForDeath: false);
        }
    }

    public interface IDetectionStrategy
    {
        bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            EnemyDetectionQueryOptions options = default);
    }

    public enum LineOfSightSolidBlockerPolicy
    {
        BlockSolid,
        IgnoreSolid,
    }

    public readonly struct EnemyDetectionQueryOptions
    {
        public EnemyDetectionQueryOptions(LineOfSightSolidBlockerPolicy solidBlockerPolicy)
        {
            SolidBlockerPolicy = solidBlockerPolicy;
        }

        public LineOfSightSolidBlockerPolicy SolidBlockerPolicy { get; }

        public static EnemyDetectionQueryOptions Default =>
            new EnemyDetectionQueryOptions(LineOfSightSolidBlockerPolicy.BlockSolid);
    }

    public sealed class NoDetectionStrategy : IDetectionStrategy
    {
        public static readonly NoDetectionStrategy Instance = new();

        public bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            EnemyDetectionQueryOptions options = default)
        {
            return TryFindTarget(snapshot, source, settings, out target, out _, options);
        }

        internal bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            out EnemyTargetEligibilityResult result,
            EnemyDetectionQueryOptions options = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            target = default;
            result = EnemyTargetEligibilityResult.Reject(
                source.entityId,
                0,
                EnemyTargetEligibilityPurpose.FreshAcquire,
                EnemyTargetEligibilityRejectReason.TargetMissing);
            return false;
        }
    }

    public sealed class NearestOpponentDetectionStrategy : IDetectionStrategy
    {
        public static readonly NearestOpponentDetectionStrategy Instance = new();

        public bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            EnemyDetectionQueryOptions options = default)
        {
            return TryFindTarget(snapshot, source, settings, out target, out _, options);
        }

        internal bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            out EnemyTargetEligibilityResult result,
            EnemyDetectionQueryOptions options = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings.Validate(nameof(settings));

            target = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            result = default;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                var candidateResult = EnemyDetectionTargetRules.EvaluateFreshAcquire(snapshot, source, candidate, settings);
                if (!candidateResult.Eligible)
                {
                    EnemyDetectionTargetRules.CaptureRejectResult(candidateResult, ref result);
                    continue;
                }

                var distance = GetPlanarDistance(source.position, candidate.position, settings.RequireSameFace);
                if (!distance.HasValue || distance.Value > settings.SenseRange || distance.Value >= bestDistance)
                {
                    EnemyDetectionTargetRules.CaptureRejectResult(
                        EnemyTargetEligibilityResult.Reject(
                            source.entityId,
                            candidate.entityId,
                            EnemyTargetEligibilityPurpose.FreshAcquire,
                            EnemyTargetEligibilityRejectReason.OutOfRange),
                        ref result);
                    continue;
                }

                bestDistance = distance.Value;
                target = candidate;
                result = candidateResult;
            }

            if (bestDistance != int.MaxValue)
            {
                return true;
            }

            if (result.RejectReason == EnemyTargetEligibilityRejectReason.None)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    0,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
            }

            return false;
        }

        private static int? GetPlanarDistance(
            SurfaceCell source,
            SurfaceCell target,
            bool requireSameFace)
        {
            if (requireSameFace && source.face != target.face)
            {
                return null;
            }

            var sourcePlanar = source.PlanarPosition;
            var targetPlanar = target.PlanarPosition;
            return Math.Abs(targetPlanar.x - sourcePlanar.x) + Math.Abs(targetPlanar.y - sourcePlanar.y);
        }
    }

    public sealed class CrossLineOfSightOpponentDetectionStrategy : IDetectionStrategy
    {
        public static readonly CrossLineOfSightOpponentDetectionStrategy Instance = new();

        public bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            EnemyDetectionQueryOptions options = default)
        {
            return TryFindTarget(snapshot, source, settings, out target, out _, options);
        }

        internal bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target,
            out EnemyTargetEligibilityResult result,
            EnemyDetectionQueryOptions options = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings.Validate(nameof(settings));

            target = default;

            var orderedEntities = snapshot.GetOrderedEntitiesForRead();

            var bestDistance = int.MaxValue;
            result = default;
            for (var i = 0; i < orderedEntities.Length; i++)
            {
                var candidate = orderedEntities[i];
                if (!TryValidateCandidate(
                        snapshot,
                        source,
                        candidate,
                        settings,
                        options.SolidBlockerPolicy,
                        out var distance,
                        out var candidateResult) ||
                    distance >= bestDistance)
                {
                    EnemyDetectionTargetRules.CaptureRejectResult(candidateResult, ref result);
                    continue;
                }

                bestDistance = distance;
                target = candidate;
                result = candidateResult;
            }

            if (bestDistance != int.MaxValue)
            {
                return true;
            }

            if (result.RejectReason == EnemyTargetEligibilityRejectReason.None)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    0,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
            }

            return false;
        }

        internal static bool TryValidateSpecificTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            int requiredTargetEntityId,
            out EntityState target,
            EnemyDetectionQueryOptions options = default)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings.Validate(nameof(settings));

            target = default;
            if (requiredTargetEntityId <= 0 ||
                !snapshot.TryGetEntity(requiredTargetEntityId, out var candidate) ||
                !TryValidateCandidate(
                    snapshot,
                    source,
                    candidate,
                    settings,
                    options.SolidBlockerPolicy,
                    out _,
                    out _))
            {
                return false;
            }

            target = candidate;
            return true;
        }

        private static bool TryValidateCandidate(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate,
            in DetectionSettings settings,
            LineOfSightSolidBlockerPolicy solidBlockerPolicy,
            out int distance,
            out EnemyTargetEligibilityResult result)
        {
            distance = 0;
            result = EnemyDetectionTargetRules.EvaluateFreshAcquire(snapshot, source, candidate, settings);
            if (!result.Eligible)
            {
                return false;
            }

            var sourceCell = source.position;
            var targetCell = candidate.position;
            if (targetCell.face != sourceCell.face)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    candidate.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.OutOfRange);
                return false;
            }

            var dx = targetCell.x - sourceCell.x;
            var dy = targetCell.y - sourceCell.y;
            if (dx != 0 && dy != 0)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    candidate.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.OutOfRange);
                return false;
            }

            distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance > settings.SenseRange)
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    candidate.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.OutOfRange);
                return false;
            }

            if (solidBlockerPolicy != LineOfSightSolidBlockerPolicy.IgnoreSolid &&
                IsLineOfSightBlocked(snapshot, sourceCell, targetCell))
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    candidate.entityId,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.BlockedByProfileRule);
                return false;
            }

            return true;
        }

        private static bool IsLineOfSightBlocked(
            WorldSnapshot snapshot,
            SurfaceCell source,
            SurfaceCell target)
        {
            var dx = target.x - source.x;
            var dy = target.y - source.y;
            var distance = Math.Abs(dx) + Math.Abs(dy);
            if (distance <= 1)
            {
                return false;
            }

            var stepX = Math.Sign(dx);
            var stepY = Math.Sign(dy);
            var current = new SurfaceCell(source.face, source.x + stepX, source.y + stepY);

            while (current.x != target.x || current.y != target.y)
            {
                if (snapshot.TryGetSolidSemanticAt(current, out _))
                {
                    return true;
                }

                current = new SurfaceCell(current.face, current.x + stepX, current.y + stepY);
            }

            return false;
        }
    }

    internal static class EnemyDetectionTargetRules
    {
        public static bool IsValidTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate,
            in DetectionSettings settings)
        {
            return EvaluateFreshAcquire(snapshot, source, candidate, settings).Eligible;
        }

        public static EnemyTargetEligibilityResult EvaluateFreshAcquire(
            WorldSnapshot snapshot,
            in EntityState source,
            in EntityState candidate,
            in DetectionSettings settings)
        {
            return EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(snapshot, source, candidate, settings);
        }

        internal static void CaptureRejectResult(
            in EnemyTargetEligibilityResult candidateResult,
            ref EnemyTargetEligibilityResult result)
        {
            if (candidateResult.Eligible)
            {
                return;
            }

            if (candidateResult.RejectReason == EnemyTargetEligibilityRejectReason.FreshSelectionSuppressedBySpatialState ||
                result.RejectReason == EnemyTargetEligibilityRejectReason.None)
            {
                result = candidateResult;
            }
        }
    }

    internal static class EnemyTargetSelector
    {
        public static bool TryAcquireFreshTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            IDetectionStrategy detectionStrategy,
            in DetectionSettings settings,
            out EntityState target,
            out EnemyTargetEligibilityResult result,
            EnemyDetectionQueryOptions options = default)
        {
            if (detectionStrategy is NearestOpponentDetectionStrategy nearest)
            {
                return nearest.TryFindTarget(snapshot, source, settings, out target, out result, options);
            }

            if (detectionStrategy is CrossLineOfSightOpponentDetectionStrategy crossLineOfSight)
            {
                return crossLineOfSight.TryFindTarget(snapshot, source, settings, out target, out result, options);
            }

            var found = detectionStrategy.TryFindTarget(snapshot, source, settings, out target, options);
            result = found
                ? EnemyTargetEligibilityPolicy.EvaluateFreshAcquire(snapshot, source, target, settings)
                : EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    0,
                    EnemyTargetEligibilityPurpose.FreshAcquire,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
            return found;
        }

        public static bool TryRetainLockedTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            int lockedTargetEntityId,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            target = default;
            if (lockedTargetEntityId <= 0 ||
                !snapshot.TryGetEntity(lockedTargetEntityId, out target))
            {
                result = EnemyTargetEligibilityResult.Reject(
                    source.entityId,
                    lockedTargetEntityId,
                    EnemyTargetEligibilityPurpose.RetainLockedTarget,
                    EnemyTargetEligibilityRejectReason.TargetMissing);
                return false;
            }

            result = EnemyTargetEligibilityPolicy.EvaluateRetainLockedTarget(snapshot, source, target);
            if (!result.Eligible)
            {
                target = default;
                return false;
            }

            return true;
        }

        public static bool TryFindLocalEngagementTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            EnemyCombatCapabilityRuntime combatCapability,
            EnemyPassiveContactCapabilityRuntime passiveContactCapability,
            List<EntityState> buffer,
            out EntityState target,
            out EnemyTargetEligibilityResult result)
        {
            return EnemyLocalContactPolicy.TryFindLocalEngagementTarget(
                snapshot,
                source,
                combatCapability,
                passiveContactCapability,
                buffer,
                out target,
                out result);
        }
    }
}
