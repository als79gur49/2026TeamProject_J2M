using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Core
{
    public sealed class ProjectedWorldFastImportCoreTests
    {
        private static readonly BoardBounds TestBounds = new(
            Vector2Int.zero,
            new Vector2Int(4, 4));

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_ProducesSameSnapshotAsSlowImport()
        {
            var baseSnapshot = CreateRichSnapshot();

            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));

            AssertSnapshotsEquivalent(slowSnapshot, fastSnapshot);
        }

        [Test]
        [Category("Core")]
        public void WorldState_FastImport_PreservesOccupancyAndPlacementQueries()
        {
            var baseSnapshot = CreateRichSnapshot();
            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));
            var unitCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var boxCell = new SurfaceCell(FaceId.Floor, 2, 1);

            Assert.That(fastSnapshot.HasAnyUnitAt(unitCell), Is.EqualTo(slowSnapshot.HasAnyUnitAt(unitCell)));
            Assert.That(fastSnapshot.TryGetPrimaryUnitAt(unitCell, out var fastUnit), Is.EqualTo(slowSnapshot.TryGetPrimaryUnitAt(unitCell, out var slowUnit)));
            Assert.That(fastUnit.entityId, Is.EqualTo(slowUnit.entityId));
            Assert.That(fastSnapshot.TryGetSolidOccupantAt(boxCell, out var fastBox), Is.EqualTo(slowSnapshot.TryGetSolidOccupantAt(boxCell, out var slowBox)));
            Assert.That(fastBox.entityId, Is.EqualTo(slowBox.entityId));
            Assert.That(
                fastSnapshot.TryGetPlacementBlocker(EntityType.Box, unitCell, ignoredEntityId: 0, out var fastBlocker),
                Is.EqualTo(slowSnapshot.TryGetPlacementBlocker(EntityType.Box, unitCell, ignoredEntityId: 0, out var slowBlocker)));
            Assert.That(fastBlocker.Kind, Is.EqualTo(slowBlocker.Kind));
            Assert.That(fastBlocker.EntityId, Is.EqualTo(slowBlocker.EntityId));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_PreservesStackedUnitsTileFeaturesAndOrderedEnumeration()
        {
            var baseSnapshot = CreateRichSnapshot();
            var slowSnapshot = SnapshotBuilder.Create(ProjectedWorld.MaterializeWorldStateSlowForTest(baseSnapshot));
            var fastSnapshot = SnapshotBuilder.Create(WorldState.CreateFromSnapshotFast(baseSnapshot));
            var stackedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var tileFeatureCell = new SurfaceCell(FaceId.Floor, 0, 1);

            CollectionAssert.AreEqual(
                CollectUnitOccupancyIdsAt(slowSnapshot, stackedCell),
                CollectUnitOccupancyIdsAt(fastSnapshot, stackedCell));
            CollectionAssert.AreEqual(
                CollectTileFeatureIdsAt(slowSnapshot, tileFeatureCell),
                CollectTileFeatureIdsAt(fastSnapshot, tileFeatureCell));
            CollectionAssert.AreEqual(
                Collect<EntityState>(slowSnapshot.EnumerateEntitiesOrdered).Select(entity => entity.entityId).ToArray(),
                Collect<EntityState>(fastSnapshot.EnumerateEntitiesOrdered).Select(entity => entity.entityId).ToArray());
            CollectionAssert.AreEqual(
                Collect<TileFeatureState>(slowSnapshot.EnumerateTileFeaturesOrdered).Select(tile => tile.TileId).ToArray(),
                Collect<TileFeatureState>(fastSnapshot.EnumerateTileFeaturesOrdered).Select(tile => tile.TileId).ToArray());
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_DoesNotUseSlowBaseSpawnPath()
        {
            var baseSnapshot = CreateRichSnapshot();
            var projectedWorld = new ProjectedWorld(baseSnapshot);

            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                projectedWorld.CreateSnapshot(ProjectedWorldSnapshotReason.PlanPostPreMovement);
                counts = capture.Counts;
            }

            Assert.That(counts.FastBaseSnapshotImportCount, Is.EqualTo(1));
            Assert.That(counts.SlowBaseSnapshotImportCount, Is.EqualTo(0));
            Assert.That(counts.FastImportedEntityCount, Is.EqualTo(baseSnapshot.EntityCount));
            Assert.That(counts.FastImportedTileFeatureCount, Is.EqualTo(baseSnapshot.TileFeatureCount));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_OverlayEntityOperationStillValidates()
        {
            var baseSnapshot = CreateRichSnapshot();
            var invalidProjectedWorld = new ProjectedWorld(baseSnapshot);
            var invalidBatch = new FinalizationBatch();
            invalidBatch.SpawnEntity(CreateBox(90, new SurfaceCell(FaceId.Floor, 8, 8)));
            invalidProjectedWorld.ApplyBatch(invalidBatch);

            Assert.Throws<InvalidOperationException>(() => invalidProjectedWorld.CreateSnapshot());

            var validProjectedWorld = new ProjectedWorld(baseSnapshot);
            var validBatch = new FinalizationBatch();
            validBatch.SpawnEntity(CreateBox(91, new SurfaceCell(FaceId.Floor, 4, 4)));
            validProjectedWorld.ApplyBatch(validBatch);

            var projectedSnapshot = validProjectedWorld.CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetEntity(91, out var entity), Is.True);
            Assert.That(entity.position, Is.EqualTo(new SurfaceCell(FaceId.Floor, 4, 4)));
        }

        [Test]
        [Category("Core")]
        public void ProjectedWorld_FastImport_OverlayTileFeatureOperationStillValidates()
        {
            var baseSnapshot = CreateRichSnapshot();
            var invalidProjectedWorld = new ProjectedWorld(baseSnapshot);
            var invalidFeature = CreateTileFeature(90, new SurfaceCell(FaceId.Floor, 8, 8), TileFeatureKind.Button);

            Assert.Throws<InvalidOperationException>(
                () => invalidProjectedWorld.ApplyTileFeatureOperations(
                    new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(invalidFeature) })));

            var validProjectedWorld = new ProjectedWorld(baseSnapshot);
            var validFeature = CreateTileFeature(91, new SurfaceCell(FaceId.Floor, 4, 4), TileFeatureKind.Button);
            validProjectedWorld.ApplyTileFeatureOperations(
                new TileFeatureOperationBatch(new[] { TileFeatureOperation.Add(validFeature) }));

            var projectedSnapshot = validProjectedWorld.CreateSnapshot();

            Assert.That(projectedSnapshot.TryGetTileFeature(91, out var tileFeature), Is.True);
            Assert.That(tileFeature, Is.EqualTo(validFeature));
        }

        private static WorldSnapshot CreateRichSnapshot()
        {
            var stackedCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var worldState = GameplayCompositionRoot.CreateWorldState(
                new[]
                {
                    CreateUnit(10, stackedCell, UnitRole.Player, teamId: 1),
                    CreateUnit(20, stackedCell, UnitRole.Enemy, teamId: 2),
                    CreateUnit(30, new SurfaceCell(FaceId.Floor, 0, 2), UnitRole.Enemy, teamId: 2),
                    CreateUnit(60, new SurfaceCell(FaceId.Floor, 3, 3), UnitRole.Enemy, teamId: 2, boardPresence: EntityBoardPresence.Detached),
                    CreateBox(40, new SurfaceCell(FaceId.Floor, 2, 1)),
                },
                TestBounds,
                new CubeTopologyState(FaceId.Floor),
                new[]
                {
                    CreateTileFeature(20, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Button),
                    CreateTileFeature(10, new SurfaceCell(FaceId.Floor, 0, 1), TileFeatureKind.Slide),
                    CreateTileFeature(30, new SurfaceCell(FaceId.Front, 1, 0), TileFeatureKind.Button),
                });
            var writeContext = worldState.CreateWriteContext();
            writeContext.SetPlayerControlState(
                10,
                new PlayerControlState
                {
                    nextExplicitActionAllowedTick = 7,
                    actionSequenceCounter = 3,
                    activeAction = new PlayerActionRuntimeState
                    {
                        kind = PlayerActionKind.Push,
                        sequence = 3,
                        direction = Direction.Right,
                        targetEntityId = 40,
                        startTick = 2,
                        executeTick = 4,
                        recoveryEndTick = 6,
                    },
                });
            writeContext.SetPlayerDamageState(10, new PlayerDamageState { nextDamageAllowedTick = 11 });
            writeContext.SetEnemyActionState(
                20,
                new EnemyActionRuntimeState
                {
                    kind = EnemyActionKind.Melee,
                    sequence = 5,
                    lockedTargetEntityId = 10,
                    direction = Direction.Left,
                    startTick = 8,
                    executeTick = 9,
                });
            writeContext.SetEnemyPatrolState(
                20,
                new EnemyPatrolRuntimeState
                {
                    sequence = 6,
                    homeCell = stackedCell,
                    lastCommittedDirection = Direction.Right,
                });
            writeContext.SetEnemyChargeState(
                20,
                new EnemyChargeRuntimeState
                {
                    sequence = 7,
                    phase = EnemyChargePhase.Active,
                    lockedDirection = Direction.Left,
                    remainingActiveSteps = 2,
                });
            writeContext.SetEnemyGlideState(
                20,
                EnemyGlideRuntimeState.Create(
                    EnemyGlidePhase.Cooldown,
                    sequence: 8,
                    windupUntilTickExclusive: 0,
                    activeUntilTickExclusive: 0,
                    recoveryUntilTickExclusive: 0,
                    cooldownUntilTickExclusive: 20,
                    windupTicks: 1,
                    durationTicks: 2,
                    recoveryTicks: 3,
                    cooldownTicks: 4,
                    lastExitedTick: 16));
            writeContext.SetEntityExecutionLockState(
                20,
                new EntityExecutionLockState
                {
                    phase = EntityExecutionPhase.Attack,
                    sequence = 9,
                    unlockTickExclusive = 30,
                });
            writeContext.SetEnemyJumpState(
                60,
                new EnemyJumpRuntimeState
                {
                    phase = EnemyJumpPhase.Airborne,
                    sequence = 10,
                    sourceCell = new SurfaceCell(FaceId.Floor, 3, 3),
                    lockedTargetCell = new SurfaceCell(FaceId.Floor, 3, 2),
                    landingTick = 18,
                });
            writeContext.SetEnemyUtilityState(20, new EnemyUtilityRuntimeState(Array.Empty<EnemyUtilityEffectState>()));
            writeContext.SetBoxInteractionLockState(
                40,
                new BoxInteractionLockState(sourceEntityId: 20, sourceEffectIndex: 1, expiresTickExclusive: 40, blocksPush: true, blocksFlip: false));
            writeContext.SetEnemyGravityFieldAuraFieldState(
                1,
                new EnemyGravityFieldAuraFieldState(
                    sourceEntityId: 40,
                    sourceEffectIndex: 1,
                    activationSequence: 1,
                    originCell: new SurfaceCell(FaceId.Floor, 2, 1),
                    radius: 2,
                    startedTick: 3,
                    expiresTickExclusive: 20,
                    blocksPush: true,
                    blocksFlip: true,
                    blocksDestroy: false));
            writeContext.SetUnitKinematicState(20, CreateKinematicState());
            writeContext.SetUnitContinuousLocomotionState(10, CreateContinuousLocomotionState());
            writeContext.SetPhasedState(30, PhasedRuntimeStateQueries.BeginMovementPreMovement(default, tickIndex: 5, exitTickExclusive: 12));
            writeContext.SetSummonedEntityState(30, new SummonedEntityState(sourceEntityId: 20, sourceEffectIndex: 2));
            writeContext.SetEnemyDefinitionBindingState(20, new EnemyDefinitionBindingState(new EnemyUnitArchetypeId("FastImportParityEnemy")));
            writeContext.AddPendingCellImpact(
                new PendingCellImpact(
                    impactId: 1,
                    ownerId: 20,
                    sourceEnemyId: 20,
                    sourceCell: new SurfaceCell(FaceId.Floor, 0, 0),
                    targetCell: new SurfaceCell(FaceId.Floor, 1, 0),
                    launchTopology: new CubeTopologyState(FaceId.Floor),
                    direction: Direction.Down,
                    damage: 1,
                    createdTick: 6,
                    releaseTick: 8,
                    impactTick: 10));

            return worldState.CreateSnapshot();
        }

        private static void AssertSnapshotsEquivalent(WorldSnapshot expected, WorldSnapshot actual)
        {
            Assert.That(actual.BoardBounds, Is.EqualTo(expected.BoardBounds));
            Assert.That(actual.Topology, Is.EqualTo(expected.Topology));
            Assert.That(actual.TopologyRevision, Is.EqualTo(expected.TopologyRevision));
            CollectionAssert.AreEqual(Collect<EntityState>(expected.EnumerateEntitiesOrdered), Collect<EntityState>(actual.EnumerateEntitiesOrdered));
            CollectionAssert.AreEqual(Collect<TileFeatureState>(expected.EnumerateTileFeaturesOrdered), Collect<TileFeatureState>(actual.EnumerateTileFeaturesOrdered));
            CollectionAssert.AreEqual(Collect<SnapshotOccupancyEntry>(expected.EnumerateUnitOccupancyOrdered), Collect<SnapshotOccupancyEntry>(actual.EnumerateUnitOccupancyOrdered));
            CollectionAssert.AreEqual(Collect<SnapshotOccupancyEntry>(expected.EnumerateSolidOccupancyOrdered), Collect<SnapshotOccupancyEntry>(actual.EnumerateSolidOccupancyOrdered));
            CollectionAssert.AreEqual(Collect<PlayerControlSnapshotEntry>(expected.EnumeratePlayerControlStatesOrdered), Collect<PlayerControlSnapshotEntry>(actual.EnumeratePlayerControlStatesOrdered));
            CollectionAssert.AreEqual(Collect<PlayerDamageSnapshotEntry>(expected.EnumeratePlayerDamageStatesOrdered), Collect<PlayerDamageSnapshotEntry>(actual.EnumeratePlayerDamageStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyActionSnapshotEntry>(expected.EnumerateEnemyActionStatesOrdered), Collect<EnemyActionSnapshotEntry>(actual.EnumerateEnemyActionStatesOrdered));
            CollectionAssert.AreEqual(Collect<PendingCellImpactSnapshotEntry>(expected.EnumeratePendingCellImpactsOrdered), Collect<PendingCellImpactSnapshotEntry>(actual.EnumeratePendingCellImpactsOrdered));
            CollectionAssert.AreEqual(Collect<EnemyPatrolSnapshotEntry>(expected.EnumerateEnemyPatrolStatesOrdered), Collect<EnemyPatrolSnapshotEntry>(actual.EnumerateEnemyPatrolStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyChargeSnapshotEntry>(expected.EnumerateEnemyChargeStatesOrdered), Collect<EnemyChargeSnapshotEntry>(actual.EnumerateEnemyChargeStatesOrdered));
            CollectionAssert.AreEqual(Collect<EntityExecutionLockSnapshotEntry>(expected.EnumerateEntityExecutionLockStatesOrdered), Collect<EntityExecutionLockSnapshotEntry>(actual.EnumerateEntityExecutionLockStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyJumpSnapshotEntry>(expected.EnumerateEnemyJumpStatesOrdered), Collect<EnemyJumpSnapshotEntry>(actual.EnumerateEnemyJumpStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyGlideSnapshotEntry>(expected.EnumerateEnemyGlideStatesOrdered), Collect<EnemyGlideSnapshotEntry>(actual.EnumerateEnemyGlideStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyUtilitySnapshotEntry>(expected.EnumerateEnemyUtilityStatesOrdered), Collect<EnemyUtilitySnapshotEntry>(actual.EnumerateEnemyUtilityStatesOrdered));
            CollectionAssert.AreEqual(Collect<BoxInteractionLockSnapshotEntry>(expected.EnumerateBoxInteractionLockStatesOrdered), Collect<BoxInteractionLockSnapshotEntry>(actual.EnumerateBoxInteractionLockStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyGravityFieldAuraFieldSnapshotEntry>(expected.EnumerateEnemyGravityFieldAuraFieldStatesOrdered), Collect<EnemyGravityFieldAuraFieldSnapshotEntry>(actual.EnumerateEnemyGravityFieldAuraFieldStatesOrdered));
            CollectionAssert.AreEqual(Collect<SummonedEntitySnapshotEntry>(expected.EnumerateSummonedEntityStatesOrdered), Collect<SummonedEntitySnapshotEntry>(actual.EnumerateSummonedEntityStatesOrdered));
            CollectionAssert.AreEqual(Collect<EnemyDefinitionBindingSnapshotEntry>(expected.EnumerateEnemyDefinitionBindingStatesOrdered), Collect<EnemyDefinitionBindingSnapshotEntry>(actual.EnumerateEnemyDefinitionBindingStatesOrdered));
            CollectionAssert.AreEqual(Collect<UnitKinematicSnapshotEntry>(expected.EnumerateUnitKinematicStatesOrdered), Collect<UnitKinematicSnapshotEntry>(actual.EnumerateUnitKinematicStatesOrdered));
            CollectionAssert.AreEqual(Collect<UnitContinuousLocomotionSnapshotEntry>(expected.EnumerateUnitContinuousLocomotionStatesOrdered), Collect<UnitContinuousLocomotionSnapshotEntry>(actual.EnumerateUnitContinuousLocomotionStatesOrdered));
            CollectionAssert.AreEqual(Collect<PhasedSnapshotEntry>(expected.EnumeratePhasedStatesOrdered), Collect<PhasedSnapshotEntry>(actual.EnumeratePhasedStatesOrdered));
        }

        private static List<T> Collect<T>(Action<List<T>> enumerate)
        {
            var buffer = new List<T>();
            enumerate(buffer);
            return buffer;
        }

        private static int[] CollectUnitOccupancyIdsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            return Collect<SnapshotOccupancyEntry>(snapshot.EnumerateUnitOccupancyOrdered)
                .Where(entry => entry.Cell == cell)
                .Select(entry => entry.EntityId)
                .ToArray();
        }

        private static int[] CollectTileFeatureIdsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            var tileFeatures = new List<TileFeatureState>();
            snapshot.EnumerateTileFeaturesAt(cell, tileFeatures);
            return tileFeatures.Select(tile => tile.TileId).ToArray();
        }

        private static TileFeatureState CreateTileFeature(int tileId, SurfaceCell cell, TileFeatureKind kind)
        {
            return new TileFeatureState(
                tileId,
                cell,
                kind,
                TileFeatureFlags.None,
                sourceEntityId: 0,
                ownerEntityId: 0,
                teamId: 0,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static EntityState CreateUnit(
            int entityId,
            SurfaceCell position,
            UnitRole unitRole,
            int teamId,
            EntityBoardPresence boardPresence = EntityBoardPresence.Occupying)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = teamId,
                type = EntityType.Unit,
                unitRole = unitRole,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = boardPresence,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 2,
                maxHp = 2,
                teamId = 0,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip,
            };
        }

        private static UnitKinematicRuntimeState CreateKinematicState()
        {
            return new UnitKinematicRuntimeState
            {
                localOffset = new SimulationOffset2(KinematicFixed.FromRaw(256), KinematicFixed.Zero),
                velocity = new SimulationVelocity2(KinematicFixed.FromRaw(256), KinematicFixed.Zero),
                mode = MotionMode.Voluntary,
                forcedOp = ForcedMotionOp.None,
                remainingDistanceUnits = 1024,
                remainingTicks = 3,
                speedScalePermille = 1000,
                sequenceId = 4,
                elapsedTicks = 1,
                totalTicks = 5,
                commitTick = 12,
                startedTick = 7,
                stepDirectionX = 1,
            }.NormalizedForStorage();
        }

        private static UnitContinuousLocomotionState CreateContinuousLocomotionState()
        {
            return new UnitContinuousLocomotionState
            {
                localOffset = new SimulationOffset2(KinematicFixed.FromRaw(-256), KinematicFixed.Zero),
                velocity = new SimulationVelocity2(KinematicFixed.FromRaw(-256), KinematicFixed.Zero),
                facing = Direction.Left,
                lastMoveDirection = Direction.Left,
                speedUnitsPerTick = 256,
                mode = ContinuousLocomotionMode.Moving,
                sequenceId = 5,
            }.NormalizedForStorage();
        }
    }
}
