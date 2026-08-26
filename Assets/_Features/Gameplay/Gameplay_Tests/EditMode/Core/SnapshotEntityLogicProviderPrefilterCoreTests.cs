using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly EntityType UnknownType = (EntityType)int.MaxValue;

        [Test]
        [Category("Core")]
        public void CandidateCache_PreservesUnscopedFallbackAndFiltersOnlyAuditedKnownTypes()
        {
            var calls = new List<string>();
            var prefiltered = new RecordingPrefilterFactory("default", calls, EntityType.Unit);
            var custom = new RecordingFactory("custom", calls);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                prefiltered,
                custom,
            });

            provider.Build(
                CreateSnapshot(
                    CreateEntity(10, 0, EntityType.None),
                    CreateEntity(20, 1, EntityType.Unit),
                    CreateEntity(30, 2, EntityType.Box)),
                Array.Empty<IEntityLogic>());
            DispatchRawEntity(
                provider,
                CreateSnapshot(),
                CreateEntity(40, 3, UnknownType));

            CollectionAssert.AreEqual(
                new[]
                {
                    "custom.Can:10:None", "custom.Create:10:None",
                    "default.Can:20:Unit", "default.Create:20:Unit",
                    "custom.Can:20:Unit", "custom.Create:20:Unit",
                    "custom.Can:30:Box", "custom.Create:30:Box",
                    $"default.Can:40:{(int)UnknownType}",
                    $"custom.Can:40:{(int)UnknownType}", $"custom.Create:40:{(int)UnknownType}",
                },
                calls);
            CollectionAssert.AreEqual(
                new[] { EntityType.None, EntityType.Unit, EntityType.Box },
                prefiltered.AuditedTypes);
        }

        [Test]
        [Category("Core")]
        public void CandidateCache_IsPerProviderConstructorOnlyAndPreservesDuplicateRegistrations()
        {
            var calls = new List<string>();
            var duplicated = new RecordingPrefilterFactory("duplicate", calls, EntityType.Unit);
            GameplayTickWorkloadCounts counts;

            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
                {
                    duplicated,
                    duplicated,
                });
                provider.Build(CreateSnapshot(CreateEntity(10, 0, EntityType.Unit)), Array.Empty<IEntityLogic>());
                provider.Build(CreateSnapshot(CreateEntity(20, 1, EntityType.Unit)), Array.Empty<IEntityLogic>());
                counts = capture.Counts;
            }

            Assert.That(counts.EntityLogicCandidateCacheConstructionCount, Is.EqualTo(1));
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
        public void CandidateCache_PreservesEntityMajorFactoryOrderAndPhaseOwnershipPrecedence()
        {
            var calls = new List<string>();
            var first = new MovementPrefilterFactory("first", calls, EntityType.Unit);
            var second = new MovementPrefilterFactory("second", calls, EntityType.Unit);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { first, second });
            var snapshot = CreateSnapshot(
                CreateEntity(10, 0, EntityType.Unit),
                CreateEntity(20, 1, EntityType.Unit));

            var dynamicOnly = provider.Build(snapshot, Array.Empty<IEntityLogic>());
            CollectionAssert.AreEqual(
                new[] { "first.Can:10", "first.Create:10", "second.Can:10", "second.Create:10", "first.Can:20", "first.Create:20", "second.Can:20", "second.Create:20" },
                calls);
            CollectionAssert.AreEqual(new[] { "first-10", "first-20" }, Labels(dynamicOnly.MovementLogics));

            var staticOwner = new MovementLogic("static-10", 10);
            var withStatic = provider.Build(snapshot, new IEntityLogic[] { staticOwner });
            CollectionAssert.AreEqual(new[] { "static-10", "first-20" }, Labels(withStatic.MovementLogics));
        }

        [Test]
        [Category("Core")]
        public void CandidateCache_UsesFreshSnapshotsForWallSpawnRemovalAndSameIdTypeReuse()
        {
            var calls = new List<string>();
            var custom = new RecordingFactory("custom", calls);
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { custom });
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
                new[] { "custom.Can:10:None", "custom.Create:10:None", "custom.Can:10:Unit", "custom.Create:10:Unit", "custom.Can:10:Box", "custom.Create:10:Box" },
                calls);
        }

        [Test]
        [Category("Core")]
        public void DefaultFactoryPrefilters_AreConservativeUnitOrBoxSupersets()
        {
            var definition = CreateEnemyDefinition();
            var factories = new IEntityLogicFactory[]
            {
                new EnemyEntityLogicFactory(definition),
                new EnemyActionStateEntityLogicFactory(definition),
                new EnemyCombatEntityLogicFactory(definition),
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

            var entities = new[]
            {
                CreateEntity(10, 0, EntityType.Unit, EnemyAiMode.Patrol),
                CreateEntity(20, 1, EntityType.Box, boxCapabilities: BoxCapabilities.Push),
            };
            var snapshot = CreateSnapshot(entities);
            foreach (var factory in factories)
            {
                foreach (var entity in entities)
                {
                    var context = new EntityLogicCreationContext(snapshot, entity);
                    if (factory.CanCreate(context))
                    {
                        Assert.That(
                            ((IEntityLogicFactoryEntityTypePrefilter)factory).MayCreateForEntityType(entity.type),
                            Is.True,
                            factory.GetType().Name);
                    }
                }
            }
        }

        [Test]
        [Category("Core")]
        public void OptimizedProvider_MatchesResolverCapableLegacyFullScanOutputsAndSequences()
        {
            var optimizedCalls = new List<string>();
            var legacyCalls = new List<string>();
            var optimizedDefault = new ResolverPrefilterFactory("default", optimizedCalls, EntityType.Unit);
            var optimizedCustom = new RecordingFactory("custom", optimizedCalls);
            var legacyDefaultInner = new ResolverPrefilterFactory("default", legacyCalls, EntityType.Unit);
            var legacyCustomInner = new RecordingFactory("custom", legacyCalls);
            var optimized = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { optimizedDefault, optimizedCustom });
            var legacy = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new LegacyResolverFactoryAdapter(legacyDefaultInner),
                new LegacyFactoryAdapter(legacyCustomInner),
            });
            var entities = new[]
            {
                CreateEntity(10, 0, EntityType.None),
                CreateEntity(20, 1, EntityType.Unit),
                CreateEntity(30, 2, EntityType.Box),
            };
            var optimizedSet = optimized.Build(CreateSnapshot(entities), Array.Empty<IEntityLogic>());
            var legacySet = legacy.Build(CreateSnapshot(entities), Array.Empty<IEntityLogic>());
            DispatchRawEntity(optimized, CreateSnapshot(), CreateEntity(40, 3, UnknownType));
            DispatchRawEntity(legacy, CreateSnapshot(), CreateEntity(40, 3, UnknownType));

            CollectionAssert.AreEqual(Labels(legacySet.MovementLogics), Labels(optimizedSet.MovementLogics));
            CollectionAssert.AreEqual(
                legacyCalls.Where(call => call.StartsWith("custom.", StringComparison.Ordinal)),
                optimizedCalls.Where(call => call.StartsWith("custom.", StringComparison.Ordinal)));
            CollectionAssert.AreEqual(
                new[] { "default.Can:20:Unit", "default.Create:20:Unit", $"default.Can:40:{(int)UnknownType}" },
                optimizedCalls.Where(call => call.StartsWith("default.", StringComparison.Ordinal)));

            var resolverEntity = CreateEntity(99, 0, EntityType.Box);
            Assert.That(optimized.TryResolveEnemyGlidePresentationSettings(CreateSnapshot(resolverEntity), resolverEntity, out var optimizedSettings), Is.True);
            Assert.That(legacy.TryResolveEnemyGlidePresentationSettings(CreateSnapshot(resolverEntity), resolverEntity, out var legacySettings), Is.True);
            Assert.That(optimizedSettings.LiftHeightUnits, Is.EqualTo(legacySettings.LiftHeightUnits));
            Assert.That(optimizedSettings.RecoveryDipHeightUnits, Is.EqualTo(legacySettings.RecoveryDipHeightUnits));

            var optimizedTick = new GameplayBootstrapper(optimized).CreateTickPipeline(CreateWorld(entities)).RunTick(new TickInput(7));
            var legacyTick = new GameplayBootstrapper(legacy).CreateTickPipeline(CreateWorld(entities)).RunTick(new TickInput(7));
            CollectionAssert.AreEqual(legacyTick.FinalEntities, optimizedTick.FinalEntities);
            CollectionAssert.AreEqual(legacyTick.EventLog, optimizedTick.EventLog);
            Assert.That(optimizedTick.DeterminismHash, Is.EqualTo(legacyTick.DeterminismHash));
            Assert.That(optimizedTick.Trace.Text, Is.EqualTo(legacyTick.Trace.Text));
        }

        [Test]
        [Category("Core")]
        public void CandidateCache_DiagnosticsAndSourceGuardPinConstructorOnlyArchitecture()
        {
            var custom = new RecordingFactory("custom", new List<string>());
            GameplayTickWorkloadCounts counts;
            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
                {
                    new EnemyEntityLogicFactory(),
                    new EnemyActionStateEntityLogicFactory(),
                    new EnemyCombatEntityLogicFactory(),
                    new SlidingBoxEntityLogicFactory(),
                    custom,
                });
                provider.Build(
                    CreateSnapshot(
                        CreateEntity(10, 0, EntityType.None),
                        CreateEntity(20, 1, EntityType.Unit),
                        CreateEntity(30, 2, EntityType.Box)),
                    Array.Empty<IEntityLogic>());
                counts = capture.Counts;
            }

            Assert.That(counts.EntityLogicCandidateCacheConstructionCount, Is.EqualTo(1));
            Assert.That(counts.EntityLogicBuildMetrics.FactoryOpportunityCount, Is.EqualTo(15));
            Assert.That(counts.EntityLogicBuildMetrics.PrefilterSkipCount, Is.EqualTo(8));
            Assert.That(counts.EntityLogicBuildMetrics.CanCreateProbeCount, Is.EqualTo(7));
            Assert.That(counts.EntityLogicBuildMetrics.None.CanCreateProbeCount, Is.EqualTo(1));
            Assert.That(counts.EntityLogicBuildMetrics.None.PrefilterSkipCount, Is.EqualTo(4));
            Assert.That(custom.CanCreateCount, Is.EqualTo(3));

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            var source = File.ReadAllText(Path.Combine(
                projectRoot,
                "Assets/_Features/Gameplay/Gameplay_Entities/Runtime/SnapshotEntityLogicProvider.cs"));
            Assert.That(CountOccurrences(source, "BuildEntityTypeCandidateCache("), Is.EqualTo(2));
            var buildStart = source.IndexOf("public EntityLogicSet Build(", StringComparison.Ordinal);
            var candidateCacheStart = source.IndexOf("private void BuildEntityTypeCandidateCache(", StringComparison.Ordinal);
            var buildSource = source.Substring(buildStart, candidateCacheStart - buildStart);
            Assert.That(buildSource, Does.Contain("ResolveCandidateFactories("));
            Assert.That(buildSource, Does.Not.Contain("new List<IEntityLogicFactory>"));
        }

        private static WorldState CreateWorld(params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(8, 4)),
                new CubeTopologyState(FaceId.Floor));
        }

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities) => CreateWorld(entities).CreateSnapshot();

        private static EntityState CreateEntity(
            int entityId,
            int x,
            EntityType type,
            EnemyAiMode aiMode = EnemyAiMode.None,
            BoxCapabilities boxCapabilities = BoxCapabilities.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = type,
                unitRole = UnitRole.Enemy,
                aiMode = aiMode,
                boxCapabilities = boxCapabilities,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }

        private static EnemyAiRuntimeDefinition CreateEnemyDefinition()
        {
            return new EnemyAiRuntimeDefinition(
                new EnemyCoreRuntime(
                    new EnemyAiCommonSettings(50, 50, 1),
                    new EnemyLocomotionTimingSettings(0)),
                new EnemyBrainRuntime(
                    new EnemyStateResolverRuntime(EnemyAiStateResolverKind.Default, DefaultEnemyAiStateResolver.Instance),
                    new EnemyPatrolRuntime(PatrolStrategyKind.Forward, PatrolSettings.CreateDefault(), ForwardPatrolStrategy.Instance),
                    new EnemyDetectionRuntime(DetectionStrategyKind.NearestOpponent, DetectionSettings.CreateStandardEnemyDetection(), NearestOpponentDetectionStrategy.Instance),
                    new EnemyChaseRuntime(ChaseStrategyKind.AxisPriority, ChaseSettings.CreateDefault(), AxisPriorityChaseStrategy.Instance)),
                new EnemyCapabilityRuntimeSet(null, null, null, null));
        }

        private static string[] Labels(IEnumerable<IMovementEntityLogic> logics) =>
            logics.Cast<MovementLogic>().Select(logic => logic.Label).ToArray();

        private static void DispatchRawEntity(
            SnapshotEntityLogicProvider provider,
            WorldSnapshot snapshot,
            EntityState entity)
        {
            var resolver = typeof(SnapshotEntityLogicProvider).GetMethod(
                "ResolveCandidateFactories",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(resolver, Is.Not.Null);
            var factories = (IReadOnlyList<IEntityLogicFactory>)resolver.Invoke(
                provider,
                new object[] { entity.type });
            var context = new EntityLogicCreationContext(snapshot, entity);
            for (var i = 0; i < factories.Count; i++)
            {
                if (factories[i].CanCreate(context))
                {
                    factories[i].Create(context);
                }
            }
        }

        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            for (var index = 0; (index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
            {
                count++;
            }

            return count;
        }

        private sealed class RecordingFactory : IEntityLogicFactory
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public RecordingFactory(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public int CanCreateCount { get; private set; }

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                CanCreateCount++;
                _calls.Add($"{_name}.Can:{context.Entity.entityId}:{FormatType(context.Entity.type)}");
                return true;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}:{FormatType(context.Entity.type)}");
                return new NoPhaseLogic();
            }
        }

        private class RecordingPrefilterFactory : IEntityLogicFactory, IEntityLogicFactoryEntityTypePrefilter
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly EntityType _supportedType;

            public RecordingPrefilterFactory(string name, List<string> calls, EntityType supportedType)
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

            public virtual bool CanCreate(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Can:{context.Entity.entityId}:{FormatType(context.Entity.type)}");
                return context.Entity.type == _supportedType;
            }

            public virtual IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}:{FormatType(context.Entity.type)}");
                return new NoPhaseLogic();
            }
        }

        private sealed class ResolverPrefilterFactory : RecordingPrefilterFactory, IEnemyGlidePresentationSettingsResolver
        {
            public ResolverPrefilterFactory(string name, List<string> calls, EntityType supportedType)
                : base(name, calls, supportedType)
            {
            }

            public override IEntityLogic Create(in EntityLogicCreationContext context)
            {
                base.Create(context);
                return new MovementLogic($"default-{context.Entity.entityId}", context.Entity.entityId);
            }

            public bool TryResolveEnemyGlidePresentationSettings(
                WorldSnapshot snapshot,
                in EntityState entity,
                out EnemyGlidePresentationSettings settings)
            {
                settings = new EnemyGlidePresentationSettings(123, 45);
                return true;
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

            public bool MayCreateForEntityType(EntityType entityType) => entityType == _supportedType;

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Can:{context.Entity.entityId}");
                return context.Entity.type == _supportedType;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}");
                return new MovementLogic($"{_name}-{context.Entity.entityId}", context.Entity.entityId);
            }
        }

        private class LegacyFactoryAdapter : IEntityLogicFactory
        {
            private readonly IEntityLogicFactory _inner;

            public LegacyFactoryAdapter(IEntityLogicFactory inner) => _inner = inner;

            public bool CanCreate(in EntityLogicCreationContext context) => _inner.CanCreate(context);

            public IEntityLogic Create(in EntityLogicCreationContext context) => _inner.Create(context);
        }

        private sealed class LegacyResolverFactoryAdapter : LegacyFactoryAdapter, IEnemyGlidePresentationSettingsResolver
        {
            private readonly IEnemyGlidePresentationSettingsResolver _resolver;

            public LegacyResolverFactoryAdapter(ResolverPrefilterFactory inner)
                : base(inner)
            {
                _resolver = inner;
            }

            public bool TryResolveEnemyGlidePresentationSettings(
                WorldSnapshot snapshot,
                in EntityState entity,
                out EnemyGlidePresentationSettings settings) =>
                _resolver.TryResolveEnemyGlidePresentationSettings(snapshot, entity, out settings);
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

        private static string FormatType(EntityType type) =>
            type == EntityType.None || type == EntityType.Unit || type == EntityType.Box
                ? type.ToString()
                : ((int)type).ToString();
    }
}
