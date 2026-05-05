using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public static class StageAuthoringProjection
    {
        public static StageAuthoringAllocationPlan BuildAllocationPlan(StageAuthoringDefinition source)
        {
            if (source == null)
            {
                return StageAuthoringAllocationPlan.Empty;
            }

            var existingByGuid = new Dictionary<string, StageAuthoringIdMapping>(StringComparer.Ordinal);
            var usedEntityIds = new HashSet<int>();
            var maxEntityId = 0;
            var existingMappings = source.EntityIdMappings;
            for (var i = 0; i < existingMappings.Count; i++)
            {
                var mapping = existingMappings[i];
                var stableGuid = Normalize(mapping.StableGuid);
                if (string.IsNullOrEmpty(stableGuid) || mapping.EntityId <= 0)
                {
                    continue;
                }

                if (!existingByGuid.ContainsKey(stableGuid))
                {
                    existingByGuid.Add(stableGuid, new StageAuthoringIdMapping
                    {
                        StableGuid = stableGuid,
                        EntityId = mapping.EntityId,
                        Retired = mapping.Retired,
                        LastKnownDisplayName = Normalize(mapping.LastKnownDisplayName),
                    });
                }

                usedEntityIds.Add(mapping.EntityId);
                maxEntityId = Math.Max(maxEntityId, mapping.EntityId);
            }

            var activeGuids = new HashSet<string>(StringComparer.Ordinal);
            var entityIdsByGuid = new Dictionary<string, int>(StringComparer.Ordinal);
            var mappings = new List<StageAuthoringIdMapping>();
            var newMappings = new List<StageAuthoringIdMapping>();
            var retiredMappings = new List<StageAuthoringIdMapping>();
            var placements = source.Placements;

            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    continue;
                }

                var stableGuid = Normalize(placement.StableGuid);
                if (string.IsNullOrEmpty(stableGuid) || !activeGuids.Add(stableGuid))
                {
                    continue;
                }

                StageAuthoringIdMapping mapping;
                if (existingByGuid.TryGetValue(stableGuid, out var existingMapping))
                {
                    mapping = new StageAuthoringIdMapping
                    {
                        StableGuid = stableGuid,
                        EntityId = existingMapping.EntityId,
                        Retired = false,
                        LastKnownDisplayName = ResolveDisplayName(placement),
                    };
                }
                else
                {
                    var nextEntityId = ++maxEntityId;
                    while (usedEntityIds.Contains(nextEntityId))
                    {
                        nextEntityId++;
                    }

                    maxEntityId = nextEntityId;
                    usedEntityIds.Add(nextEntityId);
                    mapping = new StageAuthoringIdMapping
                    {
                        StableGuid = stableGuid,
                        EntityId = nextEntityId,
                        Retired = false,
                        LastKnownDisplayName = ResolveDisplayName(placement),
                    };
                    newMappings.Add(mapping);
                }

                entityIdsByGuid[stableGuid] = mapping.EntityId;
                mappings.Add(mapping);
            }

            foreach (var pair in existingByGuid)
            {
                if (activeGuids.Contains(pair.Key))
                {
                    continue;
                }

                var retired = new StageAuthoringIdMapping
                {
                    StableGuid = pair.Key,
                    EntityId = pair.Value.EntityId,
                    Retired = true,
                    LastKnownDisplayName = Normalize(pair.Value.LastKnownDisplayName),
                };
                mappings.Add(retired);
                retiredMappings.Add(retired);
            }

            mappings.Sort(CompareMappings);
            newMappings.Sort(CompareMappings);
            retiredMappings.Sort(CompareMappings);
            return new StageAuthoringAllocationPlan(entityIdsByGuid, mappings, newMappings, retiredMappings);
        }

        public static StageAuthoringNormalizedGameplaySnapshot ProjectExpectedGameplay(
            StageAuthoringDefinition source,
            StageAuthoringAllocationPlan allocationPlan)
        {
            if (source == null)
            {
                return new StageAuthoringNormalizedGameplaySnapshot(
                    default,
                    Array.Empty<StageAuthoringNormalizedSpawn>(),
                    Array.Empty<StageAuthoringNormalizedTileFeature>(),
                    Array.Empty<StageAuthoringNormalizedZone>(),
                    StageAuthoringNormalizedObjective.Empty);
            }

            allocationPlan ??= BuildAllocationPlan(source);
            var spawns = new List<StageAuthoringNormalizedSpawn>();
            var placements = source.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    continue;
                }

                var stableGuid = Normalize(placement.StableGuid);
                if (string.IsNullOrEmpty(stableGuid) ||
                    !allocationPlan.EntityIdsByStableGuid.TryGetValue(stableGuid, out var entityId))
                {
                    continue;
                }

                spawns.Add(new StageAuthoringNormalizedSpawn(
                    stableGuid,
                    entityId,
                    ToSpawnKind(placement.Kind),
                    placement.Cell,
                    placement.Facing,
                    placement.Hp,
                    Normalize(placement.UnitStackGroup),
                    placement.BoxCapabilities,
                    placement.EnemyAiMode,
                    placement.EnemyAiStateTimer,
                    placement.EnemyAiProfileOverride));
            }

            return new StageAuthoringNormalizedGameplaySnapshot(
                source.Board,
                SortSpawns(spawns),
                ProjectTileFeatures(source.TileFeatures),
                ProjectZones(source.Zones),
                ProjectObjective(source.Objective));
        }

        public static StageAuthoringNormalizedGameplaySnapshot ProjectActualGameplay(StageDefinition stage)
        {
            if (stage == null)
            {
                return null;
            }

            var spawns = new List<StageAuthoringNormalizedSpawn>();
            AddActualSpawns(spawns, stage.PlayerSpawns);
            AddActualSpawns(spawns, stage.EnemySpawns);
            AddActualSpawns(spawns, stage.BoxSpawns);
            AddActualSpawns(spawns, stage.WallSpawns);
            return new StageAuthoringNormalizedGameplaySnapshot(
                stage.Board,
                SortSpawns(spawns),
                ProjectTileFeatures(stage.TileFeatures),
                ProjectZones(stage.Zones),
                ProjectObjective(stage.Objective));
        }

        public static StageAuthoringNormalizedPresentationSnapshot ProjectExpectedPresentation(
            StageAuthoringDefinition source,
            StageAuthoringAllocationPlan allocationPlan)
        {
            if (source == null)
            {
                return new StageAuthoringNormalizedPresentationSnapshot(
                    Array.Empty<StageAuthoringNormalizedPresentationBinding>(),
                    Array.Empty<StageAuthoringNormalizedPresentationBinding>());
            }

            allocationPlan ??= BuildAllocationPlan(source);
            var enemy = new List<StageAuthoringNormalizedPresentationBinding>();
            var statics = new List<StageAuthoringNormalizedPresentationBinding>();
            var placements = source.Placements;
            for (var i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement == null)
                {
                    continue;
                }

                var stableGuid = Normalize(placement.StableGuid);
                var presentationId = Normalize(placement.PresentationId);
                if (string.IsNullOrEmpty(stableGuid) ||
                    string.IsNullOrEmpty(presentationId) ||
                    !allocationPlan.EntityIdsByStableGuid.TryGetValue(stableGuid, out var entityId))
                {
                    continue;
                }

                switch (StageAuthoringKindRegistry.GetPresentationLane(placement.Kind))
                {
                    case StageAuthoringPresentationLane.Enemy:
                        enemy.Add(new StageAuthoringNormalizedPresentationBinding(entityId, presentationId));
                        break;
                    case StageAuthoringPresentationLane.Static:
                        statics.Add(new StageAuthoringNormalizedPresentationBinding(entityId, presentationId));
                        break;
                }
            }

            return new StageAuthoringNormalizedPresentationSnapshot(SortBindings(enemy), SortBindings(statics));
        }

        public static StageAuthoringNormalizedPresentationSnapshot ProjectActualPresentation(
            StagePresentationDefinition presentation)
        {
            if (presentation == null)
            {
                return null;
            }

            return new StageAuthoringNormalizedPresentationSnapshot(
                ProjectEnemyBindings(presentation.EnemyPresentationBindings),
                ProjectStaticBindings(presentation.StaticEntityPresentationBindings));
        }

        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static void AddActualSpawns(ICollection<StageAuthoringNormalizedSpawn> normalized, IReadOnlyList<StageSpawnDefinition> spawns)
        {
            if (spawns == null)
            {
                return;
            }

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                normalized.Add(new StageAuthoringNormalizedSpawn(
                    string.Empty,
                    spawn.EntityId,
                    spawn.Kind,
                    spawn.Cell,
                    spawn.Facing,
                    spawn.Hp,
                    Normalize(spawn.UnitStackGroup),
                    spawn.BoxCapabilities,
                    spawn.EnemyAiMode,
                    spawn.EnemyAiStateTimer,
                    spawn.EnemyAiProfile));
            }
        }

        private static StageAuthoringNormalizedZone[] ProjectZones(IReadOnlyList<StageZoneDefinition> zones)
        {
            if (zones == null || zones.Count == 0)
            {
                return Array.Empty<StageAuthoringNormalizedZone>();
            }

            var normalized = new List<StageAuthoringNormalizedZone>(zones.Count);
            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                var regions = zone.GetRegionsOrEmpty();
                var normalizedRegions = new StageAuthoringNormalizedZoneRegion[regions.Length];
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    normalizedRegions[regionIndex] = new StageAuthoringNormalizedZoneRegion(
                        regions[regionIndex].MinInclusive,
                        regions[regionIndex].MaxInclusive);
                }

                Array.Sort(normalizedRegions, CompareRegions);
                normalized.Add(new StageAuthoringNormalizedZone(
                    Normalize(zone.ZoneId),
                    zone.FaceId,
                    normalizedRegions));
            }

            normalized.Sort((left, right) => string.Compare(left.ZoneId, right.ZoneId, StringComparison.Ordinal));
            return normalized.ToArray();
        }

        private static StageAuthoringNormalizedTileFeature[] ProjectTileFeatures(
            IReadOnlyList<StageTileFeatureDefinition> tileFeatures)
        {
            if (tileFeatures == null || tileFeatures.Count == 0)
            {
                return Array.Empty<StageAuthoringNormalizedTileFeature>();
            }

            var normalized = new List<StageAuthoringNormalizedTileFeature>(tileFeatures.Count);
            for (var i = 0; i < tileFeatures.Count; i++)
            {
                var tileFeature = tileFeatures[i];
                normalized.Add(new StageAuthoringNormalizedTileFeature(
                    tileFeature.TileId,
                    tileFeature.Cell,
                    tileFeature.Kind,
                    tileFeature.ActivationRule,
                    tileFeature.Direction,
                    tileFeature.BoxSelector,
                    tileFeature.BoundEntityId,
                    Normalize(tileFeature.PresentationKey)));
            }

            normalized.Sort((left, right) => left.TileId.CompareTo(right.TileId));
            return normalized.ToArray();
        }

        private static StageAuthoringNormalizedObjective ProjectObjective(StageObjectiveAuthoring objective)
        {
            var entries = objective.GetConditionEntriesOrEmpty();
            if (entries.Length == 0)
            {
                return new StageAuthoringNormalizedObjective(objective.CompletionPolicy, Array.Empty<StageAuthoringNormalizedObjectiveCondition>());
            }

            var conditions = new StageAuthoringNormalizedObjectiveCondition[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                conditions[i] = new StageAuthoringNormalizedObjectiveCondition(
                    entries[i].Condition,
                    entries[i].Required,
                    entries[i].Role,
                    Normalize(entries[i].StableConditionId));
            }

            return new StageAuthoringNormalizedObjective(objective.CompletionPolicy, conditions);
        }

        private static StageAuthoringNormalizedPresentationBinding[] ProjectEnemyBindings(IReadOnlyList<EnemyPresentationBinding> bindings)
        {
            var normalized = new List<StageAuthoringNormalizedPresentationBinding>();
            if (bindings != null)
            {
                for (var i = 0; i < bindings.Count; i++)
                {
                    normalized.Add(new StageAuthoringNormalizedPresentationBinding(
                        bindings[i].EntityId,
                        Normalize(bindings[i].PresentationId)));
                }
            }

            return SortBindings(normalized);
        }

        private static StageAuthoringNormalizedPresentationBinding[] ProjectStaticBindings(IReadOnlyList<StaticEntityPresentationBinding> bindings)
        {
            var normalized = new List<StageAuthoringNormalizedPresentationBinding>();
            if (bindings != null)
            {
                for (var i = 0; i < bindings.Count; i++)
                {
                    normalized.Add(new StageAuthoringNormalizedPresentationBinding(
                        bindings[i].EntityId,
                        Normalize(bindings[i].PresentationId)));
                }
            }

            return SortBindings(normalized);
        }

        private static StageAuthoringNormalizedSpawn[] SortSpawns(List<StageAuthoringNormalizedSpawn> spawns)
        {
            spawns.Sort(CompareSpawns);
            return spawns.ToArray();
        }

        private static StageAuthoringNormalizedPresentationBinding[] SortBindings(List<StageAuthoringNormalizedPresentationBinding> bindings)
        {
            bindings.Sort((left, right) =>
            {
                var idCompare = left.EntityId.CompareTo(right.EntityId);
                return idCompare != 0
                    ? idCompare
                    : string.Compare(left.PresentationId, right.PresentationId, StringComparison.Ordinal);
            });
            return bindings.ToArray();
        }

        private static int CompareSpawns(StageAuthoringNormalizedSpawn left, StageAuthoringNormalizedSpawn right)
        {
            var kindCompare = left.Kind.CompareTo(right.Kind);
            if (kindCompare != 0)
            {
                return kindCompare;
            }

            var idCompare = left.EntityId.CompareTo(right.EntityId);
            if (idCompare != 0)
            {
                return idCompare;
            }

            var faceCompare = left.Cell.face.CompareTo(right.Cell.face);
            if (faceCompare != 0)
            {
                return faceCompare;
            }

            var xCompare = left.Cell.x.CompareTo(right.Cell.x);
            return xCompare != 0 ? xCompare : left.Cell.y.CompareTo(right.Cell.y);
        }

        private static int CompareRegions(StageAuthoringNormalizedZoneRegion left, StageAuthoringNormalizedZoneRegion right)
        {
            var minXCompare = left.MinInclusive.x.CompareTo(right.MinInclusive.x);
            if (minXCompare != 0)
            {
                return minXCompare;
            }

            var minYCompare = left.MinInclusive.y.CompareTo(right.MinInclusive.y);
            if (minYCompare != 0)
            {
                return minYCompare;
            }

            var maxXCompare = left.MaxInclusive.x.CompareTo(right.MaxInclusive.x);
            return maxXCompare != 0 ? maxXCompare : left.MaxInclusive.y.CompareTo(right.MaxInclusive.y);
        }

        private static int CompareMappings(StageAuthoringIdMapping left, StageAuthoringIdMapping right)
        {
            var idCompare = left.EntityId.CompareTo(right.EntityId);
            return idCompare != 0
                ? idCompare
                : string.Compare(left.StableGuid, right.StableGuid, StringComparison.Ordinal);
        }

        private static StageSpawnKind ToSpawnKind(StageAuthoringEntityKind kind)
        {
            return kind switch
            {
                StageAuthoringEntityKind.Player => StageSpawnKind.Player,
                StageAuthoringEntityKind.Enemy => StageSpawnKind.Enemy,
                StageAuthoringEntityKind.Box => StageSpawnKind.Box,
                StageAuthoringEntityKind.Wall => StageSpawnKind.Wall,
                _ => StageSpawnKind.Player,
            };
        }

        private static string ResolveDisplayName(StagePlacedEntityAuthoring placement)
        {
            var displayName = Normalize(placement.DisplayName);
            return string.IsNullOrEmpty(displayName) ? placement.Kind.ToString() : displayName;
        }
    }

    public sealed class StageAuthoringAllocationPlan
    {
        public static readonly StageAuthoringAllocationPlan Empty = new(
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<StageAuthoringIdMapping>(),
            Array.Empty<StageAuthoringIdMapping>(),
            Array.Empty<StageAuthoringIdMapping>());

        public StageAuthoringAllocationPlan(
            IReadOnlyDictionary<string, int> entityIdsByStableGuid,
            IReadOnlyList<StageAuthoringIdMapping> mappings,
            IReadOnlyList<StageAuthoringIdMapping> newMappings,
            IReadOnlyList<StageAuthoringIdMapping> retiredMappings)
        {
            EntityIdsByStableGuid = entityIdsByStableGuid;
            Mappings = mappings;
            NewMappings = newMappings;
            RetiredMappings = retiredMappings;
        }

        public IReadOnlyDictionary<string, int> EntityIdsByStableGuid { get; }

        public IReadOnlyList<StageAuthoringIdMapping> Mappings { get; }

        public IReadOnlyList<StageAuthoringIdMapping> NewMappings { get; }

        public IReadOnlyList<StageAuthoringIdMapping> RetiredMappings { get; }
    }
}
