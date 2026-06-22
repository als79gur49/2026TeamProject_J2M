using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.Attack.Collection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

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
            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(5));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(1));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(10));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(11));
            Assert.That(counts.ProjectedWorldEmptyApplyBatchCount, Is.EqualTo(11));
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_TileEffectResolver_ReceivesFinalAttackReadSnapshotAndDefinitions()
        {
            var tileFeature = CreateTileFeature(10);
            var definition = CreateDefinition(tileFeature.TileId);
            var eventLog = new List<string>();
            var resolver = new CapturingTileEffectResolver(TileEffectResolutionResult.Empty, eventLog);
            var attackLogic = new CapturingAttackLogic(eventLog);
            var worldState = CreateWorldState(tileFeature);
            var pipeline = CreatePipeline(
                worldState,
                new[] { definition },
                resolver,
                new IEntityLogic[] { attackLogic });

            pipeline.RunTick(new TickInput(7));

            Assert.That(resolver.ResolveCount, Is.EqualTo(1));
            Assert.That(resolver.CapturedTickIndex, Is.EqualTo(7));
            Assert.That(resolver.CapturedSnapshot, Is.Not.Null);
            Assert.That(attackLogic.CapturedSnapshots.Count, Is.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1], Is.SameAs(resolver.CapturedSnapshot));
            Assert.That(resolver.CapturedSnapshot.TryGetTileFeature(tileFeature.TileId, out var capturedFeature), Is.True);
            Assert.That(capturedFeature, Is.EqualTo(tileFeature));
            CollectionAssert.AreEqual(new[] { definition }, resolver.CapturedDefinitions);
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_TileEffectResolver_RunsAfterPreliminaryAndBeforeFinalAttackCollection()
        {
            var eventLog = new List<string>();
            var resolver = new CapturingTileEffectResolver(TileEffectResolutionResult.Empty, eventLog);
            var attackLogic = new CapturingAttackLogic(eventLog);
            var worldState = CreateWorldState(CreateTileFeature(10));
            var pipeline = CreatePipeline(
                worldState,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                resolver,
                new IEntityLogic[] { attackLogic });

            pipeline.RunTick(new TickInput(7));

            CollectionAssert.AreEqual(
                new[] { "AttackCollect:1", "TileEffectResolve", "AttackCollect:2" },
                eventLog);
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_EmptyTileEffectResolver_UsesMovementResolvedAttackReadSnapshot()
        {
            var eventLog = new List<string>();
            var resolver = new CapturingTileEffectResolver(TileEffectResolutionResult.Empty, eventLog);
            var attackLogic = new CapturingAttackLogic(eventLog);
            var movementLogic = new ScriptedMovementLogic(
                new RawMovementIntent(10, priority: 100, new Vector2Int(1, 0)));
            var worldState = CreateWorldState(
                CreateTileFeature(20),
                new[] { CreateUnit(10, 0, 0) });
            var pipeline = CreatePipeline(
                worldState,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                resolver,
                new IEntityLogic[] { movementLogic, attackLogic });

            pipeline.RunTick(new TickInput(7));

            Assert.That(resolver.CapturedSnapshot.TryGetEntity(10, out var capturedEntity), Is.True);
            Assert.That(capturedEntity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(attackLogic.CapturedSnapshots.Count, Is.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1], Is.SameAs(resolver.CapturedSnapshot));
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_TileEffectLazySeam_DoesNotCreatePostTileEffectSnapshotOrFailFast()
        {
            var source = System.IO.File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");

            Assert.That(source, Does.Not.Contain("postTileEffectSnapshot"));
            Assert.That(source, Does.Not.Contain("TileEffect operations are not enabled yet"));
        }

        [Test]
        [Category("Core")]
        public void TickPipeline_NonEmptyTileEffectOperations_ProjectAndApplyBeforeFinalAttackInput()
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
            var attackLogic = new CapturingAttackLogic(
                null,
                snapshot => new RawAttackIntent(10, priority: 100, targetId: 20));
            var attacker = CreateUnit(10, 0, 0, teamId: 1);
            var target = CreateUnit(20, 1, 0, teamId: 2);
            var worldState = CreateWorldState(tileFeature, new[] { attacker, target });
            var pipeline = CreatePipeline(
                worldState,
                Array.Empty<TileFeatureRuntimeDefinition>(),
                resolver,
                new IEntityLogic[] { attackLogic });

            pipeline.RunTick(new TickInput(7));

            Assert.That(attackLogic.CollectCount, Is.EqualTo(2));
            Assert.That(attackLogic.CapturedSnapshots[1].TryGetTileFeature(tileFeature.TileId, out var attackReadFeature), Is.True);
            Assert.That(attackReadFeature, Is.EqualTo(replacement));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetTileFeature(tileFeature.TileId, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(replacement));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions,
            ITileEffectResolver tileEffectResolver,
            IReadOnlyList<IEntityLogic> entityLogics = null)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new TickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                GameplayEntityLogicProviderFactory.CreateDefault(),
                timingProfile,
                CreateDefaultPlayerControlTimingSnapshot(timingProfile),
                playerRespawnDelayTicks: 1,
                objectiveDefinition: null,
                enemySpawnDefaultsByArchetypeId: null,
                allowPlayerRespawn: true,
                runtimeFeatureFlags: default,
                unitKinematicLocomotionTiming: default,
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
            return CreateWorldState(tileFeature, Array.Empty<EntityState>());
        }

        private static WorldState CreateWorldState(TileFeatureState tileFeature, IReadOnlyList<EntityState> entities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                entities ?? Array.Empty<EntityState>(),
                new BoardBounds(UnityEngine.Vector2Int.zero, new UnityEngine.Vector2Int(4, 4)),
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

        private static EntityState CreateUnit(int entityId, int x, int y, int hp = 10, int teamId = 1)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, x, y),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
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
            private readonly List<string> _eventLog;

            public CapturingTileEffectResolver(TileEffectResolutionResult result, List<string> eventLog = null)
            {
                _result = result;
                _eventLog = eventLog;
            }

            public int ResolveCount { get; private set; }

            public int CapturedTickIndex { get; private set; }

            public WorldSnapshot CapturedSnapshot { get; private set; }

            public TileFeatureRuntimeDefinition[] CapturedDefinitions { get; private set; }

            public TileEffectResolutionResult Resolve(in TileEffectResolutionContext context)
            {
                ResolveCount++;
                _eventLog?.Add("TileEffectResolve");
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

        private sealed class CapturingAttackLogic : IAttackEntityLogic, IEntityLogicSourceBinding
        {
            private readonly List<string> _eventLog;
            private readonly Func<WorldSnapshot, RawAttackIntent?> _attackIntentFactory;

            public CapturingAttackLogic(
                List<string> eventLog,
                Func<WorldSnapshot, RawAttackIntent?> attackIntentFactory = null)
            {
                _eventLog = eventLog;
                _attackIntentFactory = attackIntentFactory;
            }

            public int ControlledEntityId => 0;

            public int CollectCount { get; private set; }

            public List<WorldSnapshot> CapturedSnapshots { get; } = new();

            public void CollectAttackIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawAttackIntent> buffer)
            {
                CollectCount++;
                _eventLog?.Add($"AttackCollect:{CollectCount}");
                CapturedSnapshots.Add(snapshot);
                if (_attackIntentFactory == null)
                {
                    return;
                }

                var attackIntent = _attackIntentFactory(snapshot);
                if (attackIntent.HasValue)
                {
                    buffer.Add(attackIntent.Value);
                }
            }
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _movementIntent;

            public ScriptedMovementLogic(RawMovementIntent movementIntent)
            {
                _movementIntent = movementIntent;
            }

            public int ControlledEntityId => _movementIntent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                buffer.Add(_movementIntent);
            }
        }
    }
}
