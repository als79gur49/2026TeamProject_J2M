using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BlockAudio;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Objectives;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests
{
    public sealed class BlockAudioRuntimeTests
    {
        [Test]
        public void Planner_BuildsFlipLandingCue_ForFlipMotion()
        {
            var planner = new BlockAudioRequestPlanner();
            var motion = new TickEntityMotion(
                entityId: 10,
                TickEntityMotionKind.Flip,
                Cell(0, 0),
                Cell(1, 0));

            var requests = planner.BuildRequests(
                CreateTickResult(CreatePresentationData(entityMotions: new[] { motion })),
                CreateTimingProfile(flipMotionDurationSeconds: 0.5f));

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(BlockAudioCue.FlipLanding));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(10));
            Assert.That(requests[0].DelaySeconds, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void Planner_BuildsFlipLandingCue_ForFlipImpactStayOnly()
        {
            var planner = new BlockAudioRequestPlanner();
            var stay = new FlipImpactPresentationSignal(
                sourceActionPlanId: 100,
                boxEntityId: 11,
                impactTargetEntityId: 21,
                actorEntityId: 1,
                sourceCell: Cell(0, 0),
                impactCell: Cell(1, 0),
                new CubeTopologyState(FaceId.Floor),
                Direction.Right,
                Direction.Right,
                FlipImpactPresentationDisposition.Stay);
            var destroySelf = new FlipImpactPresentationSignal(
                sourceActionPlanId: 101,
                boxEntityId: 12,
                impactTargetEntityId: 22,
                actorEntityId: 1,
                sourceCell: Cell(0, 1),
                impactCell: Cell(1, 1),
                new CubeTopologyState(FaceId.Floor),
                Direction.Right,
                Direction.Right,
                FlipImpactPresentationDisposition.DestroySelf);

            var requests = planner.BuildRequests(
                CreateTickResult(CreatePresentationData(flipImpactSignals: new[] { stay, destroySelf })),
                CreateTimingProfile(flipMotionDurationSeconds: 0.5f));

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(BlockAudioCue.FlipLanding));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(11));
            Assert.That(requests[0].DelaySeconds, Is.EqualTo(0.31f).Within(0.0001f));
        }

        [Test]
        public void Planner_BuildsCrashCue_ForSlidingContinuationStoppedBySolidEntityOnly()
        {
            var planner = new BlockAudioRequestPlanner();
            var solidStop = new BoxSlideStopPresentationSignal(
                boxEntityId: 20,
                sourceCell: Cell(2, 0),
                stopperCell: Cell(3, 0),
                Direction.Right,
                BoxSlideStopperKind.SolidEntity,
                stopperEntityId: 30,
                SolidKind.Wall,
                new CubeTopologyState(FaceId.Floor),
                BoxSlideStopCause.SlidingContinuationBlocked);
            var terrainStop = new BoxSlideStopPresentationSignal(
                boxEntityId: 21,
                sourceCell: Cell(2, 1),
                stopperCell: Cell(3, 1),
                Direction.Right,
                BoxSlideStopperKind.Terrain,
                stopperEntityId: 0,
                SolidKind.Wall,
                new CubeTopologyState(FaceId.Floor),
                BoxSlideStopCause.SlidingContinuationBlocked);
            var barricadeStop = new BoxSlideStopPresentationSignal(
                boxEntityId: 22,
                sourceCell: Cell(2, 2),
                stopperCell: Cell(3, 2),
                Direction.Right,
                BoxSlideStopperKind.Barricade,
                stopperEntityId: 0,
                SolidKind.Wall,
                new CubeTopologyState(FaceId.Floor),
                BoxSlideStopCause.SlidingContinuationBlocked,
                stopperTileId: 100);

            var requests = planner.BuildRequests(
                CreateTickResult(CreatePresentationData(boxSlideStopSignals: new[] { solidStop, terrainStop, barricadeStop })),
                CreateTimingProfile(flipMotionDurationSeconds: 0.5f));

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(BlockAudioCue.BoxSlideSolidStop));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(20));
            Assert.That(requests[0].DelaySeconds, Is.Zero);
        }

        [Test]
        public void Planner_BuildsBoxSlideStartedCue_ForBoxSlideStartSignal()
        {
            var planner = new BlockAudioRequestPlanner();
            var boxSlideStart = new BoxSlideStartPresentationSignal(
                boxEntityId: 20,
                actorEntityId: 10,
                sourceCell: Cell(1, 0),
                destinationCell: Cell(2, 0),
                new CubeTopologyState(FaceId.Floor));

            var requests = planner.BuildRequests(
                CreateTickResult(CreatePresentationData(boxSlideStartSignals: new[] { boxSlideStart })),
                CreateTimingProfile(flipMotionDurationSeconds: 0.5f));

            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].Cue, Is.EqualTo(BlockAudioCue.BoxSlideStarted));
            Assert.That(requests[0].OwnerEntityId, Is.EqualTo(20));
            Assert.That(requests[0].DelaySeconds, Is.Zero);
        }

        private static TickPresentationData CreatePresentationData(
            IReadOnlyList<TickEntityMotion> entityMotions = null,
            IReadOnlyList<FlipImpactPresentationSignal> flipImpactSignals = null,
            IReadOnlyList<BoxSlideStopPresentationSignal> boxSlideStopSignals = null,
            IReadOnlyList<BoxSlideStartPresentationSignal> boxSlideStartSignals = null)
        {
            return new TickPresentationData(
                entityMotions ?? Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(),
                Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                flipImpactSignals ?? Array.Empty<FlipImpactPresentationSignal>(),
                boxSlideStopSignals: boxSlideStopSignals ?? Array.Empty<BoxSlideStopPresentationSignal>(),
                boxSlideStartSignals: boxSlideStartSignals ?? Array.Empty<BoxSlideStartPresentationSignal>());
        }

        private static TickResult CreateTickResult(TickPresentationData presentationData)
        {
            return new TickResult(
                1,
                new[] { TickPhase.Plan },
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                new CubeTopologyState(FaceId.Floor),
                presentationData,
                string.Empty,
                TickTrace.Empty,
                StageObjectiveTickResult.NoObjective);
        }

        private static GameplayTimingProfile CreateTimingProfile(float flipMotionDurationSeconds)
        {
            return new GameplayTimingProfile(
                simulationTicksPerSecond: 30,
                initialMoveDelaySeconds: 0f,
                repeatedMoveIntervalSeconds: 0.1f,
                boxSlideStepIntervalSeconds: 0.1f,
                projectileStepIntervalSeconds: 0.1f,
                moveMotionDurationSeconds: 0.1f,
                pushMotionDurationSeconds: 0.1f,
                topologyMotionDurationSeconds: 0.1f,
                flipMotionDurationSeconds: flipMotionDurationSeconds,
                flipArcHeightInCells: 1f,
                maxTicksPerFrame: 4,
                itemConsumeEffectDurationSeconds: 0.1f,
                boxDestroyEffectDurationSeconds: 0.1f,
                enemyDeathEffectDurationSeconds: 0.1f);
        }

        private static SurfaceCell Cell(int x, int y)
        {
            return new SurfaceCell(FaceId.Floor, x, y);
        }
    }
}
