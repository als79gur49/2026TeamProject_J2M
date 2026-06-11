using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.TileFeatureAudio;
using NUnit.Framework;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class MoonBlockGeneratorRespawnTests
    {
        private static readonly BoardBounds Bounds = new(
            new UnityEngine.Vector2Int(0, 0),
            new UnityEngine.Vector2Int(4, 4));
        private static readonly SurfaceCell GeneratorCell = new(FaceId.Floor, 1, 1);
        private static readonly SurfaceCell InitialMoonCell = new(FaceId.Floor, 2, 1);

        [Test]
        [Category("Core")]
        public void AliveMoonBlockAnywhere_NoOps()
        {
            var moon = CreateMoonBlock(20, InitialMoonCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), moon });
            var pipeline = CreatePipeline(worldState, moon);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out var finalMoon), Is.True);
            Assert.That(finalMoon.position, Is.EqualTo(InitialMoonCell));
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorRespawnCommitted"));
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            Assert.That(result.PresentationData.VisibilityChanges.Select(change => change.EntityId), Has.No.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void MissingMoonBlock_ActiveGenerator_RespawnsAtGeneratorCellWithStableId()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var worldState = CreateWorld(new[] { CreatePlayer() });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.position, Is.EqualTo(GeneratorCell));
            Assert.That(moon.entityId, Is.EqualTo(20));
            Assert.That(moon.type, Is.EqualTo(EntityType.Box));
            Assert.That(moon.boxArchetype, Is.EqualTo(BoxArchetype.Moon));
            Assert.That((moon.boxCapabilities & RequiredMoonCapabilities), Is.EqualTo(RequiredMoonCapabilities));
            Assert.That(moon.hp, Is.EqualTo(template.maxHp));
            Assert.That(moon.maxHp, Is.EqualTo(template.maxHp));
            Assert.That(moon.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(snapshot.TryGetSolidOccupantAt(GeneratorCell, out var solidOccupant), Is.True);
            Assert.That(solidOccupant.entityId, Is.EqualTo(20));
            Assert.That(snapshot.TryGetActiveBoxInteractionLockState(20, 1, out var lockState), Is.True);
            Assert.That(lockState.BlocksPush, Is.True);
            Assert.That(lockState.BlocksFlip, Is.True);
            Assert.That(lockState.BlocksDestroy, Is.False);
            Assert.That(lockState.ExpiresTickExclusive, Is.EqualTo(1 + MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks));
            Assert.That(lockState.SourceReason, Is.EqualTo(BoxInteractionLockSourceReason.MoonBlockGeneratorSpawn));
            Assert.That(moon.markedForDeath, Is.False);
            Assert.That(moon.kineticInstigatorEntityId, Is.Zero);
            Assert.That(moon.kineticInstigatorTeamId, Is.Zero);
            Assert.That(moon.facing, Is.EqualTo(template.facing));
            AssertMoonBlockGeneratedEvent(result, moonBlockEntityId: 20);
            Assert.That(result.PresentationData.VisibilityChanges.Select(change => change.EntityId), Has.No.EqualTo(20));
        }

        [Test]
        [Category("Core")]
        public void InactiveGenerator_DefersUntilTopologyMakesCellBottomFace()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var worldState = CreateWorld(
                new[] { CreatePlayer() },
                topology: new CubeTopologyState(FaceId.Front));
            var pipeline = CreatePipeline(worldState, template);

            var inactiveResult = pipeline.RunTick(new TickInput(1));
            Assert.That(GameplayCompositionRoot.CreateSnapshot(worldState).TryGetEntity(20, out _), Is.False);
            Assert.That(inactiveResult.PresentationData.TileEvents, Is.Empty);

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Floor));
            var respawnResult = pipeline.RunTick(new TickInput(2));

            Assert.That(GameplayCompositionRoot.CreateSnapshot(worldState).TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.position, Is.EqualTo(GeneratorCell));
            AssertMoonBlockGeneratedEvent(respawnResult, moonBlockEntityId: 20);
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellNormalBox_DestroysBlockingBoxThenSpawnsMoonBlock()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var blocker = CreateBox(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), blocker });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.position, Is.EqualTo(GeneratorCell));
            Assert.That(snapshot.TryGetEntity(30, out var finalBlocker), Is.True);
            Assert.That(finalBlocker.boardPresence, Is.EqualTo(EntityBoardPresence.Detached));
            Assert.That(finalBlocker.markedForDeath, Is.True);
            Assert.That(snapshot.TryGetSolidOccupantAt(GeneratorCell, out var solid), Is.True);
            Assert.That(solid.entityId, Is.EqualTo(20));
            AssertMoonBlockGeneratedEvent(result, moonBlockEntityId: 20);
        }

        [Test]
        [Category("Core")]
        public void SpawnInteractionLock_ExpiresOnExclusiveTick()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var worldState = CreateWorld(new[] { CreatePlayer() });
            var pipeline = CreatePipeline(worldState, template);

            pipeline.RunTick(new TickInput(1));
            var activeSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            Assert.That(activeSnapshot.TryGetActiveBoxInteractionLockState(20, 7, out _), Is.True);

            pipeline.RunTick(new TickInput(8));
            var expiredSnapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(expiredSnapshot.TryGetActiveBoxInteractionLockState(20, 8, out _), Is.False);
            Assert.That(expiredSnapshot.TryGetBoxInteractionLockState(20, out _), Is.False);
            Assert.That(expiredSnapshot.TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellUnit_DefersWithoutKillingOrMovingUnit()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var unit = CreateEnemy(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), unit });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out var finalUnit), Is.True);
            Assert.That(finalUnit.position, Is.EqualTo(GeneratorCell));
            Assert.That(finalUnit.hp, Is.EqualTo(unit.hp));
            Assert.That(finalUnit.boardPresence, Is.EqualTo(EntityBoardPresence.Occupying));
            Assert.That(finalUnit.markedForDeath, Is.False);
            AssertMoonBlockGeneratorBlockedEvent(
                result,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellPlayerUnit_DefersWithUnitOccupantPayload()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var player = CreatePlayer(GeneratorCell);
            var worldState = CreateWorld(new[] { player });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));

            AssertMoonBlockGeneratorBlockedEvent(
                result,
                blockerEntityId: 10,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellProjectile_DoesNotBlockSpawn()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var projectile = CreateProjectile(40, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), projectile });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.position, Is.EqualTo(GeneratorCell));
            Assert.That(snapshot.TryGetEntity(40, out var finalProjectile), Is.True);
            Assert.That(finalProjectile.position, Is.EqualTo(GeneratorCell));
            AssertMoonBlockGeneratedEvent(result, moonBlockEntityId: 20);
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellWallLikeSolid_Defers()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var wall = CreateWall(50, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), wall });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(50, out var finalWall), Is.True);
            Assert.That(finalWall.position, Is.EqualTo(GeneratorCell));
            AssertMoonBlockGeneratorBlockedEvent(
                result,
                blockerEntityId: 50,
                MoonBlockGeneratorBlockedReason.WallLikeSolid);
        }

        [Test]
        [Category("Core")]
        public void GeneratorCellPlacementBlocked_DefersWithPlacementPayload()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var terrain = new TerrainData(new[]
            {
                new TerrainCellState(GeneratorCell, TerrainKind.Generic, TerrainFlags.BlocksGroundTraversal),
            });
            var worldState = CreateWorld(new[] { CreatePlayer() }, terrainData: terrain);
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));

            Assert.That(GameplayCompositionRoot.CreateSnapshot(worldState).TryGetEntity(20, out _), Is.False);
            AssertMoonBlockGeneratorBlockedEvent(
                result,
                blockerEntityId: 0,
                MoonBlockGeneratorBlockedReason.PlacementBlocked);
        }

        [Test]
        [Category("Core")]
        public void RepeatedUnitConflictDefer_EmitsBlockedEventRequestAudioOnce()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var unit = CreateEnemy(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), unit });
            var pipeline = CreatePipeline(worldState, template);
            var requestPlanner = new TilePresentationRequestPlanner();
            var audioPlanner = new TileFeatureAudioRequestPlanner();

            var first = pipeline.RunTick(new TickInput(1));
            var second = pipeline.RunTick(new TickInput(2));

            AssertGeneratorDeferEmitsBlocked(
                first,
                requestPlanner,
                audioPlanner,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
            AssertGeneratorDeferIsSilent(second, requestPlanner, audioPlanner);
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);
            Assert.That(snapshot.TryGetEntity(20, out _), Is.False);
            Assert.That(snapshot.TryGetEntity(30, out var finalUnit), Is.True);
            Assert.That(finalUnit.position, Is.EqualTo(GeneratorCell));
            Assert.That(finalUnit.markedForDeath, Is.False);
        }

        [Test]
        [Category("Core")]
        public void UnitConflict_ClearThenReappear_EmitsBlockedAgainAfterGeneratedClearsDebounce()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var unit = CreateEnemy(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), unit });
            var pipeline = CreatePipeline(worldState, template);

            var first = pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().RemoveEntity(30);
            var generated = pipeline.RunTick(new TickInput(2));
            worldState.CreateWriteContext().RemoveEntity(20);
            worldState.CreateWriteContext().SpawnEntity(CreateEnemy(30, GeneratorCell));
            var blockedAgain = pipeline.RunTick(new TickInput(3));

            AssertMoonBlockGeneratorBlockedEvent(
                first,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
            AssertMoonBlockGeneratedEvent(generated, moonBlockEntityId: 20);
            AssertMoonBlockGeneratorBlockedEvent(
                blockedAgain,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
        }

        [Test]
        [Category("Core")]
        public void UnitConflict_DifferentBlockingEntity_EmitsBlockedAgain()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var unit = CreateEnemy(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), unit });
            var pipeline = CreatePipeline(worldState, template);

            var first = pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().RemoveEntity(30);
            worldState.CreateWriteContext().SpawnEntity(CreateEnemy(31, GeneratorCell));
            var second = pipeline.RunTick(new TickInput(2));

            AssertMoonBlockGeneratorBlockedEvent(
                first,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
            AssertMoonBlockGeneratorBlockedEvent(
                second,
                blockerEntityId: 31,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
        }

        [Test]
        [Category("Core")]
        public void BlockedReasonChange_EmitsBlockedAgain()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var unit = CreateEnemy(30, GeneratorCell);
            var worldState = CreateWorld(new[] { CreatePlayer(), unit });
            var pipeline = CreatePipeline(worldState, template);

            var first = pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().RemoveEntity(30);
            worldState.CreateWriteContext().SpawnEntity(CreateWall(50, GeneratorCell));
            var second = pipeline.RunTick(new TickInput(2));

            AssertMoonBlockGeneratorBlockedEvent(
                first,
                blockerEntityId: 30,
                MoonBlockGeneratorBlockedReason.UnitOccupant);
            AssertMoonBlockGeneratorBlockedEvent(
                second,
                blockerEntityId: 50,
                MoonBlockGeneratorBlockedReason.WallLikeSolid);
        }

        [Test]
        [Category("Core")]
        public void CleanupRemovedMoonBlock_RespawnsAfterCleanup()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var deadMoon = template;
            deadMoon.hp = 0;
            deadMoon.markedForDeath = true;
            var worldState = CreateWorld(new[] { CreatePlayer(), deadMoon });
            var pipeline = CreatePipeline(worldState, template);

            var result = pipeline.RunTick(new TickInput(1));
            var snapshot = GameplayCompositionRoot.CreateSnapshot(worldState);

            Assert.That(result.EventLog, Does.Contain("CleanupRemoved|E=20"));
            Assert.That(snapshot.TryGetEntity(20, out var moon), Is.True);
            Assert.That(moon.position, Is.EqualTo(GeneratorCell));
            Assert.That(moon.hp, Is.EqualTo(template.maxHp));
            Assert.That(moon.markedForDeath, Is.False);
            AssertMoonBlockGeneratedEvent(result, moonBlockEntityId: 20);
        }

        [Test]
        [Category("Core")]
        public void RespawnHash_IsDeterministicAndDiffersFromNoOp()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var firstRespawn = RunSingleTick(CreateWorld(new[] { CreatePlayer() }), template);
            var secondRespawn = RunSingleTick(CreateWorld(new[] { CreatePlayer() }), template);
            var noOp = RunSingleTick(CreateWorld(new[] { CreatePlayer(), template }), template);

            Assert.That(firstRespawn.DeterminismHash, Is.Not.Empty);
            Assert.That(firstRespawn.DeterminismHash, Is.EqualTo(secondRespawn.DeterminismHash));
            Assert.That(firstRespawn.DeterminismHash, Is.Not.EqualTo(noOp.DeterminismHash));
            Assert.That(firstRespawn.EventLog, Does.Contain("MoonBlockGeneratorRespawnCommitted|TileId=100|E=20|Pos=(1,1)|Face=Floor|Tick=1"));
            Assert.That(noOp.EventLog, Has.None.Contains("MoonBlockGeneratorRespawnCommitted"));
            AssertMoonBlockGeneratedEvent(firstRespawn, moonBlockEntityId: 20);
            Assert.That(noOp.PresentationData.TileEvents, Is.Empty);
        }

        [Test]
        [Category("Core")]
        public void ConflictDestroyAndRespawnHash_IsDeterministic()
        {
            var template = CreateMoonBlock(20, InitialMoonCell);
            var first = RunSingleTick(CreateWorld(new[] { CreatePlayer(), CreateBox(30, GeneratorCell) }), template);
            var second = RunSingleTick(CreateWorld(new[] { CreatePlayer(), CreateBox(30, GeneratorCell) }), template);

            Assert.That(first.DeterminismHash, Is.Not.Empty);
            Assert.That(first.DeterminismHash, Is.EqualTo(second.DeterminismHash));
            Assert.That(first.EventLog, Does.Contain("MoonBlockGeneratorBlockingBoxDestroyed|TileId=100|Blocker=30|E=20|Tick=1"));
            Assert.That(first.EventLog, Does.Contain("MoonBlockGeneratorRespawnCommitted|TileId=100|E=20|Pos=(1,1)|Face=Floor|Tick=1"));
            AssertMoonBlockGeneratedEvent(first, moonBlockEntityId: 20);
        }

        private static void AssertMoonBlockGeneratedEvent(TickResult result, int moonBlockEntityId)
        {
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.PresentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.MoonBlockGenerated));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(GeneratorCell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(moonBlockEntityId));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.None));
            Assert.That(tileEvent.SourceEntityId, Is.EqualTo(101));
            Assert.That(tileEvent.OwnerEntityId, Is.EqualTo(102));
            Assert.That(tileEvent.TeamId, Is.EqualTo(7));
            Assert.That(tileEvent.SpawnTick, Is.EqualTo(result.TickIndex));
            Assert.That(tileEvent.SpawnInteractionLockTicks, Is.EqualTo(MoonBlockGeneratorRespawnDefaults.SpawnInteractionLockTicks));
        }

        private static void AssertMoonBlockGeneratorBlockedEvent(
            TickResult result,
            int blockerEntityId,
            MoonBlockGeneratorBlockedReason reason)
        {
            Assert.That(result.PresentationData.TileEvents, Has.Count.EqualTo(1));
            var tileEvent = result.PresentationData.TileEvents[0];
            Assert.That(tileEvent.EventKind, Is.EqualTo(TilePresentationEventKind.MoonBlockGeneratorBlocked));
            Assert.That(tileEvent.TileId, Is.EqualTo(100));
            Assert.That(tileEvent.Cell, Is.EqualTo(GeneratorCell));
            Assert.That(tileEvent.TileFeatureKind, Is.EqualTo(TileFeatureKind.MoonBlockGenerator));
            Assert.That(tileEvent.TargetEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(tileEvent.Direction, Is.EqualTo(Direction.None));
            Assert.That(tileEvent.SourceEntityId, Is.EqualTo(101));
            Assert.That(tileEvent.OwnerEntityId, Is.EqualTo(102));
            Assert.That(tileEvent.TeamId, Is.EqualTo(7));
            Assert.That(tileEvent.MoonBlockGeneratorBlockedPayload.IsValid, Is.True);
            Assert.That(tileEvent.MoonBlockGeneratorBlockedPayload.Reason, Is.EqualTo(reason));
            Assert.That(tileEvent.MoonBlockGeneratorBlockedPayload.BlockingEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(tileEvent.MoonBlockGeneratorBlockedPayload.BlockedCell, Is.EqualTo(GeneratorCell));
            Assert.That(
                result.PresentationData.TileEvents.Any(evt => evt.EventKind == TilePresentationEventKind.MoonBlockGenerated),
                Is.False);
        }

        private static void AssertGeneratorDeferEmitsBlocked(
            TickResult result,
            TilePresentationRequestPlanner requestPlanner,
            TileFeatureAudioRequestPlanner audioPlanner,
            int blockerEntityId,
            MoonBlockGeneratorBlockedReason reason)
        {
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorRespawnCommitted"));
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorBlockingBoxDestroyed"));
            AssertMoonBlockGeneratorBlockedEvent(result, blockerEntityId, reason);
            var requests = requestPlanner.BuildRequests(result.PresentationData);
            Assert.That(requests, Has.Count.EqualTo(1));
            Assert.That(requests[0].RequestKind, Is.EqualTo(TilePresentationRequestKind.MoonBlockGeneratorBlocked));
            Assert.That(requests[0].TargetEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(requests[0].MoonBlockGeneratorBlockedPayload.Reason, Is.EqualTo(reason));
            Assert.That(requests[0].MoonBlockGeneratorBlockedPayload.BlockingEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(requests[0].MoonBlockGeneratorBlockedPayload.BlockedCell, Is.EqualTo(GeneratorCell));
            var audioRequests = audioPlanner.BuildRequests(requests);
            Assert.That(audioRequests, Has.Count.EqualTo(1));
            Assert.That(audioRequests[0].Cue, Is.EqualTo(TileFeatureAudioCue.MoonBlockGeneratorBlocked));
            Assert.That(audioRequests[0].TargetEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(audioRequests[0].MoonBlockGeneratorBlockedPayload.Reason, Is.EqualTo(reason));
            Assert.That(audioRequests[0].MoonBlockGeneratorBlockedPayload.BlockingEntityId, Is.EqualTo(blockerEntityId));
            Assert.That(audioRequests[0].MoonBlockGeneratorBlockedPayload.BlockedCell, Is.EqualTo(GeneratorCell));
        }

        private static void AssertGeneratorDeferIsSilent(
            TickResult result,
            TilePresentationRequestPlanner requestPlanner,
            TileFeatureAudioRequestPlanner audioPlanner)
        {
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorRespawnCommitted"));
            Assert.That(result.EventLog, Has.None.Contains("MoonBlockGeneratorBlockingBoxDestroyed"));
            Assert.That(result.PresentationData.TileEvents, Is.Empty);
            var requests = requestPlanner.BuildRequests(result.PresentationData);
            Assert.That(requests, Is.Empty);
            Assert.That(audioPlanner.BuildRequests(requests), Is.Empty);
        }

        private static TickResult RunSingleTick(WorldState worldState, EntityState template)
        {
            return CreatePipeline(worldState, template).RunTick(new TickInput(1));
        }

        private static TickPipeline CreatePipeline(WorldState worldState, EntityState template)
        {
            var timing = GameplayTimingProfile.CreateDefault();
            return new GameplayBootstrapper(EmptyEntityLogicProvider.Instance)
                .CreateTickPipeline(
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    timing,
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        timing.SimulationTicksPerSecond,
                        timing.RepeatedMoveIntervalSeconds),
                    playerRespawnDelayTicks: 1,
                    objectiveDefinition: null,
                    allowPlayerRespawn: true,
                    runtimeFeatureFlags: default,
                    playerKinematicLocomotionTiming: default,
                    playerContinuousLocomotion: default,
                    tileFeatureDefinitions: CreateTileFeatureDefinitions(),
                    moonBlockRespawnDefinitions: new[] { CreateRespawnDefinition(template) });
        }

        private static WorldState CreateWorld(
            IReadOnlyList<EntityState> entities,
            CubeTopologyState topology = default,
            TerrainData terrainData = null)
        {
            if (topology.Equals(default(CubeTopologyState)))
            {
                topology = new CubeTopologyState(FaceId.Floor);
            }

            return GameplayCompositionRoot.CreateWorldState(
                entities,
                Bounds,
                terrainData ?? TerrainData.Empty,
                topology,
                new[] { CreateGeneratorTileFeatureState() });
        }

        private static MoonBlockRespawnDefinition CreateRespawnDefinition(EntityState template)
        {
            return new MoonBlockRespawnDefinition(
                generatorTileId: 100,
                moonBlockEntityId: template.entityId,
                spawnCell: GeneratorCell,
                template: template);
        }

        private static TileFeatureState CreateGeneratorTileFeatureState()
        {
            return new TileFeatureState(
                100,
                GeneratorCell,
                TileFeatureKind.MoonBlockGenerator,
                TileFeatureFlags.None,
                sourceEntityId: 101,
                ownerEntityId: 102,
                teamId: 7,
                lifetimeTicks: 0,
                charges: 0);
        }

        private static TileFeatureRuntimeDefinition[] CreateTileFeatureDefinitions()
        {
            return new[]
            {
                new TileFeatureRuntimeDefinition(
                    100,
                    TileFeatureActivationRule.BottomFaceOnly,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: 20,
                    presentationKey: string.Empty),
            };
        }

        private static EntityState CreatePlayer()
        {
            return CreatePlayer(new SurfaceCell(FaceId.Floor, 0, 0));
        }

        private static EntityState CreatePlayer(SurfaceCell position)
        {
            return new EntityState
            {
                entityId = 10,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateEnemy(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateMoonBlock(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 2,
                maxHp = 2,
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = RequiredMoonCapabilities,
                boxArchetype = BoxArchetype.Moon,
                kineticInstigatorEntityId = 99,
                kineticInstigatorTeamId = 9,
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
                teamId = 0,
                type = EntityType.Box,
                unitRole = UnitRole.None,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Destroy,
                boxArchetype = BoxArchetype.Normal,
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
                teamId = 1,
                type = EntityType.Projectile,
                unitRole = UnitRole.None,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static EntityState CreateWall(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                teamId = 0,
                type = EntityType.None,
                unitRole = UnitRole.None,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private const BoxCapabilities RequiredMoonCapabilities =
            BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy;

        private sealed class EmptyEntityLogicProvider : ISnapshotEntityLogicProvider
        {
            public static readonly EmptyEntityLogicProvider Instance = new();

            public EntityLogicSet Build(
                WorldSnapshot snapshot,
                IReadOnlyList<IEntityLogic> staticEntityLogics)
            {
                return new EntityLogicSet(
                    Array.Empty<IPreMovementStateLogic>(),
                    Array.Empty<IEnemyAiStateLogic>(),
                    Array.Empty<IEnemyActionStateLogic>(),
                    Array.Empty<IMovementEntityLogic>(),
                    Array.Empty<IAttackEntityLogic>());
            }
        }
    }
}
