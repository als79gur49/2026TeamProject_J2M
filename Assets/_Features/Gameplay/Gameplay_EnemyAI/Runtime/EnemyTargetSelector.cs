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
            out EntityState target);
    }

    public sealed class NearestOpponentDetectionStrategy : IDetectionStrategy
    {
        public static readonly NearestOpponentDetectionStrategy Instance = new();

        public bool TryFindTarget(
            WorldSnapshot snapshot,
            in EntityState source,
            in DetectionSettings settings,
            out EntityState target)
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
                if (!IsValidTarget(snapshot, source, candidate, settings))
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

        private static bool IsValidTarget(
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
}
