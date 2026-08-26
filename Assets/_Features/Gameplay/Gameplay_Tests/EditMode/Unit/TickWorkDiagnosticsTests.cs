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
        public void BuilderResultPath_B2RecordsOneEnumerationNoCopyOneWrapperAndOneTrustedShare()
        {
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()));

            GameplayTickWorkloadCounts counts;
            using (var capture = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(1));
                counts = capture.Counts;
            }

            Assert.That(counts.FinalEntityEnumerationCount, Is.EqualTo(1));
            Assert.That(counts.FinalEntityEnumeratedItemCount, Is.Zero);
            Assert.That(counts.FinalEntityDefensiveCopyCount, Is.Zero);
            Assert.That(counts.FinalEntityDefensiveCopiedItemCount, Is.Zero);
            Assert.That(counts.FinalEntityOwnedWrapperCreationCount, Is.EqualTo(1));
            Assert.That(counts.TickResultFinalEntityTrustedShareCount, Is.EqualTo(1));
            Assert.That(counts.EntityLogicProviderBuildCount, Is.EqualTo(1));
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
        public void Capture_DoesNotChangeCanonicalOutputs_AndNestedScopesRestoreWithoutLeak()
        {
            var uncaptured = GameplayCompositionRoot
                .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                .RunTick(new TickInput(7));

            TickResult captured;
            using (var outer = GameplayTickWorkloadDiagnostics.BeginCapture())
            {
                GameplayTickWorkloadDiagnostics.RecordFinalEntityEnumeration(2);
                using (var inner = GameplayTickWorkloadDiagnostics.BeginCapture())
                {
                    GameplayTickWorkloadDiagnostics.RecordFinalEntityEnumeration(3);
                    Assert.That(inner.Counts.FinalEntityEnumerationCount, Is.EqualTo(1));
                    Assert.That(inner.Counts.FinalEntityEnumeratedItemCount, Is.EqualTo(3));
                }

                Assert.That(GameplayTickWorkloadDiagnostics.Current.FinalEntityEnumerationCount, Is.EqualTo(1));
                Assert.That(outer.Counts.FinalEntityEnumeratedItemCount, Is.EqualTo(2));
                captured = GameplayCompositionRoot
                    .CreateTickPipeline(GameplayWorldStateTestFactory.CreateBounded(Array.Empty<EntityState>()))
                    .RunTick(new TickInput(7));
            }

            Assert.That(GameplayTickWorkloadDiagnostics.Current.FinalEntityEnumerationCount, Is.Zero);
            CollectionAssert.AreEqual(uncaptured.FinalEntities, captured.FinalEntities);
            CollectionAssert.AreEqual(uncaptured.EventLog, captured.EventLog);
            CollectionAssert.AreEqual(uncaptured.PhaseTrace, captured.PhaseTrace);
            Assert.That(captured.DeterminismHash, Is.EqualTo(uncaptured.DeterminismHash));
            Assert.That(captured.Trace.Text, Is.EqualTo(uncaptured.Trace.Text));
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
