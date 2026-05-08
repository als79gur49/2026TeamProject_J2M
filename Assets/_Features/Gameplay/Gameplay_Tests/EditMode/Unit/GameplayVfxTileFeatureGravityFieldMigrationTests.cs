using System;
using System.IO;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxTileFeatureGravityFieldMigrationTests
    {
        [Test]
        [Category("Extended")]
        public void ProductionRuntime_TileFeatureAndGravityFieldRequests_ArePlannedByVfxRuntime()
        {
            var owner = new GameObject("TileFeatureGravityFieldRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    tileEvents: new[]
                    {
                        new TilePresentationEvent(
                            TilePresentationEventKind.ButtonActivated,
                            1,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            TileFeatureKind.Button,
                            10,
                            0,
                            1),
                    },
                    gravityFieldEvents: new[]
                    {
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.Activated,
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0)),
                    },
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(4));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_TileFeatureAndGravityFieldFlagsOff_DoNotPlanFallbacks()
        {
            var owner = new GameObject("TileFeatureGravityFieldFlagsOffRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxTileFeatureLane = false;
                runtime.EnableGameplayVfxGravityFieldEvents = false;
                runtime.EnableGameplayVfxGravityFieldContinuous = false;
                runtime.EnableGameplayVfxGravityFieldLockedTarget = false;

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    tileEvents: new[]
                    {
                        new TilePresentationEvent(
                            TilePresentationEventKind.ButtonActivated,
                            1,
                            new SurfaceCell(FaceId.Floor, 0, 0),
                            TileFeatureKind.Button,
                            10,
                            0,
                            1),
                    },
                    gravityFieldEvents: new[]
                    {
                        new GravityFieldPresentationEvent(
                            GravityFieldPresentationEventKind.Activated,
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0)),
                    },
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.MissingBindingCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_GravityFieldContinuousState_DropsWhenPresentationStateDisappears()
        {
            var owner = new GameObject("GravityFieldContinuousRuntime");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreatePresentationData(
                    gravityFieldVisualStates: new[]
                    {
                        new GravityFieldVisualState(
                            40,
                            new SurfaceCell(FaceId.Floor, 1, 0),
                            GravityFieldPhase.Active,
                            1,
                            3,
                            1f,
                            lockedTargetEntityIds: new[] { 10 }),
                    })));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));

                runtime.Present(CreateExtensionContext(CreatePresentationData()));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DoesNotReferencePr28VisualControllersOrVfxController()
        {
            var coordinator = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Runtime/GameplayTickPresentationCoordinator.cs");

            Assert.That(coordinator, Does.Not.Contain("TileFeatureVisualPresentationController"));
            Assert.That(coordinator, Does.Not.Contain("GravityFieldVisualPresentationController"));
            Assert.That(coordinator, Does.Not.Contain("TileFeatureVisualRegistry"));
            Assert.That(coordinator, Does.Not.Contain("GravityFieldVisualTargetView"));
            Assert.That(coordinator, Does.Not.Contain("GameplayVfxPresentationController"));
        }

        [Test]
        [Category("Extended")]
        public void GameplayHost_DoesNotDirectlyDependOnVfxCoreOrAuthoring()
        {
            var hostAsmdef = ReadRepoFile("Assets/_Features/Gameplay/Gameplay_Host/Gameplay.Host.asmdef");

            Assert.That(hostAsmdef, Does.Not.Contain("Gameplay.Vfx"));
            Assert.That(hostAsmdef, Does.Not.Contain("Gameplay.Vfx.Authoring"));
        }

        [Test]
        [Category("Extended")]
        public void Governance_DocumentsTileFeatureAndGravityFieldNoLegacyFallbackPolicy()
        {
            var document = ReadRepoFile("Docs/Architecture/Gameplay-VFX-Governance.md");

            Assert.That(document, Does.Contain("TileFeature and GravityField visual migration is VFX-lane only."));
            Assert.That(document, Does.Contain("There is no coordinator fallback for TileFeature or GravityField visual lanes."));
            Assert.That(document, Does.Contain("does not restore PR #28 direct visual controllers"));
            Assert.That(document, Does.Contain("TileFeatureAudio and GravityFieldAudio are not VFX."));
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(TickPresentationData presentationData)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(Vector3.zero, Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[40] = new GameplayEntityPose(Vector3.right, Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(presentationData, topology),
                topology,
                stateStore,
                projector);
        }

        private static TickResult CreateResult(TickPresentationData presentationData, CubeTopologyState topology)
        {
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                new[] { CreateUnit(10), CreateUnit(40) },
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            TilePresentationEvent[] tileEvents = null,
            GravityFieldPresentationEvent[] gravityFieldEvents = null,
            GravityFieldVisualState[] gravityFieldVisualStates = null)
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
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                tileEvents: tileEvents,
                gravityFieldEvents: gravityFieldEvents,
                gravityFieldVisualStates: gravityFieldVisualStates);
        }

        private static EntityState CreateUnit(int entityId)
        {
            return new EntityState
            {
                entityId = entityId,
                position = new SurfaceCell(FaceId.Floor, entityId == 10 ? 0 : 1, 0),
                hp = 3,
                maxHp = 3,
                teamId = entityId == 10 ? 1 : 2,
                type = EntityType.Unit,
                unitRole = entityId == 10 ? UnitRole.Player : UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
            };
        }

        private static string ReadRepoFile(string relativePath)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            return File.ReadAllText(fullPath);
        }
    }
}
