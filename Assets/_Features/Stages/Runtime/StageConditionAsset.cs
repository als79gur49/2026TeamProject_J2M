using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    public readonly struct StageConditionValidationContext
    {
        private readonly IReadOnlyDictionary<string, StageZoneDefinition> _zonesById;

        public StageConditionValidationContext(
            string stageName,
            IReadOnlyDictionary<string, StageZoneDefinition> zonesById)
        {
            StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
            _zonesById = zonesById ?? throw new ArgumentNullException(nameof(zonesById));
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
    }

    public readonly struct StageConditionCompilationContext
    {
        private readonly IReadOnlyDictionary<string, StageZoneRuntimeDefinition> _zonesById;

        public StageConditionCompilationContext(
            string stageName,
            int playerEntityId,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById)
            : this(stageName, playerEntityId, zonesById, StageSimulationTiming.Default)
        {
        }

        public StageConditionCompilationContext(
            string stageName,
            int playerEntityId,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById,
            StageSimulationTiming timing)
        {
            StageName = string.IsNullOrWhiteSpace(stageName) ? "<unnamed stage>" : stageName;
            PlayerEntityId = playerEntityId;
            _zonesById = zonesById ?? throw new ArgumentNullException(nameof(zonesById));
            Timing = timing;
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
