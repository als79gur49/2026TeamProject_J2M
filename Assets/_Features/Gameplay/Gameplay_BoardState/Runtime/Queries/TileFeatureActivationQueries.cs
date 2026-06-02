using System;
using System.Collections.Generic;

namespace Game.Feature.Gameplay.BoardState
{
    public static class TileFeatureActivationQueries
    {
        public static bool IsSupportedDestroyActivation(TileFeatureActivationRule activationRule)
        {
            return activationRule == TileFeatureActivationRule.BottomFaceOnly ||
                   activationRule == TileFeatureActivationRule.FrontFaceOnly ||
                   activationRule == TileFeatureActivationRule.ActiveFaceOnly ||
                   activationRule == TileFeatureActivationRule.InactiveFaceOnly;
        }

        public static bool IsActive(
            TileFeatureState state,
            TileFeatureRuntimeDefinition definition,
            CubeTopologyState topology)
        {
            if (state.TileId != definition.TileId)
            {
                return false;
            }

            switch (definition.ActivationRule)
            {
                case TileFeatureActivationRule.Always:
                    return true;
                case TileFeatureActivationRule.BottomFaceOnly:
                    return state.Cell.face == topology.BottomFace;
                case TileFeatureActivationRule.FrontFaceOnly:
                    return state.Cell.face == topology.FrontFace;
                case TileFeatureActivationRule.ActiveFaceOnly:
                    return topology.IsFaceActive(state.Cell.face);
                case TileFeatureActivationRule.InactiveFaceOnly:
                    return !topology.IsFaceActive(state.Cell.face);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(definition),
                        definition.ActivationRule,
                        "Unknown TileFeature activation rule.");
            }
        }
    }

    internal readonly struct BarricadeEffectiveActivationState
    {
        public BarricadeEffectiveActivationState(
            bool topologyActive,
            int blockingUnitId)
        {
            TopologyActive = topologyActive;
            BlockingUnitId = blockingUnitId;
        }

        public bool TopologyActive { get; }

        public int BlockingUnitId { get; }

        public bool HasBlockingUnit => BlockingUnitId > 0;

        public bool EffectiveActive => TopologyActive && !HasBlockingUnit;

        public bool BlocksNewEntrant => TopologyActive;
    }

    internal static class BarricadeEffectiveActivationPolicy
    {
        public static BarricadeEffectiveActivationState Evaluate(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            TileFeatureState tileFeature,
            TileFeatureRuntimeDefinition definition)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var topologyActive =
                tileFeature.Kind == TileFeatureKind.Barricade &&
                TileFeatureActivationQueries.IsActive(tileFeature, definition, topology);
            if (!topologyActive)
            {
                return new BarricadeEffectiveActivationState(false, blockingUnitId: 0);
            }

            return BarricadeActivationOccupantQuery.TryGetBlockingUnitId(
                snapshot,
                topology,
                tileFeature.Cell,
                out var unitId)
                ? new BarricadeEffectiveActivationState(true, unitId)
                : new BarricadeEffectiveActivationState(true, blockingUnitId: 0);
        }

        public static bool IsEffectiveActive(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            TileFeatureState tileFeature,
            TileFeatureRuntimeDefinition definition)
        {
            return Evaluate(snapshot, topology, tileFeature, definition).EffectiveActive;
        }
    }

    internal static class BarricadeActivationOccupantQuery
    {
        public static bool TryGetBlockingUnit(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            SurfaceCell cell,
            out EntityState unit)
        {
            unit = default;
            if (snapshot == null)
            {
                return false;
            }

            var unitBuffer = new List<EntityState>();
            snapshot.EnumerateUnitsAt(topology, cell, unitBuffer);

            var found = false;
            for (var i = 0; i < unitBuffer.Count; i++)
            {
                var candidate = unitBuffer[i];
                if (!IsValidBlockingUnit(candidate, cell))
                {
                    continue;
                }

                if (!found || candidate.entityId < unit.entityId)
                {
                    unit = candidate;
                    found = true;
                }
            }

            return found;
        }

        public static bool TryGetBlockingUnitId(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            SurfaceCell cell,
            out int unitId)
        {
            if (TryGetBlockingUnit(snapshot, topology, cell, out var unit))
            {
                unitId = unit.entityId;
                return true;
            }

            unitId = 0;
            return false;
        }

        public static bool IsExistingBlockingUnitAt(
            WorldSnapshot snapshot,
            CubeTopologyState topology,
            SurfaceCell cell,
            int entityId)
        {
            if (entityId <= 0 || snapshot == null)
            {
                return false;
            }

            var unitBuffer = new List<EntityState>();
            snapshot.EnumerateUnitsAt(topology, cell, unitBuffer);
            for (var i = 0; i < unitBuffer.Count; i++)
            {
                var candidate = unitBuffer[i];
                if (candidate.entityId == entityId &&
                    IsValidBlockingUnit(candidate, cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidBlockingUnit(in EntityState entity, SurfaceCell cell)
        {
            return entity.type == EntityType.Unit &&
                   entity.position == cell &&
                   entity.boardPresence == EntityBoardPresence.Occupying &&
                   entity.hp > 0 &&
                   !entity.markedForDeath;
        }
    }
}
