using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEngine;
using GameplayTerrainData = Game.Feature.Gameplay.BoardState.TerrainData;

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
            Assert.That(result.PresentationData.GravityFieldEvents, Has.Count.EqualTo(1));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EventKind, Is.EqualTo(GravityFieldPresentationEventKind.Activated));
            Assert.That(result.PresentationData.GravityFieldEvents[0].EmitterEntityId, Is.EqualTo(30));
            Assert.That(result.PresentationData.GravityFieldEvents[0].Cell, Is.EqualTo(new SurfaceCell(FaceId.Floor, 1, 1)));
            Assert.That(result.PresentationData.GravityFieldEvents[0].TargetEntityId, Is.Zero);
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
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(30, 1, out _), Is.True);
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
        }

        [Test]
        [Category("Core")]
        public void GravityField_LockedPushDestroyBoxDoesNotSelfDestroyOnBlockedFallback()
        {
            var terrain = new GameplayTerrainData(new[]
            {
                new TerrainCellState(new SurfaceCell(FaceId.Floor, 2, 0), TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
            });
            var worldState = CreateWorldState(
                new[]
                {
                    CreatePlayer(10, new SurfaceCell(FaceId.Floor, 0, 0)),
                    CreateBox(20, new SurfaceCell(FaceId.Floor, 1, 0), BoxArchetype.Normal, BoxCapabilities.Push | BoxCapabilities.Destroy),
                    CreateBox(30, new SurfaceCell(FaceId.Floor, 1, 1), BoxArchetype.GravityField, BoxCapabilities.Push, GravityFieldPhase.Active, timerTicks: 2),
                },
                terrain);

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
                    GravityFieldPresentationEventKind.Activated,
                    30,
                    new SurfaceCell(FaceId.Floor, 0, 0))));
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
            var hashBuilder = new DeterminismHashBuilder();

            Assert.That(
                hashBuilder.Build(1, snapshot, withPresentationEvent),
                Is.EqualTo(hashBuilder.Build(1, snapshot, baseline)));
            Assert.That(
                hashBuilder.Build(1, snapshot, withVisualState),
                Is.EqualTo(hashBuilder.Build(1, snapshot, baseline)));
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

        private static WorldState CreateWorldState(EntityState[] entities, GameplayTerrainData terrain = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                entities,
                Bounds,
                terrain ?? GameplayTerrainData.Empty,
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

        private static bool TryGetEntity(WorldState worldState, int entityId, out EntityState entity)
        {
            var entities = new System.Collections.Generic.List<EntityState>();
            worldState.CreateSnapshot().EnumerateEntitiesOrdered(entities);
            entity = entities.FirstOrDefault(candidate => candidate.entityId == entityId);
            return entity.entityId == entityId;
        }
    }
}
