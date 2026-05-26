using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class BlackEyeWindupProjectileRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string WindupProjectileProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupForwardCellProjectile_DoesNotCreatePendingImpact_DuringWindup()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            var startTick = pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);

            Assert.That(action.IsActive, Is.True);
            Assert.That(action.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(startTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(startTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);

            if (action.executeTick > 2)
            {
                var windupTick = pipeline.RunTick(new TickInput(2));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
                Assert.That(windupTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(windupTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupForwardCellProjectile_CreatesPendingImpact_OnExecuteTickOnly()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);
            var occupancyBeforeExecute = DumpOccupancy(worldState.CreateSnapshot());

            for (var tickIndex = 2; tickIndex < action.executeTick; tickIndex++)
            {
                pipeline.RunTick(new TickInput(tickIndex));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero, $"Pending impact created before execute tick {action.executeTick}.");
            }

            var executeTick = pipeline.RunTick(new TickInput(action.executeTick));
            var pendingImpact = GetSinglePendingImpact(worldState);

            Assert.That(pendingImpact.ReleaseTick, Is.EqualTo(action.executeTick));
            Assert.That(pendingImpact.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty, "WindupForwardCellProjectile release schedules PendingCellImpact; direct damage is not same-tick.");
            Assert.That(executeTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Is.EqualTo(occupancyBeforeExecute));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_EntersRecover_AfterForwardCellProjectileExecute()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            pipeline.RunTick(new TickInput(startedAction.executeTick));

            var action = GetEnemyActionState(worldState);
            var enemy = GetEntity(worldState, EnemyId);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.EqualTo(1));
            Assert.That(action.executionAttempted, Is.True);
            Assert.That(enemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(enemy.aiStateTimer, Is.GreaterThan(0));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_DoesNotMoveDuringWindupExecuteRecover()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            var startTick = pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            AssertNoEnemyMovement(startTick);

            if (startedAction.executeTick > 2)
            {
                AssertNoEnemyMovement(pipeline.RunTick(new TickInput(2)));
            }

            AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick)));
            AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick + 1)));
            Assert.That(GetEntity(worldState, EnemyId).aiMode, Is.EqualTo(EnemyAiMode.Recover));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ResumesGroundMovement_AfterRecoverEnds()
        {
            var profile = LoadProfile();
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            pipeline.RunTick(new TickInput(startedAction.executeTick));

            worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 4));
            var recoverTicks = profile.CreateRuntimeDefinition(GameplayTimingProfile.CreateDefault().SimulationTicksPerSecond)
                .Core
                .CommonSettings
                .RecoverTicks;
            for (var offset = 1; offset < recoverTicks; offset++)
            {
                AssertNoEnemyMovement(pipeline.RunTick(new TickInput(startedAction.executeTick + offset)));
            }

            var afterRecoverEnd = pipeline.RunTick(new TickInput(startedAction.executeTick + recoverTicks));
            if (GetEntity(worldState, EnemyId).aiMode == EnemyAiMode.Recover)
            {
                afterRecoverEnd = pipeline.RunTick(new TickInput(startedAction.executeTick + recoverTicks + 1));
            }

            Assert.That(GetEntity(worldState, EnemyId).aiMode, Is.Not.EqualTo(EnemyAiMode.Recover));
            Assert.That(
                afterRecoverEnd.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == EnemyId) ||
                GetEntity(worldState, EnemyId).position != new SurfaceCell(FaceId.Floor, 0, 0),
                Is.True,
                "Current contract should re-enable ground movement participation when recover expires.");
        }

        [Test]
        [Category("Extended")]
        [TestCase("HpZero")]
        [TestCase("MarkedForDeath")]
        [TestCase("Detached")]
        public void BlackEye_TargetInvalidatedDuringWindup_DoesNotCreateStalePendingImpact(string invalidationKind)
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            InvalidatePlayerDuringWindup(worldState, invalidationKind);

            var executeTick = pipeline.RunTick(new TickInput(startedAction.executeTick));

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(executeTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(executeTick.FinalEntities.Count(entity => entity.type == EntityType.Projectile), Is.Zero);
            Assert.That(executeTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            AssertActionInactiveOrMissing(worldState);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ClearsActionState_WhenTopologyMovesOwnerOffBottomFace()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            var suspendedTick = pipeline.RunTick(new TickInput(2));

            AssertActionInactiveOrMissing(worldState);
            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
            Assert.That(suspendedTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_DoesNotCreateStalePendingImpact_AfterTopologySuspension()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var originalExecuteTick = GetEnemyActionState(worldState).executeTick;
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));

            for (var tickIndex = 2; tickIndex <= originalExecuteTick; tickIndex++)
            {
                var tick = pipeline.RunTick(new TickInput(tickIndex));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
                Assert.That(tick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ReacquiresOrRestartsAction_WhenReturnedToBottomFace()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var originalAction = GetEnemyActionState(worldState);
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));

            var restartTick = originalAction.executeTick + 1;
            pipeline.RunTick(new TickInput(restartTick));
            var restartedAction = GetEnemyActionState(worldState);

            Assert.That(restartedAction.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
            Assert.That(restartedAction.sequence, Is.GreaterThan(originalAction.sequence));
            Assert.That(restartedAction.startTick, Is.EqualTo(restartTick));
            Assert.That(restartedAction.executeTick, Is.GreaterThan(restartTick));
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectileUsesSurfaceCell_NotPlanarCellOnly()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Front, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));

            if (worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                Assert.That(action.kind, Is.Not.EqualTo(EnemyActionKind.ForwardCellProjectile));
            }

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_ForwardCellProjectile_DoesNotMutateOccupancy_WhenCreatingPendingImpact()
        {
            var worldState = CreateCombatWorld(new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var action = GetEnemyActionState(worldState);
            var before = DumpOccupancy(worldState.CreateSnapshot());
            pipeline.RunTick(new TickInput(action.executeTick));
            var pendingImpact = GetSinglePendingImpact(worldState);

            Assert.That(pendingImpact.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            Assert.That(DumpOccupancy(worldState.CreateSnapshot()), Is.EqualTo(before));
        }

        private static TickPipeline CreatePipeline(WorldState worldState)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(LoadProfile()).CreateTickPipeline(worldState);
        }

        private static void InvalidatePlayerDuringWindup(WorldState worldState, string invalidationKind)
        {
            var writeContext = worldState.CreateWriteContext();
            switch (invalidationKind)
            {
                case "HpZero":
                    writeContext.ApplyDamage(PlayerId, amount: 99);
                    break;

                case "MarkedForDeath":
                    ((IAttackCommitContext)writeContext).MarkDestroy(PlayerId);
                    break;

                case "Detached":
                    writeContext.SetBoardPresence(PlayerId, EntityBoardPresence.Detached);
                    break;

                default:
                    Assert.Fail($"Unsupported invalidation kind '{invalidationKind}'.");
                    break;
            }
        }

        private static EnemyAiProfile LoadProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(WindupProjectileProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing EnemyAiProfile asset at '{WindupProjectileProfilePath}'.");
            return profile;
        }

        private static WorldState CreateCombatWorld(SurfaceCell playerCell)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(PlayerId, 1, playerCell, EnemyAiMode.None, Direction.Left, UnitRole.Player),
                    CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Attack, Direction.Right, UnitRole.Enemy),
                },
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell cell,
            EnemyAiMode aiMode,
            Direction facing,
            UnitRole unitRole)
        {
            return new EntityState
            {
                entityId = entityId,
                position = cell,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action), Is.True);
            return action;
        }

        private static PendingCellImpact GetSinglePendingImpact(WorldState worldState)
        {
            var impacts = new List<PendingCellImpactSnapshotEntry>();
            worldState.CreateSnapshot().EnumeratePendingCellImpactsOrdered(impacts);
            return impacts.Single().Impact;
        }

        private static void AssertNoEnemyMovement(TickResult tick)
        {
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(tick.EventLog.Any(line => line.Contains($"MoveCommitted") && line.Contains($"E={EnemyId}")), Is.False);
        }

        private static void AssertActionInactiveOrMissing(WorldState worldState)
        {
            if (!worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action))
            {
                return;
            }

            Assert.That(action.IsActive, Is.False);
        }

        private static string DumpOccupancy(WorldSnapshot snapshot)
        {
            var builder = new StringBuilder();
            AppendOccupancy(builder, "Unit", snapshot, (source, buffer) => source.EnumerateUnitOccupancyOrdered(buffer));
            AppendOccupancy(builder, "Solid", snapshot, (source, buffer) => source.EnumerateSolidOccupancyOrdered(buffer));
            AppendOccupancy(builder, "Projectile", snapshot, (source, buffer) => source.EnumerateProjectileOccupancyOrdered(buffer));
            return builder.ToString();
        }

        private static void AppendOccupancy(
            StringBuilder builder,
            string lane,
            WorldSnapshot snapshot,
            System.Action<WorldSnapshot, List<SnapshotOccupancyEntry>> enumerate)
        {
            var entries = new List<SnapshotOccupancyEntry>();
            enumerate(snapshot, entries);
            builder.Append(lane).Append(':');
            for (var i = 0; i < entries.Count; i++)
            {
                builder
                    .Append(entries[i].EntityId)
                    .Append('@')
                    .Append(entries[i].Cell)
                    .Append(';');
            }

            builder.Append('\n');
        }
    }
}
