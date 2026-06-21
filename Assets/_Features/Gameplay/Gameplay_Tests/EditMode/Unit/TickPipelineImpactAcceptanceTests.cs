using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Actions;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class TickPipelineImpactAcceptanceTests
    {
        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_AcceptedDestroyFromActorSourceAndSoleTargetOccupant_ReturnsAllowed()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_RemainingStackedOccupant_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
                CreateUnit(40, destinationCell, hp: 3, teamId: 1),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_IgnoreActiveGlideOccupants_AllowsActiveGlider()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
                CreateUnit(40, destinationCell, hp: 3, teamId: 1),
            });
            worldState.CreateWriteContext().SetEnemyGlideState(40, CreateActiveGlide());
            var snapshot = worldState.CreateSnapshot();
            Assert.That(snapshot.TryGetResolvedSpatialState(20, out var actorSpatialState), Is.True);
            var context = new SettlementContext(
                snapshot,
                new LegalityActorRef(20, EntityType.Unit, actorSpatialState),
                destinationCell,
                snapshot.Topology,
                SpatialState.Anchored);
            var destroyResolutions = new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) };

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    context,
                    new ImpactFollowThroughEvidence(10, 30, destroyResolutions)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    context,
                    new ImpactFollowThroughEvidence(
                        10,
                        30,
                        destroyResolutions,
                        ignoreActiveGlideOccupants: true)).Verdict,
                Is.EqualTo(LegalityVerdict.Allowed));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_DestroyRejected_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: false) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_MissingSourceOrTarget_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        [Test]
        [Category("Extended")]
        public void RuntimeSettlementLegalityPolicy_EvaluateImpactFollowThrough_UnrelatedLethalOccupantStillBlocks_ReturnsBlocked()
        {
            var sourceCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destinationCell = new SurfaceCell(FaceId.Floor, 2, 0);
            var snapshot = CreateSnapshot(new[]
            {
                CreateBox(20, sourceCell),
                CreateUnit(30, destinationCell, hp: 1, teamId: 2),
                CreateUnit(40, destinationCell, hp: 0, teamId: 1, markedForDeath: true),
            });

            Assert.That(
                RuntimeSettlementLegalityPolicy.EvaluateImpactFollowThrough(
                    snapshot,
                    new[] { CreateDestroyResolution(sourceId: 10, targetId: 30, accepted: true) },
                    CreateImpactReservationPayload(20, 10, sourceCell, 30, destinationCell)).Verdict,
                Is.EqualTo(LegalityVerdict.Blocked));
        }

        private static WorldSnapshot CreateSnapshot(IEnumerable<EntityState> initialEntities)
        {
            return CreateWorldState(initialEntities).CreateSnapshot();
        }

        private static WorldState CreateWorldState(IEnumerable<EntityState> initialEntities)
        {
            return GameplayWorldStateTestFactory.CreateBounded(initialEntities);
        }

        private static EnemyGlideRuntimeState CreateActiveGlide()
        {
            return EnemyGlideRuntimeState.Create(
                EnemyGlidePhase.Active,
                sequence: 1,
                windupUntilTickExclusive: 0,
                activeUntilTickExclusive: 5,
                recoveryUntilTickExclusive: 0,
                cooldownUntilTickExclusive: 0,
                windupTicks: 0,
                durationTicks: 3,
                recoveryTicks: 0,
                cooldownTicks: 1,
                glideMoveTicks: 2,
                lastExitedTick: 0,
                wantsRecover: false,
                hasLockedStep: true,
                lockedStepX: 1,
                lockedStepY: 0);
        }

        private static MovementImpactReservationPayload CreateImpactReservationPayload(
            int sourceEntityId,
            int attackSourceEntityId,
            SurfaceCell sourceCell,
            int targetEntityId,
            SurfaceCell destinationCell)
        {
            return new MovementImpactReservationPayload(
                new BoxImpactParticipants(attackSourceEntityId, sourceEntityId, new[] { targetEntityId }),
                new ImpactTravelGeometry(sourceCell, destinationCell, destinationCell, Direction.Right),
                new ImpactAttackHandoff(attackSourceEntityId, damageAmount: 1, sequence: 0),
                new ImpactSourceDispositionPayload(
                    ImpactDispositionPolicyKind.PushLike,
                    hasImpactSourcePoseCommit: true,
                    new ImpactSourcePoseCommit(sourceEntityId, Direction.Right),
                    hasStateChange: true,
                    state: EntityPhaseState.Sliding,
                    stateTimer: 12,
                    semanticKind: ResolvedActionSemanticKind.Push));
        }

        private static DestroyResolutionRecord CreateDestroyResolution(
            int sourceId,
            int targetId,
            bool accepted)
        {
            return new DestroyResolutionRecord(
                actionPlanId: 1,
                intentId: 1,
                sourceId,
                targetId,
                DestroyCondition.WhenHpDepleted,
                finalHp: accepted ? 0 : 1,
                accepted,
                localActionIndex: 0);
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.Box,
                facing = Direction.Right,
                boxCapabilities = BoxCapabilities.Push,
            };
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int hp,
            int teamId,
            bool markedForDeath = false)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = hp > 0 ? hp : 1,
                teamId = teamId,
                type = EntityType.Unit,
                facing = Direction.Right,
                markedForDeath = markedForDeath,
            };
        }
    }
}
