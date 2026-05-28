using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
using Game.Feature.Gameplay.Objectives;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class FlipB1HostileImpactScenarioTests
    {
        [Test]
        [Category("Extended")]
        [Ignore("Legacy same-tick hostile Flip impact characterization; Sprint 2 B-1 now schedules contact instead.")]
        public void CurrentFlipHostileImpact_ExecuteTick_CreatesImpactReservation()
        {
            var result = RunPlayerFlipImpact(hp: 3, out _, out _);

            CollectionAssert.AreEqual(
                new[]
                {
                    (SourceId: 20, TargetId: 30, Position: new SurfaceCell(FaceId.Floor, -1, 0), Damage: 1),
                },
                result.AttackPhaseResult
                    .DrainedImpactReservations
                    .Select(reservation => (
                        reservation.SourceId,
                        reservation.TargetId,
                        reservation.ImpactCell,
                        reservation.Damage))
                    .ToArray());
        }

        [Test]
        [Category("Extended")]
        [Ignore("Legacy same-tick hostile Flip impact characterization; Sprint 2 B-1 now defers damage.")]
        public void CurrentFlipHostileImpact_ExecuteTick_AppliesDamageSameTick()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        [Ignore("Legacy same-tick hostile Flip impact characterization; Sprint 2 B-1 leaves disposition for due resolver.")]
        public void CurrentFlipHostileImpact_TargetSurvives_ResolvesDestroySelfSameTick()
        {
            var result = RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            Assert.That(result.PresentationData.PlayerActionSignals.Single().FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.DestroySelf));
            Assert.That(result.PresentationData.FlipImpactSignals.Single().Disposition, Is.EqualTo(FlipImpactPresentationDisposition.DestroySelf));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        [Ignore("Legacy same-tick hostile Flip impact characterization; Sprint 2 B-1 leaves disposition for due resolver.")]
        public void CurrentFlipHostileImpact_TargetDies_ResolvesFollowThroughSameTick()
        {
            var result = RunPlayerFlipImpact(hp: 1, out var worldState, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(result.PresentationData.PlayerActionSignals.Single().FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.FollowThrough));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        [Ignore("Legacy same-tick hostile Flip impact characterization; Sprint 2 B-1 leaves disposition for due resolver.")]
        public void CurrentFlipHostileImpact_TargetDiesLandingDenied_ResolvesStaySameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateUnit(entityId: 12, position: new Vector2Int(-1, 1), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 1, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new StubMovementLogic(
                        new RawMovementIntent(
                            sourceId: 12,
                            priority: 200,
                            destination: new Vector2Int(-1, 0),
                            MovementCommandKind.Move,
                            localSequence: 0),
                        tickIndex: 2),
                    new PlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(result.PresentationData.PlayerActionSignals.Single().FlipOutcome, Is.EqualTo(TickPlayerFlipOutcomeKind.Stay));
            Assert.That(result.PresentationData.FlipImpactSignals.Single().Disposition, Is.EqualTo(FlipImpactPresentationDisposition.Stay));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_NoImpactReservation()
        {
            var result = RunPlayerFlipImpact(hp: 3, out _, out _);

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.AttackPhaseResult.FrozenMovementReservationExport.ImpactReservations, Is.Empty);
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(evt => evt.StartsWith("ImpactReservationCreated|")),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_NoAttackInputNormalizerSyntheticAttack()
        {
            var result = RunPlayerFlipImpact(hp: 3, out _, out _);

            Assert.That(result.AttackPhaseResult.FrozenMovementReservationExport.ImpactReservations, Is.Empty);
            Assert.That(result.AttackPhaseResult.ResolutionRecords, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_SchedulesContact()
        {
            var result = RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var contacts = GetScheduledContacts(worldState);

            Assert.That(contacts, Has.Count.EqualTo(1));
            Assert.That(
                result.MovementPhaseResult.CommitEvents.Any(evt => evt.StartsWith("FlipB1ContactScheduled|")),
                Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_SourceBoxBecomesInFlight()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(snapshot.TryGetResolvedSpatialState(20, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.BoxInFlight));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_DoesNotDamageTarget()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_DoesNotMarkTargetForDeath()
        {
            RunPlayerFlipImpact(hp: 1, out var worldState, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out var target), Is.True);
            Assert.That(target.markedForDeath, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_DoesNotRemoveTarget()
        {
            RunPlayerFlipImpact(hp: 1, out var worldState, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_SourceBoxNoLongerSolidOccupant()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(snapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ActualInput_DueTickIsActionVisualImpactTick()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var contact = GetScheduledContacts(worldState).Single();
            var expectedDelayTicks =
                GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault());

            Assert.That(contact.ActionStartTick, Is.EqualTo(1));
            Assert.That(contact.ExecuteTick, Is.EqualTo(1 + PlayerControlTimingSettings.DefaultFlipExecuteDelayTicksAtDefaultSimulationRate));
            Assert.That(contact.FlipInputLockDurationTicks, Is.EqualTo(PlayerControlTimingSettings.DefaultFlipInputLockDurationTicksAtDefaultSimulationRate));
            Assert.That(expectedDelayTicks, Is.EqualTo(53));
            Assert.That(contact.DueTick, Is.Not.EqualTo(contact.ExecuteTick + GameplayTimingProfile.DefaultFlipContactDelayTicks));
            Assert.That(contact.DueTick, Is.EqualTo(contact.ActionStartTick + expectedDelayTicks));
            Assert.That(
                (contact.DueTick - contact.ActionStartTick) / (float)contact.FlipInputLockDurationTicks,
                Is.EqualTo(0.936f).Within(0.01f));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_NoTargetReservation()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var contact = GetScheduledContacts(worldState).Single();

            Assert.That(contact.ActionId, Is.GreaterThan(0));
            Assert.That(contact.ActorEntityId, Is.EqualTo(10));
            Assert.That(contact.SourceBoxEntityId, Is.EqualTo(20));
            Assert.That(contact.ContactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(contact.LandingCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(contact.DamageSpec.DamageAmount, Is.EqualTo(1));
            Assert.That(contact.DamageSpec.SourceKind, Is.EqualTo(AttackSourceKind.B1ScheduledContactDue));
            Assert.That(contact.DamageSpec.SourceActionId, Is.EqualTo(contact.ActionId));
            Assert.That(typeof(ScheduledFlipContact).GetProperty("TargetEntityId"), Is.Null);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_DoesNotEmitObjectiveProgress()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, unitRole: UnitRole.Player),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 1, teamId: 2, unitRole: UnitRole.Enemy),
            });
            var pipeline = CreatePipeline(worldState, objectiveDefinition: CreateAllEnemiesDefeatedObjective());

            var startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = RunUntilExecute(pipeline);

            Assert.That(startTick.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(executeTick.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_NoDamageOnExecuteTick()
        {
            var executeTick = RunPlayerFlipImpact(hp: 1, out var worldState, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(executeTick.PresentationData.FlipDueContactSignals, Is.Empty);
            Assert.That(executeTick.PresentationData.EnemyDamageSignals, Is.Empty);
            Assert.That(executeTick.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 30), Is.False);
            Assert.That(executeTick.PresentationData.FlipFloorImpactSignals, Is.Empty);
            Assert.That(snapshot.TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(1));
            Assert.That(target.markedForDeath, Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(GetScheduledContacts(worldState), Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ActualInput_NoDamageAtCurrentBroken0614Timing()
        {
            var pipeline = CreateDefaultImpactPipeline(hp: 1, out var worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = RunUntilExecute(pipeline);
            TickResult brokenTimingTick = executeTick;
            for (var tick = executeTick.TickIndex + 1; tick <= 1 + 35; tick++)
            {
                brokenTimingTick = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
            }

            var snapshot = worldState.CreateSnapshot();
            var contact = GetScheduledContacts(worldState).Single();
            Assert.That(brokenTimingTick.PresentationData.FlipDueContactSignals, Is.Empty);
            Assert.That(brokenTimingTick.PresentationData.EnemyDamageSignals, Is.Empty);
            Assert.That(brokenTimingTick.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 30), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(1));
            Assert.That(target.markedForDeath, Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(contact.ExecuteTick, Is.EqualTo(1 + PlayerControlTimingSettings.DefaultFlipExecuteDelayTicksAtDefaultSimulationRate));
            Assert.That(contact.DueTick, Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(contact.DueTick, Is.GreaterThan(brokenTimingTick.TickIndex));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_DoesNotUseMotionDuration()
        {
            var timingProfile = CreateTimingProfile(flipMotionDurationSeconds: 0.05f);
            var pipeline = CreateDefaultImpactPipeline(3, timingProfile, out var worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);

            var contact = GetScheduledContacts(worldState).Single();
            Assert.That(contact.ExecuteTick, Is.EqualTo(1 + PlayerControlTimingSettings.DefaultFlipExecuteDelayTicksAtDefaultSimulationRate));
            Assert.That(contact.DueTick, Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(contact.DueTick, Is.Not.EqualTo(contact.ExecuteTick + 6));

            var creationSlice = ExtractScheduledContactCreationSlice(
                ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs"));
            Assert.That(creationSlice, Does.Not.Contain("FlipMotionDuration"));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_DoesNotUseMotionTrackStartDelay()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs");
            var creationSlice = ExtractScheduledContactCreationSlice(source);

            Assert.That(creationSlice, Does.Not.Contain("StartDelay"));
            Assert.That(creationSlice, Does.Not.Contain("MotionTrack"));
            Assert.That(creationSlice, Does.Contain("actionStartTick + actionVisualImpactDelayTicks"));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_DoesNotUseSamplerSlamEndTime()
        {
            var source = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Movement/Runtime/Expansion/MovementExpander.cs");
            var creationSlice = ExtractScheduledContactCreationSlice(source);

            Assert.That(creationSlice, Does.Not.Contain("BoxFlipSlamSampler"));
            Assert.That(creationSlice, Does.Not.Contain("SlamEndTime"));
            Assert.That(creationSlice, Does.Not.Contain("FlipImpactInteractionOnsetNormalizedTime"));
            Assert.That(creationSlice, Does.Not.Contain("SecondsToCeilTicks"));
            Assert.That(creationSlice, Does.Contain("actionStartTick + actionVisualImpactDelayTicks"));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_NoEnemySuppression()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 3, teamId: 2),
            });
            var pipeline = CreatePipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(10),
                    new StubMovementLogic(
                        new RawMovementIntent(
                            sourceId: 30,
                            priority: 200,
                            destination: new Vector2Int(-2, 0),
                            MovementCommandKind.Move,
                            localSequence: 0),
                        tickIndex: 25),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var preDueMoveTick = pipeline.RunTick(new TickInput(25, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var target), Is.True);
            Assert.That(target.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -2, 0)));
            Assert.That(target.hp, Is.EqualTo(3));
            Assert.That(preDueMoveTick.PresentationData.EnemyDamageSignals, Is.Empty);
            Assert.That(GetScheduledContacts(worldState), Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_RemovesScheduledContact()
        {
            RunPlayerFlipImpactToDue(hp: 3, out var worldState, out _, out var dueTick);

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.EventLog.Any(entry => entry.StartsWith("FlipB1Removed|")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void TickPresentationData_ContainsB1DueContactFactWithImmediateTiming()
        {
            RunPlayerFlipImpactToDue(hp: 3, out _, out _, out var dueTick);

            var signal = dueTick.PresentationData.FlipDueContactSignals.Single();

            Assert.That(signal.SourceActionPlanId, Is.GreaterThan(0));
            Assert.That(signal.BoxEntityId, Is.EqualTo(20));
            Assert.That(signal.HitEntityId, Is.EqualTo(30));
            Assert.That(signal.ContactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(signal.BoxDisposition, Is.EqualTo(FlipBoxDisposition.DestroySelf));
            Assert.That(signal.TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(signal.VisualContactNormalizedTime, Is.EqualTo(0f));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ActualInput_DamageOnlyAtVisualImpactDueTick()
        {
            RunPlayerFlipImpactToDue(hp: 1, out var worldState, out var executeTick, out var dueTick);

            Assert.That(
                dueTick.TickIndex,
                Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(dueTick.PresentationData.EnemyDamageSignals.Single().EntityId, Is.EqualTo(30));
            Assert.That(dueTick.PresentationData.EntityExitSignals.Any(signal => signal.ExitedEntityId == 30), Is.True);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostileFlipB1_StillHitsAtVisualImpactDue()
        {
            RunPlayerFlipImpactToDue(hp: 3, out var worldState, out _, out var dueTick);

            Assert.That(
                dueTick.TickIndex,
                Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ActualInput_DamagePathIsB1ScheduledContactDue()
        {
            RunPlayerFlipImpactToDue(hp: 1, out _, out _, out var dueTick);

            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("DamagePath=B1ScheduledContactDue")), Is.True);
            Assert.That(dueTick.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(dueTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(dueTick.AttackPhaseResult.DamageResolutions, Is.Empty);
            Assert.That(
                dueTick.MovementPhaseResult.CommitEvents.Any(entry => entry.StartsWith("ImpactReservationCreated|")),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_RuntimeProfile_UsesActionVisualImpactDelay()
        {
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds);
            var dueDelayFromActionStart = GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(playerControlTiming);

            Assert.That(playerControlTiming.FlipExecuteDelayTicks, Is.EqualTo(23));
            Assert.That(playerControlTiming.FlipInputLockDurationTicks, Is.EqualTo(57));
            Assert.That(GameplayFlipMotionTiming.VisualSlamContactNormalizedTime, Is.EqualTo(0.936f).Within(0.0001f));
            Assert.That(dueDelayFromActionStart, Is.EqualTo(53));
            Assert.That(dueDelayFromActionStart - playerControlTiming.FlipExecuteDelayTicks, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1Presentation_DueDamageAndDeathUseImmediateExitOwnedTail()
        {
            RunPlayerFlipImpactToDue(hp: 1, out _, out _, out var dueTick);

            Assert.That(dueTick.PresentationData.EnemyDamageSignals.Single().EntityId, Is.EqualTo(30));
            var exitSignal = dueTick.PresentationData.EntityExitSignals.Single(signal => signal.ExitedEntityId == 30);
            Assert.That(exitSignal.ExitCause, Is.EqualTo(TickEntityExitCause.EnemyDeath));
            Assert.That(exitSignal.Timing, Is.EqualTo(EntityExitPresentationTiming.Immediate));
            Assert.That(exitSignal.TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(exitSignal.VisualContactNormalizedTime, Is.EqualTo(0f));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1Vfx_DueTick_ContactVfxUsesContactCellWithoutLegacyDelay()
        {
            RunPlayerFlipImpactToDue(hp: 1, out _, out _, out var dueTick);

            var signal = dueTick.PresentationData.FlipFloorImpactSignals.Single();

            Assert.That(signal.ContactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(signal.TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(signal.VisualContactNormalizedTime, Is.EqualTo(0f));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1Presentation_DueDestroySelf_BoxExitUsesImmediateDueFact()
        {
            RunPlayerFlipImpactToDue(hp: 3, out _, out _, out var dueTick);

            var boxExit = dueTick.PresentationData.EntityExitSignals.Single(signal => signal.ExitedEntityId == 20);

            Assert.That(boxExit.ExitCause, Is.EqualTo(TickEntityExitCause.BoxDestroy));
            Assert.That(boxExit.Timing, Is.EqualTo(EntityExitPresentationTiming.Immediate));
            Assert.That(boxExit.TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(boxExit.SourceCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_CurrentSnapshotRequeryAtDue()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(31, new Vector2Int(-1, 0), hp: 3, teamId: 2));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var original), Is.True);
            Assert.That(original.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(31, out var entered), Is.True);
            Assert.That(entered.hp, Is.EqualTo(2));
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Occupant=31")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_OriginalHostileMovedAway_EmptyLand()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(snapshot.TryGetEntity(30, out var original), Is.True);
            Assert.That(original.hp, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_FriendlyEntered_BlocksNoDamage()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(31, new Vector2Int(-1, 0), hp: 3, teamId: 1));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(31, out var friendly), Is.True);
            Assert.That(friendly.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_SolidEntered_BlocksAndReturnsOrDestroys()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(31, new Vector2Int(-1, 0), BoxCapabilities.Push));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(31, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_HostileSurvives_SourceBoxDestroySelf()
        {
            RunPlayerFlipImpactToDue(hp: 3, out var worldState, out _, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var target), Is.True);
            Assert.That(target.hp, Is.EqualTo(2));
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_HostileDies_SettlementAllowed_FollowThrough()
        {
            RunPlayerFlipImpactToDue(hp: 1, out var worldState, out _, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_HostileDies_SettlementDenied_SourceFree_Stay()
        {
            var pipeline = CreateDefaultImpactPipeline(hp: 1, out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(29, new Vector2Int(-1, 0), hp: 1, teamId: 2));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(29, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out var secondary), Is.True);
            Assert.That(secondary.hp, Is.EqualTo(1));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_DestroySelfDoesNotRequireDestroyCapability()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 3, teamId: 2),
            });
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            RunUntilDue(pipeline);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_MultipleUnits_HitsDeterministicPrimaryOnly()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(29, new Vector2Int(-1, 0), hp: 3, teamId: 2));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(29, out var primary), Is.True);
            Assert.That(primary.hp, Is.EqualTo(2));
            Assert.That(snapshot.TryGetEntity(30, out var secondary), Is.True);
            Assert.That(secondary.hp, Is.EqualTo(3));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_DeadEnemyDoesNotMoveSameTick()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 1, teamId: 2),
            });
            var dueTickIndex = 1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(
                GameplayTimingProfile.CreateDefault());
            var pipeline = CreatePipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(10),
                    new StubMovementLogic(
                        new RawMovementIntent(
                            sourceId: 30,
                            priority: 200,
                            destination: new Vector2Int(-2, 0),
                            MovementCommandKind.Move,
                            localSequence: 0),
                        dueTickIndex),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var dueTick = RunUntilDue(pipeline);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.False);
            Assert.That(dueTick.MovementPhaseResult.RawIntents.Any(intent => intent.SourceId == 30), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_LastEnemyKilled_ObjectiveCompletesAfterDueCleanup()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, unitRole: UnitRole.Player),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: 1, teamId: 2, unitRole: UnitRole.Enemy),
            });
            var pipeline = CreatePipeline(worldState, objectiveDefinition: CreateAllEnemiesDefeatedObjective());

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = RunUntilExecute(pipeline);
            var dueTick = RunUntilDue(pipeline);

            Assert.That(executeTick.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(dueTick.ObjectiveResult.ClearedThisTick, Is.True);
            Assert.That(dueTick.EventLog.Any(entry => entry == "CleanupRemoved|E=30"), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_ActorGone_SafeReturnOrDestroy()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            ((IAttackCommitContext)worldState.CreateWriteContext()).ApplyDamage(10, 99);

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            if (snapshot.TryGetEntity(20, out var sourceBox))
            {
                Assert.That(sourceBox.boardPresence, Is.Not.EqualTo(EntityBoardPresence.InFlight));
            }
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_BoxGone_RemovesTokenNoOrphan()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().RemoveEntity(20);

            var dueTick = RunUntilDue(pipeline);

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Reason=BoxGone")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_BoxNotInFlight_RemovesTokenNoOrphan()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SetBoardPresence(20, EntityBoardPresence.Occupying);

            var dueTick = RunUntilDue(pipeline);

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Reason=BoxNotInFlight")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_ExecuteTick_DoesNotMaterializeLandingImmediately()
        {
            RunOrdinaryFlipToExecute(out var worldState, out var result);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(result.AttackPhaseResult.FrozenMovementReservationExport.GetCellStatus(new SurfaceCell(FaceId.Floor, -1, 0)), Is.EqualTo(ReservationStatus.None));
            Assert.That(result.PresentationData.FlipB1InFlightMotionSignals.Single().BoxEntityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(GetScheduledContacts(worldState).Single().Kind, Is.EqualTo(ScheduledFlipContactKind.OrdinaryLanding));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_ExecuteTick_BoxBecomesInFlight()
        {
            RunOrdinaryFlipToExecute(out var worldState, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(GetScheduledContacts(worldState).Single().Kind, Is.EqualTo(ScheduledFlipContactKind.OrdinaryLanding));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_ExecuteTick_LandingCellRemainsUnreserved()
        {
            RunOrdinaryFlipToExecute(out _, out var result);

            Assert.That(result.AttackPhaseResult.FrozenMovementReservationExport.GetCellStatus(new SurfaceCell(FaceId.Floor, -1, 0)), Is.EqualTo(ReservationStatus.None));
            Assert.That(result.AttackPhaseResult.FrozenMovementReservationExport.ImpactReservations, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_ExecuteTick_SourceCellNoLongerSolidOccupant()
        {
            RunOrdinaryFlipToExecute(out var worldState, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
            Assert.That(snapshot.TryGetSolidSemanticAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_EmptyLanding_MaterializesBox()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);

            Assert.That(worldState.CreateSnapshot().TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out _), Is.False);

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_EmptyLanding_AppliesFacingReset()
        {
            RunOrdinaryFlipToDue(out var worldState, out _, out _);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var box), Is.True);
            Assert.That(box.facing, Is.EqualTo(Direction.Left));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_EmptyLanding_RemovesScheduledToken()
        {
            RunOrdinaryFlipToDue(out var worldState, out _, out var dueTick);

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.EventLog.Any(entry => entry.StartsWith("FlipB1Removed|")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_EmptyLanding_ClosesBoxInFlight()
        {
            RunOrdinaryFlipToDue(out var worldState, out _, out _);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, 1, 0), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_NoMaterializeAtOld0614()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var executeTick = RunUntilExecute(pipeline);
            TickResult brokenTimingTick = executeTick;
            for (var tick = executeTick.TickIndex + 1; tick <= 1 + 35; tick++)
            {
                brokenTimingTick = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
            }

            var snapshot = worldState.CreateSnapshot();
            var contact = GetScheduledContacts(worldState).Single();

            Assert.That(brokenTimingTick.PresentationData.FlipDueContactSignals, Is.Empty);
            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out _), Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(contact.Kind, Is.EqualTo(ScheduledFlipContactKind.OrdinaryLanding));
            Assert.That(contact.DueTick, Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(contact.DueTick, Is.GreaterThan(brokenTimingTick.TickIndex));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_MaterializesAtActionNorm0930()
        {
            RunOrdinaryFlipToDue(out var worldState, out _, out var dueTick);

            Assert.That(
                dueTick.TickIndex,
                Is.EqualTo(1 + GameplayFlipMotionTiming.ResolveB1VisualImpactDelayTicks(GameplayTimingProfile.CreateDefault())));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().TimingMode, Is.EqualTo(GameplayPresentationTimingMode.DueContactImmediate));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.EmptyLand));
            Assert.That(worldState.CreateSnapshot().TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out var occupant), Is.True);
            Assert.That(occupant.entityId, Is.EqualTo(20));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_HostileEnteredLanding_UsesCurrentHostile()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 3, teamId: 2));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var hostile), Is.True);
            Assert.That(hostile.hp, Is.EqualTo(2));
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("DamagePath=B1ScheduledContactDue")), Is.True);
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Occupant=30")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_OriginalEnemyLeft_DoesNotHitOriginalEnemy()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 3, teamId: 2));
            writeContext.MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var original), Is.True);
            Assert.That(original.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_DifferentHostileEntered_HitsDifferentHostile()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 3, teamId: 2));
            writeContext.MoveEntity(30, new SurfaceCell(FaceId.Floor, -2, 0));
            writeContext.SpawnEntity(CreateUnit(31, new Vector2Int(-1, 0), hp: 3, teamId: 2));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var original), Is.True);
            Assert.That(original.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(31, out var current), Is.True);
            Assert.That(current.hp, Is.EqualTo(2));
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Occupant=31")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_HostileDies_SettlementAllowed_FollowThrough()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 1, teamId: 2));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(sourceBox.facing, Is.EqualTo(Direction.Left));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingHostileKilledFollowThrough));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_HostileDies_SettlementDenied_SourceFree_StaysAtSource()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 1, teamId: 2));
            writeContext.SpawnEntity(CreateUnit(31, new Vector2Int(-1, 0), hp: 3, teamId: 2));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(31, out var remainingHostile), Is.True);
            Assert.That(remainingHostile.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingHostileKilledSourceFallback));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_FriendlyEntered_BlocksNoDamage()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateUnit(30, new Vector2Int(-1, 0), hp: 3, teamId: 1));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out var friendly), Is.True);
            Assert.That(friendly.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("DamagePath=B1ScheduledContactDue") && entry.Contains("Occupant=30")), Is.False);
            Assert.That(dueTick.PresentationData.EnemyDamageSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_PlayerEntered_BlocksNoDamage()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().MoveEntity(10, new SurfaceCell(FaceId.Floor, -1, 0));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(10, out var player), Is.True);
            Assert.That(player.hp, Is.EqualTo(3));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.AttackPhaseResult.RawIntents, Is.Empty);
            Assert.That(dueTick.PresentationData.PlayerDamageSignals, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_SolidEntered_Blocks()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateBox(30, new Vector2Int(-1, 0), BoxCapabilities.Push));

            RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(30, out _), Is.True);
            Assert.That(snapshot.TryGetSolidOccupantAt(new SurfaceCell(FaceId.Floor, -1, 0), out var landingOccupant), Is.True);
            Assert.That(landingOccupant.entityId, Is.EqualTo(30));
            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_BlockedLanding_SourceFree_StaysAtSource()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            worldState.CreateWriteContext().SpawnEntity(CreateBox(30, new Vector2Int(-1, 0), BoxCapabilities.Push));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingSolidBlockSourceFallback));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_BlockedLanding_SourceBlocked_DestroySelf()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            var writeContext = worldState.CreateWriteContext();
            writeContext.SpawnEntity(CreateBox(30, new Vector2Int(-1, 0), BoxCapabilities.Push));
            writeContext.SpawnEntity(CreateBox(31, new Vector2Int(1, 0), BoxCapabilities.Push));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out _), Is.True);
            Assert.That(snapshot.TryGetEntity(31, out _), Is.True);
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingSolidBlockDestroySelf));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_InvalidLanding_SourceFree_CancelsToSource()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            ReplaceOrdinaryLandingCell(worldState, new SurfaceCell(FaceId.Floor, 99, 99));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 0)));
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingInvalidSourceFallback));
        }

        [Test]
        [Category("Extended")]
        public void OrdinaryFlipB1_DueTick_InvalidLanding_SourceBlocked_DestroySelf()
        {
            var pipeline = CreateOrdinaryFlipPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            RunUntilExecute(pipeline);
            ReplaceOrdinaryLandingCell(worldState, new SurfaceCell(FaceId.Floor, 99, 99));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(30, new Vector2Int(1, 0), BoxCapabilities.Push));

            var dueTick = RunUntilDue(pipeline);
            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out _), Is.True);
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(dueTick.PresentationData.FlipDueContactSignals.Single().ResolutionKind, Is.EqualTo(FlipContactResolutionKind.OrdinaryLandingInvalidDestroySelf));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_FriendlyBlockedFlip_DoesNotUseB1HostilePath()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), teamId: 1),
            });
            var pipeline = CreatePipeline(
                worldState,
                new IEntityLogic[] { CreatePlayerLogic(10) });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var result = RunUntilExecute(pipeline);

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(result.PresentationData.FlipB1InFlightMotionSignals, Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        private static TickResult RunPlayerFlipImpact(int hp, out WorldState worldState, out TickResult startTick)
        {
            var pipeline = CreateDefaultImpactPipeline(hp, out worldState);

            startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            return RunUntilExecute(pipeline);
        }

        private static void RunOrdinaryFlipToExecute(out WorldState worldState, out TickResult executeTick)
        {
            var pipeline = CreateOrdinaryFlipPipeline(out worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            executeTick = RunUntilExecute(pipeline);
        }

        private static void RunOrdinaryFlipToDue(
            out WorldState worldState,
            out TickResult executeTick,
            out TickResult dueTick)
        {
            var pipeline = CreateOrdinaryFlipPipeline(out worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            executeTick = RunUntilExecute(pipeline);
            dueTick = RunUntilDue(pipeline);
        }

        private static TickPipeline CreateOrdinaryFlipPipeline(out WorldState worldState)
        {
            worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            return CreatePipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(10),
                });
        }

        private static void RunPlayerFlipImpactToDue(
            int hp,
            out WorldState worldState,
            out TickResult executeTick,
            out TickResult dueTick)
        {
            var pipeline = CreateDefaultImpactPipeline(hp, out worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            executeTick = RunUntilExecute(pipeline);
            dueTick = RunUntilDue(pipeline);
        }

        private static TickPipeline CreateDefaultImpactPipeline(out WorldState worldState)
        {
            return CreateDefaultImpactPipeline(3, out worldState);
        }

        private static TickPipeline CreateDefaultImpactPipeline(int hp, out WorldState worldState)
        {
            return CreateDefaultImpactPipeline(hp, GameplayTimingProfile.CreateDefault(), out worldState);
        }

        private static TickPipeline CreateDefaultImpactPipeline(
            int hp,
            GameplayTimingProfile timingProfile,
            out WorldState worldState)
        {
            worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: hp, teamId: 2),
            });
            return CreatePipeline(
                worldState,
                new IEntityLogic[]
                {
                    CreatePlayerLogic(10, timingProfile),
                },
                timingProfile: timingProfile);
        }

        private static TickResult RunUntilDue(TickPipeline pipeline)
        {
            TickResult result = null;
            for (var tick = PlayerControlTimingSettings.DefaultFlipExecuteDelayTicksAtDefaultSimulationRate + 2; tick <= 80; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
                if (result.PresentationData.FlipDueContactSignals.Count > 0)
                {
                    return result;
                }
            }

            return result;
        }

        private static TickResult RunUntilExecute(TickPipeline pipeline)
        {
            TickResult result = null;
            var executeTick = 1 + PlayerControlTimingSettings.DefaultFlipExecuteDelayTicksAtDefaultSimulationRate;
            for (var tick = 2; tick <= executeTick; tick++)
            {
                result = pipeline.RunTick(new TickInput(tick, PlayerTickCommand.None));
            }

            return result;
        }

        private static List<ScheduledFlipContact> GetScheduledContacts(WorldState worldState)
        {
            var contacts = new List<ScheduledFlipContact>();
            worldState.CreateSnapshot().EnumerateDueScheduledFlipContactsOrdered(int.MaxValue, contacts);
            return contacts;
        }

        private static void ReplaceOrdinaryLandingCell(WorldState worldState, SurfaceCell landingCell)
        {
            var contact = GetScheduledContacts(worldState).Single();
            Assert.That(contact.Kind, Is.EqualTo(ScheduledFlipContactKind.OrdinaryLanding));
            var writeContext = worldState.CreateWriteContext();
            writeContext.RemoveScheduledFlipContact(contact.ActionId);
            writeContext.AddScheduledFlipContact(
                new ScheduledFlipContact(
                    contact.Kind,
                    contact.ActionId,
                    contact.ActorEntityId,
                    contact.SourceBoxEntityId,
                    contact.SourceCell,
                    contact.ContactCell,
                    landingCell,
                    contact.FlipDirection,
                    contact.SourceFace,
                    contact.SourceCapabilitiesSnapshot,
                    contact.DamageSpec,
                    contact.KineticInstigatorEntityId,
                    contact.KineticInstigatorTeamId,
                    contact.ActionStartTick,
                    contact.ActionVisualImpactTick,
                    contact.FlipExecuteDelayTicks,
                    contact.FlipInputLockDurationTicks,
                    contact.ExecuteTick,
                    contact.DueTick,
                    contact.OrderingKey,
                    contact.CancellationPolicy,
                    contact.DispositionPolicy));
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null,
            GameplayTimingProfile timingProfile = null)
        {
            var timing = timingProfile ?? GameplayTimingProfile.CreateDefault();
            var playerControlTiming = PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                timing.SimulationTicksPerSecond,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds);
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogics ?? new IEntityLogic[] { CreatePlayerLogic(10, playerControlTiming) },
                timing,
                playerControlTiming,
                objectiveDefinition: objectiveDefinition);
        }

        private static PlayerLogic CreatePlayerLogic(
            int entityId,
            GameplayTimingProfile timingProfile = null)
        {
            var timing = timingProfile ?? GameplayTimingProfile.CreateDefault();
            return CreatePlayerLogic(
                entityId,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timing.SimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds));
        }

        private static PlayerLogic CreatePlayerLogic(
            int entityId,
            PlayerControlTimingAuthoritativeSnapshot playerControlTiming)
        {
            return new PlayerLogic(
                entityId,
                playerControlTiming.PushWindupTicks,
                playerControlTiming.PushRecoveryTicks,
                playerControlTiming.FlipWindupTicks,
                playerControlTiming.FlipRecoveryTicks);
        }

        private static GameplayTimingProfile CreateTimingProfile(float flipMotionDurationSeconds)
        {
            return new GameplayTimingProfile(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
                GameplayTimingProfile.DefaultBoxSlideStepIntervalSeconds,
                GameplayTimingProfile.DefaultProjectileStepIntervalSeconds,
                GameplayTimingProfile.DefaultMoveMotionDurationSeconds,
                GameplayTimingProfile.DefaultPushMotionDurationSeconds,
                GameplayTimingProfile.DefaultTopologyMotionDurationSeconds,
                flipMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipArcHeightInCells,
                GameplayTimingProfile.DefaultMaxTicksPerFrame,
                GameplayTimingProfile.DefaultItemConsumeEffectDurationSeconds,
                GameplayTimingProfile.DefaultBoxDestroyEffectDurationSeconds,
                GameplayTimingProfile.DefaultMoveOccupancyDurationSeconds,
                GameplayTimingProfile.DefaultEnemyDeathEffectDurationSeconds,
                GameplayTimingProfile.DefaultPlayerDeathDisplacementDurationSeconds,
                GameplayTimingProfile.DefaultPlayerDeathDisplacementDistanceInCells,
                GameplayTimingProfile.DefaultPlayerDeathDisplacementCameraBiasWeight,
                GameplayTimingProfile.DefaultMoonBlockEmergenceDurationSeconds,
                GameplayTimingProfile.DefaultFlipContactDelayTicks);
        }

        private static string ExtractScheduledContactCreationSlice(string source)
        {
            const string startMarker = "new ScheduledFlipContactDraft(";
            const string endMarker = "FlipContactDispositionPolicy.DefaultB1HostileImpact";
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return source.Substring(start, end - start);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(absolutePath);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EntityState CreateUnit(
            int entityId,
            Vector2Int position,
            int hp = 3,
            int teamId = 1,
            Direction facing = Direction.Right,
            UnitRole unitRole = UnitRole.None)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = facing,
            };
        }

        private static EntityState CreateBox(int entityId, Vector2Int position, BoxCapabilities capabilities)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boxCapabilities = capabilities,
            };
        }

        private static StageObjectiveRuntimeDefinition CreateAllEnemiesDefeatedObjective()
        {
            return new StageObjectiveRuntimeDefinition(
                StageCompletionPolicy.RequireAllConditions,
                playerEntityId: 10,
                zones: new[]
                {
                    new StageZoneRuntimeDefinition(
                        "unused",
                        FaceId.Floor,
                        new[] { new StageZoneRuntimeRegion(Vector2Int.zero, Vector2Int.zero) }),
                },
                conditionEntries: new[]
                {
                    new StageObjectiveConditionRuntimeDefinitionEntry(
                        new AllEnemiesDefeatedConditionRuntimeDefinition(),
                        required: true,
                        StageObjectiveConditionRole.PrimaryGoal,
                        "all-enemies-defeated"),
                });
        }

        private sealed class AllEnemiesDefeatedConditionRuntimeDefinition : StageConditionRuntimeDefinition
        {
            public AllEnemiesDefeatedConditionRuntimeDefinition()
                : base("all-enemies-defeated", "All Enemies Defeated")
            {
            }

            public override IStageConditionRuntime CreateRuntime()
            {
                return new Runtime(ConditionId, DisplayName);
            }

            private sealed class Runtime : IStageConditionRuntime
            {
                private readonly string _conditionId;
                private readonly string _displayName;

                public Runtime(string conditionId, string displayName)
                {
                    _conditionId = conditionId;
                    _displayName = displayName;
                }

                public bool IsSatisfied { get; private set; }

                public void Reset()
                {
                    IsSatisfied = false;
                }

                public void Advance(WorldSnapshot finalSnapshot, in StageObjectiveTickFacts tickFacts)
                {
                    var entities = new List<EntityState>();
                    finalSnapshot.EnumerateEntitiesOrdered(entities);
                    IsSatisfied = !entities.Any(entity =>
                        EntityRolePolicy.IsEnemyUnit(entity) &&
                        entity.hp > 0 &&
                        !entity.markedForDeath);
                }

                public StageConditionStatus CreateStatus()
                {
                    return new StageConditionStatus(
                        _conditionId,
                        _displayName,
                        "AllEnemiesDefeatedTestCondition",
                        IsSatisfied,
                        $"Satisfied={(IsSatisfied ? 1 : 0)}");
                }
            }
        }

        private sealed class StubMovementLogic : IMovementEntityLogic, IEntityLogicSourceBinding
        {
            private readonly RawMovementIntent _intent;
            private readonly int _tickIndex;

            public StubMovementLogic(RawMovementIntent intent, int tickIndex)
            {
                _intent = intent;
                _tickIndex = tickIndex;
                SourceEntityId = intent.SourceId;
            }

            public int SourceEntityId { get; }

            public int ControlledEntityId => SourceEntityId;

            public void CollectMovementIntents(
                WorldSnapshot snapshot,
                in TickInput input,
                List<RawMovementIntent> buffer)
            {
                if (input.TickIndex != _tickIndex ||
                    !snapshot.TryGetEntity(SourceEntityId, out _))
                {
                    return;
                }

                buffer.Add(_intent);
            }
        }
    }
}
