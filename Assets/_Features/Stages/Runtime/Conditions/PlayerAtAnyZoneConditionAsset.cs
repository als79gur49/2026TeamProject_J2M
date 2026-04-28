using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Objectives;
using UnityEngine;

namespace Game.Feature.Stages
{
    [CreateAssetMenu(
        menuName = "Gameplay/Stages/Conditions/Player At Any Zone",
        fileName = "PlayerAtAnyZoneCondition")]
    public sealed class PlayerAtAnyZoneConditionAsset : StageConditionAsset
    {
        [SerializeField] private string[] zoneIds = Array.Empty<string>();
        [SerializeField] private bool requireAlive = true;

        public string[] ZoneIds => zoneIds ?? Array.Empty<string>();

        public bool RequireAlive => requireAlive;

        public override void Validate(in StageConditionValidationContext context)
        {
            if (ZoneIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Stage '{context.StageName}' condition '{name}' requires at least one zone id.");
            }

            var uniqueZoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < ZoneIds.Length; i++)
            {
                var zoneId = ZoneIds[i];
                if (string.IsNullOrWhiteSpace(zoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' contains an empty zone id at index {i}.");
                }

                var normalizedZoneId = zoneId.Trim();
                if (!uniqueZoneIds.Add(normalizedZoneId))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' contains duplicate zone id '{normalizedZoneId}'.");
                }

                if (!context.TryGetZone(normalizedZoneId, out _))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{normalizedZoneId}'.");
                }
            }
        }

        internal override StageConditionRuntimeDefinition Compile(in StageConditionCompilationContext context)
        {
            var targetZones = new StageZoneRuntimeDefinition[ZoneIds.Length];
            for (var i = 0; i < ZoneIds.Length; i++)
            {
                if (!context.TryGetZone(ZoneIds[i], out targetZones[i]))
                {
                    throw new InvalidOperationException(
                        $"Stage '{context.StageName}' condition '{name}' references unknown zone id '{ZoneIds[i]}'.");
                }
            }

            return new PlayerAtAnyZoneConditionRuntimeDefinition(
                CreateConditionId(this, CreateZoneIdSuffix(targetZones)),
                string.IsNullOrWhiteSpace(name) ? nameof(PlayerAtAnyZoneConditionAsset) : name,
                context.PlayerEntityId,
                targetZones,
                requireAlive);
        }

        private static string CreateZoneIdSuffix(IReadOnlyList<StageZoneRuntimeDefinition> targetZones)
        {
            if (targetZones == null || targetZones.Count == 0)
            {
                return string.Empty;
            }

            var suffix = targetZones[0].ZoneId;
            for (var i = 1; i < targetZones.Count; i++)
            {
                suffix += $",{targetZones[i].ZoneId}";
            }

            return suffix;
        }
    }
}
