using System.Collections.Generic;
using System.Linq;
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
        public void FlipB1HostileImpact_ExecuteTick_DoesNotCreateImpactReservation()
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
        public void FlipB1HostileImpact_ExecuteTick_DoesNotEnterAttackInputNormalizer()
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
        public void FlipB1HostileImpact_ExecuteTick_ScheduledContactDueTickIsExplicitDelay()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var contact = GetScheduledContacts(worldState).Single();

            Assert.That(contact.ExecuteTick, Is.EqualTo(2));
            Assert.That(contact.DueTick, Is.EqualTo(2 + GameplayTimingProfile.DefaultFlipContactDelayTicks));
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_ExecuteTick_ScheduledContactHasNoTargetId()
        {
            RunPlayerFlipImpact(hp: 3, out var worldState, out _);

            var contact = GetScheduledContacts(worldState).Single();

            Assert.That(contact.ActionId, Is.GreaterThan(0));
            Assert.That(contact.ActorEntityId, Is.EqualTo(10));
            Assert.That(contact.SourceBoxEntityId, Is.EqualTo(20));
            Assert.That(contact.ContactCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(contact.LandingCell, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(contact.DamageSpec.DamageAmount, Is.EqualTo(1));
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
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));

            Assert.That(startTick.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(executeTick.ObjectiveResult.ClearedThisTick, Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(30, out _), Is.True);
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
        public void FlipB1HostileImpact_DueTick_RequeriesCurrentSnapshot()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            RunUntilDue(pipeline);

            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_DueTick_MultipleUnits_HitsDeterministicPrimaryOnly()
        {
            var pipeline = CreateDefaultImpactPipeline(out var worldState);
            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            var dueTickIndex = 2 + GameplayTimingProfile.DefaultFlipContactDelayTicks;
            var pipeline = CreatePipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            var executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
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
            pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            worldState.CreateWriteContext().SetBoardPresence(20, EntityBoardPresence.Occupying);

            var dueTick = RunUntilDue(pipeline);

            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var sourceBox), Is.True);
            Assert.That(sourceBox.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(dueTick.EventLog.Any(entry => entry.Contains("Reason=BoxNotInFlight")), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void FlipB1HostileImpact_OrdinarySuccess_RemainsSameTickMaterialize_InPhase1()
        {
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            var snapshot = worldState.CreateSnapshot();

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, -1, 0)));
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
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
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[] { new PlayerLogic(10) });

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            var result = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));

            Assert.That(result.AttackPhaseResult.DrainedImpactReservations, Is.Empty);
            Assert.That(GetScheduledContacts(worldState), Is.Empty);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        private static TickResult RunPlayerFlipImpact(int hp, out WorldState worldState, out TickResult startTick)
        {
            var pipeline = CreateDefaultImpactPipeline(hp, out worldState);

            startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            return pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
        }

        private static void RunPlayerFlipImpactToDue(
            int hp,
            out WorldState worldState,
            out TickResult executeTick,
            out TickResult dueTick)
        {
            var pipeline = CreateDefaultImpactPipeline(hp, out worldState);

            pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            executeTick = pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
            dueTick = RunUntilDue(pipeline);
        }

        private static TickPipeline CreateDefaultImpactPipeline(out WorldState worldState)
        {
            return CreateDefaultImpactPipeline(3, out worldState);
        }

        private static TickPipeline CreateDefaultImpactPipeline(int hp, out WorldState worldState)
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
                    new PlayerLogic(10),
                });
        }

        private static TickResult RunUntilDue(TickPipeline pipeline)
        {
            TickResult result = null;
            var dueTick = 2 + GameplayTimingProfile.DefaultFlipContactDelayTicks;
            for (var tick = 3; tick <= dueTick; tick++)
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

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IEnumerable<IEntityLogic> entityLogics = null,
            StageObjectiveRuntimeDefinition objectiveDefinition = null)
        {
            var timing = GameplayTimingProfile.CreateDefault();
            return GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                entityLogics ?? new IEntityLogic[] { new PlayerLogic(10) },
                timing,
                PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                    timing.SimulationTicksPerSecond,
                    GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                objectiveDefinition: objectiveDefinition);
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
