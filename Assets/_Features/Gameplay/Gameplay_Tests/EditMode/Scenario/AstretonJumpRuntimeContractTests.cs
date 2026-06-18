using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class AstretonJumpRuntimeContractTests
    {
        private const int PlayerId = 10;
        private const int EnemyId = 40;
        private const string JumpChaserProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_JumpChaser/EnemyAi_JumpChaser.asset";

        // Astreton is the JumpChaser movement-skill profile: Windup/Airborne/Landing are jump runtime state, and landing legality is settlement.

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_WindupDoesNotMoveDamageOrMutateOccupancy()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, targetCell, hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = 5,
                    landingTick = 10,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(sourceCell));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(GetEntity(worldState, PlayerId).hp, Is.EqualTo(3));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Windup));
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
            Assert.That(tick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_AirborneSuppressesGroundMovement()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, targetCell, hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = 0,
                    landingTick = 5,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(sourceCell));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(tick.MovementPhaseResult.RawIntents.Where(intent => intent.SourceId == EnemyId), Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_AirborneOffBottomDefersLandingUntilFaceReturns()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Front, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, targetCell, hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                },
                topology: new CubeTopologyState(FaceId.Floor));
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    landingTick = 2,
                });
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var stillOffBottom = pipeline.RunTick(new TickInput(2));
            var suspendedState = GetJumpState(worldState);

            Assert.That(suspendedState.phase, Is.EqualTo(EnemyJumpPhase.Airborne), stillOffBottom.Trace.Text);
            Assert.That(suspendedState.landingTick, Is.GreaterThan(2));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(stillOffBottom.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            var returnedTick = pipeline.RunTick(new TickInput(3));

            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne), returnedTick.Trace.Text);
            Assert.That(returnedTick.PresentationData.EnemyJumpSignals.Single().LandedThisTick, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_LandingRevalidatesSurfaceCellAfterTopologyReturn()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var staleTargetCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 4, 0), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                    CreateWall(90, staleTargetCell),
                },
                topology: new CubeTopologyState(FaceId.Front));
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = staleTargetCell,
                    landingTick = 2,
                });
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            RunUntilNotAirborne(pipeline, worldState, startTick: 2, maxTick: 12);

            var enemy = GetEntity(worldState, EnemyId);
            Assert.That(enemy.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(enemy.position, Is.Not.EqualTo(staleTargetCell), "Landing must revalidate the locked SurfaceCell and avoid a stale blocked target.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(staleTargetCell, out _), Is.True);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), enemy.position, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_PushImpactDuringWindupClearsOrPreservesDocumentedState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right),
                CreateBox(50, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Push, EntityPhaseState.Sliding, Direction.Right, PlayerId, 1),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 0),
                    windupEndTick = 5,
                    landingTick = 10,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).hp, Is.EqualTo(2), "CurrentContract: push-box impact damages windup Astreton.");
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Windup), "CurrentContract: windup state is preserved after non-lethal push impact.");
            Assert.That(tick.AttackPhaseResult.DrainedImpactReservations.Any(reservation => reservation.TargetId == EnemyId), Is.True);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactDuringAirborneClearsOrPreservesDocumentedState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, 3, 0), hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                CreateBox(50, new SurfaceCell(FaceId.Floor, -1, 0), BoxCapabilities.Flip, EntityPhaseState.Sliding, Direction.Right, PlayerId, 1),
            });
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 0),
                    landingTick = 5,
                });

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetEntity(worldState, EnemyId).hp, Is.EqualTo(3), "CurrentContract: detached airborne Astreton is ignored by the sliding flip-capable impact carrier.");
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(tick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_PushImpactDuringWindupIfKilledClearsJumpStateAndPreventsLanding()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateImpactWorld(
                sourceCell,
                targetCell,
                enemyHp: 1,
                EnemyJumpPhase.Windup,
                BoxCapabilities.Push,
                EntityBoardPresence.Occupying);
            var pipeline = CreatePipeline(worldState);

            var impactTick = pipeline.RunTick(new TickInput(1));
            pipeline.RunTick(new TickInput(2));
            pipeline.RunTick(new TickInput(10));

            Assert.That(impactTick.AttackPhaseResult.DrainedImpactReservations.Any(reservation => reservation.TargetId == EnemyId), Is.True);
            AssertNoStaleJumpLanding(worldState, sourceCell, targetCell);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_PushImpactDuringAirborneIfKilledClearsJumpStateAndPreventsLanding()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateImpactWorld(
                sourceCell,
                targetCell,
                enemyHp: 1,
                EnemyJumpPhase.Airborne,
                BoxCapabilities.Push,
                EntityBoardPresence.Detached);
            var pipeline = CreatePipeline(worldState);

            var impactTick = pipeline.RunTick(new TickInput(1));
            pipeline.RunTick(new TickInput(2));
            pipeline.RunTick(new TickInput(5));

            Assert.That(impactTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty, "CurrentContract: detached airborne Astreton is not targetable by push impact.");
            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Occupying), "CurrentContract: untargeted airborne Astreton eventually resolves landing normally.");
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), targetCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactDuringWindupUsesDispositionAndDoesNotLeaveStaleJumpState()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 5, 0);
            var worldState = CreateFlipImpactWorld(
                sourceCell,
                targetCell,
                enemyHp: 1,
                EnemyJumpPhase.Windup,
                EntityBoardPresence.Occupying);
            var pipeline = CreatePipeline(worldState, null, CreateFlipImpactLogic(sourceCell));

            var impactTick = pipeline.RunTick(new TickInput(1));
            pipeline.RunTick(new TickInput(2));
            pipeline.RunTick(new TickInput(10));
            var disposition = impactTick.MovementPhaseResult.ImpactDispositionRecords.Single(record => record.ImpactSourceEntityId == 50);

            Assert.That(disposition.PolicyKind, Is.EqualTo(ImpactDispositionPolicyKind.Flip));
            Assert.That(disposition.TargetDestroyed, Is.True);
            Assert.That(
                disposition.DispositionKind,
                Is.EqualTo(ImpactDispositionKind.FollowThrough).Or.EqualTo(ImpactDispositionKind.Stay),
                "CurrentContract: lethal flip impact follows through when settlement is legal, otherwise stays.");
            AssertNoStaleJumpLanding(worldState, sourceCell, targetCell);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactDuringAirborneTargetabilityIsDocumented()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateImpactWorld(
                sourceCell,
                targetCell,
                enemyHp: 1,
                EnemyJumpPhase.Airborne,
                BoxCapabilities.Flip,
                EntityBoardPresence.Detached);

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(tick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(tick.MovementPhaseResult.ImpactDispositionRecords, Is.Empty);
            Assert.That(GetEntity(worldState, EnemyId).hp, Is.EqualTo(1), "CurrentContract: detached airborne Astreton is ignored by flip impact.");
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactDuringAirborneDoesNotCreateDoubleOccupancy()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var worldState = CreateImpactWorld(
                sourceCell,
                targetCell,
                enemyHp: 3,
                EnemyJumpPhase.Airborne,
                BoxCapabilities.Flip,
                EntityBoardPresence.Detached);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            pipeline.RunTick(new TickInput(5));

            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), sourceCell, EnemyId), Is.Zero);
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), targetCell, EnemyId), Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_PushImpactThenTopologyReturnDoesNotReviveStaleLanding()
        {
            AssertImpactThenTopologyReturnDoesNotReviveStaleLanding(BoxCapabilities.Push);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_FlipImpactThenTopologyReturnDoesNotReviveStaleLanding()
        {
            AssertImpactThenTopologyReturnDoesNotReviveStaleLanding(BoxCapabilities.Flip);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedDestroyTileDoesNotHardBlockJumpLandingSettlement()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destroyTile = CreateTileFeature(100, targetCell, TileFeatureKind.Destroy);
            var blockedCells = FullyBlockedRightFacingLandingCells(sourceCell, targetCell)
                .Where(cell => cell != targetCell);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, -3, -3), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                }.Concat(blockedCells.Select((cell, index) => CreateWall(90 + index, cell))),
                initialTileFeatures: new[] { destroyTile });
            SeedJumpState(worldState, sourceCell, targetCell, EnemyJumpPhase.Airborne, landingTick: 2);

            var landingTick = CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(2));

            AssertLandingResolvedWithoutIllegalOverlap(worldState, targetCell, landingTick);
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Landed));
            if (worldState.CreateSnapshot().TryGetEnemyJumpState(EnemyId, out var jumpState))
            {
                Assert.That(jumpState.phase, Is.Not.EqualTo(EnemyJumpPhase.Airborne), "DestroyTile is not a hard landing settlement blocker when no neutral landing exists.");
            }

            Assert.That(worldState.CreateSnapshot().TryGetTileFeature(100, out _), Is.True);
        }

        // TileFeature jump settlement policy is face-aware: DestroyTile is a non-hard-blocking risk candidate, active Barricade blocks settlement without Solid occupancy, and generated MoonBlock Solid blocks as Solid.

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedDestroyTileOnSamePlanarOtherFaceDoesNotAffectLandingSettlementSurfaceCell()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 3, 0);
            var destroyTile = CreateTileFeature(101, otherFaceCell, TileFeatureKind.Destroy);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(destroyTile);

            CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(targetCell));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.Not.EqualTo(otherFaceCell));
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedDestroyTileLandingCandidateIsAvoidedWhenFallbackExists()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var destroyTile = CreateTileFeature(102, targetCell, TileFeatureKind.Destroy);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(destroyTile);

            var landingTick = CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(fallbackCell), landingTick.Trace.Text);
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedBarricadeOnExactLandingCellBlocksExactLandingAndUsesFallback()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var barricade = CreateTileFeature(103, targetCell, TileFeatureKind.Barricade);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(barricade);

            var landingTick = CreatePipeline(worldState, CreateActiveDefinitions(barricade)).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(fallbackCell), landingTick.Trace.Text);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(targetCell, out _), Is.False);
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_InactiveBarricadeOnExactLandingCellDoesNotBlockSettlement()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var barricade = CreateTileFeature(106, targetCell, TileFeatureKind.Barricade);
            var definitions = CreateInactiveDefinitions(barricade);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(barricade);

            var landingTick = CreatePipeline(worldState, definitions).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(targetCell), landingTick.Trace.Text);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(targetCell, out _), Is.False);
            AssertLandingResolvedWithoutIllegalOverlap(worldState, targetCell, landingTick);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedDestroyTileOnlyCandidateDoesNotRetryAsHardBlocked()
        {
            EnemyJump_AstretonProfile_ActivatedDestroyTileDoesNotHardBlockJumpLandingSettlement();
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedBarricadeOnSamePlanarOtherFaceDoesNotBlockLandingSettlementSurfaceCell()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 3, 0);
            var barricade = CreateTileFeature(104, otherFaceCell, TileFeatureKind.Barricade);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(barricade);

            CreatePipeline(worldState, CreateActiveDefinitions(barricade)).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(targetCell));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.Not.EqualTo(otherFaceCell));
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedBarricadeAppearsDuringAirborne_RevalidatesLandingAtResolve()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var barricade = CreateTileFeature(105, targetCell, TileFeatureKind.Barricade);
            var worldState = CreateAirborneLandingWorld(targetCell);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(barricade);
            pipeline = CreatePipeline(worldState, CreateActiveDefinitions(barricade));
            var landingTick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(fallbackCell), landingTick.Trace.Text);
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_InactiveBarricadeAppearsDuringAirborne_DoesNotChangeLanding()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var barricade = CreateTileFeature(107, targetCell, TileFeatureKind.Barricade);
            var definitions = CreateInactiveDefinitions(barricade);
            var worldState = CreateAirborneLandingWorld(targetCell);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(barricade);
            pipeline = CreatePipeline(worldState, definitions);
            var landingTick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(targetCell), landingTick.Trace.Text);
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(targetCell, out _), Is.False);
            AssertLandingResolvedWithoutIllegalOverlap(worldState, targetCell, landingTick);
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_GeneratedMoonBlockSolidOnExactLandingCellBlocksSettlementAndUsesFallback()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateAirborneLandingWorld(targetCell);
            var pipeline = CreatePipeline(worldState);

            worldState.CreateWriteContext().AddTileFeature(CreateTileFeature(104, targetCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 90));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(90, targetCell, BoxCapabilities.Push | BoxCapabilities.Flip, EntityPhaseState.Idle, Direction.None, 0, 0, BoxArchetype.Moon));
            var landingTick = pipeline.RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(targetCell, out var solid), Is.True);
            Assert.That(solid.Kind, Is.EqualTo(SolidKind.Box));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(fallbackCell), "Generated MoonBlock Solid blocks exact jump landing settlement and Astreton uses the first legal fallback.");
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Landed));
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_GeneratedMoonBlockSolidOnSamePlanarOtherFaceDoesNotBlockLandingSettlement()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 3, 0);
            var worldState = CreateAirborneLandingWorld(targetCell);
            worldState.CreateWriteContext().AddTileFeature(CreateTileFeature(105, otherFaceCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 91));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(91, otherFaceCell, BoxCapabilities.Push | BoxCapabilities.Flip, EntityPhaseState.Idle, Direction.None, 0, 0, BoxArchetype.Moon));

            CreatePipeline(worldState).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(targetCell));
            Assert.That(GetEntity(worldState, EnemyId).position, Is.Not.EqualTo(otherFaceCell));
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_GeneratedMoonBlockSolidDuringAirborne_RevalidatesLandingSettlementAtResolve()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var fallbackCell = new SurfaceCell(FaceId.Floor, 4, 0);
            var worldState = CreateAirborneLandingWorld(targetCell);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(92, targetCell, BoxCapabilities.Push | BoxCapabilities.Flip, EntityPhaseState.Idle, Direction.None, 0, 0, BoxArchetype.Moon));
            var landingTick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).position, Is.EqualTo(fallbackCell), landingTick.Trace.Text);
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Landed));
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_BlockedLandingDoesNotCreateGhostOccupancyOrStaleJumpState()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var blockedCells = FullyBlockedRightFacingLandingCells(sourceCell, targetCell);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, -3, -3), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                }.Concat(blockedCells.Select((cell, index) => CreateWall(90 + index, cell))));
            SeedJumpState(worldState, sourceCell, targetCell, EnemyJumpPhase.Airborne, landingTick: 2);
            var landingTick = CreatePipeline(worldState).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(GetJumpState(worldState).landingTick, Is.GreaterThan(2), "CurrentPolicy: fully blocked landing remains airborne and retries.");
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Retried));
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), targetCell, EnemyId), Is.Zero);
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        [Test]
        [Category("Extended")]
        public void EnemyJump_AstretonProfile_ActivatedBarricadeBlocksExactAndFallbackCells_RetriesWithoutGhostOrOverlap()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 3, 0);
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var barricades = FullyBlockedRightFacingLandingCells(sourceCell, targetCell)
                .Select((cell, index) => CreateTileFeature(130 + index, cell, TileFeatureKind.Barricade))
                .ToArray();
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, new SurfaceCell(FaceId.Floor, -3, -3), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
                },
                initialTileFeatures: barricades);
            SeedJumpState(worldState, sourceCell, targetCell, EnemyJumpPhase.Airborne, landingTick: 2);

            var landingTick = CreatePipeline(worldState, CreateActiveDefinitions(barricades)).RunTick(new TickInput(2));

            Assert.That(GetEntity(worldState, EnemyId).boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(GetJumpState(worldState).phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(GetJumpState(worldState).landingTick, Is.GreaterThan(2));
            Assert.That(landingTick.PresentationData.EnemyJumpSignals.Single().Outcome, Is.EqualTo(TickEnemyJumpPresentationOutcome.Retried));
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), targetCell, EnemyId), Is.Zero);
            AssertNoIllegalUnitSolidOverlap(worldState.CreateSnapshot());
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null,
            params IEntityLogic[] entityLogics)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(JumpChaserProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing Astreton JumpChaser profile at '{JumpChaserProfilePath}'.");

            return GameplayTestRuntimeFactory.CreateDefaultBootstrapper(profile).CreateTickPipeline(
                worldState,
                entityLogics ?? Array.Empty<IEntityLogic>(),
                GameplayTimingProfile.CreateDefault(),
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                unitKinematicLocomotionTiming: CreateOneTickKinematicTiming(),
                tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static void AssertImpactThenTopologyReturnDoesNotReviveStaleLanding(BoxCapabilities capabilities)
        {
            var sourceCell = capabilities == BoxCapabilities.Flip
                ? new SurfaceCell(FaceId.Front, 3, 0)
                : new SurfaceCell(FaceId.Front, 0, 0);
            var targetCell = capabilities == BoxCapabilities.Flip
                ? new SurfaceCell(FaceId.Front, 5, 0)
                : new SurfaceCell(FaceId.Front, 3, 0);
            var worldState = capabilities == BoxCapabilities.Flip
                ? CreateFlipImpactWorld(
                    sourceCell,
                    targetCell,
                    enemyHp: 1,
                    EnemyJumpPhase.Windup,
                    EntityBoardPresence.Occupying,
                    topology: new CubeTopologyState(FaceId.Front))
                : CreateImpactWorld(
                    sourceCell,
                    targetCell,
                    enemyHp: 1,
                    EnemyJumpPhase.Windup,
                    capabilities,
                    EntityBoardPresence.Occupying,
                    topology: new CubeTopologyState(FaceId.Front));
            var pipeline = capabilities == BoxCapabilities.Flip
                ? CreatePipeline(worldState, null, CreateFlipImpactLogic(sourceCell))
                : CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(10));

            AssertNoStaleJumpLanding(worldState, sourceCell, targetCell);
        }

        private static WorldState CreateImpactWorld(
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            int enemyHp,
            EnemyJumpPhase phase,
            BoxCapabilities capabilities,
            EntityBoardPresence boardPresence,
            CubeTopologyState? topology = null)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, targetCell, hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: enemyHp, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: boardPresence),
                    CreateBox(50, sourceCell + new Vector2Int(-1, 0), capabilities, EntityPhaseState.Sliding, Direction.Right, PlayerId, 1),
                },
                topology: topology);
            SeedJumpState(worldState, sourceCell, targetCell, phase, landingTick: phase == EnemyJumpPhase.Airborne ? 5 : 10);
            return worldState;
        }

        private static WorldState CreateFlipImpactWorld(
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            int enemyHp,
            EnemyJumpPhase phase,
            EntityBoardPresence boardPresence,
            CubeTopologyState? topology = null)
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateUnit(PlayerId, 1, sourceCell + new Vector2Int(-1, 0), hp: 3),
                    CreateUnit(EnemyId, 2, sourceCell, hp: enemyHp, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: boardPresence),
                    CreateBox(50, sourceCell + new Vector2Int(-2, 0), BoxCapabilities.Flip, EntityPhaseState.Idle, Direction.None, 0, 0),
                },
                topology: topology);
            SeedJumpState(worldState, sourceCell, targetCell, phase, landingTick: phase == EnemyJumpPhase.Airborne ? 5 : 10);
            return worldState;
        }

        private static WorldState CreateAirborneLandingWorld(SurfaceCell targetCell)
        {
            var sourceCell = new SurfaceCell(targetCell.face, 0, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(PlayerId, 1, new SurfaceCell(targetCell.face, -3, -3), hp: 3),
                CreateUnit(EnemyId, 2, sourceCell, hp: 3, aiMode: EnemyAiMode.Chase, facing: Direction.Right, boardPresence: EntityBoardPresence.Detached),
            });
            SeedJumpState(worldState, sourceCell, targetCell, EnemyJumpPhase.Airborne, landingTick: 2);
            return worldState;
        }

        private static void SeedJumpState(
            WorldState worldState,
            SurfaceCell sourceCell,
            SurfaceCell targetCell,
            EnemyJumpPhase phase,
            int landingTick)
        {
            worldState.CreateWriteContext().SetEnemyJumpState(
                EnemyId,
                new EnemyJumpRuntimeState
                {
                    phase = phase,
                    sequence = 1,
                    sourceCell = sourceCell,
                    lockedTargetCell = targetCell,
                    windupEndTick = phase == EnemyJumpPhase.Windup ? 5 : 0,
                    landingTick = landingTick,
                });
        }

        private static void AssertNoStaleJumpLanding(WorldState worldState, SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(EnemyId, out _), Is.False, "Killed Astreton must be removed before any stale landing can resolve.");
            if (snapshot.TryGetEnemyJumpState(EnemyId, out var jumpState))
            {
                Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.None));
            }

            Assert.That(CountUnitsAt(snapshot, sourceCell, EnemyId), Is.Zero);
            Assert.That(CountUnitsAt(snapshot, targetCell, EnemyId), Is.Zero);
            AssertNoIllegalUnitSolidOverlap(snapshot);
        }

        private static void AssertLandingResolvedWithoutIllegalOverlap(WorldState worldState, SurfaceCell targetCell, TickResult tick)
        {
            var snapshot = worldState.CreateSnapshot();
            AssertNoIllegalUnitSolidOverlap(snapshot);
            if (snapshot.TryGetEntity(EnemyId, out var enemy))
            {
                Assert.That(enemy.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying), tick.Trace.Text);
                Assert.That(CountUnitsAt(snapshot, enemy.position, EnemyId), Is.EqualTo(1));
            }
            else
            {
                Assert.That(CountUnitsAt(snapshot, targetCell, EnemyId), Is.Zero);
            }
        }

        private static void AssertNoIllegalUnitSolidOverlap(WorldSnapshot snapshot)
        {
            var entities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(entities);
            foreach (var entity in entities)
            {
                if (entity.type != EntityType.Unit ||
                    entity.boardPresence != EntityBoardPresence.Occupying)
                {
                    continue;
                }

                Assert.That(
                    snapshot.TryGetSolidSemanticAt(entity.position, out var solid) && solid.Entity.entityId != entity.entityId,
                    Is.False,
                    $"Unit {entity.entityId} illegally overlaps Solid at {entity.position}.");
            }
        }

        private static IReadOnlyList<SurfaceCell> FullyBlockedRightFacingLandingCells(SurfaceCell sourceCell, SurfaceCell targetCell)
        {
            var offsets = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(0, 1),
                new Vector2Int(-1, 0),
                new Vector2Int(2, 0),
                new Vector2Int(1, -1),
                new Vector2Int(1, 1),
                new Vector2Int(0, -2),
                new Vector2Int(0, 2),
                new Vector2Int(-1, -1),
                new Vector2Int(-1, 1),
                new Vector2Int(-2, 0),
            };

            return offsets
                .Select(offset => targetCell + offset)
                .Concat(offsets.Select(offset => sourceCell + offset))
                .Distinct()
                .ToArray();
        }

        private static IEntityLogic CreateFlipImpactLogic(SurfaceCell sourceCell)
        {
            return new SingleTickMovementLogic(
                new RawMovementIntent(
                    PlayerId,
                    priority: 100,
                    (sourceCell + new Vector2Int(-2, 0)).PlanarPosition,
                    MovementCommandKind.Flip,
                    localSequence: 0),
                tickIndex: 1);
        }

        private static void RunUntilNotAirborne(TickPipeline pipeline, WorldState worldState, int startTick, int maxTick)
        {
            for (var tick = startTick; tick <= maxTick; tick++)
            {
                pipeline.RunTick(new TickInput(tick));
                if (GetJumpState(worldState).phase != EnemyJumpPhase.Airborne)
                {
                    return;
                }
            }

            Assert.Fail($"Astreton stayed airborne through tick {maxTick}.");
        }

        private sealed class SingleTickMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _movementIntent;
            private readonly int _tickIndex;

            public SingleTickMovementLogic(RawMovementIntent movementIntent, int tickIndex)
            {
                _movementIntent = movementIntent;
                _tickIndex = tickIndex;
            }

            public int ControlledEntityId => _movementIntent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (input.TickIndex == _tickIndex)
                {
                    buffer.Add(_movementIntent);
                }
            }
        }

        private static UnitKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new UnitKinematicLocomotionTimingSettings
            {
                MoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayWorldStateTestFactory.CreateBounded(
                initialEntities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)),
                topology ?? new CubeTopologyState(FaceId.Floor),
                GameplayTimingProfile.CreateDefault(),
                initialTileFeatures);
        }

        private static EntityState GetEntity(WorldState worldState, int entityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEntity(entityId, out var entity), Is.True);
            return entity;
        }

        private static EnemyJumpRuntimeState GetJumpState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyJumpState(EnemyId, out var jumpState), Is.True);
            return jumpState;
        }

        private static int CountUnitsAt(WorldSnapshot snapshot, SurfaceCell cell, int entityId)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Count(unit => unit.entityId == entityId);
        }

        private static EntityState CreateUnit(
            int entityId,
            int teamId,
            SurfaceCell position,
            int hp,
            EnemyAiMode aiMode = EnemyAiMode.None,
            Direction facing = Direction.Right,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = teamId == 1 ? UnitRole.Player : UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = boardPresence,
                spawnTick = 0,
                aiMode = aiMode,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxCapabilities capabilities,
            EntityPhaseState state,
            Direction facing,
            int kineticInstigatorEntityId,
            int kineticInstigatorTeamId,
            BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = state,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
                boxArchetype = boxArchetype,
                kineticInstigatorEntityId = kineticInstigatorEntityId,
                kineticInstigatorTeamId = kineticInstigatorTeamId,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            int boundEntityId = 0)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: boundEntityId);
        }

        private static TileFeatureRuntimeDefinition[] CreateActiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.Always, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateInactiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.InactiveFaceOnly, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateDefinitions(
            TileFeatureActivationRule activationRule,
            params TileFeatureState[] tileFeatures)
        {
            return tileFeatures
                .Select(tileFeature => new TileFeatureRuntimeDefinition(
                    tileFeature.TileId,
                    activationRule,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: tileFeature.Charges,
                    presentationKey: string.Empty))
                .ToArray();
        }
    }
}
