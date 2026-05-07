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
    public sealed class GameplayVfxEnemyDeathMotionMigrationTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string MotionPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/EnemyDeathMotionVfx.prefab";
        private const string MotionBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/EnemyDeathMotion_Binding.asset";
        private const string CommandPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/EnemyDeathMotionVfxCommandBuilder.cs";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";

        [Test]
        [Category("Extended")]
        public void EnemyDeathExit_BuildsDeathMotionCommand()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);

                var result = EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                Assert.That(result, Is.True);
                Assert.That(command.EntityId, Is.EqualTo(40));
                Assert.That(command.SourceActorEntityId, Is.EqualTo(10));
                Assert.That(command.SourceCell, Is.EqualTo(signal.SourceCell));
                Assert.That(command.Topology, Is.EqualTo(signal.Topology));
                Assert.That(command.PresentationSeed, Is.EqualTo(signal.PresentationSeed));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void NonDeathExit_DoesNotBuild()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.BoxDestroy)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.ItemConsume)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath, entityType: EntityType.Box)), Is.False);
                Assert.That(TryBuild(fixture, CreateEnemyExitSignal(0, TickEntityExitCause.EnemyDeath)), Is.False);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourcePose_ResolvedFromExitSignalSourceCell()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);

                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);
                fixture.PoseResolver.TryResolveEntityExitSignalLocalPose(
                    fixture.Projector,
                    signal,
                    out var expectedPose);

                Assert.That(Vector3.Distance(command.SourceLocalPosition, expectedPose.Position), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(command.SourceLocalRotation, expectedPose.Rotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void TargetPose_UsesLegacyCameraDirectionLogic()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                var signal = CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath);
                fixture.PoseResolver.TryResolveEntityExitSignalLocalPose(
                    fixture.Projector,
                    signal,
                    out var sourcePose);
                var expectedPlan = EnemyDeathExitEffectPlanBuilder.Build(
                    fixture.LocalSpaceRoot.transform,
                    sourcePose,
                    fixture.PlayerPose,
                    fixture.Camera,
                    fixture.Projector.CellSize,
                    signal.PresentationSeed);

                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    signal,
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                Assert.That(Vector3.Distance(command.TargetLocalPosition, expectedPlan.TargetLocalPosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Vector3.Distance(command.ArcLocalDirection.normalized, expectedPlan.ArcLocalDirection.normalized), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(command.ArcHeight, Is.EqualTo(expectedPlan.ArcHeight).Within(0.0001f));
                Assert.That(command.SpinDegrees, Is.EqualTo(expectedPlan.SpinDegrees).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ToParameterizedCommand_UsesDeathMotionCueCloneFallbackAndLegacyFade()
        {
            var fixture = CreateBuilderFixture();
            try
            {
                EnemyDeathMotionVfxCommandBuilder.TryBuild(
                    CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath),
                    fixture.TimingProfile,
                    fixture.PoseResolver,
                    fixture.Projector,
                    fixture.TargetResolver,
                    out var command);

                var parameterized = command.ToParameterizedMotionVfxCommand();

                Assert.That(parameterized.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
                Assert.That(parameterized.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback));
                Assert.That(parameterized.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.LegacyEnemyDeathFlyAway));
                Assert.That(parameterized.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.LegacyEnemyDeath));
                Assert.That(parameterized.BreakStartSeconds, Is.EqualTo(parameterized.DurationSeconds * 0.12f).Within(0.0001f));
                Assert.That(parameterized.FadeDurationSeconds, Is.EqualTo(parameterized.DurationSeconds * 0.88f).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ProductionRuntime_DeathMotionFlag_DefaultsTrue()
        {
            var owner = new GameObject("EnemyDeathMotionDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxEnemyDeathMotionMigration, Is.True);
                Assert.That(runtime.EnableGameplayVfxEnemyDeathBurstMigration, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionFlagOff_NoDeathMotionVfxAndNoOldFlyawayFallback()
        {
            var owner = new GameObject("EnemyDeathMotionFlagOffNoFallback");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = false;
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = true;
                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.Killed)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionFlagOn_BindingPresent_PlaysParameterizedMotion()
        {
            var owner = new GameObject("EnemyDeathMotionRuntime");
            var cameraObject = CreateCameraObject("EnemyDeathMotionRuntimeCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionFlagOn_MissingBinding_DiagnosticNoOldFallback()
        {
            var owner = new GameObject("EnemyDeathMotionMissingBinding");
            var cameraObject = CreateCameraObject("EnemyDeathMotionMissingBindingCamera");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionFlagOn_MissingCamera_DiagnosticNoOp()
        {
            var owner = new GameObject("EnemyDeathMotionMissingCamera");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.IsRuntimeInitialized, Is.True);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.MissingAnchorCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void MotionOnBurstOn_NewMotionAndBurstAllowed()
        {
            var owner = new GameObject("EnemyDeathMotionBurstCombo");
            var cameraObject = CreateCameraObject("EnemyDeathMotionBurstComboCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionBurstComboPrefab");
            VfxBindingDefinitionAsset motionBinding = null;
            VfxBindingDefinitionAsset burstBinding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                motionBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                burstBinding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.Death), tailSeconds: 0.25f, defaultLifetimeSeconds: 0.35f);
                cueMap = CreateCueMap(motionBinding, burstBinding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                runtime.ConfigureOutputCamera(cameraObject.GetComponent<Camera>(), owner.transform);

                runtime.Present(CreateExtensionContext(CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath)));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(2));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(2));
            }
            finally
            {
                Destroy(cueMap, motionBinding, burstBinding, prefab, cameraObject, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void Coordinator_MotionFlagOn_SuppressesOldFlyawayAndKeepsCleanup()
        {
            var scenario = CreatePresenterScenario("EnemyDeathMotionCoordinator");
            var cameraObject = CreateCameraObject("EnemyDeathMotionCoordinatorCamera");
            var prefab = CreateRuntimePrefab("EnemyDeathMotionCoordinatorPrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab, GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), tailSeconds: 0.2f);
                cueMap = CreateCueMap(binding);
                var runtime = scenario.Root.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxEnemyDeathBurstMigration = false;
                runtime.EnableGameplayVfxEnemyDeathMotionMigration = true;
                runtime.ConfigureHostDefaultMap(cueMap);
                scenario.Presenter.AttachOutputCamera(cameraObject.GetComponent<Camera>());
                scenario.Presenter.AttachPresentationExtension(runtime);
                scenario.Presenter.PresentInitial(
                    new[]
                    {
                        CreatePlayerUnit(10, new SurfaceCell(FaceId.Floor, 0, 1)),
                        CreateEnemyUnit(40, scenario.EnemyCell),
                    },
                    scenario.Topology);

                scenario.Presenter.Present(CreateResult(
                    CreatePresentationData(entityExitSignals: new[] { CreateEnemyExitSignal(40, TickEntityExitCause.EnemyDeath, scenario.EnemyCell, scenario.Topology) }),
                    scenario.Topology,
                    Array.Empty<EntityState>()));
                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
                Assert.That(scenario.Registry.TryGetView(40, out var enemyView), Is.True);
                Assert.That(enemyView.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Destroy(cueMap, binding, prefab, cameraObject);
                scenario.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotionPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MotionPrefabPath);

            Assert.That(prefab, Is.Not.Null, MotionPrefabPath);
            var validation = VfxPrefabValidationDiagnostics.ValidatePrefab(prefab);

            Assert.That(validation.HasErrors, Is.False, string.Join("\n", validation.Messages));
            Assert.That(validation.HasWarnings, Is.False, string.Join("\n", validation.Messages));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(HasComponentTypeNamed(prefab, "NavMeshAgent"), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void EnemyDeathMotionBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(MotionBindingPath);

            Assert.That(binding, Is.Not.Null, MotionBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.DefaultLifetimeSeconds, Is.EqualTo(0f).Within(0.001f));
            Assert.That(binding.TailSeconds, Is.InRange(0.18f, 0.25f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(8));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesEnemyDeathMotion()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(cueMap.TryResolvePrefab(GameplayVfxCueId.From(EnemyVfxCue.DeathMotion), out var prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        [Category("Extended")]
        public void CommandAndRuntimeSources_DoNotReferenceAuthorityTypes()
        {
            AssertForbiddenAuthorityTokensAbsent(ReadRepoFile(CommandPath) + "\n" + ReadRepoFile(ProductionRuntimePath));
        }

        private static bool TryBuild(BuilderFixture fixture, TickEntityExitPresentationSignal signal)
        {
            return EnemyDeathMotionVfxCommandBuilder.TryBuild(
                signal,
                fixture.TimingProfile,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.TargetResolver,
                out _);
        }

        private static BuilderFixture CreateBuilderFixture()
        {
            var localSpaceRoot = new GameObject("EnemyDeathMotionBuilderRoot");
            var cameraObject = CreateCameraObject("EnemyDeathMotionBuilderCamera");
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[40] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[10] = UnitRole.Player;
            var playerPose = new GameplayEntityPose(new Vector3(0.35f, 0.15f, 0f), Quaternion.identity);
            stateStore.CommittedLocalTargetPoses[10] = playerPose;
            var trackState = new GameplayPresentationTrackState();
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);
            var resolver = new EnemyDeathMotionTargetResolver(
                localSpaceRoot.transform,
                cameraObject.GetComponent<Camera>(),
                stateStore,
                projector.CellSize);
            return new BuilderFixture(
                localSpaceRoot,
                cameraObject,
                cameraObject.GetComponent<Camera>(),
                GameplayTimingProfile.CreateDefault(),
                new GameplayPoseResolver(stateStore, trackState),
                projector,
                resolver,
                playerPose);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEntityExitPresentationSignal[] exitSignals)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[40] = EntityType.Unit;
            stateStore.UnitRolesByEntityId[10] = UnitRole.Player;
            stateStore.CommittedLocalTargetPoses[10] = new GameplayEntityPose(
                new Vector3(0.35f, 0.15f, 0f),
                Quaternion.identity);
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(4, 4)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(
                    CreatePresentationData(entityExitSignals: exitSignals),
                    topology,
                    Array.Empty<EntityState>()),
                topology,
                stateStore,
                projector,
                timingProfile: GameplayTimingProfile.CreateDefault());
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
                Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(),
                entityExitSignals ?? Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>());
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
            float tailSeconds,
            float defaultLifetimeSeconds = 0f)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", cueId.Family);
            SetField(binding, "cueCode", cueId.Code);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", defaultLifetimeSeconds);
            SetField(binding, "tailSeconds", tailSeconds);
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

        private static GameObject CreateRuntimePrefab(string name)
        {
            var prefab = new GameObject(name);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(prefab.transform, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return prefab;
        }

        private static GameObject CreateCameraObject(string name)
        {
            var cameraObject = new GameObject(name);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.3f;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.identity;
            return cameraObject;
        }

        private static void AssertForbiddenAuthorityTokensAbsent(string source)
        {
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

        private static string ReadRepoFile(string path)
        {
            return File.ReadAllText(path);
        }

        private static bool HasComponentTypeNamed(GameObject root, string typeName)
        {
            var components = root.GetComponentsInChildren<Component>(includeInactive: true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
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

        private readonly struct BuilderFixture
        {
            public BuilderFixture(
                GameObject localSpaceRoot,
                GameObject cameraObject,
                Camera camera,
                GameplayTimingProfile timingProfile,
                GameplayPoseResolver poseResolver,
                GameplayCubeProjector projector,
                IEnemyDeathMotionTargetResolver targetResolver,
                GameplayEntityPose playerPose)
            {
                LocalSpaceRoot = localSpaceRoot;
                CameraObject = cameraObject;
                Camera = camera;
                TimingProfile = timingProfile;
                PoseResolver = poseResolver;
                Projector = projector;
                TargetResolver = targetResolver;
                PlayerPose = playerPose;
            }

            public GameObject LocalSpaceRoot { get; }

            private GameObject CameraObject { get; }

            public Camera Camera { get; }

            public GameplayTimingProfile TimingProfile { get; }

            public GameplayPoseResolver PoseResolver { get; }

            public GameplayCubeProjector Projector { get; }

            public IEnemyDeathMotionTargetResolver TargetResolver { get; }

            public GameplayEntityPose PlayerPose { get; }

            public void Destroy()
            {
                GameplayVfxEnemyDeathMotionMigrationTests.Destroy(CameraObject, LocalSpaceRoot);
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
                GameplayVfxEnemyDeathMotionMigrationTests.Destroy(Root);
            }
        }
    }
}
