using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Objectives;

namespace Game.Feature.Stages
{
    public static class StageRuntimeBuilder
    {
        public static StageRuntimeBuildResult Build(StageDefinition stage)
        {
            return Build(stage, StageSimulationTiming.Default);
        }

        public static StageRuntimeBuildResult Build(StageDefinition stage, StageSimulationTiming timing)
        {
            var validated = StageDefinitionValidator.ValidateAndNormalize(stage);
            var initialEntities = BuildInitialEntities(validated);
            var enemyAiProfileOverrides = BuildEnemyAiProfileOverrides(validated.Spawns);
            var objectiveRuntimeDefinition = BuildObjectiveRuntimeDefinition(validated, timing);

            return new StageRuntimeBuildResult(
                validated.BoardBounds,
                validated.InitialTopology,
                initialEntities,
                TerrainData.Empty,
                Array.Empty<TileFeatureState>(),
                validated.PlayerEntityId,
                objectiveRuntimeDefinition,
                enemyAiProfileOverrides);
        }

        private static EntityState[] BuildInitialEntities(StageDefinitionValidator.ValidatedStageData validated)
        {
            var entities = new List<EntityState>(validated.Spawns.Length);

            for (var i = 0; i < validated.Spawns.Length; i++)
            {
                entities.Add(CreateSpawnEntity(validated.Spawns[i]));
            }

            entities.Sort(EntityStateEntityIdComparer.Instance);
            return entities.ToArray();
        }

        private static EnemyAiProfileOverride[] BuildEnemyAiProfileOverrides(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var overrides = new List<EnemyAiProfileOverride>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Enemy ||
                    spawn.EnemyAiMode == EnemyAiMode.None ||
                    spawn.EnemyAiProfile == null)
                {
                    continue;
                }

                overrides.Add(new EnemyAiProfileOverride
                {
                    EntityId = spawn.EntityId,
                    Profile = spawn.EnemyAiProfile,
                });
            }

            overrides.Sort(EnemyAiProfileOverrideComparer.Instance);
            return overrides.ToArray();
        }

        private static EntityState CreateSpawnEntity(StageSpawnDefinition spawn)
        {
            switch (spawn.Kind)
            {
                case StageSpawnKind.Player:
                    return new EntityState
                    {
                        entityId = spawn.EntityId,
                        position = spawn.Cell,
                        hp = spawn.Hp,
                        maxHp = spawn.Hp,
                        teamId = 1,
                        type = EntityType.Unit,
                        unitRole = UnitRole.Player,
                        state = EntityPhaseState.Idle,
                        facing = ResolveFacing(spawn.Facing, Direction.Up),
                    };

                case StageSpawnKind.Enemy:
                    return new EntityState
                    {
                        entityId = spawn.EntityId,
                        position = spawn.Cell,
                        hp = spawn.Hp,
                        maxHp = spawn.Hp,
                        teamId = 2,
                        type = EntityType.Unit,
                        unitRole = UnitRole.Enemy,
                        state = EntityPhaseState.Idle,
                        facing = ResolveFacing(spawn.Facing, Direction.Left),
                        aiMode = spawn.EnemyAiMode,
                        aiStateTimer = spawn.EnemyAiStateTimer,
                    };

                case StageSpawnKind.Box:
                    return new EntityState
                    {
                        entityId = spawn.EntityId,
                        position = spawn.Cell,
                        hp = spawn.Hp,
                        maxHp = spawn.Hp,
                        teamId = 0,
                        type = EntityType.Box,
                        unitRole = UnitRole.None,
                        state = EntityPhaseState.Idle,
                        facing = ResolveFacing(spawn.Facing, Direction.Right),
                        boxCapabilities = spawn.BoxCapabilities,
                    };

                case StageSpawnKind.Wall:
                    return CreateWall(spawn.EntityId, spawn.Cell, spawn.Hp);

                default:
                    throw new ArgumentOutOfRangeException(nameof(spawn.Kind), spawn.Kind, "Unknown stage spawn kind.");
            }
        }

        private static Direction ResolveFacing(Direction authoringFacing, Direction fallbackFacing)
        {
            return authoringFacing != Direction.None
                ? authoringFacing
                : fallbackFacing;
        }

        private static StageObjectiveRuntimeDefinition BuildObjectiveRuntimeDefinition(
            StageDefinitionValidator.ValidatedStageData validated,
            StageSimulationTiming timing)
        {
            if (validated == null)
            {
                throw new ArgumentNullException(nameof(validated));
            }

            var zoneDefinitions = BuildZoneRuntimeDefinitions(validated.Zones);
            var zonesById = BuildZoneLookup(zoneDefinitions);
            if (validated.Objective.CompletionPolicy == StageCompletionPolicy.Disabled)
            {
                return new StageObjectiveRuntimeDefinition(
                    StageCompletionPolicy.Disabled,
                    validated.PlayerEntityId,
                    zoneDefinitions,
                    Array.Empty<StageObjectiveConditionRuntimeDefinitionEntry>(),
                    StageObjectiveDisplayMetadata.Empty);
            }

            var conditionEntries = BuildConditionRuntimeEntries(
                validated,
                zonesById,
                timing,
                out var conditionDisplayMetadata);

            return new StageObjectiveRuntimeDefinition(
                validated.Objective.CompletionPolicy,
                validated.PlayerEntityId,
                zoneDefinitions,
                conditionEntries,
                new StageObjectiveDisplayMetadata(
                    validated.Objective.ObjectiveTitle,
                    validated.Objective.ObjectiveSummary,
                    conditionDisplayMetadata));
        }

        private static StageZoneRuntimeDefinition[] BuildZoneRuntimeDefinitions(
            IReadOnlyList<StageZoneDefinition> zones)
        {
            if (zones == null || zones.Count == 0)
            {
                return Array.Empty<StageZoneRuntimeDefinition>();
            }

            var runtimeDefinitions = new StageZoneRuntimeDefinition[zones.Count];
            for (var i = 0; i < zones.Count; i++)
            {
                var authoringZone = zones[i];
                var regions = authoringZone.GetRegionsOrEmpty();
                var runtimeRegions = new StageZoneRuntimeRegion[regions.Length];
                for (var regionIndex = 0; regionIndex < regions.Length; regionIndex++)
                {
                    runtimeRegions[regionIndex] = new StageZoneRuntimeRegion(
                        regions[regionIndex].MinInclusive,
                        regions[regionIndex].MaxInclusive);
                }

                runtimeDefinitions[i] = new StageZoneRuntimeDefinition(
                    authoringZone.ZoneId,
                    authoringZone.FaceId,
                    runtimeRegions);
            }

            return runtimeDefinitions;
        }

        private static Dictionary<string, StageZoneRuntimeDefinition> BuildZoneLookup(
            IReadOnlyList<StageZoneRuntimeDefinition> zoneDefinitions)
        {
            var zonesById = new Dictionary<string, StageZoneRuntimeDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < zoneDefinitions.Count; i++)
            {
                zonesById[zoneDefinitions[i].ZoneId] = zoneDefinitions[i];
            }

            return zonesById;
        }

        private static StageObjectiveConditionRuntimeDefinitionEntry[] BuildConditionRuntimeEntries(
            StageDefinitionValidator.ValidatedStageData validated,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById,
            StageSimulationTiming timing,
            out StageObjectiveConditionDisplayMetadata[] conditionDisplayMetadata)
        {
            var conditionEntries = validated.Objective.GetConditionEntriesOrEmpty();

            var compilationContext = new StageConditionCompilationContext(
                validated.StageName,
                validated.PlayerEntityId,
                zonesById,
                timing);
            var runtimeEntries = new List<StageObjectiveConditionRuntimeDefinitionEntry>();
            var displayEntries = new List<StageObjectiveConditionDisplayMetadata>();

            for (var i = 0; i < conditionEntries.Length; i++)
            {
                var authoringEntry = conditionEntries[i];
                var runtimeDefinition = authoringEntry.Condition.Compile(in compilationContext);
                var stableConditionId = ResolveStableConditionId(authoringEntry.StableConditionId, i, runtimeDefinition);

                runtimeEntries.Add(new StageObjectiveConditionRuntimeDefinitionEntry(
                    runtimeDefinition,
                    authoringEntry.Required,
                    authoringEntry.Role,
                    stableConditionId));
                displayEntries.Add(new StageObjectiveConditionDisplayMetadata(
                    stableConditionId,
                    authoringEntry.Role,
                    authoringEntry.Required,
                    authoringEntry.DisplayText,
                    authoringEntry.SortOrder,
                    i));
            }

            conditionDisplayMetadata = displayEntries.Count == 0
                ? Array.Empty<StageObjectiveConditionDisplayMetadata>()
                : displayEntries.ToArray();
            return runtimeEntries.Count == 0
                ? Array.Empty<StageObjectiveConditionRuntimeDefinitionEntry>()
                : runtimeEntries.ToArray();
        }

        private static string ResolveStableConditionId(
            string explicitStableConditionId,
            int entryIndex,
            StageConditionRuntimeDefinition runtimeDefinition)
        {
            if (!string.IsNullOrWhiteSpace(explicitStableConditionId))
            {
                return explicitStableConditionId.Trim();
            }

            return $"entry-{entryIndex}-{runtimeDefinition.ConditionId}";
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position, int hp = 1)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
            };
        }

        private sealed class EntityStateEntityIdComparer : IComparer<EntityState>
        {
            public static readonly EntityStateEntityIdComparer Instance = new();

            public int Compare(EntityState left, EntityState right)
            {
                return left.entityId.CompareTo(right.entityId);
            }
        }

        private sealed class EnemyAiProfileOverrideComparer : IComparer<EnemyAiProfileOverride>
        {
            public static readonly EnemyAiProfileOverrideComparer Instance = new();

            public int Compare(EnemyAiProfileOverride left, EnemyAiProfileOverride right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }

        private sealed class EnemyPresentationBindingComparer : IComparer<EnemyPresentationBinding>
        {
            public static readonly EnemyPresentationBindingComparer Instance = new();

            public int Compare(EnemyPresentationBinding left, EnemyPresentationBinding right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }

        private sealed class StaticEntityPresentationBindingComparer : IComparer<StaticEntityPresentationBinding>
        {
            public static readonly StaticEntityPresentationBindingComparer Instance = new();

            public int Compare(StaticEntityPresentationBinding left, StaticEntityPresentationBinding right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }
    }
}
