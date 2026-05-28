using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class FlipB1FoundationCoreTests
    {
        [Test]
        [Category("Core")]
        public void ScheduledFlipResolution_DoesNotStoreTargetEntity()
        {
            var forbiddenNames = new HashSet<string>
            {
                "TargetEntityId",
                "TargetHpSnapshot",
                "TargetDeathProjection",
                "ReservedTargetCell",
                "SuppressedEnemyId",
            };
            var members = typeof(ScheduledFlipResolution)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property)
                .Select(member => member.Name)
                .ToArray();

            Assert.That(members, Has.None.Matches<string>(forbiddenNames.Contains));
        }

        [Test]
        [Category("Core")]
        public void ScheduledFlipResolution_OrdersDeterministically()
        {
            var contacts = new List<ScheduledFlipResolution>
            {
                CreateContact(actionId: 4, sourceBoxEntityId: 24, dueTick: 6, executeTick: 4, orderingKey: 0),
                CreateContact(actionId: 3, sourceBoxEntityId: 23, dueTick: 5, executeTick: 4, orderingKey: 2),
                CreateContact(actionId: 1, sourceBoxEntityId: 21, dueTick: 5, executeTick: 3, orderingKey: 9),
                CreateContact(actionId: 2, sourceBoxEntityId: 22, dueTick: 5, executeTick: 4, orderingKey: 1),
            };

            contacts.Sort(ScheduledFlipResolutionComparer.Instance);

            Assert.That(contacts.Select(contact => contact.ActionId), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        [Category("Core")]
        public void ScheduledFlipResolution_PreservesDamageSpecAndKineticOwner()
        {
            var contact = CreateContact(actionId: 7, sourceBoxEntityId: 27, kineticInstigatorEntityId: 10, kineticInstigatorTeamId: 2);

            Assert.That(contact.DamageSpec.DamageAmount, Is.EqualTo(1));
            Assert.That(contact.DamageSpec.DamageKind, Is.EqualTo(FlipImpactDamageKind.Impact));
            Assert.That(contact.DamageSpec.SourceKind, Is.EqualTo(AttackSourceKind.B1ScheduledContactDue));
            Assert.That(contact.DamageSpec.SourceActionId, Is.EqualTo(7));
            Assert.That(contact.KineticInstigatorEntityId, Is.EqualTo(10));
            Assert.That(contact.KineticInstigatorTeamId, Is.EqualTo(2));
        }

        [Test]
        [Category("Core")]
        public void BoxInFlight_DoesNotOccupySolidLayer()
        {
            var snapshot = CreateInFlightBoxSnapshot(out _, out var boxCell);

            Assert.That(snapshot.TryGetSolidOccupantAt(boxCell, out _), Is.False);
            Assert.That(snapshot.TryGetSolidSemanticAt(boxCell, out _), Is.False);
            Assert.That(snapshot.TryGetBoxAt(boxCell, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void InFlightBox_IsNotReturnedAsFlipTarget()
        {
            var snapshot = CreateInFlightBoxSnapshot(out var player, out _);

            Assert.That(PlayerControlQueries.TryResolveFlipTarget(snapshot, player, Direction.Right, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void InFlightBox_IsNotReturnedAsPushTarget()
        {
            var snapshot = CreateInFlightBoxSnapshot(out var player, out _);

            Assert.That(PlayerControlQueries.TryResolveAdjacentPushTarget(snapshot, player, Direction.Right, out _), Is.False);
            Assert.That(PlayerControlQueries.TryResolvePushContact(snapshot, player, Direction.Right, tickIndex: 1, out _), Is.False);
        }

        [Test]
        [Category("Core")]
        public void InFlightBox_IsNotReturnedAsAttackTarget()
        {
            var snapshot = CreateInFlightBoxSnapshot(out _, out var boxCell);

            Assert.That(snapshot.TryPickImpactTargetAt(boxCell, sourceTeamId: 1, out _), Is.False);
            var impactTargets = new List<EntityState>();
            snapshot.EnumerateUnitImpactTargetsAt(boxCell, impactTargets);
            Assert.That(impactTargets, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void BoxInFlight_DoesNotBlockPlacementTraversalOrSettlement()
        {
            var snapshot = CreateInFlightBoxSnapshot(out _, out var boxCell);

            Assert.That(snapshot.TryGetPlacementBlocker(EntityType.Unit, boxCell, ignoredEntityId: 10, out _), Is.False);
            Assert.That(
                RuntimeTraversalLegalityPolicy.EvaluateDestination(
                    snapshot,
                    EntityType.Unit,
                    boxCell,
                    ignoredEntityId: 10,
                    snapshot.Topology,
                    CubeRotationKind.None,
                    snapshot.Topology).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(snapshot, EntityType.Unit, boxCell, ignoredEntityId: 10).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Core")]
        public void InFlightBox_RemainsInEntityEnumeration()
        {
            var snapshot = CreateInFlightBoxSnapshot(out _, out _);
            var entities = new List<EntityState>();

            snapshot.EnumerateEntitiesOrdered(entities);

            Assert.That(snapshot.TryGetEntity(20, out var box), Is.True);
            Assert.That(box.boardPresence, Is.EqualTo(EntityBoardPresence.InFlight));
            Assert.That(entities.Select(entity => entity.entityId), Does.Contain(20));
        }

        [Test]
        [Category("Core")]
        public void ScheduledFlipResolutionQueue_EnumeratesDueContactsDeterministically()
        {
            var worldState = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var writeContext = worldState.CreateWriteContext();
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 3, sourceBoxEntityId: 23, dueTick: 7, executeTick: 4));
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 21, dueTick: 5, executeTick: 3));
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 2, sourceBoxEntityId: 22, dueTick: 5, executeTick: 4));

            var snapshot = worldState.CreateSnapshot();
            var contacts = new List<ScheduledFlipResolution>();
            snapshot.EnumerateDueScheduledFlipResolutionsOrdered(7, contacts);

            Assert.That(contacts.Select(contact => contact.ActionId), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        [Category("Core")]
        public void ScheduledFlipResolutionQueue_ExcludesFutureContacts()
        {
            var worldState = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var writeContext = worldState.CreateWriteContext();
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 21, dueTick: 5));
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 2, sourceBoxEntityId: 22, dueTick: 8));

            var contacts = new List<ScheduledFlipResolution>();
            worldState.CreateSnapshot().EnumerateDueScheduledFlipResolutionsOrdered(5, contacts);

            Assert.That(contacts.Select(contact => contact.ActionId), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        [Category("Core")]
        public void ScheduledFlipResolutionQueue_RemovesContactByActionOrBox()
        {
            var worldState = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var writeContext = worldState.CreateWriteContext();
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 20));
            writeContext.AddScheduledFlipResolution(CreateContact(actionId: 2, sourceBoxEntityId: 22));
            writeContext.RemoveScheduledFlipResolution(1);
            writeContext.RemoveScheduledFlipResolutionsForEntity(22);

            var contacts = new List<ScheduledFlipResolution>();
            worldState.CreateSnapshot().EnumerateDueScheduledFlipResolutionsOrdered(int.MaxValue, contacts);

            Assert.That(contacts, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_ChangesWhenScheduledFlipResolutionAdded()
        {
            var baseline = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var withContact = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            withContact.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 20));

            Assert.That(BuildHash(withContact.CreateSnapshot()), Is.Not.EqualTo(BuildHash(baseline.CreateSnapshot())));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_ChangesWhenBoxBecomesInFlight()
        {
            var baseline = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var inFlight = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            inFlight.CreateWriteContext().SetBoardPresence(20, EntityBoardPresence.InFlight);

            Assert.That(BuildHash(inFlight.CreateSnapshot()), Is.Not.EqualTo(BuildHash(baseline.CreateSnapshot())));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_IsStableForSameScheduledResolutionsDifferentInsertionOrder()
        {
            var first = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var second = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });

            first.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 2, sourceBoxEntityId: 22, dueTick: 6));
            first.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 21, dueTick: 5));
            second.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 21, dueTick: 5));
            second.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 2, sourceBoxEntityId: 22, dueTick: 6));

            Assert.That(BuildHash(first.CreateSnapshot()), Is.EqualTo(BuildHash(second.CreateSnapshot())));
        }

        [Test]
        [Category("Core")]
        public void DeterminismHash_ChangesWhenScheduledFlipResolutionKindChanges()
        {
            var hostile = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });
            var ordinary = CreateWorldState(new[] { CreateUnit(10, Cell(0, 0)), CreateBox(20, Cell(1, 0)) });

            hostile.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 20, kind: ScheduledFlipResolutionKind.HostileImpact));
            ordinary.CreateWriteContext().AddScheduledFlipResolution(CreateContact(actionId: 1, sourceBoxEntityId: 20, kind: ScheduledFlipResolutionKind.OrdinaryLanding));

            Assert.That(BuildHash(ordinary.CreateSnapshot()), Is.Not.EqualTo(BuildHash(hostile.CreateSnapshot())));
        }

        private static WorldSnapshot CreateInFlightBoxSnapshot(out EntityState player, out SurfaceCell boxCell)
        {
            boxCell = Cell(1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateUnit(10, Cell(0, 0)),
                CreateBox(20, boxCell),
            });
            worldState.CreateWriteContext().SetBoardPresence(20, EntityBoardPresence.InFlight);
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetEntity(10, out player), Is.True);
            return snapshot;
        }

        private static string BuildHash(WorldSnapshot snapshot)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new DeterminismHashBuilder().Build(
                7,
                snapshot,
                new TickResultData(finalEntities, Array.Empty<DelayedAttackEffectRecord>(), Array.Empty<string>()));
        }

        private static ScheduledFlipResolution CreateContact(
            int actionId,
            int sourceBoxEntityId,
            int dueTick = 5,
            int executeTick = 3,
            int orderingKey = 0,
            int kineticInstigatorEntityId = 10,
            int kineticInstigatorTeamId = 1,
            ScheduledFlipResolutionKind kind = ScheduledFlipResolutionKind.HostileImpact)
        {
            return new ScheduledFlipResolution(
                kind,
                actionId,
                actorEntityId: 10,
                sourceBoxEntityId: sourceBoxEntityId,
                sourceCell: Cell(1, 0),
                contactCell: Cell(1, 1),
                landingCell: Cell(1, 2),
                flipDirection: Direction.Right,
                sourceFace: FaceId.Floor,
                sourceCapabilitiesSnapshot: BoxCapabilities.Flip | BoxCapabilities.Destroy,
                damageSpec: new FlipImpactDamageSpec(
                    damageAmount: 1,
                    damageKind: FlipImpactDamageKind.Impact,
                    sourceKind: AttackSourceKind.B1ScheduledContactDue,
                    sourceActionId: actionId),
                kineticInstigatorEntityId,
                kineticInstigatorTeamId,
                actionStartTick: Math.Max(0, executeTick - 1),
                actionVisualImpactTick: dueTick,
                flipExecuteDelayTicks: executeTick - Math.Max(0, executeTick - 1),
                flipInputLockDurationTicks: Math.Max(1, dueTick - Math.Max(0, executeTick - 1)),
                executeTick,
                dueTick,
                orderingKey,
                FlipContactCancellationPolicy.SafeReturnOrDestroy,
                FlipContactDispositionPolicy.DefaultB1HostileImpact);
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(Vector2Int.zero, new Vector2Int(4, 4)),
                Game.Feature.Gameplay.BoardState.TerrainData.Empty,
                new CubeTopologyState(FaceId.Floor));
        }

        private static EntityState CreateUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
            };
        }

        private static SurfaceCell Cell(int x, int y)
        {
            return new SurfaceCell(FaceId.Floor, x, y);
        }
    }
}
