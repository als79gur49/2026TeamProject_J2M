using System;
using System.Collections.Generic;
using System.Linq;
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
        [Category("Core")]
        public void GameplayTickWorkloadDiagnostics_ProposedWall_DoesNotUseUnknownBucket()
        {
            var accumulator = new EntityLogicBuildMetricsAccumulator(registeredFactoryCount: 3);
            accumulator.RecordEntityVisited((EntityType)4);
            accumulator.RecordPrefilterSkip((EntityType)4);
            accumulator.RecordCanCreateProbe((EntityType)4);
            var metrics = accumulator.Build();

            Assert.That(metrics.EntityVisitedCount, Is.EqualTo(1));
            Assert.That(metrics.FactoryOpportunityCount, Is.EqualTo(3));
            Assert.That(metrics.PrefilterSkipCount, Is.EqualTo(1));
            Assert.That(metrics.CanCreateProbeCount, Is.EqualTo(1));
            Assert.That(
                metrics.Unknown.EntityVisitedCount,
                Is.Zero,
                "Proposed Wall diagnostics must not be attributed to the Unknown bucket.");
        }

        [Test]
        [Category("Core")]
        public void GameplayTickWorkloadDiagnostics_WallBucketBalancesAggregate()
        {
            var accumulator = new EntityLogicBuildMetricsAccumulator(registeredFactoryCount: 3);
            var entityTypes = new[]
            {
                EntityType.None,
                EntityType.Unit,
                EntityType.Box,
                (EntityType)4,
                (EntityType)99,
            };
            for (var i = 0; i < entityTypes.Length; i++)
            {
                accumulator.RecordEntityVisited(entityTypes[i]);
                accumulator.RecordPrefilterSkip(entityTypes[i]);
                accumulator.RecordCanCreateProbe(entityTypes[i]);
                accumulator.RecordCreated(entityTypes[i]);
                accumulator.RecordAccepted(entityTypes[i]);
                accumulator.RecordConflictRejected(entityTypes[i]);
            }

            var metrics = accumulator.Build();
            var wallProperty = typeof(EntityLogicBuildMetrics).GetProperty("Wall");

            Assert.That(wallProperty, Is.Not.Null, "Wall diagnostics must expose an explicit bucket.");
            var wall = (EntityLogicTypeMetrics)wallProperty.GetValue(metrics);
            var buckets = new[] { metrics.None, metrics.Unit, metrics.Box, wall, metrics.Unknown };
            Assert.That(wall.EntityVisitedCount, Is.EqualTo(1));
            Assert.That(wall.FactoryOpportunityCount, Is.EqualTo(3));
            Assert.That(wall.PrefilterSkipCount, Is.EqualTo(1));
            Assert.That(wall.CanCreateProbeCount, Is.EqualTo(1));
            Assert.That(wall.CreatedLogicCount, Is.EqualTo(1));
            Assert.That(wall.AcceptedLogicCount, Is.EqualTo(1));
            Assert.That(wall.ConflictRejectedLogicCount, Is.EqualTo(1));
            Assert.That(metrics.Unknown.EntityVisitedCount, Is.EqualTo(1));
            Assert.That(metrics.RegisteredFactoryCount, Is.EqualTo(3));
            Assert.That(metrics.EntityVisitedCount, Is.EqualTo(buckets.Sum(bucket => bucket.EntityVisitedCount)));
            Assert.That(metrics.FactoryOpportunityCount, Is.EqualTo(buckets.Sum(bucket => bucket.FactoryOpportunityCount)));
            Assert.That(metrics.PrefilterSkipCount, Is.EqualTo(buckets.Sum(bucket => bucket.PrefilterSkipCount)));
            Assert.That(metrics.CanCreateProbeCount, Is.EqualTo(buckets.Sum(bucket => bucket.CanCreateProbeCount)));
            Assert.That(metrics.CreatedLogicCount, Is.EqualTo(buckets.Sum(bucket => bucket.CreatedLogicCount)));
            Assert.That(metrics.AcceptedLogicCount, Is.EqualTo(buckets.Sum(bucket => bucket.AcceptedLogicCount)));
            Assert.That(metrics.ConflictRejectedLogicCount, Is.EqualTo(buckets.Sum(bucket => bucket.ConflictRejectedLogicCount)));
        }

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
