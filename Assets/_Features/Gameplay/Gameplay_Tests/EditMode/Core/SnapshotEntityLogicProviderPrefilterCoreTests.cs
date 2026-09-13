using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class SnapshotEntityLogicProviderPrefilterCoreTests
    {
        [Test]
        [Category("Core")]
        public void SnapshotEntityLogicProvider_ProposedWall_UsesKnownWallCandidateSet()
        {
            var calls = new List<string>();
            var proposedWallType = (EntityType)4;
            var proposedWallTypeLabel = proposedWallType.ToString();
            var wall = new MovementPrefilterFactory("wall", calls, proposedWallType);
            var custom = new RecordingFactory("custom", calls);
            var unit = new MovementPrefilterFactory("unit", calls, EntityType.Unit);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { wall, custom, unit });

            provider.Build(
                CreateRawSnapshot(CreateEntity(40, 0, proposedWallType)),
                Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(
                new[]
                {
                    $"wall.Can:40:{proposedWallTypeLabel}",
                    $"wall.Create:40:{proposedWallTypeLabel}",
                    $"custom.Can:40:{proposedWallTypeLabel}",
                    $"custom.Create:40:{proposedWallTypeLabel}",
                },
                calls,
                "Proposed Wall must use its conservative known-type candidate set while preserving unscoped factory order.");
        }

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
                    (EntityType)4, (EntityType)4,
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

        [Test]
        [Category("Core")]
        public void Build_UnknownEntityTypeUsesFullRegistrationArrayInOriginalOrder()
        {
            var calls = new List<string>();
            var unit = new MovementPrefilterFactory("unit", calls, EntityType.Unit);
            var custom = new RecordingFactory("custom", calls);
            var box = new MovementPrefilterFactory("box", calls, EntityType.Box);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { unit, custom, box });
            var unknown = CreateEntity(99, 0, (EntityType)int.MaxValue);

            GameplayTickWorkloadCounts counts;
            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                provider.Build(CreateRawSnapshot(unknown), Array.Empty<IEntityLogic>());
                counts = capture.Counts;
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    "unit.Can:99:2147483647",
                    "custom.Can:99:2147483647",
                    "custom.Create:99:2147483647",
                    "box.Can:99:2147483647",
                },
                calls);
            Assert.That(counts.EntityLogicBuildMetrics.Unknown.EntityVisitedCount, Is.EqualTo(1));
            Assert.That(counts.EntityLogicBuildMetrics.Unknown.FactoryOpportunityCount, Is.EqualTo(3));
            Assert.That(counts.EntityLogicBuildMetrics.Unknown.PrefilterSkipCount, Is.Zero);
            Assert.That(counts.EntityLogicBuildMetrics.Unknown.CanCreateProbeCount, Is.EqualTo(3));
        }

        [Test]
        [Category("Core")]
        public void CandidateArrays_MatchLegacyObservableLogicSetAndFirstPhaseOwners()
        {
            var optimizedCalls = new List<string>();
            var legacyCalls = new List<string>();
            var optimized = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new MovementPrefilterFactory("first", optimizedCalls, EntityType.Unit),
                new UnscopedMovementFactory("custom", optimizedCalls),
                new MovementPrefilterFactory("second", optimizedCalls, EntityType.Unit),
            });
            var legacy = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new LegacyFactoryAdapter(new MovementPrefilterFactory("first", legacyCalls, EntityType.Unit)),
                new UnscopedMovementFactory("custom", legacyCalls),
                new LegacyFactoryAdapter(new MovementPrefilterFactory("second", legacyCalls, EntityType.Unit)),
            });
            var snapshot = CreateRawSnapshot(
                CreateEntity(10, 0, EntityType.None),
                CreateEntity(20, 1, EntityType.Unit),
                CreateEntity(30, 2, EntityType.Box));

            var optimizedSet = optimized.Build(snapshot, Array.Empty<IEntityLogic>());
            var legacySet = legacy.Build(snapshot, Array.Empty<IEntityLogic>());

            CollectionAssert.AreEqual(Labels(legacySet.MovementLogics), Labels(optimizedSet.MovementLogics));
            CollectionAssert.AreEqual(OwnerIds(legacySet.MovementLogics), OwnerIds(optimizedSet.MovementLogics));
            CollectionAssert.AreEqual(
                new[] { "custom-10", "first-20", "custom-30" },
                Labels(optimizedSet.MovementLogics));
            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, OwnerIds(optimizedSet.MovementLogics));
            Assert.That(optimizedSet.PreMovementStateLogics.Count, Is.EqualTo(legacySet.PreMovementStateLogics.Count));
            Assert.That(optimizedSet.AiStateLogics.Count, Is.EqualTo(legacySet.AiStateLogics.Count));
            Assert.That(optimizedSet.EnemyActionStateLogics.Count, Is.EqualTo(legacySet.EnemyActionStateLogics.Count));
            Assert.That(optimizedSet.AttackLogics.Count, Is.EqualTo(legacySet.AttackLogics.Count));
        }

        [Test]
        [Category("Core")]
        public void GlideResolver_FirstFailureFallsThroughAndFirstSuccessStopsRegistrationOrder()
        {
            var calls = new List<string>();
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new GlideResolverFactory("first", calls, succeeds: false, new EnemyGlidePresentationSettings(1, 2)),
                new GlideResolverFactory("second", calls, succeeds: true, new EnemyGlidePresentationSettings(3, 4)),
                new GlideResolverFactory("third", calls, succeeds: true, new EnemyGlidePresentationSettings(5, 6)),
            });
            var entity = CreateEntity(20, 1, EntityType.Unit);
            var snapshot = CreateRawSnapshot(entity);

            var resolved = provider.TryResolveEnemyGlidePresentationSettings(snapshot, entity, out var settings);

            Assert.That(resolved, Is.True);
            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.That(settings.LiftHeightUnits, Is.EqualTo(3));
            Assert.That(settings.RecoveryDipHeightUnits, Is.EqualTo(4));
        }

        private static WorldState CreateWorld(params EntityState[] entities) =>
            GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(8, 4)),
                new CubeTopologyState(FaceId.Floor));

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities) => CreateWorld(entities).CreateSnapshot();

        private static WorldSnapshot CreateRawSnapshot(params EntityState[] entities)
        {
            return new WorldSnapshot(
                entities.ToDictionary(entity => entity.entityId),
                new Dictionary<SurfaceCell, SortedSet<int>>(),
                new Dictionary<SurfaceCell, int>(),
                new Dictionary<int, TileFeatureState>(),
                new Dictionary<SurfaceCell, SortedSet<int>>(),
                new Dictionary<int, EnemyActionRuntimeState>(),
                new Dictionary<int, PendingCellImpact>(),
                new Dictionary<int, PendingEnemyBlockedReaction>(),
                new Dictionary<int, EnemyPatrolRuntimeState>(),
                new Dictionary<int, EnemyChargeRuntimeState>(),
                new Dictionary<int, EntityExecutionLockState>(),
                new Dictionary<int, EnemyJumpRuntimeState>(),
                new Dictionary<int, EnemyGlideRuntimeState>(),
                new Dictionary<int, EnemyUtilityRuntimeState>(),
                new Dictionary<int, EnemySummonBehaviorRuntimeState>(),
                new Dictionary<int, BoxInteractionLockState>(),
                new Dictionary<int, EnemyGravityFieldAuraFieldState>(),
                new Dictionary<int, PhasedRuntimeState>(),
                new Dictionary<int, PlayerDamageState>(),
                new Dictionary<int, PlayerControlState>(),
                new Dictionary<int, SummonedEntityState>(),
                new Dictionary<int, EnemyDefinitionBindingState>(),
                new Dictionary<int, UnitKinematicRuntimeState>(),
                new Dictionary<int, UnitContinuousLocomotionState>(),
                new CubeTopologyState(FaceId.Floor),
                0,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(8, 4)));
        }

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

        private static int[] OwnerIds(IEnumerable<IMovementEntityLogic> logics) =>
            logics.Cast<IEntityLogicSourceBinding>().Select(logic => logic.ControlledEntityId).ToArray();

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

        private sealed class UnscopedMovementFactory : IEntityLogicFactory
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public UnscopedMovementFactory(string name, List<string> calls)
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
                return new MovementLogic($"{_name}-{context.Entity.entityId}", context.Entity.entityId);
            }
        }

        private sealed class LegacyFactoryAdapter : IEntityLogicFactory
        {
            private readonly IEntityLogicFactory _inner;

            public LegacyFactoryAdapter(IEntityLogicFactory inner) => _inner = inner;

            public bool CanCreate(in EntityLogicCreationContext context) => _inner.CanCreate(context);

            public IEntityLogic Create(in EntityLogicCreationContext context) => _inner.Create(context);
        }

        private sealed class GlideResolverFactory : IEntityLogicFactory, IEnemyGlidePresentationSettingsResolver
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly bool _succeeds;
            private readonly EnemyGlidePresentationSettings _settings;

            public GlideResolverFactory(
                string name,
                List<string> calls,
                bool succeeds,
                EnemyGlidePresentationSettings settings)
            {
                _name = name;
                _calls = calls;
                _succeeds = succeeds;
                _settings = settings;
            }

            public bool CanCreate(in EntityLogicCreationContext context) => false;

            public IEntityLogic Create(in EntityLogicCreationContext context) => throw new InvalidOperationException();

            public bool TryResolveEnemyGlidePresentationSettings(
                WorldSnapshot snapshot,
                in EntityState entity,
                out EnemyGlidePresentationSettings settings)
            {
                _calls.Add(_name);
                settings = _settings;
                return _succeeds;
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
