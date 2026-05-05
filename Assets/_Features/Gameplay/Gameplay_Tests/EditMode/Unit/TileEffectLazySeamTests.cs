using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TileEffectLazySeamTests
    {
        [Test]
        [Category("Core")]
        public void EmptyTileEffectResolver_ReturnsEmptyResult()
        {
            var snapshot = CreateWorldState(CreateTileFeature(10)).CreateSnapshot();
            var context = new TileEffectResolutionContext(
                7,
                snapshot,
                Array.Empty<TileFeatureRuntimeDefinition>());

            var result = EmptyTileEffectResolver.Instance.Resolve(context);

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.Operations.IsEmpty, Is.True);
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_DefaultTileEffectResolver_PreservesTileFeaturesAndSnapshotBudget()
        {
            var tileFeature = CreateTileFeature(10);
            var worldState = CreateWorldState(tileFeature);
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                pipeline.RunTick(new TickInput(7));
                counts = capture.Counts;
            }

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetTileFeature(tileFeature.TileId, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(tileFeature));
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(15));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(11));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(2));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(12));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(12));
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_TileEffectResolver_ReceivesTickPostAttackSnapshotAndDefinitions()
        {
            var tileFeature = CreateTileFeature(10);
            var definition = CreateDefinition(tileFeature.TileId);
            var resolver = new CapturingTileEffectResolver(TileEffectResolutionResult.Empty);
            var worldState = CreateWorldState(tileFeature);
            var pipeline = CreatePipeline(
                worldState,
                new[] { definition },
                resolver);

            pipeline.RunTick(new TickInput(7));

            Assert.That(resolver.ResolveCount, Is.EqualTo(1));
            Assert.That(resolver.CapturedTickIndex, Is.EqualTo(7));
            Assert.That(resolver.CapturedSnapshot, Is.Not.Null);
            Assert.That(resolver.CapturedSnapshot.TryGetTileFeature(tileFeature.TileId, out var capturedFeature), Is.True);
            Assert.That(capturedFeature, Is.EqualTo(tileFeature));
            CollectionAssert.AreEqual(new[] { definition }, resolver.CapturedDefinitions);
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_NonEmptyTileEffectOperations_RejectsBeforeApplying()
        {
            var tileFeature = CreateTileFeature(10);
            var replacement = new TileFeatureState(
                tileFeature.TileId,
                tileFeature.Cell,
                tileFeature.Kind,
                tileFeature.Flags,
                tileFeature.SourceEntityId,
                tileFeature.OwnerEntityId,
                tileFeature.TeamId,
                tileFeature.LifetimeTicks,
                charges: 99);
            var resolver = new CapturingTileEffectResolver(
                new TileEffectResolutionResult(
                    new TileFeatureOperationBatch(new[] { TileFeatureOperation.Update(replacement) })));
            var worldState = CreateWorldState(tileFeature);
            var pipeline = CreatePipeline(
                worldState,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                resolver);

            var exception = Assert.Throws<InvalidOperationException>(() => pipeline.RunTick(new TickInput(7)));

            StringAssert.Contains("TileEffect operations are not enabled yet", exception.Message);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetTileFeature(tileFeature.TileId, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(tileFeature));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            ITileEffectResolver tileEffectResolver)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new TickPipeline(
                worldState,
                Array.Empty<IEntityLogic>(),
                GameplayEntityLogicProviderFactory.CreateDefault(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                playerRespawnDelayTicks: 1,
                objectiveDefinition: null,
                enemySpawnDefaultsByArchetypeId: null,
                allowPlayerRespawn: true,
                runtimeFeatureFlags: default,
                playerKinematicLocomotionTiming: default,
                playerContinuousLocomotion: default,
                tileFeatureDefinitions: tileFeatureDefinitions,
                tileEffectResolver: tileEffectResolver);
        }

        private static PlayerControlTimingAuthoritativeSnapshot CreateDefaultPlayerControlTimingSnapshot(
            GameplayTimingProfile timingProfile)
        {
            return PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timingProfile.SimulationTicksPerSecond,
                timingProfile.RepeatedMoveIntervalSeconds);
        }

        private static WorldState CreateWorldState(TileFeatureState tileFeature)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                Array.Empty<EntityState>(),
                new BoardBounds(UnityEngine.Vector2Int.zero, new UnityEngine.Vector2Int(4, 4)),
                TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                new[] { tileFeature });
        }

        private static TileFeatureState CreateTileFeature(int tileId)
        {
            return new TileFeatureState(
                tileId,
                new SurfaceCell(FaceId.Floor, 1, 1),
                TileFeatureKind.Button,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 1);
        }

        private static TileFeatureRuntimeDefinition CreateDefinition(int tileId)
        {
            return new TileFeatureRuntimeDefinition(
                tileId,
                TileFeatureActivationRule.Always,
                Direction2D.None,
                TileFeatureBoxSelector.None,
                boundEntityId: 0,
                presentationKey: "button");
        }

        private sealed class CapturingTileEffectResolver : ITileEffectResolver
        {
            private readonly TileEffectResolutionResult _result;

            public CapturingTileEffectResolver(TileEffectResolutionResult result)
            {
                _result = result;
            }

            public int ResolveCount { get; private set; }

            public int CapturedTickIndex { get; private set; }

            public WorldSnapshot CapturedSnapshot { get; private set; }

            public TileFeatureRuntimeDefinition[] CapturedDefinitions { get; private set; }

            public TileEffectResolutionResult Resolve(in TileEffectResolutionContext context)
            {
                ResolveCount++;
                CapturedTickIndex = context.TickIndex;
                CapturedSnapshot = context.Snapshot;
                CapturedDefinitions = new TileFeatureRuntimeDefinition[context.TileFeatureDefinitions.Count];
                for (var i = 0; i < CapturedDefinitions.Length; i++)
                {
                    CapturedDefinitions[i] = context.TileFeatureDefinitions[i];
                }

                return _result;
            }
        }
    }
}
