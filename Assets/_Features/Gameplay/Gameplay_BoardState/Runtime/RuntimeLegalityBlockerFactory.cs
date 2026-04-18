using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    internal static class RuntimeLegalityBlockerFactory
    {
        public static IReadOnlyList<LegalityBlocker> CreateBoardEdge()
        {
            return new[]
            {
                new LegalityBlocker(LegalityBlockerKind.BoardEdge),
            };
        }

        public static IReadOnlyList<LegalityBlocker> CreateTerrain(TerrainFlags terrainFlags)
        {
            return new[]
            {
                new LegalityBlocker(
                    LegalityBlockerKind.Terrain,
                    terrainFlags: terrainFlags),
            };
        }

        public static IReadOnlyList<LegalityBlocker> Create(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            SlideStopper blocker)
        {
            return new[]
            {
                CreateBlocker(entitiesById, blocker),
            };
        }

        public static IReadOnlyList<LegalityBlocker> Create(EntityState entity)
        {
            return new[]
            {
                CreateEntityBlocker(entity),
            };
        }

        public static IReadOnlyList<LegalityBlocker> CreateReservationConflict()
        {
            return new[]
            {
                // Reservation stays a single top-level blocker kind for this phase.
                // Future cell/edge/entity/payload sub-facets must extend this path instead
                // of creating file-local legality enums.
                new LegalityBlocker(LegalityBlockerKind.Reservation),
            };
        }

        public static string FormatKinds(IReadOnlyList<LegalityBlocker> blockers)
        {
            if (blockers == null || blockers.Count == 0)
            {
                return "None";
            }

            var values = new string[blockers.Count];
            for (var i = 0; i < blockers.Count; i++)
            {
                values[i] = blockers[i].Kind.ToString();
            }

            return string.Join(",", values);
        }

        private static LegalityBlocker CreateBlocker(
            IReadOnlyDictionary<int, EntityState> entitiesById,
            SlideStopper blocker)
        {
            switch (blocker.Kind)
            {
                case SlideStopperKind.BoardEdge:
                    return new LegalityBlocker(LegalityBlockerKind.BoardEdge);

                case SlideStopperKind.Terrain:
                    return new LegalityBlocker(
                        LegalityBlockerKind.Terrain,
                        terrainFlags: TerrainFlags.BlocksGroundTraversal);

                case SlideStopperKind.Entity:
                    if (blocker.EntityId != 0 &&
                        entitiesById != null &&
                        entitiesById.TryGetValue(blocker.EntityId, out var entity))
                    {
                        return CreateEntityBlocker(entity);
                    }

                    return new LegalityBlocker(LegalityBlockerKind.Solid, blocker.EntityId);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static LegalityBlocker CreateEntityBlocker(EntityState entity)
        {
            if (entity.type == EntityType.Unit)
            {
                return new LegalityBlocker(
                    LegalityBlockerKind.Unit,
                    entity.entityId,
                    entityType: entity.type);
            }

            return new LegalityBlocker(
                LegalityBlockerKind.Solid,
                entity.entityId,
                entityType: entity.type,
                solidKind: entity.type == EntityType.Projectile
                    ? (SolidKind?)null
                    : SnapshotReadQueries.ResolveSolidKind(entity));
        }
    }
}
