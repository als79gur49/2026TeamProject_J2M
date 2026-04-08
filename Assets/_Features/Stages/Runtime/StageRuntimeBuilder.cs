using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;

namespace Game.Feature.Stages
{
    public static class StageRuntimeBuilder
    {
        public static StageRuntimeBuildResult Build(StageDefinition stage)
        {
            var validated = StageDefinitionValidator.ValidateAndNormalize(stage);
            var initialEntities = BuildInitialEntities(validated);
            var enemyAiProfileOverrides = BuildEnemyAiProfileOverrides(validated.Spawns);
            var enemyPresentationBindings = BuildEnemyPresentationBindings(validated.Spawns);

            return new StageRuntimeBuildResult(
                validated.BoardBounds,
                validated.InitialTopology,
                initialEntities,
                TerrainData.Empty,
                validated.PlayerEntityId,
                enemyAiProfileOverrides,
                enemyPresentationBindings);
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
                if (spawn.Kind != StageSpawnKind.Enemy || spawn.EnemyAiProfile == null)
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

        private static EnemyPresentationBinding[] BuildEnemyPresentationBindings(IReadOnlyList<StageSpawnDefinition> spawns)
        {
            var bindings = new List<EnemyPresentationBinding>();

            for (var i = 0; i < spawns.Count; i++)
            {
                var spawn = spawns[i];
                if (spawn.Kind != StageSpawnKind.Enemy)
                {
                    continue;
                }

                var presentationId = NormalizePresentationId(spawn.EnemyPresentationId);
                if (string.IsNullOrEmpty(presentationId))
                {
                    continue;
                }

                bindings.Add(new EnemyPresentationBinding
                {
                    EntityId = spawn.EntityId,
                    PresentationId = presentationId,
                });
            }

            bindings.Sort(EnemyPresentationBindingComparer.Instance);
            return bindings.ToArray();
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

        private static string NormalizePresentationId(string presentationId)
        {
            return string.IsNullOrWhiteSpace(presentationId)
                ? string.Empty
                : presentationId.Trim();
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
    }
}
