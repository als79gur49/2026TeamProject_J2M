using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;

namespace Game.Feature.Gameplay.Tests.Fuzz
{
    internal sealed class FuzzScenarioDefinition
    {
        private readonly ReadOnlyCollection<FuzzEntityScript> _entityScripts;
        private readonly ReadOnlyCollection<EntityState> _initialEntities;
        private readonly ReadOnlyCollection<TickInput> _tickInputs;

        public FuzzScenarioDefinition(
            int seed,
            IEnumerable<EntityState> initialEntities,
            IEnumerable<TickInput> tickInputs,
            IEnumerable<FuzzEntityScript> entityScripts)
        {
            if (initialEntities == null)
            {
                throw new ArgumentNullException(nameof(initialEntities));
            }

            if (tickInputs == null)
            {
                throw new ArgumentNullException(nameof(tickInputs));
            }

            if (entityScripts == null)
            {
                throw new ArgumentNullException(nameof(entityScripts));
            }

            var orderedInitialEntities = new List<EntityState>(initialEntities);
            orderedInitialEntities.Sort(EntityStateComparer.Instance);

            var orderedTickInputs = new List<TickInput>(tickInputs);
            orderedTickInputs.Sort(TickInputComparer.Instance);

            var orderedEntityScripts = new List<FuzzEntityScript>(entityScripts);
            orderedEntityScripts.Sort(FuzzEntityScriptComparer.Instance);

            Seed = seed;
            _initialEntities = new ReadOnlyCollection<EntityState>(orderedInitialEntities);
            _tickInputs = new ReadOnlyCollection<TickInput>(orderedTickInputs);
            _entityScripts = new ReadOnlyCollection<FuzzEntityScript>(orderedEntityScripts);

            Validate();
            CanonicalScenarioDump = BuildCanonicalScenarioDump();
        }

        public int Seed { get; }

        public IReadOnlyList<EntityState> InitialEntities => _initialEntities;

        public IReadOnlyList<TickInput> TickInputs => _tickInputs;

        public IReadOnlyList<FuzzEntityScript> EntityScripts => _entityScripts;

        public string CanonicalScenarioDump { get; }

        public WorldState CreateWorldState()
        {
            return GameplayWorldStateTestFactory.CreateLegacyUnbounded(_initialEntities);
        }

        public IEntityLogic[] CreateEntityLogics()
        {
            var entityLogics = new IEntityLogic[_entityScripts.Count];

            for (var i = 0; i < _entityScripts.Count; i++)
            {
                entityLogics[i] = new FuzzScriptedEntityLogic(_entityScripts[i]);
            }

            return entityLogics;
        }

        private void Validate()
        {
            if (_initialEntities.Count == 0)
            {
                throw new InvalidOperationException("Fuzz scenarios require at least one initial entity.");
            }

            if (_tickInputs.Count == 0)
            {
                throw new InvalidOperationException("Fuzz scenarios require at least one tick input.");
            }

            EnsureUniqueEntityIds();
            EnsureStrictlyAscendingTickInputs();
            EnsureScriptsMatchEntities();
            EnsureUnitsOnly();
        }

        private void EnsureUniqueEntityIds()
        {
            var lastEntityId = int.MinValue;

            for (var i = 0; i < _initialEntities.Count; i++)
            {
                var entity = _initialEntities[i];
                if (entity.entityId == lastEntityId)
                {
                    throw new InvalidOperationException($"Duplicate entity id detected in fuzz scenario: {entity.entityId}");
                }

                lastEntityId = entity.entityId;
            }
        }

        private void EnsureStrictlyAscendingTickInputs()
        {
            var previousTickIndex = int.MinValue;

            for (var i = 0; i < _tickInputs.Count; i++)
            {
                var tickIndex = _tickInputs[i].TickIndex;
                if (tickIndex <= previousTickIndex)
                {
                    throw new InvalidOperationException("Fuzz scenario tick inputs must be strictly increasing.");
                }

                previousTickIndex = tickIndex;
            }
        }

        private void EnsureScriptsMatchEntities()
        {
            if (_entityScripts.Count != _initialEntities.Count)
            {
                throw new InvalidOperationException("Fuzz scenario requires exactly one script per initial entity.");
            }

            for (var i = 0; i < _initialEntities.Count; i++)
            {
                if (_initialEntities[i].entityId != _entityScripts[i].EntityId)
                {
                    throw new InvalidOperationException(
                        $"Fuzz scenario script/entity mismatch at index {i}. Entity={_initialEntities[i].entityId}, Script={_entityScripts[i].EntityId}");
                }
            }
        }

        private void EnsureUnitsOnly()
        {
            for (var i = 0; i < _initialEntities.Count; i++)
            {
                if (_initialEntities[i].type != EntityType.Unit)
                {
                    throw new InvalidOperationException("Current fuzz infrastructure only supports Unit entities.");
                }
            }
        }

        private string BuildCanonicalScenarioDump()
        {
            var builder = new StringBuilder(2048);
            builder.Append("Seed=").Append(Seed).Append('\n');
            builder.Append("InitialEntities").Append('\n');
            AppendInitialEntities(builder);
            builder.Append("TickInputs").Append('\n');
            AppendTickInputs(builder);
            builder.Append("EntityScripts").Append('\n');
            AppendEntityScripts(builder);
            return builder.ToString();
        }

        private void AppendInitialEntities(StringBuilder builder)
        {
            for (var i = 0; i < _initialEntities.Count; i++)
            {
                var entity = _initialEntities[i];
                builder
                    .Append("  E=").Append(entity.entityId)
                    .Append("|Pos=(").Append(entity.position.x).Append(',').Append(entity.position.y).Append(')')
                    .Append("|Hp=").Append(entity.hp)
                    .Append("|MaxHp=").Append(entity.maxHp)
                    .Append("|Team=").Append(entity.teamId)
                    .Append("|Type=").Append(entity.type)
                    .Append("|State=").Append(entity.state)
                    .Append("|Timer=").Append(entity.stateTimer)
                    .Append("|Facing=").Append(entity.facing)
                    .Append("|Marked=").Append(entity.markedForDeath ? 1 : 0)
                    .Append("|SpawnTick=").Append(entity.spawnTick)
                    .Append("|BoxCapabilities=").Append(entity.boxCapabilities)
                    .Append('\n');
            }
        }

        private void AppendTickInputs(StringBuilder builder)
        {
            for (var i = 0; i < _tickInputs.Count; i++)
            {
                builder.Append("  Tick=").Append(_tickInputs[i].TickIndex).Append('\n');
            }
        }

        private void AppendEntityScripts(StringBuilder builder)
        {
            for (var i = 0; i < _entityScripts.Count; i++)
            {
                var script = _entityScripts[i];
                builder.Append("  Entity=").Append(script.EntityId).Append('\n');

                if (script.MovementCommands.Count == 0 && script.AttackCommands.Count == 0)
                {
                    builder.Append("    <empty>").Append('\n');
                    continue;
                }

                for (var movementIndex = 0; movementIndex < script.MovementCommands.Count; movementIndex++)
                {
                    var movementCommand = script.MovementCommands[movementIndex];
                    builder
                        .Append("    Move|Tick=").Append(movementCommand.TickIndex)
                        .Append("|Priority=").Append(movementCommand.Priority)
                        .Append("|Direction=").Append(movementCommand.Direction)
                        .Append('\n');
                }

                for (var attackIndex = 0; attackIndex < script.AttackCommands.Count; attackIndex++)
                {
                    var attackCommand = script.AttackCommands[attackIndex];
                    builder
                        .Append("    Attack|Tick=").Append(attackCommand.TickIndex)
                        .Append("|Priority=").Append(attackCommand.Priority)
                        .Append("|Target=").Append(attackCommand.TargetId)
                        .Append('\n');
                }
            }
        }

        private sealed class EntityStateComparer : IComparer<EntityState>
        {
            public static readonly EntityStateComparer Instance = new();

            public int Compare(EntityState left, EntityState right)
            {
                return left.entityId.CompareTo(right.entityId);
            }
        }

        private sealed class TickInputComparer : IComparer<TickInput>
        {
            public static readonly TickInputComparer Instance = new();

            public int Compare(TickInput left, TickInput right)
            {
                return left.TickIndex.CompareTo(right.TickIndex);
            }
        }

        private sealed class FuzzEntityScriptComparer : IComparer<FuzzEntityScript>
        {
            public static readonly FuzzEntityScriptComparer Instance = new();

            public int Compare(FuzzEntityScript left, FuzzEntityScript right)
            {
                return left.EntityId.CompareTo(right.EntityId);
            }
        }
    }

    internal sealed class FuzzEntityScript
    {
        private readonly ReadOnlyCollection<FuzzAttackCommand> _attackCommands;
        private readonly ReadOnlyCollection<FuzzMovementCommand> _movementCommands;

        public FuzzEntityScript(
            int entityId,
            IEnumerable<FuzzMovementCommand> movementCommands,
            IEnumerable<FuzzAttackCommand> attackCommands)
        {
            if (movementCommands == null)
            {
                throw new ArgumentNullException(nameof(movementCommands));
            }

            if (attackCommands == null)
            {
                throw new ArgumentNullException(nameof(attackCommands));
            }

            var orderedMovementCommands = new List<FuzzMovementCommand>(movementCommands);
            orderedMovementCommands.Sort(FuzzMovementCommandComparer.Instance);

            var orderedAttackCommands = new List<FuzzAttackCommand>(attackCommands);
            orderedAttackCommands.Sort(FuzzAttackCommandComparer.Instance);

            EntityId = entityId;
            _movementCommands = new ReadOnlyCollection<FuzzMovementCommand>(orderedMovementCommands);
            _attackCommands = new ReadOnlyCollection<FuzzAttackCommand>(orderedAttackCommands);

            EnsureUniqueMovementTicks();
            EnsureUniqueAttackTicks();
        }

        public int EntityId { get; }

        public IReadOnlyList<FuzzMovementCommand> MovementCommands => _movementCommands;

        public IReadOnlyList<FuzzAttackCommand> AttackCommands => _attackCommands;

        private void EnsureUniqueMovementTicks()
        {
            var previousTick = int.MinValue;

            for (var i = 0; i < _movementCommands.Count; i++)
            {
                var currentTick = _movementCommands[i].TickIndex;
                if (currentTick == previousTick)
                {
                    throw new InvalidOperationException(
                        $"Entity {EntityId} contains multiple movement commands for tick {currentTick}.");
                }

                previousTick = currentTick;
            }
        }

        private void EnsureUniqueAttackTicks()
        {
            var previousTick = int.MinValue;

            for (var i = 0; i < _attackCommands.Count; i++)
            {
                var currentTick = _attackCommands[i].TickIndex;
                if (currentTick == previousTick)
                {
                    throw new InvalidOperationException(
                        $"Entity {EntityId} contains multiple attack commands for tick {currentTick}.");
                }

                previousTick = currentTick;
            }
        }

        private sealed class FuzzMovementCommandComparer : IComparer<FuzzMovementCommand>
        {
            public static readonly FuzzMovementCommandComparer Instance = new();

            public int Compare(FuzzMovementCommand left, FuzzMovementCommand right)
            {
                var result = left.TickIndex.CompareTo(right.TickIndex);
                if (result != 0)
                {
                    return result;
                }

                result = right.Priority.CompareTo(left.Priority);
                if (result != 0)
                {
                    return result;
                }

                return ((int)left.Direction).CompareTo((int)right.Direction);
            }
        }

        private sealed class FuzzAttackCommandComparer : IComparer<FuzzAttackCommand>
        {
            public static readonly FuzzAttackCommandComparer Instance = new();

            public int Compare(FuzzAttackCommand left, FuzzAttackCommand right)
            {
                var result = left.TickIndex.CompareTo(right.TickIndex);
                if (result != 0)
                {
                    return result;
                }

                result = right.Priority.CompareTo(left.Priority);
                if (result != 0)
                {
                    return result;
                }

                return left.TargetId.CompareTo(right.TargetId);
            }
        }
    }

    internal readonly struct FuzzMovementCommand
    {
        public FuzzMovementCommand(int tickIndex, int priority, Direction direction)
        {
            if (direction == Direction.None)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), "Movement commands require a non-zero direction.");
            }

            TickIndex = tickIndex;
            Priority = priority;
            Direction = direction;
        }

        public int TickIndex { get; }

        public int Priority { get; }

        public Direction Direction { get; }
    }

    internal readonly struct FuzzAttackCommand
    {
        public FuzzAttackCommand(int tickIndex, int priority, int targetId)
        {
            if (targetId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetId), "Attack commands require a positive target id.");
            }

            TickIndex = tickIndex;
            Priority = priority;
            TargetId = targetId;
        }

        public int TickIndex { get; }

        public int Priority { get; }

        public int TargetId { get; }
    }
}
