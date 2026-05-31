using System;
using System.Collections.Generic;
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
