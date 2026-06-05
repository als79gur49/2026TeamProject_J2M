using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Tests;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class SpatialStateResolverTruthTableTests
    {
        [Test]
        [Category("Core")]
        public void Resolve_OccupyingWithoutJump_OnActiveFace_ReturnsAnchoredVisible()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: true,
                ResolvedSpatialStateSource.DefaultAnchored);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithoutJump_OnInactiveFace_ReturnsAnchoredHidden()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                isFaceActive: false);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.AnchoredHiddenByTopology);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithWindup_RemainsAnchored()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Windup },
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: true,
                ResolvedSpatialStateSource.DefaultAnchored);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithCooldown_RemainsAnchored()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Cooldown },
                isFaceActive: false);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.AnchoredHiddenByTopology);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedAirborne_ReturnsAirborne()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Detached,
                new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Airborne },
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Airborne,
                claimsAuthoritativeOccupancy: false,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.JumpAirborne);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithActivePhasedCarrier_OnActiveFace_ReturnsPhasedVisible()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5),
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Phased,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: true,
                ResolvedSpatialStateSource.PhasedVisible);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingWithActivePhasedCarrier_OnInactiveFace_ReturnsPhasedHidden()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Occupying,
                jumpState: null,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5),
                isFaceActive: false);

            AssertResolved(
                resolved,
                SpatialState.Phased,
                claimsAuthoritativeOccupancy: true,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.PhasedHiddenByTopology);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedWithoutJump_RemainsAnchoredWithoutOccupancyClaim()
        {
            var resolved = SpatialStateResolver.Resolve(
                EntityBoardPresence.Detached,
                jumpState: null,
                isFaceActive: true);

            AssertResolved(
                resolved,
                SpatialState.Anchored,
                claimsAuthoritativeOccupancy: false,
                isGameplayVisible: false,
                ResolvedSpatialStateSource.DetachedNonAirborne);
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedWindup_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Detached,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Windup },
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void Resolve_DetachedCooldown_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Detached,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Cooldown },
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingAirborne_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Occupying,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Airborne },
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void Resolve_OccupyingPhasedAndJump_ThrowsInvalidSourceCombination()
        {
            Assert.Throws<InvalidOperationException>(
                () => SpatialStateResolver.Resolve(
                    EntityBoardPresence.Occupying,
                    new EnemyJumpRuntimeState { phase = EnemyJumpPhase.Windup },
                    PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5),
                    isFaceActive: true));
        }

        [Test]
        [Category("Extended")]
        public void WorldSnapshot_HasAnyUnitAt_And_ImpactTargetQueries_IgnoreAirborneOccupant()
        {
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(
                new[]
                {
                    CreateUnit(40, targetCell, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(
                40,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 1,
                    sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                    lockedTargetCell = targetCell,
                    landingTick = 1,
                });

            var snapshot = worldState.CreateSnapshot();

            Assert.That(snapshot.TryGetResolvedSpatialState(40, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Airborne));
            Assert.That(snapshot.HasAnyUnitAt(targetCell), Is.False);
            Assert.That(snapshot.TryPickImpactTargetAt(targetCell, sourceTeamId: 1, out _), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInTraversalBlocking(spatialState), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInSettlementBlocking(spatialState), Is.False);
            Assert.That(SpatialStateSemantics.ParticipatesInTargetSelection(spatialState), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_ApplyPhasedState_MatchesAuthoritativeWorldRoundTrip()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var baseSnapshot = worldState.CreateSnapshot();
            var batch = new FinalizationBatch();
            var phasedState = PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 7);
            batch.SetPhasedState(entity.entityId, phasedState);

            var projectedWorld = new ProjectedWorld(baseSnapshot);
            projectedWorld.ApplyBatch(batch);
            var previewSnapshot = projectedWorld.CreateSnapshot();

            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(baseSnapshot.TryGetPhasedState(entity.entityId, out _), Is.False);
            Assert.That(previewSnapshot.TryGetPhasedState(entity.entityId, out var previewPhasedState), Is.True);
            Assert.That(finalSnapshot.TryGetPhasedState(entity.entityId, out var finalPhasedState), Is.True);
            Assert.That(previewPhasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.MovementPreMovement));
            Assert.That(finalPhasedState.ownerKind, Is.EqualTo(PhasedRuntimeStateOwnerKind.MovementPreMovement));
            Assert.That(previewSnapshot.TryGetResolvedSpatialState(entity.entityId, out var previewSpatial), Is.True);
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var finalSpatial), Is.True);
            Assert.That(previewSpatial.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(finalSpatial.Kind, Is.EqualTo(SpatialState.Phased));
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_PhasedClear_DoesNotRetroactivelyChangeEarlierPreviewSnapshot()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var projectedWorld = new ProjectedWorld(worldState.CreateSnapshot());

            var enterBatch = new FinalizationBatch();
            enterBatch.SetPhasedState(entity.entityId, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 7));
            projectedWorld.ApplyBatch(enterBatch);
            var snapshotAfterEnter = projectedWorld.CreateSnapshot();

            var clearBatch = new FinalizationBatch();
            clearBatch.SetPhasedState(entity.entityId, PhasedRuntimeStateQueries.Clear());
            projectedWorld.ApplyBatch(clearBatch);
            var snapshotAfterClear = projectedWorld.CreateSnapshot();

            Assert.That(snapshotAfterEnter.TryGetPhasedState(entity.entityId, out _), Is.True);
            Assert.That(snapshotAfterEnter.TryGetResolvedSpatialState(entity.entityId, out var phasedSpatial), Is.True);
            Assert.That(phasedSpatial.Kind, Is.EqualTo(SpatialState.Phased));
            Assert.That(snapshotAfterClear.TryGetPhasedState(entity.entityId, out _), Is.False);
            Assert.That(snapshotAfterClear.TryGetResolvedSpatialState(entity.entityId, out var anchoredSpatial), Is.True);
            Assert.That(anchoredSpatial.Kind, Is.EqualTo(SpatialState.Anchored));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetBoardPresenceDetached_ClearsActivePhasedCarrier()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var writeContext = worldState.CreateWriteContext();
            ((IPhasedStateCommitContext)writeContext).SetPhasedState(
                entity.entityId,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 3));

            Assert.That(worldState.CreateSnapshot().TryGetPhasedState(entity.entityId, out _), Is.True);

            writeContext.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetPhasedState(entity.entityId, out _), Is.False);
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Anchored));
            Assert.That(spatialState.ClaimsAuthoritativeOccupancy, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetBoardPresenceDetached_ClearsCooldownJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Cooldown, sequence: 7));

            writeContext.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.None));
            Assert.That(jumpState.sequence, Is.EqualTo(7));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Anchored));
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.DetachedNonAirborne));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetBoardPresenceDetached_ClearsWindupJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Windup, sequence: 11));

            writeContext.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.None));
            Assert.That(jumpState.sequence, Is.EqualTo(11));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Anchored));
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.DetachedNonAirborne));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetBoardPresenceDetached_PreservesAirborneJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Airborne, sequence: 13));

            writeContext.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(jumpState.sequence, Is.EqualTo(13));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Airborne));
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.JumpAirborne));
        }

        [Test]
        [Category("Extended")]
        public void WorldState_SetBoardPresenceOccupying_DoesNotClearCooldownJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Cooldown, sequence: 17));

            writeContext.SetBoardPresence(entity.entityId, EntityBoardPresence.Occupying);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Cooldown));
            Assert.That(jumpState.sequence, Is.EqualTo(17));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Anchored));
        }

        [Test]
        [Category("Extended")]
        public void FinalizationBatch_SetBoardPresenceDetached_ClearsCooldownJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            worldState.CreateWriteContext().SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Cooldown, sequence: 19));
            var batch = new FinalizationBatch();
            batch.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);

            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);
            var finalSnapshot = worldState.CreateSnapshot();

            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.None));
            Assert.That(jumpState.sequence, Is.EqualTo(19));
            Assert.That(finalSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.DetachedNonAirborne));
        }

        [Test]
        [Category("Extended")]
        public void ProjectedWorld_SetBoardPresenceDetached_ClearsCooldownJumpState()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            worldState.CreateWriteContext().SetEnemyJumpState(entity.entityId, CreateJumpState(EnemyJumpPhase.Cooldown, sequence: 23));
            var batch = new FinalizationBatch();
            batch.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);

            var projectedWorld = new ProjectedWorld(worldState.CreateSnapshot());
            projectedWorld.ApplyBatch(batch);
            var previewSnapshot = projectedWorld.CreateSnapshot();

            Assert.That(previewSnapshot.TryGetEnemyJumpState(entity.entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.None));
            Assert.That(jumpState.sequence, Is.EqualTo(23));
            Assert.That(previewSnapshot.TryGetResolvedSpatialState(entity.entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.DetachedNonAirborne));
        }

        [Test]
        [Category("Extended")]
        public void FinalizationBatch_JumpTakeoff_SetBoardPresenceThenSetAirborne_RemainsValid()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var windupState = CreateJumpState(EnemyJumpPhase.Windup, sequence: 29);
            var airborneState = CreateJumpState(EnemyJumpPhase.Airborne, sequence: 29);
            worldState.CreateWriteContext().SetEnemyJumpState(entity.entityId, windupState);
            var batch = new FinalizationBatch();
            batch.SetBoardPresence(entity.entityId, EntityBoardPresence.Detached);
            batch.SetEnemyJumpState(entity.entityId, airborneState);

            var projectedWorld = new ProjectedWorld(worldState.CreateSnapshot());
            projectedWorld.ApplyBatch(batch);
            var previewSnapshot = projectedWorld.CreateSnapshot();
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);
            var finalSnapshot = worldState.CreateSnapshot();

            AssertAirborneSnapshot(previewSnapshot, entity.entityId, expectedSequence: 29);
            AssertAirborneSnapshot(finalSnapshot, entity.entityId, expectedSequence: 29);
        }

        [Test]
        [Category("Extended")]
        public void FinalizationBatch_ActiveJumpEnter_RequiresPhasedClearOrdering()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var jumpState = new EnemyJumpRuntimeState
            {
                phase = EnemyJumpPhase.Windup,
                sequence = 1,
                sourceCell = entity.position,
                lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0),
                windupEndTick = 2,
                landingTick = 3,
            };
            var blockedWorld = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            ((IPhasedStateCommitContext)blockedWorld.CreateWriteContext()).SetPhasedState(
                entity.entityId,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var blockedBatch = new FinalizationBatch();
            blockedBatch.SetEnemyJumpState(entity.entityId, jumpState);

            Assert.Throws<InvalidOperationException>(
                () => blockedBatch.ApplyTo(blockedWorld.CreateWriteContext(), delayedAttackEffectSink: null));

            var orderedWorld = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            ((IPhasedStateCommitContext)orderedWorld.CreateWriteContext()).SetPhasedState(
                entity.entityId,
                PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 1));
            var orderedBatch = new FinalizationBatch();
            orderedBatch.SetPhasedState(entity.entityId, PhasedRuntimeStateQueries.Clear());
            orderedBatch.SetEnemyJumpState(entity.entityId, jumpState);
            orderedBatch.ApplyTo(orderedWorld.CreateWriteContext(), delayedAttackEffectSink: null);

            var finalSnapshot = orderedWorld.CreateSnapshot();
            Assert.That(finalSnapshot.TryGetPhasedState(entity.entityId, out _), Is.False);
            Assert.That(finalSnapshot.TryGetEnemyJumpState(entity.entityId, out var appliedJumpState), Is.True);
            Assert.That(appliedJumpState.phase, Is.EqualTo(EnemyJumpPhase.Windup));
        }

        [Test]
        [Category("Extended")]
        public void DeterminismHashBuilder_FinalHashDependsOnFinalPhasedCarrierOnly()
        {
            var entity = CreateUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), teamId: 1, boardPresence: EntityBoardPresence.Occupying);
            var worldState = GameplayWorldStateTestFactory.CreateBounded(new[] { entity });
            var baselineSnapshot = worldState.CreateSnapshot();
            var baselineHash = new DeterminismHashBuilder().Build(
                tickIndex: 7,
                baselineSnapshot,
                CreateTickResultData(baselineSnapshot));

            var batch = new FinalizationBatch();
            batch.SetPhasedState(entity.entityId, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 7));
            batch.SetPhasedState(entity.entityId, PhasedRuntimeStateQueries.Clear());
            batch.ApplyTo(worldState.CreateWriteContext(), delayedAttackEffectSink: null);

            var finalSnapshot = worldState.CreateSnapshot();
            var finalHash = new DeterminismHashBuilder().Build(
                tickIndex: 7,
                finalSnapshot,
                CreateTickResultData(finalSnapshot));

            Assert.That(finalSnapshot.TryGetPhasedState(entity.entityId, out _), Is.False);
            Assert.That(finalHash, Is.EqualTo(baselineHash));
        }

        [Test]
        [Category("Extended")]
        public void ReservedSpatialStates_AreConstrainedToAllowlistedFiles_AndKeepAttachedProducerClosed()
        {
            Assert.DoesNotThrow(() => SpatialStateSemantics.EnsureProductionSupported(SpatialState.Phased));
            Assert.Throws<InvalidOperationException>(() => SpatialStateSemantics.EnsureProductionSupported(SpatialState.Attached));
            Assert.DoesNotThrow(() => SpatialStateSemantics.EnsureLiveProducerClosed(SpatialState.Phased));
            Assert.Throws<InvalidOperationException>(() => SpatialStateSemantics.EnsureLiveProducerClosed(SpatialState.Attached));

            var reservedStateReadAllowlist = CreateReservedStateReadAllowlist();
            var filesUsingReservedStates = FindFilesContainingReservedStates();
            CollectionAssert.AreEquivalent(
                reservedStateReadAllowlist.Keys,
                filesUsingReservedStates,
                "Reserved-state reads must stay within the explicit allowlist. Add a rationale entry when a new seam-proof reader is required.");

            foreach (var rationale in reservedStateReadAllowlist.Values)
            {
                Assert.That(
                    rationale == "seam-truth-table" || rationale == "live-lifecycle-proof",
                    Is.True,
                    $"Unsupported reserved-state read rationale: {rationale}");
            }
        }

        [Test]
        [Category("Extended")]
        public void PhasedWritePath_And_FactoryReferences_AreConstrainedToAllowlistedFiles()
        {
            var nonTestSetPhasedStateReferences = FilterNonTestFiles(FindFilesContainingToken("SetPhasedState("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldStateMutationPort.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/IWorldWriteContext.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/WorldStateWriteContext.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/SystemPreMovementValidationLogic.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.FinalizationBatch.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.ProjectedWorld.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs"),
                },
                nonTestSetPhasedStateReferences);

            var nonTestBeginMovementReferences = FilterNonTestFiles(FindFilesContainingToken("BeginMovementPreMovement("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_PlayerControl/Runtime/PlayerControlStateLogic.cs"),
                },
                nonTestBeginMovementReferences);

            var nonTestBeginEnemyReferences = FilterNonTestFiles(FindFilesContainingToken("BeginEnemyPreMovement("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
                },
                nonTestBeginEnemyReferences);

            var nonTestBeginSystemValidationReferences = FilterNonTestFiles(FindFilesContainingToken("BeginSystemPreMovementValidation("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/SystemPreMovementValidationLogic.cs"),
                },
                nonTestBeginSystemValidationReferences);

            var nonTestEnemyLockRetentionReferences = FilterNonTestFiles(FindFilesContainingToken("ShouldParticipateInEnemyCurrentLockRetention("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/ModifierQuery.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyActionStateLogic.cs"),
                },
                nonTestEnemyLockRetentionReferences);

            var nonTestCurrentEnemyLockEvidenceReferences = FilterNonTestFiles(FindFilesContainingToken("CurrentEnemyLockRetentionEvidence"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/LegalityContexts.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/ModifierQuery.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyActionStateLogic.cs"),
                },
                nonTestCurrentEnemyLockEvidenceReferences);

            var nonTestEnemyPhaseThroughReferences = FilterNonTestFiles(FindFilesContainingToken("EnemyPhaseThroughLockedTargetQueries"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
                },
                nonTestEnemyPhaseThroughReferences);

            var nonTestCurrentEnemyLockModeReferences = FilterNonTestFiles(FindFilesContainingToken("FreshSelectionSuppressedWithCurrentEnemyLockRetention"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Debug/Runtime/TickTraceFormatter.cs"),
                },
                nonTestCurrentEnemyLockModeReferences);

            var nonTestPhaseThroughSkillKindReferences = FilterNonTestFiles(FindFilesContainingToken("MovementSkillStrategyKind.PhaseThroughLockedTarget"));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyAiConfig.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyAiRuntimeTypes.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
                },
                nonTestPhaseThroughSkillKindReferences);

            var nonTestCurrentTerminalCellReferences = FilterNonTestFiles(FindFilesContainingToken("TryResolveCurrentTerminalCell("));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyLogic.cs"),
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs"),
                },
                nonTestCurrentTerminalCellReferences);
        }

        [Test]
        [Category("Extended")]
        public void EnemyTargetEligibilityArchitectureBoundaries_AreConstrained()
        {
            var enemyAiFreshTargetabilityReferences = FilterNonTestFiles(FindFilesContainingToken("CanBeTargetedForNewSelection("))
                .Where(path => path.Contains("/Gameplay_EnemyAI/", StringComparison.Ordinal))
                .ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_EnemyAI/Runtime/EnemyTargetEligibilityPolicy.cs"),
                },
                enemyAiFreshTargetabilityReferences,
                "EnemyAI must call fresh targetability only through EnemyTargetEligibilityPolicy.EvaluateFreshAcquire.");

            var boardStateEnemyEligibilityReferences = FilterNonTestFiles(FindFilesContainingAnyToken(
                    "EnemyTargetEligibilityPurpose",
                    "EnemyTargetEligibilityPolicy",
                    "EnemyLocalContactPolicy"))
                .Where(path => path.Contains("/Gameplay_BoardState/", StringComparison.Ordinal))
                .ToArray();
            Assert.That(
                boardStateEnemyEligibilityReferences,
                Is.Empty,
                "BoardState must not reference EnemyAI-local eligibility policy or purpose types.");
        }

        [Test]
        [Category("Extended")]
        public void PhasedSourceMetadataCatalog_ActiveOwners_HaveCoverage_And_StageDefaults()
        {
            var coveredOwnerKinds = new HashSet<PhasedRuntimeStateOwnerKind>();
            foreach (PhasedRuntimeStateOwnerKind ownerKind in Enum.GetValues(typeof(PhasedRuntimeStateOwnerKind)))
            {
                if (ownerKind == PhasedRuntimeStateOwnerKind.None)
                {
                    continue;
                }

                Assert.That(
                    PhasedSourceMetadataCatalog.TryGet(ownerKind, out var metadata),
                    Is.True,
                    $"Missing phased source metadata row for owner {ownerKind}.");
                Assert.That(metadata.OwnerKind, Is.EqualTo(ownerKind));
                Assert.That(metadata.TimingRow, Is.Not.Empty);
                Assert.That(metadata.CancelReplaceRule, Is.Not.Empty);
                Assert.That(metadata.LifecycleRule, Is.Not.Empty);
                Assert.That(metadata.RequestedTerminalSettleMode, Is.EqualTo(PhasedRequestedTerminalSettleMode.AnchoredLikeDefault));
                coveredOwnerKinds.Add(ownerKind);
            }

            CollectionAssert.AreEquivalent(
                new[]
                {
                    PhasedRuntimeStateOwnerKind.MovementPreMovement,
                    PhasedRuntimeStateOwnerKind.EnemyPreMovement,
                    PhasedRuntimeStateOwnerKind.DebugForced,
                    PhasedRuntimeStateOwnerKind.SystemPreMovementValidation,
                },
                coveredOwnerKinds);

            Assert.That(
                PhasedSourceMetadataCatalog.TryGet(PhasedRuntimeStateOwnerKind.EnemyPreMovement, out var enemyMetadata),
                Is.True);
            Assert.That(enemyMetadata.EmittingStage, Is.EqualTo(PhasedSourceEmittingStage.PreMovementState));
            Assert.That(
                enemyMetadata.TargetabilityMode,
                Is.EqualTo(PhasedTargetabilityMode.FreshSelectionSuppressedWithCurrentEnemyLockRetention));
            Assert.That(enemyMetadata.ReservationReadClass, Is.EqualTo(PhasedReservationReadClass.CellOnlyPreSettle));
            Assert.That(enemyMetadata.EarliestObservableSnapshot, Is.EqualTo(PhasedEarliestObservableSnapshot.PlanSnapshot));

            Assert.That(
                PhasedSourceMetadataCatalog.TryGet(PhasedRuntimeStateOwnerKind.SystemPreMovementValidation, out var validationMetadata),
                Is.True);
            Assert.That(validationMetadata.EmittingStage, Is.EqualTo(PhasedSourceEmittingStage.PreMovementState));
            Assert.That(validationMetadata.TargetabilityMode, Is.EqualTo(PhasedTargetabilityMode.FreshSelectionSuppressed));
            Assert.That(validationMetadata.ReservationReadClass, Is.EqualTo(PhasedReservationReadClass.None));
            Assert.That(validationMetadata.EarliestObservableSnapshot, Is.EqualTo(PhasedEarliestObservableSnapshot.PlanSnapshot));

            foreach (PhasedRuntimeStateOwnerKind ownerKind in Enum.GetValues(typeof(PhasedRuntimeStateOwnerKind)))
            {
                if (ownerKind == PhasedRuntimeStateOwnerKind.None ||
                    !PhasedSourceMetadataCatalog.TryGet(ownerKind, out var metadata) ||
                    ownerKind == PhasedRuntimeStateOwnerKind.EnemyPreMovement)
                {
                    continue;
                }

                Assert.That(
                    metadata.TargetabilityMode,
                    Is.Not.EqualTo(PhasedTargetabilityMode.FreshSelectionSuppressedWithCurrentEnemyLockRetention),
                    $"Only EnemyPreMovement may use current enemy lock retention mode. Unexpected owner: {ownerKind}");
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyPhaseRelocationReservationReadPath_RemainsCellOnly()
        {
            var pipelineSource = ReadProjectFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var phaseReservationReadMethod = ExtractMethodWindow(
                pipelineSource,
                "private FinalizationBatch ResolveEnemyPhaseRelocationSpaceContestsCanonical(",
                "private static ReservationStatus ReadPhaseRelocationTerminalReservationStatus(");
            var reservationHelperWindow = ExtractMethodWindow(
                pipelineSource,
                "private static ReservationStatus ReadPhaseRelocationTerminalReservationStatus(",
                "private static Contest TryFindJumpLandingContest(");

            Assert.That(phaseReservationReadMethod, Does.Contain("ReadPhaseRelocationTerminalReservationStatus("));
            Assert.That(phaseReservationReadMethod, Does.Not.Contain("reservationBook.GetCellStatus("));
            Assert.That(phaseReservationReadMethod, Does.Not.Contain("reservationBook.GetCellReservationInfo("));
            Assert.That(phaseReservationReadMethod, Does.Not.Contain("reservationBook.GetEdgeStatus("));
            Assert.That(phaseReservationReadMethod, Does.Not.Contain("reservationBook.GetEntityStatus("));
            Assert.That(phaseReservationReadMethod, Does.Not.Contain("topology-exclusive"));
            Assert.That(
                CountOccurrences(reservationHelperWindow, "reservationBook.GetCellReservationInfo("),
                Is.EqualTo(1),
                "Inline reservation read count is a secondary sentinel. The primary contract is still cell-only terminal settlement semantics.");
            Assert.That(reservationHelperWindow, Does.Contain("var reservationInfo = reservationBook.GetCellReservationInfo(terminalCell);"));
            Assert.That(reservationHelperWindow, Does.Not.Contain("GetEdgeStatus("));
            Assert.That(reservationHelperWindow, Does.Not.Contain("GetEntityStatus("));
            Assert.That(reservationHelperWindow, Does.Not.Contain("topology-exclusive"));
            Assert.That(phaseReservationReadMethod.IndexOf("ReadPhaseRelocationTerminalReservationStatus(", StringComparison.Ordinal),
                Is.GreaterThan(phaseReservationReadMethod.IndexOf("traverseToDestination.Verdict != LegalityVerdict.Allowed", StringComparison.Ordinal)));
            Assert.That(phaseReservationReadMethod.IndexOf("ReadPhaseRelocationTerminalReservationStatus(", StringComparison.Ordinal),
                Is.LessThan(phaseReservationReadMethod.IndexOf("RuntimeSettlementLegalityPolicy.EvaluateLandingPlacement(", StringComparison.Ordinal)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPhaseRelocationPlanner_And_Finalizer_RemainClosedMinimalValidatorSeams()
        {
            var pipelineSource = ReadProjectFile("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs");
            var planningWindow = ExtractMethodWindow(
                pipelineSource,
                "private void ResolvePlanEnemyPhaseRelocations(",
                "private void ResolvePlanJumpLandings(");
            var finalizationWindow = ExtractMethodWindow(
                pipelineSource,
                "private FinalizationBatch ResolveEnemyPhaseRelocationSpaceContestsCanonical(",
                "private static Contest TryFindJumpLandingContest(");

            AssertContainsNoForbiddenTokens(
                planningWindow,
                "TryResolveStartAction(",
                "TryResolveLockedTarget(",
                "TryFindTarget(",
                "BuildMovementIntents(",
                "CollectMovementIntents(",
                "ResolveBaselineGroundLocomotion(",
                "ResolveFallbackAiMode(",
                "reservationBook.GetCellStatus(",
                "reservationBook.GetEdgeStatus(",
                "reservationBook.GetEntityStatus(");
            AssertContainsNoForbiddenTokens(
                finalizationWindow,
                "TryResolveStartAction(",
                "TryResolveLockedTarget(",
                "TryFindTarget(",
                "BuildMovementIntents(",
                "CollectMovementIntents(",
                "EnqueueDelayedAttackEffect(",
                "SetTopology(");
        }

        [Test]
        [Category("Extended")]
        public void SystemPreMovementValidationSource_RemainsReservationReadFree()
        {
            var source = ReadProjectFile("Assets/_Features/Gameplay/Gameplay_Entities/Runtime/SystemPreMovementValidationLogic.cs");

            Assert.That(source, Does.Not.Contain("MovementReservationBook"));
            Assert.That(source, Does.Not.Contain("FrozenMovementReservationExport"));
            Assert.That(source, Does.Not.Contain("GetCellStatus("));
            Assert.That(source, Does.Not.Contain("GetEdgeStatus("));
            Assert.That(source, Does.Not.Contain("GetEntityStatus("));
        }

        private static void AssertResolved(
            ResolvedSpatialState resolved,
            SpatialState expectedKind,
            bool claimsAuthoritativeOccupancy,
            bool isGameplayVisible,
            ResolvedSpatialStateSource expectedSource)
        {
            Assert.That(resolved.Kind, Is.EqualTo(expectedKind));
            Assert.That(resolved.ClaimsAuthoritativeOccupancy, Is.EqualTo(claimsAuthoritativeOccupancy));
            Assert.That(resolved.IsGameplayVisible, Is.EqualTo(isGameplayVisible));
            Assert.That(resolved.Source, Is.EqualTo(expectedSource));
        }

        private static IEnumerable<string> FindFilesContainingReservedStates()
        {
            return FindFilesContainingAnyToken("SpatialState.Phased", "SpatialState.Attached");
        }

        private static IReadOnlyDictionary<string, string> CreateReservedStateReadAllowlist()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/SpatialState.cs")] = "seam-truth-table",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_BoardState/Runtime/StateQuery.cs")] = "seam-truth-table",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPipeline.cs")] = "live-lifecycle-proof",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Scenario/SystemPreMovementValidationScenarioTests.cs")] = "seam-truth-table",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/ModifierCapabilityGeneralizationTests.cs")] = "live-lifecycle-proof",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/PlayerMovementInputTests.cs")] = "live-lifecycle-proof",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/SpatialStateResolverTruthTableTests.cs")] = "seam-truth-table",
                [NormalizeRelativePath("Assets/_Features/Gameplay/Gameplay_Tests/EditMode/Unit/TickPipelineJumpLandingExactStackTests.cs")] = "seam-truth-table",
            };
        }

        private static IEnumerable<string> FindFilesContainingToken(string token)
        {
            return FindFilesContainingAnyToken(token);
        }

        private static IEnumerable<string> FilterNonTestFiles(IEnumerable<string> relativePaths)
        {
            foreach (var relativePath in relativePaths)
            {
                if (relativePath.Contains("/Gameplay_Tests/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return relativePath;
            }
        }

        private static IEnumerable<string> FindFilesContainingAnyToken(params string[] tokens)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                var containsToken = false;
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (!source.Contains(tokens[i], StringComparison.Ordinal))
                    {
                        continue;
                    }

                    containsToken = true;
                    break;
                }

                if (!containsToken)
                {
                    continue;
                }

                results.Add(NormalizeRelativePath(Path.GetRelativePath(projectRoot, file)));
            }

            return results;
        }

        private static string ReadProjectFile(string relativePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static string ExtractMethodWindow(string source, string startToken, string endToken)
        {
            var startIndex = source.IndexOf(startToken, StringComparison.Ordinal);
            var endIndex = source.IndexOf(endToken, StringComparison.Ordinal);

            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), $"Missing start token: {startToken}");
            Assert.That(endIndex, Is.GreaterThan(startIndex), $"Missing end token after start token: {endToken}");

            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            return relativePath.Replace('\\', '/');
        }

        private static int CountOccurrences(string source, string token)
        {
            var count = 0;
            var searchIndex = 0;
            while ((searchIndex = source.IndexOf(token, searchIndex, StringComparison.Ordinal)) >= 0)
            {
                count++;
                searchIndex += token.Length;
            }

            return count;
        }

        private static void AssertContainsNoForbiddenTokens(string source, params string[] forbiddenTokens)
        {
            for (var i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source, Does.Not.Contain(forbiddenTokens[i]), $"Forbidden phased-validator token found: {forbiddenTokens[i]}");
            }
        }

        private static TickResultData CreateTickResultData(WorldSnapshot snapshot)
        {
            var finalEntities = new List<EntityState>();
            snapshot.EnumerateEntitiesOrdered(finalEntities);
            return new TickResultData(
                finalEntities,
                Array.Empty<DelayedAttackEffectRecord>(),
                Array.Empty<string>());
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            int teamId,
            EntityBoardPresence boardPresence)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
            };
        }

        private static EnemyJumpRuntimeState CreateJumpState(EnemyJumpPhase phase, int sequence)
        {
            return new EnemyJumpRuntimeState
            {
                phase = phase,
                sequence = sequence,
                sourceCell = new SurfaceCell(FaceId.Floor, 0, 0),
                lockedTargetCell = new SurfaceCell(FaceId.Floor, 1, 0),
                windupEndTick = 2,
                landingTick = 3,
                cooldownRemainingTicks = phase == EnemyJumpPhase.Cooldown ? 2 : 0,
            };
        }

        private static void AssertAirborneSnapshot(WorldSnapshot snapshot, int entityId, int expectedSequence)
        {
            Assert.That(snapshot.TryGetEnemyJumpState(entityId, out var jumpState), Is.True);
            Assert.That(jumpState.phase, Is.EqualTo(EnemyJumpPhase.Airborne));
            Assert.That(jumpState.sequence, Is.EqualTo(expectedSequence));
            Assert.That(snapshot.TryGetResolvedSpatialState(entityId, out var spatialState), Is.True);
            Assert.That(spatialState.Kind, Is.EqualTo(SpatialState.Airborne));
            Assert.That(spatialState.Source, Is.EqualTo(ResolvedSpatialStateSource.JumpAirborne));
        }
    }
}
