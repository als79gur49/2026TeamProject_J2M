using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Movement.Intents;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class KinematicLocomotionFacingContractCoreTests
    {
        [Test]
        [Category("Core")]
        public void KinematicTrack_DirectionMatchesPoseFacing_ForLocomotion()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var record = new KinematicPresentationRecord(
                entityId: 10,
                tickIndex: 1,
                operationId: 241,
                actionSequenceId: 0,
                modeBefore: MotionMode.Voluntary,
                modeAfter: MotionMode.Voluntary,
                anchorCellBefore: sourceCell,
                anchorCellAfter: destinationCell,
                localOffsetBefore: KinematicOffset2.Zero,
                localOffsetAfter: KinematicOffset2.Zero,
                velocityBefore: KinematicVelocity2.Zero,
                velocityAfter: KinematicVelocity2.Zero,
                forcedMotionOpAfter: ForcedMotionOp.None,
                hasAuthoritativeStateBefore: true,
                hasAuthoritativeStateAfter: true,
                isSettledAtAnchorBefore: false,
                isSettledAtAnchorAfter: false,
                mutationKind: KinematicMutationKind.ResumeVoluntary,
                source: "KinematicLocomotion",
                reason: "KinematicMotionOutcome",
                kinematicDirection: Direction.Right,
                facingBefore: Direction.Left,
                facingAfter: Direction.Right,
                shouldUpdateFacing: true,
                directionKind: KinematicDirectionKind.AnchorDelta,
                facingPolicy: KinematicFacingPolicy.MatchKinematicDirection);

            var presentationData = BuildKinematicPresentationData(
                sourceCell,
                destinationCell,
                Direction.Left,
                Direction.Right,
                record);

            var track = presentationData.KinematicMotionTracks.Single();
            Assert.That(track.KinematicDirection, Is.EqualTo(Direction.Right));
            Assert.That(track.PoseFacing, Is.EqualTo(Direction.Right));
            Assert.That(track.ShouldUpdateFacing, Is.True);
            Assert.That(track.DirectionKind, Is.EqualTo(KinematicDirectionKind.AnchorDelta));
            Assert.That(track.FacingPolicy, Is.EqualTo(KinematicFacingPolicy.MatchKinematicDirection));
        }

        [Test]
        [Category("Core")]
        public void CombatAction_LocomotionLease_DoubleRelease_IsIdempotent()
        {
            var worldState = CreateWorldState(new[] { CreateEntity(10, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });
            var released = CreateCombatLease(10, sequence: 7, EntityLocomotionLeaseStateKind.Completed);
            released.lastReleaseTick = 11;
            released.lastReleaseReason = EntityLocomotionLeaseReleaseReason.RecoverComplete;
            released.finalReleaseReason = EntityLocomotionLeaseReleaseReason.RecoverComplete;
            var batch = new FinalizationBatch();

            batch.SetEntityLocomotionLeaseState(10, released);
            batch.SetEntityLocomotionLeaseState(10, released);
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out var stored), Is.True);
            Assert.That(stored, Is.EqualTo(released.NormalizedForStorage()));
        }

        [Test]
        [Category("Core")]
        public void KinematicNotSettled_WithoutLeaseOrSettle_IsDefect()
        {
            var worldState = CreateWorldState(new[] { CreateEntity(10, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });
            worldState.CreateWriteContext().SetUnitKinematicState(
                10,
                UnitKinematicRuntimeState.CreateHeldFreeze(CreateVoluntaryKinematic()));

            var diagnostic = EntityLocomotionLeaseDiagnostics.BuildKinematicNotSettledDiagnostic(
                worldState.CreateSnapshot(),
                tickIndex: 3,
                entityId: 10,
                stage: "CoreTest",
                intentId: 99);

            Assert.That(diagnostic, Does.Contain("Operation=OrphanedKinematicNotSettled"));
            Assert.That(diagnostic, Does.Contain("OwnerKind=None"));
            Assert.That(diagnostic, Does.Contain("Policy=CoreTest"));
        }

        [Test]
        [Category("Core")]
        public void Jump_UsesExistingSeparateContract_NotCombatLease()
        {
            var worldState = CreateWorldState(new[] { CreateEnemy(10, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });

            worldState.CreateWriteContext().SetEnemyJumpState(
                10,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Windup,
                    sequence = 2,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0),
                    windupEndTick = 4,
                    landingTick = 7,
                });

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void Charge_UsesExistingSeparateContract_NotCombatLease()
        {
            var worldState = CreateWorldState(new[] { CreateEnemy(10, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });

            worldState.CreateWriteContext().SetEnemyChargeState(
                10,
                new EnemyChargeRuntimeState
                {
                    phase = EnemyChargePhase.Active,
                    sequence = 3,
                    lockedDirection = Direction.Right,
                    remainingActiveSteps = 1,
                });

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void DrSaturn_Utility_DoesNotAcquireCombatLease()
        {
            var worldState = CreateWorldState(new[] { CreateEnemy(10, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Right) });

            worldState.CreateWriteContext().SetEnemyUtilityState(
                10,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            phase = EnemyUtilityEffectPhase.Windup,
                            windupStartTick = 1,
                            windupEndTick = 4,
                            activationSequence = 8,
                        },
                    }));

            Assert.That(worldState.CreateSnapshot().TryGetEntityLocomotionLeaseState(10, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void KinematicSettle_SameAnchor_PreservesFacing()
        {
            var anchorCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var record = new KinematicPresentationRecord(
                entityId: 10,
                tickIndex: 1,
                operationId: 242,
                actionSequenceId: 0,
                modeBefore: MotionMode.Voluntary,
                modeAfter: MotionMode.Settled,
                anchorCellBefore: anchorCell,
                anchorCellAfter: anchorCell,
                localOffsetBefore: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                localOffsetAfter: KinematicOffset2.Zero,
                velocityBefore: new KinematicVelocity2(KinematicFixed.FromRaw(-512), KinematicFixed.Zero),
                velocityAfter: KinematicVelocity2.Zero,
                forcedMotionOpAfter: ForcedMotionOp.None,
                hasAuthoritativeStateBefore: true,
                hasAuthoritativeStateAfter: false,
                isSettledAtAnchorBefore: false,
                isSettledAtAnchorAfter: true,
                mutationKind: KinematicMutationKind.Settle,
                source: "KinematicSettle",
                reason: "Settle",
                kinematicDirection: Direction.None,
                facingBefore: Direction.Up,
                facingAfter: Direction.Up,
                shouldUpdateFacing: false,
                directionKind: KinematicDirectionKind.None,
                facingPolicy: KinematicFacingPolicy.PreserveFacing);

            var presentationData = BuildKinematicPresentationData(
                anchorCell,
                anchorCell,
                Direction.Up,
                Direction.Up,
                record);

            var track = presentationData.KinematicMotionTracks.Single();
            Assert.That(track.PoseFacing, Is.EqualTo(Direction.Up));
            Assert.That(track.ShouldUpdateFacing, Is.False);
            Assert.That(track.FacingPolicy, Is.EqualTo(KinematicFacingPolicy.PreserveFacing));
        }

        [Test]
        [Category("Core")]
        public void KinematicPresentationRecord_CreatedForKinematicLocomotion()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var record = CreateLocomotionRecord(sourceCell, destinationCell, Direction.Right);

            var presentationData = BuildKinematicPresentationData(
                sourceCell,
                destinationCell,
                Direction.Left,
                Direction.Right,
                record);

            Assert.That(presentationData.EntityMotions, Is.Empty);
            var track = presentationData.KinematicMotionTracks.Single();
            Assert.That(track.Source, Is.EqualTo("KinematicLocomotion"));
            Assert.That(track.MutationKind, Is.EqualTo(KinematicMutationKind.ResumeVoluntary));
            Assert.That(track.KinematicDirection, Is.EqualTo(Direction.Right));
            Assert.That(track.PoseFacing, Is.EqualTo(Direction.Right));
        }

        [Test]
        [Category("Core")]
        public void KinematicPresentationRecord_LocomotionPoseFacingMatchesDirection()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 0, 1);
            var record = CreateLocomotionRecord(sourceCell, destinationCell, Direction.Up);

            var presentationData = BuildKinematicPresentationData(
                sourceCell,
                destinationCell,
                Direction.Left,
                Direction.Up,
                record);

            var track = presentationData.KinematicMotionTracks.Single();
            Assert.That(track.KinematicDirection, Is.EqualTo(Direction.Up));
            Assert.That(track.PoseFacing, Is.EqualTo(track.KinematicDirection));
            Assert.That(track.ShouldUpdateFacing, Is.True);
        }

        [Test]
        [Category("Core")]
        public void KinematicPresentationRecord_ReleasePreservesFacing()
        {
            var anchorCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var record = CreateKinematicCleanupRecord(
                KinematicMutationKind.ReleaseHold,
                "KinematicRelease",
                anchorCell,
                Direction.Down);

            var presentationData = BuildKinematicPresentationData(
                anchorCell,
                anchorCell,
                Direction.Down,
                Direction.Down,
                record);

            var track = presentationData.KinematicMotionTracks.Single();
            Assert.That(track.PoseFacing, Is.EqualTo(Direction.Down));
            Assert.That(track.ShouldUpdateFacing, Is.False);
            Assert.That(track.FacingPolicy, Is.EqualTo(KinematicFacingPolicy.PreserveFacing));
            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void KinematicLocomotion_CreatesTickKinematicMotionTrack()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var record = CreateLocomotionRecord(sourceCell, destinationCell, Direction.Right);

            var presentationData = BuildKinematicPresentationData(
                sourceCell,
                destinationCell,
                Direction.Left,
                Direction.Right,
                record);

            Assert.That(presentationData.KinematicMotionTracks, Has.Count.EqualTo(1));
            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void KinematicSettle_CreatesKinematicTrackWithoutEntityMotion()
        {
            var anchorCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var record = CreateKinematicCleanupRecord(
                KinematicMutationKind.Settle,
                "KinematicSettle",
                anchorCell,
                Direction.Left);

            var presentationData = BuildKinematicPresentationData(
                anchorCell,
                anchorCell,
                Direction.Left,
                Direction.Left,
                record);

            Assert.That(presentationData.KinematicMotionTracks, Has.Count.EqualTo(1));
            Assert.That(presentationData.KinematicMotionTracks[0].MutationKind, Is.EqualTo(KinematicMutationKind.Settle));
            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void KinematicOnly_DoesNotCreateEntityMotions()
        {
            var anchorCell = new SurfaceCell(FaceId.Floor, 0, 0);
            var record = CreateKinematicCleanupRecord(
                KinematicMutationKind.ReleaseHold,
                "KinematicRelease",
                anchorCell,
                Direction.Up);

            var presentationData = BuildKinematicPresentationData(
                anchorCell,
                anchorCell,
                Direction.Up,
                Direction.Up,
                record);

            Assert.That(presentationData.EntityMotions, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void KinematicPresentationRecord_LogOnlyForKinematicMutation()
        {
            var source = File.ReadAllText(Path.GetFullPath(
                "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"));

            Assert.That(source, Does.Contain("[KinematicPresentationRecord]"));
            Assert.That(source, Does.Contain("KinematicDirection={record.KinematicDirection}"));
            Assert.That(source, Does.Contain("FacingAfter={record.FacingAfter}"));
            Assert.That(source, Does.Contain("FacingPolicy={record.FacingPolicy}"));
            Assert.That(source, Does.Contain("Source == PoseMutationSource.KinematicLocomotion"));
            Assert.That(source, Does.Contain("request.Kind == PoseMutationKind.KinematicAndFacing"));
        }

        [Test]
        [Category("Core")]
        public void KinematicLocomotion_RightMove_FacesRight()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateEnemy(40, new SurfaceCell(FaceId.Floor, 0, 0), Direction.Left),
            });
            var logic = new ScriptedMovementLogic(
                new RawMovementIntent(40, 50, new Vector2Int(1, 0), MovementCommandKind.Move));
            var tick = CreatePipeline(worldState, logic).RunTick(new TickInput(1));

            var finalEnemy = tick.FinalEntities.Single(entity => entity.entityId == 40);
            Assert.That(finalEnemy.facing, Is.EqualTo(Direction.Right), DescribeTick(tick));
            Assert.That(
                tick.PresentationData.KinematicMotionTracks.Any(track =>
                    track.EntityId == 40 &&
                    track.KinematicDirection == Direction.Right &&
                    track.PoseFacing == Direction.Right &&
                    track.ShouldUpdateFacing),
                Is.True,
                DescribeTick(tick));
        }

        private static string DescribeTick(TickResult tick)
        {
            var tracks = string.Join(
                ";",
                tick.PresentationData.KinematicMotionTracks.Select(track =>
                    $"E={track.EntityId},Dir={track.KinematicDirection},Pose={track.PoseFacing},Update={track.ShouldUpdateFacing},Source={track.Source},Kind={track.MutationKind}"));
            return $"Events=[{string.Join(";", tick.EventLog)}] Tracks=[{tracks}]";
        }

        private static KinematicPresentationRecord CreateLocomotionRecord(
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            Direction direction)
        {
            return new KinematicPresentationRecord(
                entityId: 10,
                tickIndex: 1,
                operationId: 241,
                actionSequenceId: 0,
                modeBefore: MotionMode.Voluntary,
                modeAfter: MotionMode.Voluntary,
                anchorCellBefore: sourceCell,
                anchorCellAfter: destinationCell,
                localOffsetBefore: KinematicOffset2.Zero,
                localOffsetAfter: KinematicOffset2.Zero,
                velocityBefore: KinematicVelocity2.Zero,
                velocityAfter: KinematicVelocity2.Zero,
                forcedMotionOpAfter: ForcedMotionOp.None,
                hasAuthoritativeStateBefore: true,
                hasAuthoritativeStateAfter: true,
                isSettledAtAnchorBefore: false,
                isSettledAtAnchorAfter: false,
                mutationKind: KinematicMutationKind.ResumeVoluntary,
                source: "KinematicLocomotion",
                reason: "KinematicMotionOutcome",
                kinematicDirection: direction,
                facingBefore: Direction.Left,
                facingAfter: direction,
                shouldUpdateFacing: true,
                directionKind: KinematicDirectionKind.AnchorDelta,
                facingPolicy: KinematicFacingPolicy.MatchKinematicDirection);
        }

        private static KinematicPresentationRecord CreateKinematicCleanupRecord(
            KinematicMutationKind mutationKind,
            string source,
            SurfaceCell anchorCell,
            Direction facing)
        {
            return new KinematicPresentationRecord(
                entityId: 10,
                tickIndex: 1,
                operationId: 242,
                actionSequenceId: 0,
                modeBefore: MotionMode.Voluntary,
                modeAfter: mutationKind == KinematicMutationKind.Settle ? MotionMode.Settled : MotionMode.Voluntary,
                anchorCellBefore: anchorCell,
                anchorCellAfter: anchorCell,
                localOffsetBefore: new KinematicOffset2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                localOffsetAfter: KinematicOffset2.Zero,
                velocityBefore: new KinematicVelocity2(KinematicFixed.FromRaw(-512), KinematicFixed.Zero),
                velocityAfter: KinematicVelocity2.Zero,
                forcedMotionOpAfter: ForcedMotionOp.None,
                hasAuthoritativeStateBefore: true,
                hasAuthoritativeStateAfter: mutationKind != KinematicMutationKind.Settle,
                isSettledAtAnchorBefore: false,
                isSettledAtAnchorAfter: mutationKind == KinematicMutationKind.Settle,
                mutationKind: mutationKind,
                source: source,
                reason: source,
                kinematicDirection: Direction.None,
                facingBefore: facing,
                facingAfter: facing,
                shouldUpdateFacing: false,
                directionKind: KinematicDirectionKind.None,
                facingPolicy: KinematicFacingPolicy.PreserveFacing);
        }

        private static TickPresentationData BuildKinematicPresentationData(
            SurfaceCell sourceCell,
            SurfaceCell destinationCell,
            Direction sourceFacing,
            Direction destinationFacing,
            KinematicPresentationRecord record)
        {
            var preMovementSnapshot = CreateWorldState(new[]
            {
                CreateEntity(10, sourceCell, sourceFacing),
            }).CreateSnapshot();
            var postMovementSnapshot = CreateWorldState(new[]
            {
                CreateEntity(10, destinationCell, destinationFacing),
            }).CreateSnapshot();

            return new TickPresentationDataBuilder().Build(
                new TickPresentationBuildContext(
                    preMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    postMovementSnapshot,
                    CreateMovementPhaseResultWithKinematicPresentationRecords(record),
                    AttackPhaseResult.Empty,
                    CleanupPhaseResult.Empty,
                    currentTickIndex: record.TickIndex));
        }

        private static MovementPhaseResult CreateMovementPhaseResultWithKinematicPresentationRecords(
            params KinematicPresentationRecord[] records)
        {
            return new MovementPhaseResult(
                Array.Empty<RawMovementIntent>(),
                Array.Empty<MoveIntent>(),
                Array.Empty<ResolutionRecord>(),
                Array.Empty<ImpactDispositionResolutionRecord>(),
                Array.Empty<FinalizationOperation>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                kinematicPresentationRecords: records ?? Array.Empty<KinematicPresentationRecord>());
        }

        private static TickPipeline CreatePipeline(WorldState worldState, params IEntityLogic[] logics)
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                logics,
                timingProfile,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timingProfile.SimulationTicksPerSecond,
                    timingProfile.RepeatedMoveIntervalSeconds),
                runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                new BoardBounds(new Vector2Int(-1, -1), new Vector2Int(4, 4)),
                GameplayTerrainData.Empty);
        }

        private static EntityState CreateEntity(
            int entityId,
            SurfaceCell position,
            Direction facing)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = facing,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemy(
            int entityId,
            SurfaceCell position,
            Direction facing)
        {
            var entity = CreateEntity(entityId, position, facing);
            entity.teamId = 2;
            entity.aiMode = EnemyAiMode.Chase;
            return entity;
        }

        private static EntityLocomotionLeaseState CreateCombatLease(
            int entityId,
            int sequence,
            EntityLocomotionLeaseStateKind stateKind)
        {
            return new EntityLocomotionLeaseState
            {
                leaseId = entityId * 100000 + sequence,
                entityId = entityId,
                ownerKind = EntityLocomotionLeaseOwnerKind.CombatAction,
                stateKind = stateKind,
                ownerActionSequenceId = sequence,
                capturedKinematic = CreateVoluntaryKinematic(),
                anchorAtAcquire = new SurfaceCell(FaceId.Floor, 0, 0),
                acquiredTick = 2,
                lastReleaseTick = stateKind == EntityLocomotionLeaseStateKind.Completed ? 3 : 0,
                lastReleaseReason = stateKind == EntityLocomotionLeaseStateKind.Completed
                    ? EntityLocomotionLeaseReleaseReason.RecoverComplete
                    : EntityLocomotionLeaseReleaseReason.None,
                finalReleaseReason = stateKind == EntityLocomotionLeaseStateKind.Completed
                    ? EntityLocomotionLeaseReleaseReason.RecoverComplete
                    : EntityLocomotionLeaseReleaseReason.None,
            };
        }

        private static UnitKinematicRuntimeState CreateVoluntaryKinematic()
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new KinematicOffset2(KinematicFixed.FromRaw(1024), KinematicFixed.Zero),
                velocity = new KinematicVelocity2(KinematicFixed.FromRaw(512), KinematicFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = KinematicFixed.UnitsPerCell - 1024,
                remainingTicks = 8,
                speedScalePermille = 1000,
                sequenceId = 5,
                elapsedTicks = 2,
                totalTicks = 10,
                commitTick = 1,
                startedTick = 0,
                stepDirectionX = 1,
                stepDirectionY = 0,
            }.NormalizedForStorage();
        }

        private sealed class ScriptedMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _intent;

            public ScriptedMovementLogic(RawMovementIntent intent)
            {
                _intent = intent;
            }

            public int ControlledEntityId => _intent.SourceId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                buffer.Add(_intent);
            }
        }
    }
}
