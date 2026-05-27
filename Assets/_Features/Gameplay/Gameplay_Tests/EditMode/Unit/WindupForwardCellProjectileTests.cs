using System;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class WindupForwardCellProjectileTests
    {
        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_AtRange1_StartsWindup()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));

                var action = GetEnemyActionState(worldState);
                Assert.That(action.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
                Assert.That(action.executionAttempted, Is.False);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_OutsideSimulationStartRange_ApproachesInsteadOfIdling()
        {
            var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
            worldState.CreateWriteContext().SetUnitContinuousLocomotionState(
                PlayerId,
                new UnitContinuousLocomotionState
                {
                    localOffset = new KinematicOffset2(
                        KinematicFixed.FromRaw(KinematicFixed.MaxPositiveLocalOffset),
                        KinematicFixed.Zero),
                    velocity = new KinematicVelocity2(KinematicFixed.FromRaw(1), KinematicFixed.Zero),
                    facing = Direction.Right,
                    speedUnitsPerTick = 1,
                    mode = ContinuousLocomotionMode.Moving,
                });

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(EnemyId, out var enemy), Is.True);
            Assert.That(snapshot.TryGetEntity(PlayerId, out var player), Is.True);

            var result = WindupMeleeCombatPoseQueries.QueryStartWindupForwardCellProjectile(
                snapshot,
                enemy,
                player,
                WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                new AttackDecisionSettings(1),
                WindupForwardCellProjectileSettings.CreateDefault(),
                out _);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.ShouldApproach, Is.True);
            Assert.That(result.BlockReason, Is.EqualTo(WindupMeleeStartBlockReason.OutsideSimulationStartRange));
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_WindupLocksForwardTargetCell()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));

                var action = GetEnemyActionState(worldState);
                Assert.That(action.hasLockedForwardCellImpact, Is.True);
                Assert.That(action.lockedAttackBaseCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(action.lockedAttackDirection, Is.EqualTo(Direction.Right));
                Assert.That(action.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_Range4_LocksPlayerForwardCell()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(
                attackRange: 4,
                impactDelayTicksPerCell: 24);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 4, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));

                var action = GetEnemyActionState(worldState);
                Assert.That(action.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
                Assert.That(action.hasLockedForwardCellImpact, Is.True);
                Assert.That(action.lockedAttackBaseCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(action.lockedAttackDirection, Is.EqualTo(Direction.Right));
                Assert.That(action.lockedTargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_Range4_RejectsDiagonalTarget()
        {
            var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 2, 2));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(EnemyId, out var enemy), Is.True);
            Assert.That(snapshot.TryGetEntity(PlayerId, out var player), Is.True);

            var settings = new WindupForwardCellProjectileSettings(
                WindupMeleeSettings.DefaultVisualRangeSlackCells,
                impactDelayTicks: 24,
                damage: 1,
                activePendingImpactLimitPerOwner: 1,
                impactDelayTicksPerCell: 24);

            var result = WindupMeleeCombatPoseQueries.QueryStartWindupForwardCellProjectile(
                snapshot,
                enemy,
                player,
                WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                new AttackDecisionSettings(4),
                settings,
                out _);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(WindupMeleeStartBlockReason.InvalidForwardTargetCell));
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_WindupEmitsProjectileWindupSignal()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                var tick = pipeline.RunTick(new TickInput(1));
                var signal = tick.PresentationData.ForwardCellProjectileWindupSignals.Single();
                var action = GetEnemyActionState(worldState);

                Assert.That(signal.PresentationKey, Is.EqualTo(ComputePresentationKey(EnemyId, action.sequence)));
                Assert.That(signal.OwnerId, Is.EqualTo(EnemyId));
                Assert.That(signal.SourceEnemyId, Is.EqualTo(EnemyId));
                Assert.That(signal.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(signal.Direction, Is.EqualTo(Direction.Right));
                Assert.That(signal.StartedTick, Is.EqualTo(action.startTick));
                Assert.That(signal.ExpectedReleaseTick, Is.EqualTo(action.executeTick));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_TargetCellDoesNotTrackPlayerDuringWindup()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(windupTicks: 2);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var lockedTargetCell = GetEnemyActionState(worldState).lockedTargetCell;
                worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 1, 1));
                pipeline.RunTick(new TickInput(2));

                Assert.That(GetEnemyActionState(worldState).lockedTargetCell, Is.EqualTo(lockedTargetCell));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ReleaseAndRecover_PreservesLockedFacing()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(
                windupTicks: 3,
                attackRange: 4);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 4, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var startedAction = GetEnemyActionState(worldState);
                Assert.That(startedAction.lockedAttackDirection, Is.EqualTo(Direction.Right));

                worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 1));
                DriftProjectileActionFacing(worldState, actionDirection: Direction.Up, entityFacing: Direction.Up);

                var activeTick = pipeline.RunTick(new TickInput(2));
                Assert.That(GetEntityAfterTick(activeTick, EnemyId).facing, Is.EqualTo(Direction.Right));
                Assert.That(activeTick.PresentationData.ForwardCellProjectileReleaseSignals, Is.Empty);

                DriftProjectileActionFacing(worldState, actionDirection: Direction.Up, entityFacing: Direction.Up);
                var releaseTickIndex = startedAction.executeTick;
                var releaseTick = pipeline.RunTick(new TickInput(releaseTickIndex));
                var releasedEnemy = GetEntityAfterTick(releaseTick, EnemyId);
                var releaseSignal = releaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single();
                var pendingImpacts = new System.Collections.Generic.List<PendingCellImpactSnapshotEntry>();
                worldState.CreateSnapshot().EnumeratePendingCellImpactsOrdered(pendingImpacts);
                var pendingImpact = pendingImpacts.Single().Impact;

                Assert.That(releasedEnemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
                Assert.That(releasedEnemy.facing, Is.EqualTo(Direction.Right));
                Assert.That(GetEntity(worldState, EnemyId).facing, Is.EqualTo(Direction.Right));
                Assert.That(releaseSignal.Direction, Is.EqualTo(Direction.Right));
                Assert.That(pendingImpact.Direction, Is.EqualTo(Direction.Right));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void BlackEye_WindupProjectile_SuppressedMovement_DoesNotRefaceDuringRecover()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(BlackEyeWindupProjectileProfilePath);
            Assert.That(profile, Is.Not.Null, BlackEyeWindupProjectileProfilePath);

            var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 4, 0));
            var pipeline = CreateEnemyPipeline(worldState, profile);

            pipeline.RunTick(new TickInput(1));
            var startedAction = GetEnemyActionState(worldState);
            Assert.That(startedAction.lockedAttackDirection, Is.EqualTo(Direction.Right));

            worldState.CreateWriteContext().MoveEntity(PlayerId, new SurfaceCell(FaceId.Floor, 0, 1));
            DriftProjectileActionFacing(worldState, actionDirection: Direction.Up, entityFacing: Direction.Up);

            var activeTick = pipeline.RunTick(new TickInput(2));
            Assert.That(activeTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(GetEntityAfterTick(activeTick, EnemyId).facing, Is.EqualTo(Direction.Right));

            DriftProjectileActionFacing(worldState, actionDirection: Direction.Up, entityFacing: Direction.Up);
            var releaseTick = pipeline.RunTick(new TickInput(startedAction.executeTick));
            var releasedEnemy = GetEntityAfterTick(releaseTick, EnemyId);
            var releaseSignal = releaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single();

            Assert.That(releaseTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(releasedEnemy.aiMode, Is.EqualTo(EnemyAiMode.Recover));
            Assert.That(releasedEnemy.facing, Is.EqualTo(Direction.Right));
            Assert.That(releaseSignal.Direction, Is.EqualTo(Direction.Right));

            var recoverTick = pipeline.RunTick(new TickInput(startedAction.executeTick + 1));
            Assert.That(recoverTick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(GetEntityAfterTick(recoverTick, EnemyId).facing, Is.EqualTo(Direction.Right));
            Assert.That(GetEntity(worldState, EnemyId).facing, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ReleaseSchedulesImpactButDoesNotDamage()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                var releaseTick = pipeline.RunTick(new TickInput(releaseTickIndex));

                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.EqualTo(1));
                Assert.That(releaseTick.AttackPhaseResult.DamageResolutions, Is.Empty);
                Assert.That(
                    releaseTick.PresentationData.EnemyActionSignals.Any(signal =>
                        signal.EntityId == EnemyId && signal.ExecutedThisTick),
                    Is.True);

                var releaseSignal = releaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single();
                Assert.That(releaseSignal.PresentationKey, Is.EqualTo(releaseSignal.ImpactId));
                Assert.That(releaseSignal.OwnerId, Is.EqualTo(EnemyId));
                Assert.That(releaseSignal.SourceEnemyId, Is.EqualTo(EnemyId));
                Assert.That(releaseSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(releaseSignal.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
                Assert.That(releaseSignal.Direction, Is.EqualTo(Direction.Right));
                Assert.That(releaseSignal.ReleaseTick, Is.EqualTo(releaseTickIndex));
                Assert.That(releaseSignal.ImpactTick, Is.EqualTo(releaseTickIndex + 1));
                Assert.That(releaseSignal.ImpactDelayTicks, Is.EqualTo(1));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_Range4_ReleaseSchedulesDistanceBasedImpact()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(
                attackRange: 4,
                impactDelayTicksPerCell: 24);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 4, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                var releaseTick = pipeline.RunTick(new TickInput(releaseTickIndex));

                var releaseSignal = releaseTick.PresentationData.ForwardCellProjectileReleaseSignals.Single();
                Assert.That(releaseSignal.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
                Assert.That(releaseSignal.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 0)));
                Assert.That(releaseSignal.ReleaseTick, Is.EqualTo(releaseTickIndex));
                Assert.That(releaseSignal.ImpactTick, Is.EqualTo(releaseTickIndex + 96));
                Assert.That(releaseSignal.ImpactDelayTicks, Is.EqualTo(96));
                Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.EqualTo(1));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ReleaseStartsAttackCooldown()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(attackCooldownTicks: 3);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                pipeline.RunTick(new TickInput(releaseTickIndex));

                var enemy = GetEntity(worldState, EnemyId);
                Assert.That(enemy.enemyAttackCooldownTicks, Is.EqualTo(3));
                Assert.That(enemy.enemyAttackCooldownTotalTicks, Is.EqualTo(3));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_CooldownBlocksRewindupUntilExpired()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(attackCooldownTicks: 3);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var firstSequence = GetEnemyActionState(worldState).sequence;
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                pipeline.RunTick(new TickInput(releaseTickIndex));
                pipeline.RunTick(new TickInput(releaseTickIndex + 1));

                var blockedAction = GetEnemyActionState(worldState);
                Assert.That(blockedAction.IsActive, Is.False);
                Assert.That(GetEntity(worldState, EnemyId).enemyAttackCooldownTicks, Is.EqualTo(2));

                pipeline.RunTick(new TickInput(releaseTickIndex + 2));
                pipeline.RunTick(new TickInput(releaseTickIndex + 3));

                var restartedAction = GetEnemyActionState(worldState);
                Assert.That(restartedAction.kind, Is.EqualTo(EnemyActionKind.ForwardCellProjectile));
                Assert.That(restartedAction.sequence, Is.GreaterThan(firstSequence));
                Assert.That(GetEntity(worldState, EnemyId).enemyAttackCooldownTicks, Is.Zero);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_CooldownDoesNotBlockPendingImpact()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(attackCooldownTicks: 3);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                pipeline.RunTick(new TickInput(releaseTickIndex));
                var impactTick = pipeline.RunTick(new TickInput(releaseTickIndex + 1));

                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
                Assert.That(impactTick.AttackPhaseResult.PendingCellImpactResolutions.Single().Hit, Is.True);
                Assert.That(GetEntity(worldState, EnemyId).enemyAttackCooldownTicks, Is.EqualTo(2));
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ImpactHitsPlayerInsideTargetCell()
        {
            var (result, worldState) = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 0));

            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(2));
            Assert.That(result.AttackPhaseResult.PendingCellImpactResolutions.Single().Hit, Is.True);
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ImpactMissesPlayerOutsideTargetCell()
        {
            var (result, worldState) = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 1));

            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            Assert.That(result.AttackPhaseResult.PendingCellImpactResolutions.Single().Hit, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ImpactEmitsImpactPresentationSignalOnMiss()
        {
            var (result, _) = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 1));
            var resolution = result.AttackPhaseResult.PendingCellImpactResolutions.Single();
            var signal = result.PresentationData.ForwardCellImpactSignals.Single();

            Assert.That(signal.PresentationKey, Is.EqualTo(resolution.Impact.ImpactId));
            Assert.That(signal.ImpactId, Is.EqualTo(resolution.Impact.ImpactId));
            Assert.That(signal.OwnerId, Is.EqualTo(EnemyId));
            Assert.That(signal.SourceEnemyId, Is.EqualTo(EnemyId));
            Assert.That(signal.TargetCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(signal.Direction, Is.EqualTo(Direction.Right));
            Assert.That(signal.Hit, Is.False);
            Assert.That(signal.TargetEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ImpactUsesCurrentPlayerCellNotLockedTargetId()
        {
            var (result, worldState) = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 2, 0));

            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            Assert.That(result.AttackPhaseResult.PendingCellImpactResolutions.Single().TargetEntityId, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_PendingImpactRemovedAfterImpact()
        {
            var (_, worldState) = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 0));

            Assert.That(worldState.CreateSnapshot().CountPendingCellImpactsForOwner(EnemyId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_ActivePendingImpactPreventsRewindup()
        {
            var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
            worldState.CreateWriteContext().AddPendingCellImpact(CreatePendingImpact(impactTick: 10));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(EnemyId, out var enemy), Is.True);
            Assert.That(snapshot.TryGetEntity(PlayerId, out var player), Is.True);

            var result = WindupMeleeCombatPoseQueries.QueryStartWindupForwardCellProjectile(
                snapshot,
                enemy,
                player,
                WindupForwardCellProjectileAttackDecisionStrategy.Instance,
                new AttackDecisionSettings(1),
                WindupForwardCellProjectileSettings.CreateDefault(),
                out _);

            Assert.That(result.CanStart, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(WindupMeleeStartBlockReason.ActivePendingImpactLimitReached));
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_DoesNotUseRendererTransform()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var files = new[]
            {
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/WindupMeleeCombatPoseQueries.cs",
                "Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyActionStateLogic.cs",
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs",
            };

            var source = string.Join(
                "\n",
                files.Select(file => File.ReadAllText(Path.Combine(root, file))));

            Assert.That(source, Does.Not.Contain(".transform"));
            Assert.That(source, Does.Not.Contain("Transform "));
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_PassiveContactDoesNotBypassTelegraph()
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile(includePassiveContact: false);
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 0, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                var tick = pipeline.RunTick(new TickInput(1));

                Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
                Assert.That(tick.AttackPhaseResult.DamageResolutions, Is.Empty);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        [Test]
        [Category("Extended")]
        public void WindupForwardCellProjectile_Determinism_ReplaySameInputSameResult()
        {
            var first = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 0));
            var second = RunReleasedImpact(playerCellBeforeImpact: new SurfaceCell(FaceId.Floor, 1, 0));

            Assert.That(GetEntity(first.WorldState, PlayerId).hp, Is.EqualTo(GetEntity(second.WorldState, PlayerId).hp));
            CollectionAssert.AreEqual(first.Result.EventLog, second.Result.EventLog);
            CollectionAssert.AreEqual(
                first.Result.AttackPhaseResult.PendingCellImpactResolutions.Select(ToComparableResolution).ToArray(),
                second.Result.AttackPhaseResult.PendingCellImpactResolutions.Select(ToComparableResolution).ToArray());
            Assert.That(first.Result.DeterminismHash, Is.EqualTo(second.Result.DeterminismHash));
        }

        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string BlackEyeWindupProjectileProfilePath =
            "Assets/_Features/Stages/Content/Campaigns/campaign-main/_Shared/Gameplay/EnemyAI/Profiles/Enemy_WindupProjectile/EnemyAi_WindupProjectile.asset";

        private static (TickResult Result, WorldState WorldState) RunReleasedImpact(SurfaceCell playerCellBeforeImpact)
        {
            var profile = EnemyAiProfileTestFactory.CreateWindupForwardCellProjectile();
            try
            {
                var worldState = CreateCombatWorld(playerCell: new SurfaceCell(FaceId.Floor, 1, 0));
                var pipeline = CreateEnemyPipeline(worldState, profile);

                pipeline.RunTick(new TickInput(1));
                var releaseTickIndex = Math.Max(2, GetEnemyActionState(worldState).executeTick);
                pipeline.RunTick(new TickInput(releaseTickIndex));
                worldState.CreateWriteContext().MoveEntity(PlayerId, playerCellBeforeImpact);
                var impactTick = pipeline.RunTick(new TickInput(releaseTickIndex + 1));

                return (impactTick, worldState);
            }
            finally
            {
                EnemyAiProfileTestFactory.Destroy(profile);
            }
        }

        private static string ToComparableResolution(PendingCellImpactResolutionRecord resolution)
        {
            return $"{resolution.Impact.ImpactId}:{resolution.Impact.TargetCell}:{resolution.Hit}:{resolution.TargetEntityId}";
        }

        private static int ComputePresentationKey(int ownerId, int actionSequence)
        {
            return checked((ownerId * 100000) + Math.Max(1, actionSequence));
        }

        private static TickPipeline CreateEnemyPipeline(WorldState worldState, EnemyAiProfile profile)
        {
            return GameplayCompositionRoot.CreateDefaultBootstrapper(profile).CreateTickPipeline(worldState);
        }

        private static WorldState CreateCombatWorld(SurfaceCell playerCell)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(PlayerId, 1, playerCell, EnemyAiMode.None, Direction.Left, UnitRole.Player),
                    CreateUnit(EnemyId, 2, new SurfaceCell(FaceId.Floor, 0, 0), EnemyAiMode.Attack, Direction.Right, UnitRole.Enemy),
                },
                new BoardBounds(new Vector2Int(-2, -2), new Vector2Int(4, 4)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty);
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
                stateTimer = 0,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                markedForDeath = false,
                spawnTick = 0,
                aiMode = aiMode,
                aiStateTimer = 0,
            };
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EntityState GetEntityAfterTick(TickResult tickResult, int entityId)
        {
            return tickResult.FinalEntities.Single(entity => entity.entityId == entityId);
        }

        private static EnemyActionRuntimeState GetEnemyActionState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyActionState(EnemyId, out var action), Is.True);
            return action;
        }

        private static void DriftProjectileActionFacing(
            WorldState worldState,
            Direction actionDirection,
            Direction entityFacing)
        {
            var action = GetEnemyActionState(worldState);
            action.direction = actionDirection;

            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyActionState(EnemyId, action);
            ((IPreMovementStateCommitContext)writeContext).SetFacing(EnemyId, entityFacing);
        }

        private static PendingCellImpact CreatePendingImpact(int impactTick)
        {
            return new PendingCellImpact(
                impactId: 1,
                ownerId: EnemyId,
                sourceEnemyId: EnemyId,
                targetCell: new SurfaceCell(FaceId.Floor, 1, 0),
                direction: Direction.Right,
                damage: 1,
                createdTick: 0,
                releaseTick: impactTick - 1,
                impactTick: impactTick);
        }
    }
}
