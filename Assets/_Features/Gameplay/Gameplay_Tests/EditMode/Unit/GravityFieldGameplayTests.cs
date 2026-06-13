using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.GravityFieldAudio;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GravityFieldGameplayTests
    {
        private static readonly BoardBounds Bounds = new(new Vector2Int(-4, -4), new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void GravityFieldBox_StartsChargingWithEightSecondTimer()
        {
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -2, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField),
            });

            Assert.That(TryGetEntity(worldState, 20, out var gravityField), Is.True);
            Assert.That(gravityField.type, Is.EqualTo(EntityType.Box));
            Assert.That(gravityField.boxArchetype, Is.EqualTo(BoxArchetype.GravityField));
            Assert.That(gravityField.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(gravityField.gravityFieldTimerTicks, Is.EqualTo(8 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
        }

        [Test]
        [Category("Core")]
        public void GravityField_ChargingReachesActiveAndLocksSameTickBeforePushStart()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 1),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState)
                .RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));

            Assert.That(TryGetEntity(worldState, 30, out var emitter), Is.True);
            Assert.That(emitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Active));
            Assert.That(emitter.gravityFieldTimerTicks, Is.EqualTo(3 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out var lockState), Is.True);
            Assert.That(lockState.BlocksPush, Is.True);
            Assert.That(lockState.BlocksFlip, Is.True);
            Assert.That(lockState.BlocksDestroy, Is.True);
            Assert.That(lockState.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.GravityField));
            var snapshot = worldState.CreateSnapshot();
            Assert.That(TryGetEntity(worldState, 10, out var player), Is.True);
            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, 1, out _), Is.False);
            Assert.That(TryGetEntity(worldState, 20, out var target), Is.True);
            Assert.That(target.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.PresentationData.GravityFieldEvents, Has.Count.EqualTo(2));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EventKind, Is.EqualTo(GravityFieldPresentationEventKind.Activated));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EmitterEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.GravityFieldEvents[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(result.PresentationData.GravityFieldEvents[0].TargetEntityId, Is.Zero);
            AssertLockedBoxEvent(
                result.PresentationData.GravityFieldEvents[1],
                emitterEntityId: 30,
                targetEntityId: 20,
                emitterCell: new SurfaceCell(FaceId.Floor, 1, 1),
                targetCell: new SurfaceCell(FaceId.Floor, 1, 0));
        }

        [Test]
        [Category("Core")]
        public void GravityField_ActiveLocksOnlySameFaceThreeByThreeStationaryBoxes()
        {
            var sliding = CreateBox(23, new SurfaceCell(FaceId.Floor, 1, -1), BoxArchetype.Normal, BoxCapabilities.Push);
            sliding.state = EntityPhaseState.Sliding;
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(21, new SurfaceCell(FaceId.Floor, 3, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(22, new SurfaceCell(FaceId.Back, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                sliding,
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(20, 1, out _), Is.True);
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(30, 1, out _), Is.False);
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(21, 1, out _), Is.False);
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(22, 1, out _), Is.False);
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(23, 1, out _), Is.False);
            Assert.That(result.PresentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            Assert.That(
                result.PresentationData.GravityFieldVisualStates[0].AreaCells.ToArray(),
                Is.EqualTo(new[]
                {
                    new SurfaceCell(FaceId.Floor, -1, -1),
                    new SurfaceCell(FaceId.Floor, 0, -1),
                    new SurfaceCell(FaceId.Floor, 1, -1),
                    new SurfaceCell(FaceId.Floor, -1, 0),
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    new SurfaceCell(FaceId.Floor, 1, 0),
                    new SurfaceCell(FaceId.Floor, -1, 1),
                    new SurfaceCell(FaceId.Floor, 0, 1),
                    new SurfaceCell(FaceId.Floor, 1, 1),
                }));
            Assert.That(
                result.PresentationData.GravityFieldVisualStates[0].LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
            Assert.That(
                LockedBoxTargetIds(result.PresentationData.GravityFieldEvents),
                Is.EqualTo(new[] { 20 }));

            var presentationRequests = new GravityFieldPresentationRequestPlanner().BuildRequests(result.PresentationData);
            Assert.That(
                presentationRequests.Select(request => request.TargetEntityId).ToArray(),
                Is.EqualTo(new[] { 20 }));
            Assert.That(
                new GravityFieldAudioRequestPlanner().BuildRequests(presentationRequests)
                    .Select(request => request.Cue)
                    .ToArray(),
                Is.EqualTo(new[] { GravityFieldAudioCue.LockedBox }));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualState_LockedTargetIdsAreDeterministicAndExcludeIneligibleTargets()
        {
            var sliding = CreateBox(45, new SurfaceCell(FaceId.Floor, 1, -1), BoxArchetype.Normal, BoxCapabilities.Push);
            sliding.state = EntityPhaseState.Sliding;
            var dead = CreateBox(46, new SurfaceCell(FaceId.Floor, -1, 1), BoxArchetype.Normal, BoxCapabilities.Push);
            dead.hp = 0;
            var marked = CreateBox(47, new SurfaceCell(FaceId.Floor, 0, 1), BoxArchetype.Normal, BoxCapabilities.Push);
            marked.markedForDeath = true;
            var detached = CreateBox(48, new SurfaceCell(FaceId.Floor, 1, 1), BoxArchetype.Normal, BoxCapabilities.Push);
            detached.boardPresence = EntityBoardPresence.Detached;

            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(40, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(20, new SurfaceCell(FaceId.Floor, -1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, -1), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(41, new SurfaceCell(FaceId.Floor, 3, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(42, new SurfaceCell(FaceId.Back, 0, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                sliding,
                dead,
                marked,
                detached,
                CreateProjectile(49, new SurfaceCell(FaceId.Floor, -1, -1)),
                CreateBox(50, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(result.PresentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            Assert.That(
                result.PresentationData.GravityFieldVisualStates[0].LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20, 30, 40 }));
        }

        [Test]
        [Category("Core")]
        public void GravityField_ActiveTimerExpiryReturnsToChargingWithoutApplyingLock()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 1),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(TryGetEntity(worldState, 30, out var emitter), Is.True);
            Assert.That(emitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(emitter.gravityFieldTimerTicks, Is.EqualTo(8 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out _), Is.False);
            Assert.That(result.PresentationData.GravityFieldEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EventKind, Is.EqualTo(GravityFieldPresentationEventKind.Expired));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EmitterEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.GravityFieldEvents[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 0, 0)));
            Assert.That(result.PresentationData.GravityFieldEvents[0].TargetEntityId, Is.Zero);
            Assert.That(result.PresentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.GravityFieldVisualStates[0].LockedTargetEntityIds, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityField_IneligibleEmitterResetsChargingAndDoesNotLock()
        {
            var emitter = CreateBox(
                30,
                new SurfaceCell(FaceId.Floor, 0, 0),
                BoxArchetype.GravityField,
                BoxCapabilities.Push,
                GravityFieldPhase.Active,
                timerTicks: 2);
            emitter.state = EntityPhaseState.Sliding;
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                emitter,
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(TryGetEntity(worldState, 30, out var updatedEmitter), Is.True);
            Assert.That(updatedEmitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(updatedEmitter.gravityFieldTimerTicks, Is.EqualTo(8 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out _), Is.False);
            Assert.That(result.PresentationData.GravityFieldEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityField_FrontFaceEmitterSuspendsTimerAndDoesNotLockOrShowVisuals()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Front, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Front, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(TryGetEntity(worldState, 30, out var updatedEmitter), Is.True);
            Assert.That(updatedEmitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Active));
            Assert.That(updatedEmitter.gravityFieldTimerTicks, Is.EqualTo(2));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(30, 1, out _), Is.False);
            Assert.That(result.PresentationData.GravityFieldEvents, Is.Empty);
            var visualState = result.PresentationData.GravityFieldVisualStates.Single();
            Assert.That(visualState.Phase, Is.EqualTo(GravityFieldPhase.None));
            Assert.That(visualState.AreaCells, Is.Empty);
            Assert.That(visualState.LockedTargetEntityIds, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityField_ChargingOffBottomSuspendsThenActivatesWhenBottomReturns()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 1),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
            var suspended = pipeline.RunTick(new TickInput(1));

            Assert.That(TryGetEntity(worldState, 30, out var suspendedEmitter), Is.True);
            Assert.That(suspendedEmitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(suspendedEmitter.gravityFieldTimerTicks, Is.EqualTo(1));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out _), Is.False);
            Assert.That(suspended.PresentationData.GravityFieldEvents, Is.Empty);
            Assert.That(suspended.PresentationData.GravityFieldVisualStates.Single().Phase, Is.EqualTo(GravityFieldPhase.None));
            Assert.That(suspended.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds, Is.Empty);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            var resumed = pipeline.RunTick(new TickInput(2));

            Assert.That(TryGetEntity(worldState, 30, out var resumedEmitter), Is.True);
            Assert.That(resumedEmitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Active));
            Assert.That(resumedEmitter.gravityFieldTimerTicks, Is.EqualTo(3 * GameplayTimingProfile.DefaultSimulationTicksPerSecond));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 2, out _), Is.True);
            Assert.That(resumed.PresentationData.GravityFieldEvents[0].EventKind, Is.EqualTo(GravityFieldPresentationEventKind.Activated));
            Assert.That(
                resumed.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
        }

        [Test]
        [Category("Core")]
        public void GravityField_ActiveOffBottomSuspendsAndResumesWithoutRepeatingLockedBoxOneShot()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 5),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstActive = pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Back));
            var suspended = pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            var resumed = pipeline.RunTick(new TickInput(3));

            Assert.That(LockedBoxTargetIds(firstActive.PresentationData.GravityFieldEvents), Is.EqualTo(new[] { 20 }));
            Assert.That(TryGetEntity(worldState, 30, out var resumedEmitter), Is.True);
            Assert.That(resumedEmitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Active));
            Assert.That(resumedEmitter.gravityFieldTimerTicks, Is.EqualTo(3));
            Assert.That(suspended.PresentationData.GravityFieldEvents, Is.Empty);
            Assert.That(suspended.PresentationData.GravityFieldVisualStates.Single().Phase, Is.EqualTo(GravityFieldPhase.None));
            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 3, out _), Is.True);
            Assert.That(
                resumed.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
            Assert.That(LockedBoxTargetIds(resumed.PresentationData.GravityFieldEvents), Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityField_TimerDecrementWithoutTransition_CreatesNoPresentationEvent()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 2),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(TryGetEntity(worldState, 30, out var emitter), Is.True);
            Assert.That(emitter.gravityFieldPhase, Is.EqualTo(GravityFieldPhase.Charging));
            Assert.That(emitter.gravityFieldTimerTicks, Is.EqualTo(1));
            Assert.That(result.PresentationData.GravityFieldEvents, Is.Empty);
            Assert.That(result.PresentationData.GravityFieldVisualStates, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.GravityFieldVisualStates[0].LockedTargetEntityIds, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldVisualState_LockedTargetIdsPersistDuringActiveWindow_ClearOnExpiry_AndRepopulateOnNextActivation()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 3),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstActive = pipeline.RunTick(new TickInput(1));
            var secondActive = pipeline.RunTick(new TickInput(2));
            var expired = pipeline.RunTick(new TickInput(3));

            Assert.That(
                firstActive.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
            Assert.That(
                secondActive.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
            Assert.That(expired.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds, Is.Empty);

            var nextWindowWorldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 1),
            });

            var nextWindow = GameplayCompositionRoot.CreateTickPipeline(nextWindowWorldState).RunTick(new TickInput(4));

            Assert.That(
                nextWindow.PresentationData.GravityFieldVisualStates.Single().LockedTargetEntityIds.ToArray(),
                Is.EqualTo(new[] { 20 }));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBox_ActiveWindowDebouncesRetainedLeaveReenterAndReopensNextWindow()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 5),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstActive = pipeline.RunTick(new TickInput(1));
            var retained = pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().MoveEntity(20, new SurfaceCell(FaceId.Floor, 4, 4));
            var left = pipeline.RunTick(new TickInput(3));
            worldState.CreateWriteContext().MoveEntity(20, new SurfaceCell(FaceId.Floor, 1, 0));
            var reentered = pipeline.RunTick(new TickInput(4));
            worldState.CreateWriteContext().SetGravityFieldState(30, GravityFieldPhase.Charging, timerTicks: 1);
            var nextWindow = pipeline.RunTick(new TickInput(5));

            Assert.That(LockedBoxTargetIds(firstActive.PresentationData.GravityFieldEvents), Is.EqualTo(new[] { 20 }));
            Assert.That(LockedBoxTargetIds(retained.PresentationData.GravityFieldEvents), Is.Empty);
            Assert.That(LockedBoxTargetIds(left.PresentationData.GravityFieldEvents), Is.Empty);
            Assert.That(LockedBoxTargetIds(reentered.PresentationData.GravityFieldEvents), Is.Empty);
            Assert.That(LockedBoxTargetIds(nextWindow.PresentationData.GravityFieldEvents), Is.EqualTo(new[] { 20 }));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBox_MultipleEmittersDebounceIndependentlyAndOrderByEmitterThenTarget()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(21, new SurfaceCell(FaceId.Floor, 2, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 3),
                CreateBox(31, new SurfaceCell(FaceId.Floor, 2, 1), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 3),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstActive = pipeline.RunTick(new TickInput(1));
            var retained = pipeline.RunTick(new TickInput(2));

            Assert.That(
                firstActive.PresentationData.GravityFieldEvents
                    .Where(evt => evt.EventKind == GravityFieldPresentationEventKind.LockedBox)
                    .Select(evt => (evt.EmitterEntityId, evt.TargetEntityId))
                    .ToArray(),
                Is.EqualTo(new[]
                {
                    (30, 20),
                    (31, 20),
                    (31, 21),
                }));
            Assert.That(LockedBoxTargetIds(retained.PresentationData.GravityFieldEvents), Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBox_IneligibleTargetsDoNotEmit()
        {
            var marked = CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push);
            marked.markedForDeath = true;
            var detached = CreateBox(21, new SurfaceCell(FaceId.Floor, -1, 0), BoxArchetype.Normal, BoxCapabilities.Push);
            detached.boardPresence = EntityBoardPresence.Detached;
            var sliding = CreateBox(22, new SurfaceCell(FaceId.Floor, 0, 1), BoxArchetype.Normal, BoxCapabilities.Push);
            sliding.state = EntityPhaseState.Sliding;
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                marked,
                detached,
                sliding,
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            var result = GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(LockedBoxTargetIds(result.PresentationData.GravityFieldEvents), Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBox_IneligibleEmitterClearsMemory()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 4),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(worldState);

            var firstActive = pipeline.RunTick(new TickInput(1));
            ((IMovementCommitContext)worldState.CreateWriteContext()).ApplyStateChange(30, EntityPhaseState.Sliding, stateTimer: 1);
            var ineligible = pipeline.RunTick(new TickInput(2));
            ((IMovementCommitContext)worldState.CreateWriteContext()).ApplyStateChange(30, EntityPhaseState.Idle, stateTimer: 0);
            worldState.CreateWriteContext().SetGravityFieldState(30, GravityFieldPhase.Active, timerTicks: 2);
            var activeAgain = pipeline.RunTick(new TickInput(3));

            Assert.That(LockedBoxTargetIds(firstActive.PresentationData.GravityFieldEvents), Is.EqualTo(new[] { 20 }));
            Assert.That(LockedBoxTargetIds(ineligible.PresentationData.GravityFieldEvents), Is.Empty);
            Assert.That(LockedBoxTargetIds(activeAgain.PresentationData.GravityFieldEvents), Is.EqualTo(new[] { 20 }));
        }

        [Test]
        [Category("Core")]
        public void GravityField_LockedPushDestroyBoxDoesNotSelfDestroyOnBlockedFallback()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push | BoxCapabilities.Destroy),
                    CreateBox(21, new SurfaceCell(FaceId.Floor, 2, 0), BoxArchetype.Normal, BoxCapabilities.Flip),
                    CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
                });

            GameplayCompositionRoot.CreateTickPipeline(worldState)
                .RunTick(new TickInput(1, PlayerTickCommand.Push(Direction.Right)));

            Assert.That(TryGetEntity(worldState, 20, out var target), Is.True);
            Assert.That(target.markedForDeath, Is.False);
            Assert.That(target.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Core")]
        public void GravityField_ActiveLockBlocksFlipStart()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Flip),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            GameplayCompositionRoot.CreateTickPipeline(worldState)
                .RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));

            var snapshot = worldState.CreateSnapshot();
            Assert.That(TryGetEntity(worldState, 10, out var player), Is.True);
            Assert.That(PlayerControlQueries.TryResolveFlipTarget(snapshot, player, Direction.Right, 1, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void GravityField_OverlappingFieldsMergeLocksDeterministically()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
                CreateBox(31, new SurfaceCell(FaceId.Floor, 2, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            GameplayCompositionRoot.CreateTickPipeline(worldState).RunTick(new TickInput(1));

            Assert.That(worldState.CreateSnapshot().TryGetActiveBoxInteractionLockState(20, 1, out var lockState), Is.True);
            Assert.That(lockState.SourceEntityId, Is.EqualTo(30));
            Assert.That(lockState.BlocksPush, Is.True);
            Assert.That(lockState.BlocksFlip, Is.True);
            Assert.That(lockState.BlocksDestroy, Is.True);
        }

        [Test]
        [Category("Core")]
        public void GravityField_AuthoritativePhaseTimerAndLockAffectDeterminismHash()
        {
            var baseline = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 2),
            });
            var active = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
            });

            var baselineResult = GameplayCompositionRoot.CreateTickPipeline(baseline).RunTick(new TickInput(1));
            var activeResult = GameplayCompositionRoot.CreateTickPipeline(active).RunTick(new TickInput(1));

            Assert.That(activeResult.DeterminismHash, Is.Not.EqualTo(baselineResult.DeterminismHash));
            Assert.That(activeResult.Trace.Text, Does.Contain("GravityFieldPhase=Active"));
            Assert.That(activeResult.Trace.Text, Does.Contain("BlocksDestroy=1"));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldPresentationEvents_DoNotEnterDeterminismHashDirectly()
        {
            var worldState = CreateWorldState(new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 2),
            });
            var snapshot = worldState.CreateSnapshot();
            var finalEntities = new[]
            {
                CreatePlayer(10, new SurfaceCell(FaceId.Floor, -4, -4)),
                CreateBox(30, new SurfaceCell(FaceId.Floor, 0, 0), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Charging, timerTicks: 2),
            };
            var baseline = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                TickPresentationData.Empty);
            var withPresentationEvent = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                CreateGravityFieldPresentationData(new GravityFieldPresentationEvent(
                    GravityFieldPresentationEventKind.LockedBox,
                    30,
                    new SurfaceCell(FaceId.Floor, 0, 0),
                    targetEntityId: 20,
                    lockedBoxPayload: new GravityFieldLockedBoxPayload(
                        30,
                        20,
                        new SurfaceCell(FaceId.Floor, 0, 0),
                        new SurfaceCell(FaceId.Floor, 1, 0)))));
            var withVisualState = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                CreateGravityFieldPresentationData(
                    new[]
                    {
                        new GravityFieldVisualState(
                            30,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            GravityFieldPhase.Charging,
                            timerTicks: 2,
                            durationTicks: 8 * GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                            progress01: 0.5f,
                            areaFootprint: new GravityFieldAreaFootprint(
                                new[]
                                {
                                    new SurfaceCell(FaceId.Floor, -1, -1),
                                    new SurfaceCell(FaceId.Floor, 0, -1),
                                    new SurfaceCell(FaceId.Floor, 1, -1),
                                    new SurfaceCell(FaceId.Floor, -1, 0),
                                    new SurfaceCell(FaceId.Floor, 0, 0),
                                    new SurfaceCell(FaceId.Floor, 1, 0),
                                    new SurfaceCell(FaceId.Floor, -1, 1),
                                    new SurfaceCell(FaceId.Floor, 0, 1),
                                    new SurfaceCell(FaceId.Floor, 1, 1),
                                },
                                slotVisibilityMask: 0x1FF)),
                    }));
            var withLockedTargetReadModel = new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>(),
                CreateGravityFieldPresentationData(
                    new[]
                    {
                        new GravityFieldVisualState(
                            30,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            GravityFieldPhase.Active,
                            timerTicks: 2,
                            durationTicks: 3 * GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                            progress01: 0.5f,
                            areaFootprint: GravityFieldAreaFootprint.Empty,
                            lockedTargetEntityIds: new[] { 20, 30 }),
                    }));
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(1, snapshot, withPresentationEvent),
                Is.EqualTo(hashBuilder.Build(1, snapshot, baseline)));
            Assert.That(
                hashBuilder.Build(1, snapshot, withVisualState),
                Is.EqualTo(hashBuilder.Build(1, snapshot, baseline)));
            Assert.That(
                hashBuilder.Build(1, snapshot, withLockedTargetReadModel),
                Is.EqualTo(hashBuilder.Build(1, snapshot, baseline)));
        }

        [Test]
        [Category("Core")]
        public void GravityFieldLockedBoxEventRequestAndAudioCue_AreOpenedForOneShotFeedback()
        {
            Assert.That(Enum.GetNames(typeof(GravityFieldPresentationEventKind)), Does.Contain("LockedBox"));
            Assert.That(Enum.GetNames(typeof(GravityFieldPresentationRequestKind)), Does.Contain("LockedBox"));
            Assert.That(Enum.GetNames(typeof(GravityFieldAudioCue)), Does.Contain("LockedBox"));
        }

        private static int[] LockedBoxTargetIds(IReadOnlyList<GravityFieldPresentationEvent> events)
        {
            return events
                .Where(evt => evt.EventKind == GravityFieldPresentationEventKind.LockedBox)
                .Select(evt => evt.TargetEntityId)
                .ToArray();
        }

        private static void AssertLockedBoxEvent(
            GravityFieldPresentationEvent evt,
            int emitterEntityId,
            int targetEntityId,
            SurfaceCell emitterCell,
            SurfaceCell targetCell)
        {
            Assert.That(evt.EventKind, Is.EqualTo(GravityFieldPresentationEventKind.LockedBox));
            Assert.That(evt.EmitterEntityId, Is.EqualTo(emitterEntityId));
            Assert.That(evt.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(evt.Cell, Is.EqualTo(emitterCell));
            Assert.That(evt.LockedBoxPayload.IsValid, Is.True);
            Assert.That(evt.LockedBoxPayload.EmitterEntityId, Is.EqualTo(emitterEntityId));
            Assert.That(evt.LockedBoxPayload.TargetEntityId, Is.EqualTo(targetEntityId));
            Assert.That(evt.LockedBoxPayload.EmitterCell, Is.EqualTo(emitterCell));
            Assert.That(evt.LockedBoxPayload.TargetCell, Is.EqualTo(targetCell));
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return CreateGravityFieldPresentationData(
                Array.Empty<GravityFieldVisualState>(),
                gravityFieldEvents);
        }

        private static TickPresentationData CreateGravityFieldPresentationData(
            IReadOnlyList<GravityFieldVisualState> gravityFieldVisualStates,
            params GravityFieldPresentationEvent[] gravityFieldEvents)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
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
                Array.Empty<TickEnemyChargePresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<TickImpactTransientPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates);
        }

        private static WorldState CreateWorldState(EntityState[] entities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                Bounds,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreatePlayer(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateBox(
            int entityId,
            SurfaceCell position,
            BoxArchetype archetype,
            BoxCapabilities capabilities = BoxCapabilities.None,
            GravityFieldPhase phase = GravityFieldPhase.None,
            int timerTicks = 0)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = capabilities,
                boxArchetype = archetype,
                gravityFieldPhase = phase,
                gravityFieldTimerTicks = timerTicks,
            };
        }

        private static EntityState CreateProjectile(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 2,
                type = EntityType.Projectile,
                unitRole = UnitRole.None,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static bool TryGetEntity(WorldState worldState, int entityId, out EntityState entity)
        {
            var entities = new System.Collections.Generic.List<EntityState>();
            worldState.CreateSnapshot().EnumerateEntitiesOrdered(entities);
            entity = entities.FirstOrDefault(candidate => candidate.entityId == entityId);
            return entity.entityId == entityId;
        }
    }
}
