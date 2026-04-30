using System;
using System.Collections.Generic;

namespace Game.Feature.Stages.Editor
{
    internal static class StageAuthoringEntityIdAllocator
    {
        public static StageAuthoringEntityIdAllocation BuildAllocationPlan(
            StageAuthoringDefinition source,
            StageAuthoringGenerationReport report)
        {
            var existingByGuid = new Dictionary<string, StageAuthoringIdMapping>(StringComparer.Ordinal);
            var usedEntityIds = new HashSet<int>();
            var maxEntityId = 0;
            var sourcePath = string.Empty;
            var existingMappings = source.EntityIdMappings;
            for (var i = 0; i < existingMappings.Count; i++)
            {
                var mapping = existingMappings[i];
                var stableGuid = Normalize(mapping.StableGuid);
                if (string.IsNullOrEmpty(stableGuid))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id-mapping.guid-empty",
                        $"EntityId mapping[{i}] must declare a stable guid.",
                        source,
                        sourcePath);
                    continue;
                }

                if (mapping.EntityId <= 0)
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id-mapping.non-positive",
                        $"EntityId mapping '{stableGuid}' must use a positive EntityId.",
                        source,
                        sourcePath);
                    continue;
                }

                if (!existingByGuid.TryAdd(stableGuid, new StageAuthoringIdMapping
                    {
                        StableGuid = stableGuid,
                        EntityId = mapping.EntityId,
                        Retired = mapping.Retired,
                        LastKnownDisplayName = Normalize(mapping.LastKnownDisplayName),
                    }))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id-mapping.guid-duplicate",
                        $"Duplicate EntityId mapping stable guid '{stableGuid}'.",
                        source,
                        sourcePath);
                }

                if (!usedEntityIds.Add(mapping.EntityId))
                {
                    report.Add(
                        StageValidationSeverity.Error,
                        "authoring.entity-id-mapping.id-duplicate",
                        $"Duplicate EntityId mapping id {mapping.EntityId}.",
                        source,
                        sourcePath);
                }

                maxEntityId = Math.Max(maxEntityId, mapping.EntityId);
            }

            if (report.HasErrors)
            {
                return new StageAuthoringEntityIdAllocation(
                    new Dictionary<string, int>(StringComparer.Ordinal),
                    Array.Empty<StageAuthoringIdMapping>());
            }

            var activeGuids = new HashSet<string>(StringComparer.Ordinal);
            var entityIdsByGuid = new Dictionary<string, int>(StringComparer.Ordinal);
            var mappings = new List<StageAuthoringIdMapping>();
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

                if (existingByGuid.TryGetValue(stableGuid, out var existingMapping))
                {
                    entityIdsByGuid[stableGuid] = existingMapping.EntityId;
                    mappings.Add(new StageAuthoringIdMapping
                    {
                        StableGuid = stableGuid,
                        EntityId = existingMapping.EntityId,
                        Retired = false,
                        LastKnownDisplayName = ResolveDisplayName(placement),
                    });
                    continue;
                }

                var nextEntityId = ++maxEntityId;
                while (usedEntityIds.Contains(nextEntityId))
                {
                    nextEntityId++;
                    maxEntityId = nextEntityId;
                }

                usedEntityIds.Add(nextEntityId);
                entityIdsByGuid[stableGuid] = nextEntityId;
                mappings.Add(new StageAuthoringIdMapping
                {
                    StableGuid = stableGuid,
                    EntityId = nextEntityId,
                    Retired = false,
                    LastKnownDisplayName = ResolveDisplayName(placement),
                });
            }

            foreach (var pair in existingByGuid)
            {
                if (activeGuids.Contains(pair.Key))
                {
                    continue;
                }

                var mapping = pair.Value;
                mappings.Add(new StageAuthoringIdMapping
                {
                    StableGuid = pair.Key,
                    EntityId = mapping.EntityId,
                    Retired = true,
                    LastKnownDisplayName = Normalize(mapping.LastKnownDisplayName),
                });
            }

            mappings.Sort((left, right) =>
            {
                var idCompare = left.EntityId.CompareTo(right.EntityId);
                return idCompare != 0
                    ? idCompare
                    : string.Compare(left.StableGuid, right.StableGuid, StringComparison.Ordinal);
            });

            return new StageAuthoringEntityIdAllocation(entityIdsByGuid, mappings);
        }

        public static StageAuthoringEntityIdAllocation Allocate(
            StageAuthoringDefinition source,
            StageAuthoringGenerationReport report)
        {
            return BuildAllocationPlan(source, report);
        }

        private static string ResolveDisplayName(StagePlacedEntityAuthoring placement)
        {
            var displayName = Normalize(placement.DisplayName);
            return string.IsNullOrEmpty(displayName)
                ? placement.Kind.ToString()
                : displayName;
        }

        private static string Normalize(string value)
        {
            return StageAuthoringGenerator.Normalize(value);
        }
    }

    internal sealed class StageAuthoringEntityIdAllocation
    {
        public StageAuthoringEntityIdAllocation(
            IReadOnlyDictionary<string, int> entityIdsByStableGuid,
            IReadOnlyList<StageAuthoringIdMapping> mappings)
        {
            EntityIdsByStableGuid = entityIdsByStableGuid;
            Mappings = mappings;
        }

        public IReadOnlyDictionary<string, int> EntityIdsByStableGuid { get; }

        public IReadOnlyList<StageAuthoringIdMapping> Mappings { get; }
    }
}
