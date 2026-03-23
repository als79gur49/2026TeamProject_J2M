using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class FuzzScenarioGenerator
    {
        private static readonly Direction[] MovementDirections =
        {
            Direction.Up,
            Direction.Right,
            Direction.Down,
            Direction.Left,
        };

        public FuzzScenarioDefinition Generate(int seed)
        {
            var random = new XorShift32(unchecked((uint)seed));
            var entityCount = 2 + random.NextInt(4);
            var tickCount = 4 + random.NextInt(5);

            var initialEntities = BuildInitialEntities(ref random, entityCount);
            var tickInputs = BuildTickInputs(tickCount);
            var entityScripts = BuildEntityScripts(ref random, initialEntities, tickCount);

            return new FuzzScenarioDefinition(seed, initialEntities, tickInputs, entityScripts);
        }

        private static List<EntityState> BuildInitialEntities(ref XorShift32 random, int entityCount)
        {
            var cellPool = BuildCellPool();
            var initialEntities = new List<EntityState>(entityCount);

            for (var entityIndex = 0; entityIndex < entityCount; entityIndex++)
            {
                var cellIndex = random.NextInt(cellPool.Count);
                var position = cellPool[cellIndex];
                cellPool.RemoveAt(cellIndex);

                var entityId = (entityIndex + 1) * 10;
                var hp = 1 + random.NextInt(3);
                initialEntities.Add(
                    new EntityState
                    {
                        entityId = entityId,
                        position = position,
                        hp = hp,
                        maxHp = hp,
                        teamId = entityIndex % 2 == 0 ? 1 : 2,
                        type = EntityType.Unit,
                        state = EntityPhaseState.Idle,
                        stateTimer = 0,
                        facing = Direction.Right,
                        markedForDeath = false,
                        spawnTick = 0,
                    });
            }

            return initialEntities;
        }

        private static List<TickInput> BuildTickInputs(int tickCount)
        {
            var tickInputs = new List<TickInput>(tickCount);

            for (var tickIndex = 1; tickIndex <= tickCount; tickIndex++)
            {
                tickInputs.Add(new TickInput(tickIndex));
            }

            return tickInputs;
        }

        private static List<FuzzEntityScript> BuildEntityScripts(
            ref XorShift32 random,
            IReadOnlyList<EntityState> initialEntities,
            int tickCount)
        {
            var entityScripts = new List<FuzzEntityScript>(initialEntities.Count);

            for (var entityIndex = 0; entityIndex < initialEntities.Count; entityIndex++)
            {
                var entity = initialEntities[entityIndex];
                var movementCommands = new List<FuzzMovementCommand>();
                var attackCommands = new List<FuzzAttackCommand>();
                var targetIds = GetOrderedOpposingTargetIds(initialEntities, entity.teamId);

                for (var tickIndex = 1; tickIndex <= tickCount; tickIndex++)
                {
                    if (random.NextInt(3) != 0)
                    {
                        movementCommands.Add(
                            new FuzzMovementCommand(
                                tickIndex,
                                priority: 1 + random.NextInt(10),
                                direction: MovementDirections[random.NextInt(MovementDirections.Length)]));
                    }

                    if (targetIds.Count > 0 && random.NextInt(2) == 0)
                    {
                        attackCommands.Add(
                            new FuzzAttackCommand(
                                tickIndex,
                                priority: 1 + random.NextInt(10),
                                targetId: targetIds[random.NextInt(targetIds.Count)]));
                    }
                }

                entityScripts.Add(new FuzzEntityScript(entity.entityId, movementCommands, attackCommands));
            }

            return entityScripts;
        }

        private static List<int> GetOrderedOpposingTargetIds(IReadOnlyList<EntityState> initialEntities, int teamId)
        {
            var targetIds = new List<int>();

            for (var i = 0; i < initialEntities.Count; i++)
            {
                var entity = initialEntities[i];
                if (entity.teamId == teamId)
                {
                    continue;
                }

                targetIds.Add(entity.entityId);
            }

            return targetIds;
        }

        private static List<Vector2Int> BuildCellPool()
        {
            var cells = new List<Vector2Int>(16);

            for (var x = 0; x < 4; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            return cells;
        }

        private struct XorShift32
        {
            private uint _state;

            public XorShift32(uint seed)
            {
                _state = seed == 0 ? 2463534242u : seed;
            }

            public int NextInt(int exclusiveMax)
            {
                if (exclusiveMax <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
                }

                return (int)(NextUInt() % (uint)exclusiveMax);
            }

            private uint NextUInt()
            {
                var value = _state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                _state = value;
                return value;
            }
        }
    }
}
