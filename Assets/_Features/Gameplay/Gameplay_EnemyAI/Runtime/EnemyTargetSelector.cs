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

        public static DetectionSettings CreateDefaultMelee()
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
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            target = default;
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
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings.Validate(nameof(settings));

            target = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (!EnemyDetectionTargetRules.IsValidTarget(snapshot, source, candidate, settings))
                {
                    continue;
                }

                var distance = GetPlanarDistance(source.position, candidate.position, settings.RequireSameFace);
                if (!distance.HasValue || distance.Value > settings.SenseRange || distance.Value >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance.Value;
                target = candidate;
            }

            return bestDistance != int.MaxValue;
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
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            settings.Validate(nameof(settings));

            target = default;

            var orderedEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(orderedEntities);

            var bestDistance = int.MaxValue;
            for (var i = 0; i < orderedEntities.Count; i++)
            {
                var candidate = orderedEntities[i];
                if (!TryValidateCandidate(
                        snapshot,
                        source,
                        candidate,
                        settings,
                        options.SolidBlockerPolicy,
                        out var distance) ||
                    distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                target = candidate;
            }

            return bestDistance != int.MaxValue;
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
            out int distance)
        {
            distance = 0;
            if (!EnemyDetectionTargetRules.IsValidTarget(snapshot, source, candidate, settings))
            {
                return false;
            }

            var sourceCell = source.position;
            var targetCell = candidate.position;
            if (targetCell.face != sourceCell.face)
            {
                return false;
            }

            var dx = targetCell.x - sourceCell.x;
            var dy = targetCell.y - sourceCell.y;
            if (dx != 0 && dy != 0)
            {
                return false;
            }

            distance = Math.Abs(dx) + Math.Abs(dy);
            return distance <= settings.SenseRange &&
                   (solidBlockerPolicy == LineOfSightSolidBlockerPolicy.IgnoreSolid ||
                    !IsLineOfSightBlocked(snapshot, sourceCell, targetCell));
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
            if (candidate.entityId == source.entityId ||
                candidate.type != EntityType.Unit ||
                candidate.teamId == source.teamId ||
                candidate.hp <= 0)
            {
                return false;
            }

            if (candidate.markedForDeath)
            {
                return settings.CanTargetMarkedForDeath &&
                       candidate.boardPresence == EntityBoardPresence.Occupying &&
                       snapshot.Topology.IsFaceActive(candidate.position.face);
            }

            return snapshot.CanBeTargetedForNewSelection(candidate.entityId);
        }
    }
}
