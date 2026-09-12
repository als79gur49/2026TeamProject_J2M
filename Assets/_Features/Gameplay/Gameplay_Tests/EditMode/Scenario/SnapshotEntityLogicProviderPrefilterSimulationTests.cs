using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class SnapshotEntityLogicProviderPrefilterSimulationTests
    {
        [Test]
        [Category("Extended")]
        public void CandidateArrayProvider_MatchesLegacyFullScanTickOutputsOrderingAndGlideResolution()
        {
            var optimizedCalls = new List<string>();
            var legacyCalls = new List<string>();
            var optimizedDefault = new ResolverPrefilterFactory("default", optimizedCalls, EntityType.Unit);
            var optimizedCustom = new RecordingFactory("custom", optimizedCalls);
            var legacyDefault = new ResolverPrefilterFactory("default", legacyCalls, EntityType.Unit);
            var legacyCustom = new RecordingFactory("custom", legacyCalls);
            var optimized = new SnapshotEntityLogicProvider(new IEntityLogicFactory[] { optimizedDefault, optimizedCustom });
            var legacy = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                new LegacyResolverFactoryAdapter(legacyDefault),
                new LegacyFactoryAdapter(legacyCustom),
            });
            var entities = new[]
            {
                CreateEntity(10, 0, EntityType.None),
                CreateEntity(20, 1, EntityType.Unit),
                CreateEntity(30, 2, EntityType.Box),
            };

            var optimizedTick = new GameplayBootstrapper(optimized)
                .CreateTickPipeline(CreateWorld(entities))
                .RunTick(new TickInput(7));
            var legacyTick = new GameplayBootstrapper(legacy)
                .CreateTickPipeline(CreateWorld(entities))
                .RunTick(new TickInput(7));

            CollectionAssert.AreEqual(legacyTick.FinalEntities, optimizedTick.FinalEntities);
            CollectionAssert.AreEqual(legacyTick.EventLog, optimizedTick.EventLog);
            CollectionAssert.AreEqual(legacyTick.PhaseTrace, optimizedTick.PhaseTrace);
            Assert.That(optimizedTick.DeterminismHash, Is.EqualTo(legacyTick.DeterminismHash));
            Assert.That(optimizedTick.Trace.Text, Is.EqualTo(legacyTick.Trace.Text));
            CollectionAssert.AreEqual(
                legacyCalls.Where(call => call.StartsWith("custom.", StringComparison.Ordinal)),
                optimizedCalls.Where(call => call.StartsWith("custom.", StringComparison.Ordinal)));
            CollectionAssert.AreEqual(
                new[] { "default.Can:20:Unit", "default.Create:20:Unit" },
                optimizedCalls.Where(call => call.StartsWith("default.", StringComparison.Ordinal)));

            var resolverEntity = CreateEntity(99, 0, EntityType.Box);
            var resolverSnapshot = CreateWorld(resolverEntity).CreateSnapshot();
            Assert.That(optimized.TryResolveEnemyGlidePresentationSettings(resolverSnapshot, resolverEntity, out var optimizedSettings), Is.True);
            Assert.That(legacy.TryResolveEnemyGlidePresentationSettings(resolverSnapshot, resolverEntity, out var legacySettings), Is.True);
            Assert.That(optimizedSettings.LiftHeightUnits, Is.EqualTo(legacySettings.LiftHeightUnits));
            Assert.That(optimizedSettings.RecoveryDipHeightUnits, Is.EqualTo(legacySettings.RecoveryDipHeightUnits));
        }

        private static WorldState CreateWorld(params EntityState[] entities) =>
            GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(8, 4)),
                new CubeTopologyState(FaceId.Floor));

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

        private class RecordingFactory : IEntityLogicFactory
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public RecordingFactory(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public virtual bool CanCreate(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Can:{context.Entity.entityId}:{context.Entity.type}");
                return true;
            }

            public virtual IEntityLogic Create(in EntityLogicCreationContext context)
            {
                _calls.Add($"{_name}.Create:{context.Entity.entityId}:{context.Entity.type}");
                return new NoPhaseLogic();
            }
        }

        private sealed class ResolverPrefilterFactory : RecordingFactory, IEntityLogicFactoryEntityTypePrefilter, IEnemyGlidePresentationSettingsResolver
        {
            private readonly EntityType _supportedType;

            public ResolverPrefilterFactory(string name, List<string> calls, EntityType supportedType)
                : base(name, calls)
            {
                _supportedType = supportedType;
            }

            public bool MayCreateForEntityType(EntityType entityType) => entityType == _supportedType;

            public override bool CanCreate(in EntityLogicCreationContext context)
            {
                base.CanCreate(context);
                return context.Entity.type == _supportedType;
            }

            public override IEntityLogic Create(in EntityLogicCreationContext context)
            {
                base.Create(context);
                return new MovementLogic(context.Entity.entityId);
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
            public MovementLogic(int controlledEntityId) => ControlledEntityId = controlledEntityId;

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
