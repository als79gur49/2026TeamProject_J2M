using System;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxEnemyJumpTargetTests
    {
        [Test]
        [Category("Core")]
        public void EnemyPlanner_JumpWindupStart_EmitsJumperLandingTargetRequest()
        {
            var targetCell = new SurfaceCell(FaceId.Front, 0, 0);
            var topology = new CubeTopologyState(FaceId.Floor);
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(CreateJumpSignal(targetCell, startedWindup: true)),
                    topology),
                builder);

            var plan = builder.Build();
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            var request = plan.Requests[0];
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.JumperLandingTarget)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PresentationSeed, Is.EqualTo(40));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellFloor));
            Assert.That(request.Anchor.Cell, Is.EqualTo(targetCell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
        }

        [Test]
        [Category("Core")]
        public void EnemyPlanner_NonWindupJumpSignals_DoNotEmitLandingTarget()
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var targetCell = new SurfaceCell(FaceId.Floor, 1, 1);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    tickIndex: 12,
                    CreatePresentationData(
                        CreateJumpSignal(targetCell, startedAirborne: true, phase: EnemyJumpPhase.Airborne),
                        CreateJumpSignal(targetCell, landed: true, phase: EnemyJumpPhase.Cooldown),
                        CreateJumpSignal(targetCell, retry: true, phase: EnemyJumpPhase.Airborne)),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(builder.Build(), Is.SameAs(GameplayVfxRequestPlan.Empty));
        }

        [Test]
        [Category("Core")]
        public void ProductionRuntime_FlagOff_DoesNotInitializeOrPlan()
        {
            var owner = new GameObject("VfxRuntimeFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = false;
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.IsRuntimeInitialized, Is.False);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionRuntime_MissingBinding_SafelySkipsAfterPlanning()
        {
            var owner = new GameObject("VfxRuntimeMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [Category("Core")]
        public void ProductionRuntime_FlagOnWithBinding_PlaysOneTransientInstance()
        {
            var owner = new GameObject("VfxRuntimeEnabled");
            var prefab = new GameObject("JumperLandingTargetPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableEnemyJumpTargetVfx = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                var context = CreateExtensionContext(
                    CreateJumpSignal(new SurfaceCell(FaceId.Floor, 0, 0), startedWindup: true));

                runtime.Present(context);

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cueMap);
                UnityEngine.Object.DestroyImmediate(binding);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEnemyJumpPresentationSignal jumpSignal)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 2)),
                1f);
            return new GameplayTickPresentationExtensionContext(
                CreateResult(CreatePresentationData(jumpSignal), topology),
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
                Array.Empty<EntityState>(),
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(params TickEnemyJumpPresentationSignal[] jumpSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                jumpSignals,
                Array.Empty<TickEntityExitPresentationSignal>());
        }

        private static TickEnemyJumpPresentationSignal CreateJumpSignal(
            SurfaceCell targetCell,
            bool startedWindup = false,
            bool startedAirborne = false,
            bool landed = false,
            bool retry = false,
            EnemyJumpPhase phase = EnemyJumpPhase.Windup)
        {
            return new TickEnemyJumpPresentationSignal(
                entityId: 40,
                sequence: 3,
                phase: phase,
                startedWindupThisTick: startedWindup,
                startedAirborneThisTick: startedAirborne,
                landedThisTick: landed,
                retryThisTick: retry,
                sourceCell: new SurfaceCell(FaceId.Floor, 2, 0),
                lockedTargetCell: targetCell,
                presentationTargetCell: targetCell,
                facing: Direction.Right,
                landingTick: 15);
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Enemy);
            SetField(binding, "cueCode", (int)EnemyVfxCue.JumperLandingTarget);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.Optional);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.SkipOptional);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0.5f);
            SetField(binding, "tailSeconds", 0.2f);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
