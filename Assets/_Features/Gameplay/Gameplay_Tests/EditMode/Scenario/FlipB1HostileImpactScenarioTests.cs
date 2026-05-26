using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Movement;
using Game.Feature.Gameplay.Movement.Collection;
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
        [Category("B1Future")]
        [Ignore("B1 future: objective progress must be asserted once the objective fixture is wired into this characterization.")]
        public void FlipB1HostileImpact_ExecuteTick_DoesNotEmitObjectiveProgress()
        {
            Assert.Fail("B1 future objective-progress assertion is intentionally pending for the due resolver sprint.");
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
            worldState = CreateWorldState(new[]
            {
                CreateUnit(entityId: 10, position: new Vector2Int(0, 0), teamId: 1, facing: Direction.Up),
                CreateBox(entityId: 20, position: new Vector2Int(1, 0), capabilities: BoxCapabilities.Flip),
                CreateUnit(entityId: 30, position: new Vector2Int(-1, 0), hp: hp, teamId: 2),
            });
            var pipeline = GameplayCompositionRoot.CreateTickPipeline(
                worldState,
                new IEntityLogic[]
                {
                    new PlayerLogic(10),
                });

            startTick = pipeline.RunTick(new TickInput(1, PlayerTickCommand.Flip(Direction.Right)));
            return pipeline.RunTick(new TickInput(2, PlayerTickCommand.None));
        }

        private static List<ScheduledFlipContact> GetScheduledContacts(WorldState worldState)
        {
            var contacts = new List<ScheduledFlipContact>();
            worldState.CreateSnapshot().EnumerateDueScheduledFlipContactsOrdered(int.MaxValue, contacts);
            return contacts;
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
            Direction facing = Direction.Right)
        {
            return new EntityState
            {
                entityId = entityId,
                position = SurfaceCell.FromPlanar(position),
                hp = hp,
                maxHp = hp,
                teamId = teamId,
                type = EntityType.Unit,
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
                if (input.TickIndex != _tickIndex)
                {
                    return;
                }

                buffer.Add(_intent);
            }
        }
    }
}
