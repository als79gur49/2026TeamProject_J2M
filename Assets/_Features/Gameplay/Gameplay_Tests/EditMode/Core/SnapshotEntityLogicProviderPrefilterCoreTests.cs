using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class SnapshotEntityLogicProviderPrefilterCoreTests
    {
        [Test]
        [Category("Core")]
        public void CandidateArrays_PreserveUnscopedFallbackEntityMajorOrderAndPhasePrecedence()
        {
            var calls = new List<string>();
            var first = new MovementPrefilterFactory("first", calls, EntityType.Unit);
            var second = new MovementPrefilterFactory("second", calls, EntityType.Unit);
            var custom = new RecordingFactory("custom", calls);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { first, custom, second });
            var snapshot = CreateSnapshot(
                CreateEntity(10, 0, EntityType.None),
                CreateEntity(20, 1, EntityType.Unit),
                CreateEntity(30, 2, EntityType.Box));

            var dynamicOnly = provider.Build(snapshot, Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[]
                {
                    "custom.Can:10:None", "custom.Create:10:None",
                    "first.Can:20:Unit", "first.Create:20:Unit",
                    "custom.Can:20:Unit", "custom.Create:20:Unit",
                    "second.Can:20:Unit", "second.Create:20:Unit",
                    "custom.Can:30:Box", "custom.Create:30:Box",
                },
                calls);
            CollectionAssert.AreEqual(new[] { "first-20" }, Labels(dynamicOnly.MovementLogics));

            var withStatic = provider.Build(snapshot, new IEntityLogic[] { new MovementLogic("static-20", 20) });
            CollectionAssert.AreEqual(new[] { "static-20" }, Labels(withStatic.MovementLogics));
        }

        [Test]
        [Category("Core")]
        public void CandidateArrays_AreConstructorOnlyPerProviderAndPreserveDuplicateRegistrations()
        {
            var calls = new List<string>();
            var duplicated = new MovementPrefilterFactory("duplicate", calls, EntityType.Unit);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { duplicated, duplicated });

            provider.Build(CreateSnapshot(CreateEntity(10, 0, EntityType.Unit)), Array.Empty<IEntityLogic>());
            provider.Build(CreateSnapshot(CreateEntity(20, 1, EntityType.Unit)), Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[]
                {
                    EntityType.None, EntityType.None,
                    EntityType.Unit, EntityType.Unit,
                    EntityType.Box, EntityType.Box,
                },
                duplicated.AuditedTypes);
            Assert.That(calls.Count(call => call.Contains(".Can:")), Is.EqualTo(4));
            Assert.That(calls.Count(call => call.Contains(".Create:")), Is.EqualTo(4));
        }

        [Test]
        [Category("Core")]
        public void CandidateArrays_UseCurrentEntityTypeAcrossFreshSnapshotsAndSameIdReuse()
        {
            var calls = new List<string>();
            var unit = new MovementPrefilterFactory("unit", calls, EntityType.Unit);
            var box = new MovementPrefilterFactory("box", calls, EntityType.Box);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { unit, box });
            var world = CreateWorld(CreateEntity(10, 0, EntityType.None));

            provider.Build(world.CreateSnapshot(), Array.Empty<IEntityLogic>());
            var write = world.CreateWriteContext();
            write.RemoveEntity(10);
            write.SpawnEntity(CreateEntity(10, 0, EntityType.Unit));
            provider.Build(world.CreateSnapshot(), Array.Empty<IEntityLogic>());
            write.RemoveEntity(10);
            write.SpawnEntity(CreateEntity(10, 0, EntityType.Box));
            provider.Build(world.CreateSnapshot(), Array.Empty<IEntityLogic>());
            write.RemoveEntity(10);
            provider.Build(world.CreateSnapshot(), Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[] { "unit.Can:10:Unit", "unit.Create:10:Unit", "box.Can:10:Box", "box.Create:10:Box" },
                calls);
        }

        [Test]
        [Category("Core")]
        public void DefaultFactoryPrefilters_AreConservativeUnitOrBoxSupersets()
        {
            var factories = new IEntityLogicFactory[]
            {
                new EnemyEntityLogicFactory(),
                new EnemyActionStateEntityLogicFactory(),
                new EnemyCombatEntityLogicFactory(),
                new SlidingBoxEntityLogicFactory(),
            };

            for (var i = 0; i < factories.Length; i++)
            {
                Assert.That(factories[i], Is.InstanceOf<IEntityLogicFactoryEntityTypePrefilter>());
                var prefilter = (IEntityLogicFactoryEntityTypePrefilter)factories[i];
                var expectedType = i == 3 ? EntityType.Box : EntityType.Unit;
                Assert.That(prefilter.MayCreateForEntityType(EntityType.None), Is.False);
                Assert.That(prefilter.MayCreateForEntityType(EntityType.Unit), Is.EqualTo(expectedType == EntityType.Unit));
                Assert.That(prefilter.MayCreateForEntityType(EntityType.Box), Is.EqualTo(expectedType == EntityType.Box));
            }

            var box = CreateEntity(20, 1, EntityType.Box);
            box.boxCapabilities = BoxCapabilities.Push;
            var snapshot = CreateSnapshot(box);
            var context = new EntityLogicCreationContext(snapshot, box);
            Assert.That(factories[3].CanCreate(context), Is.True);
            Assert.That(((IEntityLogicFactoryEntityTypePrefilter)factories[3]).MayCreateForEntityType(box.type), Is.True);
        }

        private static WorldState CreateWorld(params EntityState[] entities) =>
            GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(8, 4)),
                new CubeTopologyState(FaceId.Floor));

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities) => CreateWorld(entities).CreateSnapshot();

        private static EntityState CreateEntity(int entityId, int x, EntityType type)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = type,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }

        private static string[] Labels(IEnumerable<IMovementEntityLogic> logics) =>
            logics.Cast<MovementLogic>().Select(logic => logic.Label).ToArray();

        private sealed class RecordingFactory : IEntityLogicFactory
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public RecordingFactory(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Can:{context.Entity.entityId}:{context.Entity.type}");
                return true;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}:{context.Entity.type}");
                return new NoPhaseLogic();
            }
        }

        private sealed class MovementPrefilterFactory : IEntityLogicFactory, IEntityLogicFactoryEntityTypePrefilter
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly EntityType _supportedType;

            public MovementPrefilterFactory(string name, List<string> calls, EntityType supportedType)
            {
                _name = name;
                _calls = calls;
                _supportedType = supportedType;
            }

            public List<EntityType> AuditedTypes { get; } = new();

            public bool MayCreateForEntityType(EntityType entityType)
            {
                AuditedTypes.Add(entityType);
                return entityType == _supportedType;
            }

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Can:{context.Entity.entityId}:{context.Entity.type}");
                return context.Entity.type == _supportedType;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}:{context.Entity.type}");
                return new MovementLogic($"{_name}-{context.Entity.entityId}", context.Entity.entityId);
            }
        }

        private sealed class MovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            public MovementLogic(string label, int controlledEntityId)
            {
                Label = label;
                ControlledEntityId = controlledEntityId;
            }

            public string Label { get; }
            public int ControlledEntityId { get; }

            public void CollectMovementIntents(WorldSnapshot snapshot, in TickInput input, List<RawMovementIntent> buffer)
            {
            }
        }

        private sealed class NoPhaseLogic : IEntityLogic
        {
        }
    }
}
