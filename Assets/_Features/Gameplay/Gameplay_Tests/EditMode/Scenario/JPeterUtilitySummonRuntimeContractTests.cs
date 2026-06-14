using System;
using System.Collections.Generic;
using System.Linq;
using Game.Feature.Gameplay.Attack;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Tests;
using Game.Feature.Stages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Scenario
{
    public sealed class JPeterUtilitySummonRuntimeContractTests
    {
        private const int EnemyId = 40;
        private const string ArchetypeSummonerProfilePath =
            StageContentPaths.SharedEnemyAiRoot + "/Profiles/Enemy_ArchetypeSummoner/EnemyAi_ArchetypeSummoner.asset";
        private const string CombinedArchetypeCatalogPath =
            StageContentPaths.SharedEnemyAiRoot + "/Catalogs/EnemyUnitArchetypeCatalog_CampaignMainEnemy.asset";

        // Jpeter is the ArchetypeSummoner utility profile: windup arms SummonMinion state, then resolve revalidates summon placement.

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_WindupArmsSummonEffectButDoesNotCreateSummoned()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedReadyUtilityState(worldState);

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.effectKind, Is.EqualTo(EnemyUtilityEffectKind.SummonMinion));
            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.Windup));
            Assert.That(state.windupStartTick, Is.EqualTo(1));
            Assert.That(state.windupEndTick, Is.GreaterThan(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            Assert.That(tick.PresentationData.SummonWindupWarnings, Has.Count.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_RecoverSuppressesImmediateReenter()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            var pipeline = CreatePipeline(worldState);

            var executeTick = pipeline.RunTick(new TickInput(1));
            var spawnedAfterExecute = GetSummonedChildren(worldState);
            var recoverState = GetUtilityEffectState(worldState);
            var nextTick = pipeline.RunTick(new TickInput(2));

            Assert.That(spawnedAfterExecute, Has.Count.EqualTo(1));
            Assert.That(recoverState.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
            Assert.That(recoverState.recoverEndTickExclusive, Is.GreaterThan(2));
            Assert.That(GetSummonedChildren(worldState), Has.Count.EqualTo(1));
            Assert.That(nextTick.EventLog, Has.None.Contains("SummonCommitted|Source=40|Effect=0|SpawnIndex=0"));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_WindupDetachedCancelsWithoutSummon()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedReadyUtilityState(worldState);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().SetBoardPresence(EnemyId, EntityBoardPresence.Detached);
            var canceledTick = pipeline.RunTick(new TickInput(2));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
            Assert.That(state.cooldownTicksRemaining, Is.GreaterThan(0));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(canceledTick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            Assert.That(canceledTick.PresentationData.EnemyUtilitySignals.Single().Phase, Is.EqualTo(EnemyUtilityPresentationPhase.Canceled));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_SummonPlacementUsesSurfaceCellFaceAfterTopologyRotation()
        {
            var sourceCell = new SurfaceCell(FaceId.Front, 0, 0);
            var worldState = CreateWorldState(
                new[] { CreateJpeter(sourceCell) },
                topology: new CubeTopologyState(FaceId.Floor));
            SeedWindupUtilityState(worldState, windupEndTick: 2);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(GetUtilityEffectState(worldState).windupEndTick, Is.EqualTo(3));

            worldState.CreateWriteContext().SetTopology(new CubeTopologyState(FaceId.Front));
            pipeline.RunTick(new TickInput(2));
            var commitTick = pipeline.RunTick(new TickInput(3));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(new SurfaceCell(FaceId.Front, 1, 0)), commitTick.Trace.Text);
            Assert.That(child.position.face, Is.EqualTo(FaceId.Front));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_MarkedForDeathDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("MarkedForDeath");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_HpZeroDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("HpZero");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_DeadDuringWindupDoesNotCreateSummoned()
        {
            AssertInvalidatedWindupDoesNotSummon("Dead");
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_SourceDeathAfterSummonLeavesExistingChildLifecycleDocumented()
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);
            worldState.CreateWriteContext().ApplyDamage(EnemyId, 99);
            pipeline.RunTick(new TickInput(2));

            Assert.That(worldState.CreateSnapshot().TryGetEntity(EnemyId, out _), Is.False);
            Assert.That(worldState.CreateSnapshot().TryGetEntity(child.entityId, out var existingChild), Is.True);
            Assert.That(existingChild.position, Is.EqualTo(child.position));
            Assert.That(worldState.CreateSnapshot().TryGetSummonedEntityState(child.entityId, out var summonedState), Is.True);
            Assert.That(summonedState.SourceEntityId, Is.EqualTo(EnemyId));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedDestroyTileDoesNotHardBlockSummonPlacement()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyTile = CreateTileFeature(100, forwardCell, TileFeatureKind.Destroy);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 0, -1)),
                    CreateWall(91, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(92, new SurfaceCell(FaceId.Floor, -1, 0)),
                },
                initialTileFeatures: new[] { destroyTile });
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            Assert.That(worldState.CreateSnapshot().TryGetPlacementBlocker(EntityType.Unit, forwardCell, ignoredEntityId: 0, out _), Is.False);

            CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell), "DestroyTile is an avoidance/risk tile, not a hard summon placement blocker when it is the only legal candidate.");
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), forwardCell), Is.EqualTo(1));
        }

        // TileFeature summon placement policy is face-aware: DestroyTile risk uses the summoned unit mobility, active Barricade is a hard TileFeature blocker, and generated MoonBlock Solid is the Solid blocker.

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedBarricadeOnSummonCellBlocksPlacementWithoutGhost()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var barricade = CreateTileFeature(101, forwardCell, TileFeatureKind.Barricade);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 0, -1)),
                    CreateWall(91, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(92, new SurfaceCell(FaceId.Floor, -1, 0)),
                },
                initialTileFeatures: new[] { barricade });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            var tick = CreatePipeline(worldState, CreateActiveDefinitions(barricade)).RunTick(new TickInput(1));

            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.Some.Contains("SummonSkipped|Source=40").And.Contains("Reason=NoCandidateCell"));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out _), Is.False);
            AssertNoSummonCandidateGhostOccupancy(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_InactiveBarricadeOnSummonCellDoesNotBlockPlacement()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var barricade = CreateTileFeature(121, forwardCell, TileFeatureKind.Barricade);
            var definitions = CreateInactiveDefinitions(barricade);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 0, -1)),
                    CreateWall(91, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(92, new SurfaceCell(FaceId.Floor, -1, 0)),
                },
                initialTileFeatures: new[] { barricade });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState, definitions).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out _), Is.False);
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_GeneratedMoonBlockSolidBlocksSummonPlacementAndUsesFallback()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var rightCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(),
                    CreateBox(50, forwardCell, BoxArchetype.Moon),
                },
                initialTileFeatures: new[] { CreateTileFeature(102, forwardCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 50) });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(rightCell), "Generated MoonBlock Solid blocks summon placement; the MoonBlockGenerator feature itself remains non-blocking.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out var solid), Is.True);
            Assert.That(solid.Kind, Is.EqualTo(SolidKind.Box));
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_SolidOnSamePlanarOtherFaceDoesNotAffectSummonPlacementSurfaceCell()
        {
            var floorForward = new SurfaceCell(FaceId.Floor, 1, 0);
            var frontSamePlanar = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                CreateWall(90, frontSamePlanar),
            });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(floorForward));
            Assert.That(child.position, Is.Not.EqualTo(frontSamePlanar));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(frontSamePlanar, out _), Is.True);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedDestroyTileAppearsDuringWindup_RevalidatesPlacementWithSummonedAirMobility()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyTile = CreateTileFeature(110, forwardCell, TileFeatureKind.Destroy);
            var worldState = CreateWindupWorldForCandidateMutation();
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(destroyTile);
            pipeline = CreatePipeline(worldState, CreateActiveDefinitions(destroyTile));
            pipeline.RunTick(new TickInput(2));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell), "Jpeter summon placement must evaluate DestroyTile risk with the summoned PassiveContactMinion Air mobility.");
            Assert.That(child.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedBarricadeAppearsDuringWindup_RevalidatesPlacementAndBlocksAtResolve()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var rightCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var barricade = CreateTileFeature(111, forwardCell, TileFeatureKind.Barricade);
            var worldState = CreateWindupWorldForCandidateMutation();
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(barricade);
            pipeline = CreatePipeline(worldState, CreateActiveDefinitions(barricade));
            pipeline.RunTick(new TickInput(2));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(rightCell), "Active Barricade blocks the original summon candidate and Jpeter selects the next legal candidate.");
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_InactiveBarricadeAppearsDuringWindup_RevalidatesAsNonBlocking()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var barricade = CreateTileFeature(122, forwardCell, TileFeatureKind.Barricade);
            var definitions = CreateInactiveDefinitions(barricade);
            var worldState = CreateWindupWorldForCandidateMutation();
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(barricade);
            pipeline = CreatePipeline(worldState, definitions);
            pipeline.RunTick(new TickInput(2));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out _), Is.False);
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_GeneratedMoonBlockSolidAppearsDuringWindup_RevalidatesPlacementAndBlocksAtResolve()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var rightCell = new SurfaceCell(FaceId.Floor, 0, -1);
            var worldState = CreateWindupWorldForCandidateMutation();
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            worldState.CreateWriteContext().AddTileFeature(CreateTileFeature(112, forwardCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 50));
            worldState.CreateWriteContext().SpawnEntity(CreateBox(50, forwardCell, BoxArchetype.Moon));
            pipeline.RunTick(new TickInput(2));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(rightCell), "Generated MoonBlock Solid blocks the forward summon placement candidate, then Jpeter selects the next legal candidate.");
            Assert.That(worldState.CreateSnapshot().TryGetSolidSemanticAt(forwardCell, out var solid), Is.True);
            Assert.That(solid.Kind, Is.EqualTo(SolidKind.Box));
            AssertSummonedMetadata(worldState, child.entityId);
            AssertNoGhostSummonState(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedDestroyTileOnSamePlanarOtherFaceDoesNotAffectSummonPlacementSurfaceCell()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 1, 0);
            var destroyTile = CreateTileFeature(113, otherFaceCell, TileFeatureKind.Destroy);
            var worldState = CreateWorldState(CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10));
            worldState.CreateWriteContext().AddTileFeature(destroyTile);
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(child.position, Is.Not.EqualTo(otherFaceCell));
            AssertSummonedMetadata(worldState, child.entityId);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedBarricadeOnSamePlanarOtherFaceDoesNotBlockSummonPlacementSurfaceCell()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 1, 0);
            var barricade = CreateTileFeature(114, otherFaceCell, TileFeatureKind.Barricade);
            var worldState = CreateWorldState(CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10));
            worldState.CreateWriteContext().AddTileFeature(barricade);
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState, CreateActiveDefinitions(barricade)).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(child.position, Is.Not.EqualTo(otherFaceCell));
            AssertSummonedMetadata(worldState, child.entityId);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_ActivatedDestroyTileSummonCandidateIsNeutralForSummonedAirMobility()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var destroyTile = CreateTileFeature(116, forwardCell, TileFeatureKind.Destroy);
            var worldState = CreateWorldState(
                new[] { CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10) },
                initialTileFeatures: new[] { destroyTile });
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState, CreateActiveDefinitions(destroyTile)).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(child.unitMobilityKind, Is.EqualTo(UnitMobilityKind.Air));
            Assert.That(CountUnitsAt(worldState.CreateSnapshot(), forwardCell), Is.EqualTo(1));
            AssertSummonedMetadata(worldState, child.entityId);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_BlockedByActivatedBarricadeConsumesRecoverWithoutSummoned()
        {
            var candidates = GetSummonCandidateCells();
            var barricades = candidates
                .Select((cell, index) => CreateTileFeature(120 + index, cell, TileFeatureKind.Barricade))
                .ToArray();
            var worldState = CreateWorldState(
                new[] { CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10) },
                initialTileFeatures: barricades);
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState, CreateActiveDefinitions(barricades)).RunTick(new TickInput(1));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover));
            Assert.That(state.recoverEndTickExclusive, Is.GreaterThan(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            AssertNoSummonCandidateGhostOccupancy(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_GeneratedMoonBlockSolidOnSamePlanarOtherFaceDoesNotAffectSummonPlacement()
        {
            var forwardCell = new SurfaceCell(FaceId.Floor, 1, 0);
            var otherFaceCell = new SurfaceCell(FaceId.Front, 1, 0);
            var worldState = CreateWorldState(new[]
            {
                CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                CreateBox(51, otherFaceCell, BoxArchetype.Moon),
            });
            worldState.CreateWriteContext().AddTileFeature(CreateTileFeature(115, otherFaceCell, TileFeatureKind.MoonBlockGenerator, boundEntityId: 51));
            SeedWindupUtilityState(worldState, windupEndTick: 1);

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var child = GetSingleSummonedChild(worldState);

            Assert.That(child.position, Is.EqualTo(forwardCell));
            Assert.That(child.position, Is.Not.EqualTo(otherFaceCell));
            AssertSummonedMetadata(worldState, child.entityId);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_BlockedSummonDoesNotCreateGhostEntityOrOccupancy()
        {
            var worldState = CreateFullyBlockedSummonWorld();

            var tick = CreatePipeline(worldState).RunTick(new TickInput(1));

            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(GetSummonedStates(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            Assert.That(tick.EventLog, Has.Some.Contains("SummonSkipped|Source=40").And.Contains("Reason=NoCandidateCell"));
            AssertNoSummonCandidateGhostOccupancy(worldState);
        }

        [Test]
        [Category("Extended")]
        public void EnemyUtility_JPeterProfile_BlockedSummonStillCompletesOrRecoversAccordingToCurrentPolicy()
        {
            var worldState = CreateFullyBlockedSummonWorld();

            CreatePipeline(worldState).RunTick(new TickInput(1));
            var state = GetUtilityEffectState(worldState);

            Assert.That(state.phase, Is.EqualTo(EnemyUtilityEffectPhase.Recover), "CurrentPolicy: blocked summon execution enters Recover instead of staying in Windup.");
            Assert.That(state.recoverEndTickExclusive, Is.GreaterThan(1));
            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            AssertNoSummonCandidateGhostOccupancy(worldState);
        }

        private static void AssertInvalidatedWindupDoesNotSummon(string invalidationKind)
        {
            var worldState = CreateWorldState(CreateJpeter());
            SeedWindupUtilityState(worldState, windupEndTick: 2);
            var pipeline = CreatePipeline(worldState);

            pipeline.RunTick(new TickInput(1));
            var writeContext = worldState.CreateWriteContext();
            switch (invalidationKind)
            {
                case "MarkedForDeath":
                    ((IAttackCommitContext)writeContext).MarkDestroy(EnemyId);
                    break;
                case "HpZero":
                    writeContext.ApplyDamage(EnemyId, 99);
                    break;
                case "Dead":
                    writeContext.ApplyEnemyAiState(EnemyId, EnemyAiMode.Dead, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidationKind), invalidationKind, null);
            }

            var tick = pipeline.RunTick(new TickInput(2));

            Assert.That(GetSummonedChildren(worldState), Is.Empty);
            Assert.That(tick.EventLog, Has.None.Contains("SummonCommitted|Source=40"));
            if (worldState.CreateSnapshot().TryGetEnemyUtilityState(EnemyId, out var utilityState))
            {
                Assert.That(utilityState.EffectStates[0].phase, Is.EqualTo(EnemyUtilityEffectPhase.None));
            }
        }

        private static TickPipeline CreatePipeline(
            WorldState worldState,
            IReadOnlyList<TileFeatureRuntimeDefinition> tileFeatureDefinitions = null)
        {
            var profile = LoadJpeterProfile();
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyUnitArchetypeCatalog>(CombinedArchetypeCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Missing enemy archetype catalog at '{CombinedArchetypeCatalogPath}'.");

            var runtimeSnapshot = new GameplaySceneHostConfiguration
            {
                SimulationTicksPerSecond = GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                DefaultEnemyAiProfile = profile,
                EnemyAiProfileOverrides = new[]
                {
                    new EnemyAiProfileOverride
                    {
                        EntityId = EnemyId,
                        Profile = profile,
                    },
                },
                EnemyUnitArchetypeCatalog = catalog,
            }.CreateEnemyAiRuntimeSnapshot();

            return new GameplayBootstrapper(
                GameplayEntityLogicProviderFactory.CreateDefault(
                    runtimeSnapshot.DefaultDefinition,
                    runtimeSnapshot.DefinitionsByEntityId,
                    runtimeSnapshot.DefinitionsByArchetypeId),
                runtimeSnapshot.SpawnDefaultsByArchetypeId)
                .CreateTickPipeline(
                    worldState,
                    Array.Empty<IEntityLogic>(),
                    GameplayTimingProfile.CreateDefault(),
                    PlayerControlTimingSettings.CreateDefault().CreateAuthoritativeSnapshot(
                        GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                        GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds),
                    runtimeFeatureFlags: GameplayRuntimeFeatureFlags.DefaultGameplayLocomotion,
                    playerKinematicLocomotionTiming: CreateOneTickKinematicTiming(),
                    tileFeatureDefinitions: tileFeatureDefinitions);
        }

        private static EnemyAiProfile LoadJpeterProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAiProfile>(ArchetypeSummonerProfilePath);
            Assert.That(profile, Is.Not.Null, $"Missing Jpeter ArchetypeSummoner profile at '{ArchetypeSummonerProfilePath}'.");
            return profile;
        }

        private static PlayerKinematicLocomotionTimingSnapshot CreateOneTickKinematicTiming()
        {
            var timingProfile = GameplayTimingProfile.CreateDefault();
            return new PlayerKinematicLocomotionTimingSettings
            {
                KinematicMoveDurationSeconds = 1f / timingProfile.SimulationTicksPerSecond,
            }.CreateAuthoritativeSnapshot(timingProfile.SimulationTicksPerSecond);
        }

        private static void SeedReadyUtilityState(WorldState worldState)
        {
            worldState.CreateWriteContext().SetEnemyUtilityState(
                EnemyId,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            effectKind = EnemyUtilityEffectKind.SummonMinion,
                            cooldownTicksRemaining = 0,
                        },
                    }));
        }

        private static void SeedWindupUtilityState(WorldState worldState, int windupEndTick)
        {
            worldState.CreateWriteContext().SetEnemyUtilityState(
                EnemyId,
                new EnemyUtilityRuntimeState(
                    new[]
                    {
                        new EnemyUtilityEffectState
                        {
                            effectKind = EnemyUtilityEffectKind.SummonMinion,
                            phase = EnemyUtilityEffectPhase.Windup,
                            windupStartTick = 0,
                            windupEndTick = windupEndTick,
                            activationSequence = 1,
                        },
                    }));
        }

        private static EnemyUtilityEffectState GetUtilityEffectState(WorldState worldState)
        {
            Assert.That(worldState.CreateSnapshot().TryGetEnemyUtilityState(EnemyId, out var state), Is.True);
            Assert.That(state.EffectStates, Has.Count.EqualTo(1));
            return state.EffectStates[0];
        }

        private static IReadOnlyList<EntityState> GetSummonedChildren(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var result = new List<EntityState>();
            for (var entityId = 1; entityId <= 200; entityId++)
            {
                if (snapshot.TryGetSummonedEntityState(entityId, out var summonedState) &&
                    summonedState.SourceEntityId == EnemyId &&
                    snapshot.TryGetEntity(entityId, out var entity))
                {
                    result.Add(entity);
                }
            }

            result.Sort((left, right) => left.entityId.CompareTo(right.entityId));
            return result;
        }

        private static EntityState GetSingleSummonedChild(WorldState worldState)
        {
            var children = GetSummonedChildren(worldState);
            Assert.That(children, Has.Count.EqualTo(1));
            return children[0];
        }

        private static IReadOnlyList<SummonedEntitySnapshotEntry> GetSummonedStates(WorldState worldState)
        {
            var entries = new List<SummonedEntitySnapshotEntry>();
            worldState.CreateSnapshot().EnumerateSummonedEntityStatesOrdered(entries);
            return entries;
        }

        private static void AssertSummonedMetadata(WorldState worldState, int childEntityId)
        {
            Assert.That(worldState.CreateSnapshot().TryGetSummonedEntityState(childEntityId, out var summonedState), Is.True);
            Assert.That(summonedState.SourceEntityId, Is.EqualTo(EnemyId));
            Assert.That(summonedState.SourceEffectIndex, Is.EqualTo(0));
        }

        private static void AssertNoGhostSummonState(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            var entries = new List<SummonedEntitySnapshotEntry>();
            snapshot.EnumerateSummonedEntityStatesOrdered(entries);
            foreach (var entry in entries)
            {
                Assert.That(snapshot.TryGetEntity(entry.EntityId, out _), Is.True, $"Summoned metadata for {entry.EntityId} must not outlive its entity.");
            }
        }

        private static void AssertNoSummonCandidateGhostOccupancy(WorldState worldState)
        {
            var snapshot = worldState.CreateSnapshot();
            foreach (var cell in GetSummonCandidateCells())
            {
                var units = CountUnitsAt(snapshot, cell);
                Assert.That(units, Is.Zero, $"Blocked summon must not leave Unit occupancy at {cell}.");
            }
        }

        private static WorldState CreateWindupWorldForCandidateMutation()
        {
            var worldState = CreateWorldState(CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10));
            SeedWindupUtilityState(worldState, windupEndTick: 2);
            return worldState;
        }

        private static WorldState CreateFullyBlockedSummonWorld()
        {
            var worldState = CreateWorldState(
                new[]
                {
                    CreateJpeter(aiMode: EnemyAiMode.Recover, aiStateTimer: 10),
                    CreateWall(90, new SurfaceCell(FaceId.Floor, 1, 0)),
                    CreateWall(91, new SurfaceCell(FaceId.Floor, 0, -1)),
                    CreateWall(92, new SurfaceCell(FaceId.Floor, 0, 1)),
                    CreateWall(93, new SurfaceCell(FaceId.Floor, -1, 0)),
                });
            SeedWindupUtilityState(worldState, windupEndTick: 1);
            return worldState;
        }

        private static IReadOnlyList<SurfaceCell> GetSummonCandidateCells()
        {
            return new[]
            {
                new SurfaceCell(FaceId.Floor, 1, 0),
                new SurfaceCell(FaceId.Floor, 0, -1),
                new SurfaceCell(FaceId.Floor, 0, 1),
                new SurfaceCell(FaceId.Floor, -1, 0),
            };
        }

        private static int CountUnitsAt(WorldSnapshot snapshot, SurfaceCell cell)
        {
            var units = new List<EntityState>();
            snapshot.EnumerateUnitsAt(cell, units);
            return units.Count;
        }

        private static WorldState CreateWorldState(
            params EntityState[] initialEntities)
        {
            return CreateWorldState((IEnumerable<EntityState>)initialEntities);
        }

        private static WorldState CreateWorldState(
            IEnumerable<EntityState> initialEntities,
            CubeTopologyState? topology = null,
            IEnumerable<TileFeatureState> initialTileFeatures = null)
        {
            return GameplayCompositionRoot.CreateWorldState(
                initialEntities,
                new BoardBounds(new Vector2Int(-4, -4), new Vector2Int(6, 6)),
                topology ?? new CubeTopologyState(FaceId.Floor),
                initialTileFeatures);
        }

        private static EntityState CreateJpeter()
        {
            return CreateJpeter(new SurfaceCell(FaceId.Floor, 0, 0));
        }

        private static EntityState CreateJpeter(EnemyAiMode aiMode, int aiStateTimer = 0)
        {
            return CreateJpeter(new SurfaceCell(FaceId.Floor, 0, 0), aiMode, aiStateTimer);
        }

        private static EntityState CreateJpeter(
            SurfaceCell position,
            EnemyAiMode aiMode = EnemyAiMode.Patrol,
            int aiStateTimer = 0)
        {
            return new EntityState
            {
                entityId = EnemyId,
                position = position,
                hp = 3,
                maxHp = 3,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                unitMobilityKind = UnitMobilityKind.Ground,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = aiMode,
                aiStateTimer = aiStateTimer,
            };
        }

        private static EntityState CreateBox(int entityId, SurfaceCell position, BoxArchetype boxArchetype = BoxArchetype.Normal)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 1,
                maxHp = 1,
                type = EntityType.Box,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
                boxCapabilities = BoxCapabilities.Push | BoxCapabilities.Flip | BoxCapabilities.Destroy,
                boxArchetype = boxArchetype,
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
                type = EntityType.None,
                state = EntityPhaseState.Idle,
                facing = Direction.None,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static TileFeatureState CreateTileFeature(
            int tileId,
            SurfaceCell cell,
            TileFeatureKind kind,
            int boundEntityId = 0)
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
                charges: boundEntityId);
        }

        private static TileFeatureRuntimeDefinition[] CreateActiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.Always, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateInactiveDefinitions(params TileFeatureState[] tileFeatures)
        {
            return CreateDefinitions(TileFeatureActivationRule.InactiveFaceOnly, tileFeatures);
        }

        private static TileFeatureRuntimeDefinition[] CreateDefinitions(
            TileFeatureActivationRule activationRule,
            params TileFeatureState[] tileFeatures)
        {
            return tileFeatures
                .Select(tileFeature => new TileFeatureRuntimeDefinition(
                    tileFeature.TileId,
                    activationRule,
                    Direction2D.None,
                    TileFeatureBoxSelector.None,
                    boundEntityId: tileFeature.Charges,
                    presentationKey: string.Empty))
                .ToArray();
        }
    }
}
