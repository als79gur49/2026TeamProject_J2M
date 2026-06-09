using System;
using System.IO;
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
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxPlayerDamageMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string PlayerDamageBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/PlayerDamageBurstVfx.prefab";
        private const string PlayerDamageBurstBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/PlayerDamageBurst_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void PlayerPlanner_DamageSignal_EmitsPlayerDamageRequest()
        {
            var request = PlanSingleRequest(CreateDamageSignal());

            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.SequenceId, Is.EqualTo(10));
            Assert.That(request.PresentationSeed, Is.EqualTo(10));
            Assert.That(request.SourceEntityId, Is.EqualTo(10));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(PlayerVfxCue.Damage)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Entity));
            Assert.That(request.Anchor.EntityId, Is.EqualTo(10));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.EntityCenter));
        }

        [Test]
        [Category("Extended")]
        public void PlayerPlanner_PlayerDeathSignal_DoesNotEmitDamageRequest()
        {
            var planner = new PlayerVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        new[] { CreateDamageSignal() },
                        new[] { CreateDeathSignal() }),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(builder.Build().Requests, Is.Empty);
        }

        [Test]
        [Category("Extended")]
        public void PlayerDeath_DoesNotCreatePlayerDamageOrDeathVfxUnlessExplicitlyAuthored()
        {
            var planner = new PlayerVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            var presentationData = CreatePresentationData(
                new[] { CreateDamageSignal() },
                new[] { CreateDeathSignal() });

            Assert.That(presentationData.PlayerDeathSignals, Has.Count.EqualTo(1));
            Assert.That(presentationData.PlayerDeathSignals[0].DidDieThisTick, Is.True);

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    presentationData,
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            var plan = builder.Build();
            Assert.That(plan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId.Equals(GameplayVfxCueId.From(PlayerVfxCue.Damage))));
            Assert.That(plan.Requests, Has.None.Matches<GameplayVfxRequest>(
                request => request.CueId.Equals(GameplayVfxCueId.From(PlayerVfxCue.Death))));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DamageMigrationFlag_DefaultsTrue()
        {
            var owner = new GameObject("DamageMigrationDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DamageMigrationMissingBinding_DiagnosticNoFallback()
        {
            var owner = new GameObject("DamageMigrationMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext());

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DamageMigrationFlagOnWithBinding_PlaysOneTransientInstance()
        {
            var owner = new GameObject("DamageMigrationEnabled");
            var prefab = new GameObject("DamageMigrationPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext());

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DamageMigrationFlagOnWithBinding_DoesNotCreateSnapshots()
        {
            var owner = new GameObject("DamageMigrationSnapshotGuard");
            var prefab = new GameObject("DamageMigrationSnapshotPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                SnapshotMaterializationCounts counts;
                using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
                {
                    runtime.Present(CreateExtensionContext());
                    counts = capture.Counts;
                }

                Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
                Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DamageMigrationFlagOff_DoesNotUseOldFallback()
        {
            var rootObject = new GameObject("Coordinator_DamageMigrationFlagOff");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("DamageMigrationFlagOff_PlayerPrefab");

            try
            {
                var presenter = CreatePresenter(rootObject, playerViewPrefab);
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                presenter.PresentInitial(new[] { CreatePlayerUnit(10, playerCell) }, topology);

                presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateDamageSignal() }, Array.Empty<TickPlayerDeathPresentationSignal>()),
                    topology,
                    new[] { CreatePlayerUnit(10, playerCell, hp: 2) }));
            }
            finally
            {
                Destroy(playerViewPrefab.gameObject, rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DamageMigrationFlagOn_UsesVfxOnly()
        {
            var rootObject = new GameObject("Coordinator_DamageMigrationFlagOn");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("DamageMigrationFlagOn_PlayerPrefab");
            var vfxPrefab = new GameObject("DamageMigrationFlagOn_NewPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;

            try
            {
                binding = CreateBinding(vfxPrefab);
                cueMap = CreateCueMap(binding);
                var presenter = CreatePresenter(rootObject, playerViewPrefab);
                var runtime = rootObject.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                presenter.AttachPresentationExtension(runtime);
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                presenter.PresentInitial(new[] { CreatePlayerUnit(10, playerCell) }, topology);

                presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateDamageSignal() }, Array.Empty<TickPlayerDeathPresentationSignal>()),
                    topology,
                    new[] { CreatePlayerUnit(10, playerCell, hp: 2) }));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, vfxPrefab, playerViewPrefab.gameObject, rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_DamageMigrationFlagOnMissingBinding_DoesNotFallbackToOldPresenter()
        {
            var rootObject = new GameObject("Coordinator_DamageMigrationMissingBinding");
            var playerViewPrefab = PlayerViewPrefabTestUtility.CreatePlayerViewPrefab("DamageMigrationMissingBinding_PlayerPrefab");

            try
            {
                var presenter = CreatePresenter(rootObject, playerViewPrefab);
                var runtime = rootObject.AddComponent<GameplayVfxProductionRuntime>();
                presenter.AttachPresentationExtension(runtime);
                var topology = new CubeTopologyState(FaceId.Floor);
                var playerCell = new SurfaceCell(FaceId.Floor, 0, 0);
                presenter.PresentInitial(new[] { CreatePlayerUnit(10, playerCell) }, topology);

                presenter.Present(CreateResult(
                    CreatePresentationData(new[] { CreateDamageSignal() }, Array.Empty<TickPlayerDeathPresentationSignal>()),
                    topology,
                    new[] { CreatePlayerUnit(10, playerCell, hp: 2) }));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(playerViewPrefab.gameObject, rootObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PlayerDamageBurstPrefab_RemovedFromDefaultAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerDamageBurstPrefabPath);

            Assert.That(prefab, Is.Null, PlayerDamageBurstPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void PlayerDamageBurstBinding_RemovedFromDefaultAuthoring()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(PlayerDamageBurstBindingPath);

            Assert.That(binding, Is.Null, PlayerDamageBurstBindingPath);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_DoesNotResolveRemovedPlayerDamageBurst()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(
                    GameplayVfxCueId.From(PlayerVfxCue.Damage),
                    out _),
                Is.False);
        }

        [Test]
        [Category("Extended")]
        public void DamagePlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
        {
            var source = File.ReadAllText(VfxPlanningPath) + "\n" + File.ReadAllText(VfxProductionRuntimePath);
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "CreateSnapshot",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }
        }

        private static GameplayVfxRequest PlanSingleRequest(TickPlayerDamagePresentationSignal damageSignal)
        {
            var planner = new PlayerVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(
                        new[] { damageSignal },
                        Array.Empty<TickPlayerDeathPresentationSignal>()),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            var plan = builder.Build();
            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static GameplayTickViewPresenter CreatePresenter(
            GameObject rootObject,
            GameplayEntityView playerViewPrefab)
        {
            var presenter = rootObject.AddComponent<GameplayTickViewPresenter>();
            var registry = rootObject.AddComponent<GameplayEntityViewRegistry>();
            var effectAuthoring = playerViewPrefab.gameObject.AddComponent<EntityEffectPresentationAuthoring>();
            SetField(effectAuthoring, "hitEffectDurationSeconds", 0.2f);
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10,
                    playerViewPrefab: playerViewPrefab));

            presenter.Initialize(
                binder,
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                new CubeTopologyState(FaceId.Floor),
                1f,
                GameplayTimingProfile.CreateDefault());
            return presenter;
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext()
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(
                new Vector3(0.25f, 0.5f, 0f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(2, 0)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(
                        new[] { CreateDamageSignal() },
                        Array.Empty<TickPlayerDeathPresentationSignal>()),
                    topology,
                    new[] { CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 0), hp: 2) }),
                topology,
                stateStore,
                projector);
        }

        private static TickResult CreateResult(
            TickPresentationData presentationData,
            CubeTopologyState topology,
            EntityState[] finalEntities)
        {
            return new TickResult(
                12,
                Array.Empty<TickPhase>(),
                Array.Empty<string>(),
                MovementPhaseResult.Empty,
                AttackPhaseResult.Empty,
                finalEntities,
                Array.Empty<string>(),
                topology,
                presentationData,
                "hash",
                TickTrace.Empty);
        }

        private static TickPresentationData CreatePresentationData(
            TickPlayerDamagePresentationSignal[] damageSignals,
            TickPlayerDeathPresentationSignal[] deathSignals)
        {
            return new TickPresentationData(
                Array.Empty<TickEntityMotion>(),
                topologyMotion: null,
                Array.Empty<TickVisibilityChange>(),
                Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(),
                Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                damageSignals,
                deathSignals,
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickPlayerDamagePresentationSignal CreateDamageSignal()
        {
            return new TickPlayerDamagePresentationSignal(10, tookDamageThisTick: true, damageAmount: 1);
        }

        private static TickPlayerDeathPresentationSignal CreateDeathSignal()
        {
            return new TickPlayerDeathPresentationSignal(
                10,
                didDieThisTick: true,
                sourceEntityId: 20,
                fallbackFacing: Direction.Right,
                resolvedDamageSourceAvailable: true,
                damageAmountAtFatalHit: 1,
                deathDirectionHintKind: DeathDirectionHintKind.AttackerReverse);
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position, int hp = 3)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = hp,
                maxHp = 3,
                teamId = 1,
                type = EntityType.Unit,
                unitRole = UnitRole.Player,
                state = EntityPhaseState.Idle,
                facing = Direction.Right,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.None,
            };
        }

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Player);
            SetField(binding, "cueCode", (int)PlayerVfxCue.Damage);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0.28f);
            SetField(binding, "tailSeconds", 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 8);
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
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void Destroy(params UnityEngine.Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }
    }
}
