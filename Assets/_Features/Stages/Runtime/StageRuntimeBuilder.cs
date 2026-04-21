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
            var validated = StageDefinitionValidator.ValidateAndNormalize(stage);
            var initialEntities = BuildInitialEntities(validated);
            var enemyAiProfileOverrides = BuildEnemyAiProfileOverrides(validated.Spawns);
            var objectiveRuntimeDefinition = BuildObjectiveRuntimeDefinition(validated);

            return new StageRuntimeBuildResult(
                validated.BoardBounds,
                validated.InitialTopology,
                initialEntities,
                TerrainData.Empty,
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
            StageDefinitionValidator.ValidatedStageData validated)
        {
            if (validated == null)
            {
                throw new ArgumentNullException(nameof(validated));
            }

            var zoneDefinitions = BuildZoneRuntimeDefinitions(validated.Zones);
            if (validated.Objective.CompletionPolicy == StageCompletionPolicy.Disabled)
            {
                return new StageObjectiveRuntimeDefinition(
                    StageCompletionPolicy.Disabled,
                    validated.PlayerEntityId,
                    zoneDefinitions,
                    Array.Empty<StageZoneRuntimeDefinition>(),
                    Array.Empty<StageConditionRuntimeDefinition>());
            }

            var zonesById = BuildZoneLookup(zoneDefinitions);
            var goalZones = BuildGoalZoneDefinitions(validated.Objective.GetGoalZoneIdsOrEmpty(), zonesById);
            var conditionDefinitions = BuildConditionRuntimeDefinitions(
                validated,
                zonesById);

            return new StageObjectiveRuntimeDefinition(
                validated.Objective.CompletionPolicy,
                validated.PlayerEntityId,
                zoneDefinitions,
                goalZones,
                conditionDefinitions);
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

        private static StageZoneRuntimeDefinition[] BuildGoalZoneDefinitions(
            IReadOnlyList<string> goalZoneIds,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById)
        {
            if (goalZoneIds == null || goalZoneIds.Count == 0)
            {
                return Array.Empty<StageZoneRuntimeDefinition>();
            }

            var goalZones = new StageZoneRuntimeDefinition[goalZoneIds.Count];
            for (var i = 0; i < goalZoneIds.Count; i++)
            {
                if (!zonesById.TryGetValue(goalZoneIds[i], out goalZones[i]))
                {
                    throw new InvalidOperationException(
                        $"Objective references unknown goal zone id '{goalZoneIds[i]}'.");
                }
            }

            return goalZones;
        }

        private static StageConditionRuntimeDefinition[] BuildConditionRuntimeDefinitions(
            StageDefinitionValidator.ValidatedStageData validated,
            IReadOnlyDictionary<string, StageZoneRuntimeDefinition> zonesById)
        {
            var requiredConditions = validated.Objective.GetRequiredConditionsOrEmpty();
            if (requiredConditions.Length == 0)
            {
                return Array.Empty<StageConditionRuntimeDefinition>();
            }

            var compilationContext = new StageConditionCompilationContext(
                validated.StageName,
                validated.PlayerEntityId,
                zonesById);
            var runtimeDefinitions = new StageConditionRuntimeDefinition[requiredConditions.Length];
            for (var i = 0; i < requiredConditions.Length; i++)
            {
                runtimeDefinitions[i] = requiredConditions[i].Compile(in compilationContext);
            }

            return runtimeDefinitions;
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
