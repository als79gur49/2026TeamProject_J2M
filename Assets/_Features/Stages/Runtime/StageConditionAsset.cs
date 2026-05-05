using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    public readonly struct StageConditionValidationContext
    {
        private readonly IReadOnlyDictionary<int, StageTileFeatureDefinition> _tileFeaturesById;
        private readonly IReadOnlyDictionary<string, StageZoneDefinition> _zonesById;

        public StageConditionValidationContext(
            string stageName,
            IReadOnlyDictionary<string, StageZoneDefinition> zonesById)
            : this(stageName, zonesById, null)
        {
        }

        public StageConditionValidationContext(
            string stageName,
            IReadOnlyDictionary<string, StageZoneDefinition> zonesById,
            IReadOnlyDictionary<int, StageTileFeatureDefinition> tileFeaturesById)
        {
            StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
            _zonesById = zonesById ?? throw new ArgumentNullException(nameof(zonesById));
            _tileFeaturesById = tileFeaturesById ?? new Dictionary<int, StageTileFeatureDefinition>();
        }

        public string StageName { get; }

        public bool TryGetZone(string zoneId, out StageZoneDefinition zone)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                zone = default;
                return false;
            }

            return _zonesById.TryGetValue(zoneId.Trim(), out zone);
        }

        public bool TryGetTileFeature(int tileId, out StageTileFeatureDefinition tileFeature)
        {
            if (_tileFeaturesById == null)
            {
                tileFeature = default;
                return false;
            }

            return _tileFeaturesById.TryGetValue(tileId, out tileFeature);
        }
    }

    public readonly struct StageConditionCompilationContext
    {
        private readonly IReadOnlyDictionary<int, TileFeatureRuntimeDefinition> _tileFeatureRuntimeDefinitionsById;
        private readonly IReadOnlyDictionary<string, StageZoneRuntimeDefinition> _zonesById;

        public StageConditionCompilationContext(
            string stageName,
            int playerEntityId,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById)
            : this(stageName, playerEntityId, zonesById, StageSimulationTiming.Default, null)
        {
        }

        public StageConditionCompilationContext(
            string stageName,
            int playerEntityId,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById,
            StageSimulationTiming timing)
            : this(stageName, playerEntityId, zonesById, timing, null)
        {
        }

        public StageConditionCompilationContext(
            string stageName,
            int playerEntityId,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById,
            StageSimulationTiming timing,
            IReadOnlyDictionary<int, TileFeatureRuntimeDefinition> tileFeatureRuntimeDefinitionsById)
        {
            StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
            PlayerEntityId = playerEntityId;
            _zonesById = zonesById ?? throw new ArgumentNullException(nameof(zonesById));
            Timing = timing;
            _tileFeatureRuntimeDefinitionsById =
                tileFeatureRuntimeDefinitionsById ?? new Dictionary<int, TileFeatureRuntimeDefinition>();
        }

        public string StageName { get; }

        public int PlayerEntityId { get; }

        public StageSimulationTiming Timing { get; }

        public bool TryGetZone(string zoneId, out StageZoneRuntimeDefinition zone)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
            {
                zone = default;
                return false;
            }

            return _zonesById.TryGetValue(zoneId.Trim(), out zone);
        }

        public bool TryGetTileFeatureRuntimeDefinition(
            int tileId,
            out TileFeatureRuntimeDefinition definition)
        {
            if (_tileFeatureRuntimeDefinitionsById == null)
            {
                definition = default;
                return false;
            }

            return _tileFeatureRuntimeDefinitionsById.TryGetValue(tileId, out definition);
        }
    }

    public abstract class StageConditionAsset : ScriptableObject
    {
        public abstract void Validate(in StageConditionValidationContext context);

        internal abstract StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context);

        protected static string CreateConditionId(StageConditionAsset asset, string suffix = "")
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            var assetName = string.IsNullOrWhiteSpace(asset.name)
                ? asset.GetType().Name
                : asset.name.Trim();

            return string.IsNullOrWhiteSpace(suffix)
                ? assetName
                : $"{assetName}:{suffix}";
        }
    }
}
