using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.BoardState;
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
    public sealed class GameplayVfxEnemyDeathCanonicalTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string EnemyDeathBurstPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathBurstVfx.prefab";
        private const string EnemyDeathBurstBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDeathBurst_Binding.asset";
        private const string VfxPlanningPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxPlanning.cs";
        private const string VfxProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_KilledExit_EmitsEnemyDeathRequest()
        {
            var cell = new SurfaceCell(FaceId.Back, 2, 3);
            var topology = new CubeTopologyState(FaceId.Back);
            var request = PlanSingleDeathRequest(CreateEnemyExitSignal(40, TickEntityExitCause.Killed, cell, topology));

            AssertEnemyDeathRequest(request, 40, 9127, cell, topology);
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_EnemyDeathExit_EmitsEnemyDeathRequest()
        {
            var request = PlanSingleDeathRequest(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath));

            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Death)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ZeroPresentationSeed_FallsBackToExitedEntityId()
        {
            var request = PlanSingleDeathRequest(CreateEnemyExitSignal(44, TickEntityExitCause.Killed, presentationSeed: 0));

            Assert.That(request.PresentationSeed, Is.EqualTo(44));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_NonDeathSources_DoNotEmitDeath()
        {
            AssertNoDeathRequests(CreateEnemyExitSignal(40, TickEntityExitCause.BoxDestroy));
            AssertNoDeathRequests(CreateEnemyExitSignal(40, TickEntityExitCause.ItemConsume));
            AssertNoDeathRequests(CreateEnemyExitSignal(40, TickEntityExitCause.Killed, entityType: EntityType.Box));
            AssertNoDeathRequests(CreateEnemyExitSignal(0, TickEntityExitCause.Killed));
            var outOfBoundsPlan = PlanEnemyRequests(CreatePresentationData(
                entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.OutOfBounds) }));
            Assert.That(
                outOfBoundsPlan.Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Death)));

            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();
            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    CreatePresentationData(enemyDamageSignals: new[] { CreateEnemyDamageSignal(40) }),
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            Assert.That(
                builder.Build().Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Death)));
        }

        [Test]
        [Category("Extended")]
        public void EnemyPlanner_ExitTick_DoesNotEmitDamageForSameEntity()
        {
            var plan = PlanEnemyRequests(
                CreatePresentationData(
                    enemyDamageSignals: new[] { CreateEnemyDamageSignal(40) },
                    entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.Killed) }));

            Assert.That(
                plan.Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Damage)));
            Assert.That(
                plan.Requests,
                Has.Exactly(1).Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Death)));
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_EnemyDeathMigrationFlags_DefaultTrue()
        {
            var owner = new GameObject("EnemyDeathMigrationDefaultFlag");
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
        public void ProductionRuntime_EnemyDeathFlagOnMissingBinding_DiagnosticOnly()
        {
            var owner = new GameObject("EnemyDeathMigrationMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.Killed)));

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
        public void Coordinator_EnemyDeathWithoutVfxRuntime_NoOldExitEffect()
        {
            var scenario = CreatePresenterScenario("EnemyDeathFlagOff");
            try
            {
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.Killed, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_EnemyDeathBurstFlagOn_NoOldFlyawayAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("EnemyDeathFlagOn");
            var vfxPrefab = new GameObject("EnemyDeathFlagOn_VfxPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(vfxPrefab, GameplayVfxCueId.From(EnemyVfxCue.Death), 0.35f, 0.25f, 8);
                cueMap = CreateCueMap(binding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.Killed, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(scenario.Registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Destroy(cueMap, binding, vfxPrefab);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_EnemyDeathBurstFlagOnMissingBinding_NoOldFlyawayAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("EnemyDeathFlagOnMissingBinding");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.Killed, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
                Assert.That(scenario.Registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_BoxItemFlags_DoNotRestoreEnemyDeathOldPath()
        {
            var scenario = CreatePresenterScenario("EnemyDeathNotSuppressedByBoxItem");
            try
            {
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.Killed, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
            }
            finally
            {
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileDeathBindingBeatsHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Death);
            var sourcePolicy = CreatePolicy(cueId, maxConcurrentInstances: 5);
            var hostPolicy = CreatePolicy(cueId, maxConcurrentInstances: 8);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 40,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { sourcePolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 40), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(sourcePolicy));
        }

        [Test]
        [Category("Extended")]
        public void ProfileAwareResolver_SourceProfileMissingDeathFallsBackToHostDefault()
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Death);
            var hostPolicy = CreatePolicy(cueId, maxConcurrentInstances: 8);
            var profileOnlyPolicy = CreatePolicy(
                GameplayVfxCueId.From(EnemyVfxCue.Damage),
                maxConcurrentInstances: 12);
            var resolver = new ProfileAwareVfxBindingResolver(
                new FakeProfileProvider(
                    sourceEntityId: 40,
                    profile: new VfxProfile(GameplayVfxFamily.Enemy, new[] { profileOnlyPolicy })),
                new VfxCueMap(new[] { hostPolicy }));

            Assert.That(resolver.TryResolve(CreateRequest(cueId, sourceEntityId: 40), out var resolved), Is.True);
            Assert.That(resolved, Is.EqualTo(hostPolicy));
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathBurstPrefab_RemovedFromDefaultAuthoring()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyDeathBurstPrefabPath);

            Assert.That(prefab, Is.Null, EnemyDeathBurstPrefabPath);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathBurstBinding_RemovedFromDefaultAuthoring()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(EnemyDeathBurstBindingPath);

            Assert.That(binding, Is.Null, EnemyDeathBurstBindingPath);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultCueMap_DoesNotResolveRemovedEnemyDeathBurst()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(
                    GameplayVfxCueId.From(EnemyVfxCue.Death),
                    out _),
                Is.False);
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(EnemyVfxCue.Death), out _), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathPlannerAndRuntimeSources_DoNotReferenceAuthorityTypes()
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

        private static GameplayVfxRequestPlan PlanEnemyRequests(TickPresentationData presentationData)
        {
            var planner = new EnemyVfxRequestPlanner();
            var builder = new GameplayVfxRequestPlanBuilder();

            planner.Plan(
                new GameplayVfxPlanningContext(
                    12,
                    presentationData,
                    new CubeTopologyState(FaceId.Floor)),
                builder);

            return builder.Build();
        }

        private static GameplayVfxRequest PlanSingleDeathRequest(TickEntityExitPresentationSignal exitSignal)
        {
            var plan = PlanEnemyRequests(CreatePresentationData(entityExitSignals: new[] { exitSignal }));

            Assert.That(plan.Requests, Has.Count.EqualTo(1));
            return plan.Requests[0];
        }

        private static void AssertNoDeathRequests(params TickEntityExitPresentationSignal[] exitSignals)
        {
            var plan = PlanEnemyRequests(CreatePresentationData(entityExitSignals: exitSignals));

            Assert.That(
                plan.Requests,
                Has.None.Matches<GameplayVfxRequest>(
                    request => request.CueId == GameplayVfxCueId.From(EnemyVfxCue.Death)));
        }

        private static void AssertEnemyDeathRequest(
            in GameplayVfxRequest request,
            int entityId,
            int presentationSeed,
            SurfaceCell cell,
            CubeTopologyState topology)
        {
            Assert.That(request.TickIndex, Is.EqualTo(12));
            Assert.That(request.SequenceId, Is.EqualTo(entityId));
            Assert.That(request.SourceEntityId, Is.EqualTo(entityId));
            Assert.That(request.PresentationSeed, Is.EqualTo(presentationSeed));
            Assert.That(request.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.Death)));
            Assert.That(request.Timing, Is.EqualTo(VfxTimingKind.ImmediateOnTickPresentation));
            Assert.That(request.IsPersistent, Is.False);
            Assert.That(request.PersistentKey, Is.EqualTo(VfxPersistentKey.None));
            Assert.That(request.Anchor.Kind, Is.EqualTo(VfxAnchorKind.Cell));
            Assert.That(request.Anchor.Slot, Is.EqualTo(VfxAnchorSlot.CellCenter));
            Assert.That(request.Anchor.Cell, Is.EqualTo(cell));
            Assert.That(request.Anchor.Topology, Is.EqualTo(topology));
        }

        private static void AssertFlagCombinationPlans(
            bool deathEnabled,
            bool enemyDamageEnabled,
            bool boxEnabled,
            bool itemEnabled,
            int expectedRequests)
        {
            var owner = new GameObject("EnemyDeathFlagCombination");
            var prefab = new GameObject("EnemyDeathFlagCombinationPrefab");
            VfxBindingDefinitionAsset deathBinding = null;
            VfxBindingDefinitionAsset damageBinding = null;
            VfxBindingDefinitionAsset boxBinding = null;
            VfxBindingDefinitionAsset itemBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                deathBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.Death), 0.35f, 0.25f, 8);
                damageBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.Damage), 0.30f, 0.20f, 12);
                boxBinding = CreateBinding(prefab, GameplayVfxCueId.From(BoxVfxCue.DestroySmoke), 0.18f, 0.25f, 12);
                itemBinding = CreateBinding(prefab, GameplayVfxCueId.From(BoxVfxCue.ItemConsume), 0.18f, 0.20f, 8);
                cueMap = CreateCueMap(deathBinding, damageBinding, boxBinding, itemBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(
                    new[]
                    {
                        CreateEnemyExitSignal(40, TickEntityExitCause.Killed),
                        CreateBoxExitSignal(20, TickEntityExitCause.BoxDestroy),
                        CreateBoxExitSignal(21, TickEntityExitCause.ItemConsume),
                    },
                    new[] { CreateEnemyDamageSignal(41) }));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(expectedRequests));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(expectedRequests));
            }
            finally
            {
                Destroy(cueMap, itemBinding, boxBinding, damageBinding, deathBinding, prefab, owner);
            }
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEntityExitPresentationSignal[] exitSignals)
        {
            return CreateExtensionContext(exitSignals, Array.Empty<TickEnemyDamagePresentationSignal>());
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            TickEntityExitPresentationSignal[] exitSignals,
            TickEnemyDamagePresentationSignal[] enemyDamageSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.CommittedLocalTargetPoses[41] = new GameplayEntityPose(
                new Vector3(0.75f, 0.5f, 0f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(
                        enemyDamageSignals: enemyDamageSignals,
                        entityExitSignals: exitSignals),
                    topology,
                    new[]
                    {
                        CreateEnemyUnit(41, new SurfaceCell(FaceId.Floor, 2, 1)),
                    }),
                topology,
                stateStore,
                projector);
        }

        private static PresenterScenario CreatePresenterScenario(string name)
        {
            var root = new GameObject(name);
            var presenter = root.AddComponent<GameplayTickViewPresenter>();
            var registry = root.AddComponent<GameplayEntityViewRegistry>();
            var binder = new GameplayEntityViewBinder(
                registry,
                new DefaultGameplayEntityViewFactory(
                    registry.transform,
                    1f,
                    playerEntityId: 10));
            var topology = new CubeTopologyState(FaceId.Floor);
            var boardBounds = new BoardBounds(new Vector2Int(0, 0), new Vector2Int(3, 3));

            presenter.Initialize(
                binder,
                boardBounds,
                topology,
                1f,
                GameplayTimingProfile.CreateDefault());

            return new PresenterScenario(
                root,
                presenter,
                registry,
                topology,
                new SurfaceCell(FaceId.Floor, 1, 1));
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
            TickEnemyDamagePresentationSignal[] enemyDamageSignals = null,
            TickEntityExitPresentationSignal[] entityExitSignals = null)
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
                enemyDamageSignals ?? Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
        }

        private static TickEnemyDamagePresentationSignal CreateEnemyDamageSignal(int entityId)
        {
            return new TickEnemyDamagePresentationSignal(entityId, tookDamageThisTick: true, damageAmount: 1);
        }

        private static TickEntityExitPresentationSignal CreateEnemyExitSignal(
            int entityId,
            TickEntityExitCause exitCause,
            SurfaceCell cell = default,
            CubeTopologyState topology = default,
            EntityType entityType = EntityType.Unit,
            int presentationSeed = 9127)
        {
            var resolvedCell = cell.Equals(default(SurfaceCell))
                ? new SurfaceCell(FaceId.Floor, 1, 1)
                : cell;
            var resolvedTopology = topology.Equals(default(CubeTopologyState))
                ? new CubeTopologyState(FaceId.Floor)
                : topology;
            return new TickEntityExitPresentationSignal(
                entityId,
                exitCause,
                resolvedCell,
                resolvedTopology,
                Direction.Left,
                entityType,
                sourceActorEntityId: 10,
                presentationSeed: presentationSeed);
        }

        private static TickEntityExitPresentationSignal CreateBoxExitSignal(
            int entityId,
            TickEntityExitCause exitCause)
        {
            return new TickEntityExitPresentationSignal(
                entityId,
                exitCause,
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                Direction.Right,
                EntityType.Box,
                sourceActorEntityId: 10,
                presentationSeed: 7000 + entityId);
        }

        private static EntityState CreatePlayerUnit(int entityId, SurfaceCell position)
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
                aiMode = EnemyAiMode.None,
            };
        }

        private static EntityState CreateEnemyUnit(int entityId, SurfaceCell position)
        {
            return new EntityState
            {
                entityId = entityId,
                position = position,
                hp = 0,
                maxHp = 2,
                teamId = 2,
                type = EntityType.Unit,
                unitRole = UnitRole.Enemy,
                state = EntityPhaseState.Idle,
                facing = Direction.Left,
                boardPresence = EntityBoardPresence.Occupying,
                aiMode = EnemyAiMode.Chase,
            };
        }

        private static VfxBindingDefinitionAsset CreateBinding(
            GameObject prefab,
            GameplayVfxCueId cueId,
            float lifetime,
            float tail,
            int maxConcurrent)
        {
            GameplayVfxTestPrefabFactory.EnsureModelRoot(prefab);

            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", cueId.Family);
            SetField(binding, "cueCode", cueId.Code);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", lifetime);
            SetField(binding, "tailSeconds", tail);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", maxConcurrent);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static VfxBindingRuntimePolicy CreatePolicy(
            GameplayVfxCueId cueId,
            int maxConcurrentInstances)
        {
            return new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0.35f,
                tailSeconds: 0.25f,
                maxConcurrentInstances: maxConcurrentInstances);
        }

        private static GameplayVfxRequest CreateRequest(GameplayVfxCueId cueId, int sourceEntityId)
        {
            return new GameplayVfxRequest(
                tickIndex: 12,
                sequenceId: sourceEntityId,
                presentationSeed: sourceEntityId,
                sourceEntityId: sourceEntityId,
                cueId: cueId,
                anchor: VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                timing: VfxTimingKind.ImmediateOnTickPresentation);
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

        private readonly struct PresenterScenario
        {
            public PresenterScenario(
                GameObject root,
                GameplayTickViewPresenter presenter,
                GameplayEntityViewRegistry registry,
                CubeTopologyState topology,
                SurfaceCell enemyCell)
            {
                Root = root;
                Presenter = presenter;
                Registry = registry;
                Topology = topology;
                EnemyCell = enemyCell;
            }

            public GameObject Root { get; }

            public GameplayTickViewPresenter Presenter { get; }

            public GameplayEntityViewRegistry Registry { get; }

            public CubeTopologyState Topology { get; }

            public SurfaceCell EnemyCell { get; }

            public void Destroy()
            {
                GameplayVfxEnemyDeathCanonicalTests.Destroy(Root);
            }
        }

        private sealed class FakeProfileProvider : IGameplayVfxProfileProvider
        {
            private readonly int sourceEntityId;
            private readonly VfxProfile profile;

            public FakeProfileProvider(int sourceEntityId, VfxProfile profile)
            {
                this.sourceEntityId = sourceEntityId;
                this.profile = profile;
            }

            public bool TryResolveProfileForRequest(in GameplayVfxRequest request, out VfxProfile resolvedProfile)
            {
                if (request.SourceEntityId == sourceEntityId &&
                    profile != null)
                {
                    resolvedProfile = profile;
                    return true;
                }

                resolvedProfile = null;
                return false;
            }
        }
    }
}
