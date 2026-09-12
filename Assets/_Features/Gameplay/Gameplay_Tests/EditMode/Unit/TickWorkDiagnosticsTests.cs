using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickWorkDiagnosticsTests
    {
        [Test]
        [Category("Extended")]
        public void ProviderBuild_BaselineMetricsMatchActualFullScanCallsByEntityType()
        {
            var firstFactory = new RecordingFactory();
            var secondFactory = new RecordingFactory();
            var provider = new SnapshotEntityLogicProvider(new IEntityLogicFactory[]
            {
                firstFactory,
                secondFactory,
            });

            GameplayTickWorkloadCounts counts;
            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                provider.Build(
                    CreateSnapshot(
                        CreateEntity(10, 0, EntityType.None),
                        CreateEntity(20, 1, EntityType.Unit),
                        CreateEntity(30, 2, EntityType.Box)),
                    Array.Empty<IEntityLogic>());
                counts = capture.Counts;
            }

            Assert.That(firstFactory.CanCreateCount, Is.EqualTo(3));
            Assert.That(secondFactory.CanCreateCount, Is.EqualTo(3));
            Assert.That(firstFactory.CreateCount, Is.EqualTo(3));
            Assert.That(secondFactory.CreateCount, Is.EqualTo(3));

            var metrics = counts.EntityLogicBuildMetrics;
            Assert.That(counts.EntityLogicProviderBuildCount, Is.EqualTo(1));
            Assert.That(metrics.EntityVisitedCount, Is.EqualTo(3));
            Assert.That(metrics.RegisteredFactoryCount, Is.EqualTo(2));
            Assert.That(metrics.FactoryOpportunityCount, Is.EqualTo(6));
            Assert.That(metrics.PrefilterSkipCount, Is.Zero);
            Assert.That(metrics.CanCreateProbeCount, Is.EqualTo(6));
            Assert.That(metrics.CreatedLogicCount, Is.EqualTo(6));
            Assert.That(metrics.AcceptedLogicCount, Is.EqualTo(6));
            Assert.That(metrics.ConflictRejectedLogicCount, Is.Zero);
            AssertTypeMetrics(metrics.None);
            AssertTypeMetrics(metrics.Unit);
            AssertTypeMetrics(metrics.Box);
            Assert.That(metrics.Unknown.EntityVisitedCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ProviderBuild_CandidateArraysRecordConstructorCacheAndExactKnownTypeSkips()
        {
            var custom = new RecordingFactory();
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

            var metrics = counts.EntityLogicBuildMetrics;
            Assert.That(counts.EntityLogicCandidateCacheConstructionCount, Is.EqualTo(1));
            Assert.That(metrics.FactoryOpportunityCount, Is.EqualTo(15));
            Assert.That(metrics.PrefilterSkipCount, Is.EqualTo(8));
            Assert.That(metrics.CanCreateProbeCount, Is.EqualTo(7));
            Assert.That(metrics.None.PrefilterSkipCount, Is.EqualTo(4));
            Assert.That(metrics.None.CanCreateProbeCount, Is.EqualTo(1));
            Assert.That(custom.CanCreateCount, Is.EqualTo(3));
        }

        private static void AssertTypeMetrics(EntityLogicTypeMetrics metrics)
        {
            Assert.That(metrics.EntityVisitedCount, Is.EqualTo(1));
            Assert.That(metrics.FactoryOpportunityCount, Is.EqualTo(2));
            Assert.That(metrics.PrefilterSkipCount, Is.Zero);
            Assert.That(metrics.CanCreateProbeCount, Is.EqualTo(2));
            Assert.That(metrics.CreatedLogicCount, Is.EqualTo(2));
            Assert.That(metrics.AcceptedLogicCount, Is.EqualTo(2));
            Assert.That(metrics.ConflictRejectedLogicCount, Is.Zero);
        }

        private static WorldSnapshot CreateSnapshot(params EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                    entities,
                    new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(6, 4)),
                    new CubeTopologyState(FaceId.Floor))
                .CreateSnapshot();
        }

        private static EntityState CreateEntity(int entityId, int x, EntityType type)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, 0),
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = type,
                boardPresence = EntityBoardPresence.Occupying,
                facing = Direction.Right,
            };
        }

        private sealed class RecordingFactory : IEntityLogicFactory
        {
            public int CanCreateCount { get; private set; }
            public int CreateCount { get; private set; }

            public bool CanCreate(in EntityLogicCreationContext context)
            {
                CanCreateCount++;
                return true;
            }

            public IEntityLogic Create(in EntityLogicCreationContext context)
            {
                CreateCount++;
                return new NoPhaseLogic();
            }
        }

        private sealed class NoPhaseLogic : IEntityLogic
        {
        }
    }
}
