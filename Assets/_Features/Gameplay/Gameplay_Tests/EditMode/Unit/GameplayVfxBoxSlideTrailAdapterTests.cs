using System;
using System.IO;
using System.Reflection;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Authoring;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxBoxSlideTrailAdapterTests
    {
        private const string HostDefaultCueMapPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Maps/GameplayVfxHostDefaultCueMap.asset";
        private const string SlideTrailPrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Prefabs/BoxSlideDustTrailVfx.prefab";
        private const string SlideTrailBindingPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Authoring/Bindings/BoxSlideDustTrail_Binding.asset";
        private const string CommandBuilderPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/BoxSlideTrailVfxCommandBuilder.cs";
        private const string ParameterizedMotionCommandPath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/ParameterizedMotion/ParameterizedMotionVfxCommand.cs";
        private const string VfxEnumsPath =
            "Assets/_Features/Gameplay/Gameplay_Vfx/Runtime/GameplayVfxEnums.cs";
        private const string ProductionRuntimePath =
            "Assets/_Features/Gameplay/Gameplay_VfxHost/Runtime/Production/GameplayVfxProductionRuntime.cs";
        private const string TickPresentationDataPath =
            "Assets/_Features/Gameplay/Gameplay_Loop/Runtime/TickPresentationData.cs";

        [Test]
        [Category("Extended")]
        public void BoxSlideMotion_EmitsSlideDustTrailCommand()
        {
            var fixture = CreateBuilderFixture();

            var built = BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                CreateMotion(),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out var command);

            Assert.That(built, Is.True);
            Assert.That(command.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail)));
            Assert.That(command.SourceEntityId, Is.EqualTo(30));
        }

        [Test]
        [Category("Extended")]
        public void NonBoxSlideMotion_DoesNotEmitCommand()
        {
            var fixture = CreateBuilderFixture();

            var built = BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                CreateMotion(motionKind: TickEntityMotionKind.Push),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out _);

            Assert.That(built, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void SourceAndTargetLocalPoses_ProjectFromCellsAndPreserveFace()
        {
            var fixture = CreateBuilderFixture(new CubeTopologyState(FaceId.Front));
            var sourceCell = new SurfaceCell(FaceId.Front, 2, 3);
            var destinationCell = new SurfaceCell(FaceId.Front, 3, 3);
            var motion = CreateMotion(
                sourceCell: sourceCell,
                destinationCell: destinationCell,
                sourceTopology: fixture.Topology,
                destinationTopology: fixture.Topology,
                sourceFacing: Direction.Right,
                destinationFacing: Direction.Right);

            var built = BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                motion,
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out var command);
            fixture.PoseResolver.TryResolveLocalPose(
                fixture.Projector,
                motion.EntityId,
                sourceCell,
                fixture.Topology,
                Direction.Right,
                out var expectedSourcePose);
            fixture.PoseResolver.TryResolveLocalPose(
                fixture.Projector,
                motion.EntityId,
                destinationCell,
                fixture.Topology,
                Direction.Right,
                out var expectedTargetPose);

            Assert.That(built, Is.True);
            Assert.That(motion.SourceCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(motion.DestinationCell.face, Is.EqualTo(FaceId.Front));
            Assert.That(Vector3.Distance(command.SourceLocalPosition, expectedSourcePose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Vector3.Distance(command.TargetLocalPosition, expectedTargetPose.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(command.SourceLocalRotation, expectedSourcePose.Rotation), Is.LessThanOrEqualTo(0.001f));
            Assert.That(Quaternion.Angle(command.TargetLocalRotation, expectedTargetPose.Rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void Duration_UsesBoxSlideTiming()
        {
            var fixture = CreateBuilderFixture(boxSlideStepIntervalSeconds: 0.37f);

            BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                CreateMotion(),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out var command);

            Assert.That(command.DurationSeconds, Is.EqualTo(0.37f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void Command_UsesLinearSampler()
        {
            var fixture = CreateBuilderFixture();

            BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                CreateMotion(),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out var command);

            Assert.That(command.SamplerMode, Is.EqualTo(ParameterizedMotionVfxSamplerMode.Linear));
        }

        [Test]
        [Category("Extended")]
        public void SequenceKey_ChangesByEntitySourceTargetTick()
        {
            var baseMotion = CreateMotion();

            var baseSequence = BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(12, baseMotion);
            Assert.That(BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(13, baseMotion), Is.Not.EqualTo(baseSequence));
            Assert.That(
                BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(12, CreateMotion(entityId: 31)),
                Is.Not.EqualTo(baseSequence));
            Assert.That(
                BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(
                    12,
                    CreateMotion(sourceCell: new SurfaceCell(FaceId.Floor, 2, 1))),
                Is.Not.EqualTo(baseSequence));
            Assert.That(
                BoxSlideTrailVfxCommandBuilder.ComputeSequenceId(
                    12,
                    CreateMotion(destinationCell: new SurfaceCell(FaceId.Floor, 4, 1))),
                Is.Not.EqualTo(baseSequence));
        }

        [Test]
        [Category("Extended")]
        public void MultiSegmentSlide_EmitsPerSegmentCommand()
        {
            var fixture = CreateBuilderFixture();
            var motions = new[]
            {
                CreateMotion(sourceCell: new SurfaceCell(FaceId.Floor, 1, 1), destinationCell: new SurfaceCell(FaceId.Floor, 2, 1)),
                CreateMotion(sourceCell: new SurfaceCell(FaceId.Floor, 2, 1), destinationCell: new SurfaceCell(FaceId.Floor, 3, 1)),
            };
            var builtCount = 0;

            for (var i = 0; i < motions.Length; i++)
            {
                if (BoxSlideTrailVfxCommandBuilder.TryBuild(
                        12 + i,
                        motions[i],
                        fixture.TimingProfile,
                        fixture.MotionTimingResolver,
                        fixture.PoseResolver,
                        fixture.Projector,
                        fixture.Topology,
                        out _))
                {
                    builtCount++;
                }
            }

            Assert.That(builtCount, Is.EqualTo(2));
        }

        [Test]
        [Category("Extended")]
        public void Command_UsesPrefabOnlyCloneAndAlphaOnlyFade()
        {
            var fixture = CreateBuilderFixture();

            BoxSlideTrailVfxCommandBuilder.TryBuild(
                12,
                CreateMotion(),
                fixture.TimingProfile,
                fixture.MotionTimingResolver,
                fixture.PoseResolver,
                fixture.Projector,
                fixture.Topology,
                out var command);

            Assert.That(command.CloneMode, Is.EqualTo(ParameterizedMotionVfxCloneMode.PrefabOnly));
            Assert.That(command.FadeMode, Is.EqualTo(ParameterizedMotionVfxFadeMode.AlphaOnly));
            Assert.That(command.ArcHeight, Is.EqualTo(0f));
            Assert.That(command.BreakStartSeconds, Is.EqualTo(command.DurationSeconds).Within(0.0001f));
            Assert.That(command.FadeDurationSeconds, Is.EqualTo(BoxSlideTrailVfxCommandBuilder.DefaultFadeDurationSeconds).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void Flag_DefaultTrue()
        {
            var owner = new GameObject("BoxSlideTrailDefaultFlag");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();

                Assert.That(runtime.EnableGameplayVfxBoxSlideTrail, Is.True);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOff_DropsSlideTrail()
        {
            var owner = new GameObject("BoxSlideTrailFlagOff");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxSlideTrail = false;

                runtime.Present(CreateExtensionContext(CreateMotion()));

                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
                Assert.That(runtime.IsRuntimeInitialized, Is.False);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOn_BindingPresent_PlaysOneParameterizedInstance()
        {
            var owner = new GameObject("BoxSlideTrailRuntime");
            var prefab = GameplayVfxParameterizedMotionRuntimeTests.CreateRuntimePrefab("BoxSlideTrailRuntimePrefab");
            VfxBindingDefinitionAsset binding = null;
            VfxCueMapAsset cueMap = null;
            try
            {
                binding = CreateBinding(prefab);
                cueMap = CreateCueMap(binding);
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxSlideTrail = true;
                runtime.ConfigureHostDefaultMap(cueMap);

                runtime.Present(CreateExtensionContext(CreateMotion()));

                Assert.That(runtime.LastPlannedRequestCount, Is.EqualTo(1));
                Assert.That(runtime.MissingBindingCount, Is.Zero);
                Assert.That(runtime.MissingPrefabCount, Is.Zero);
                Assert.That(runtime.ActiveVfxInstanceCount, Is.EqualTo(1));
            }
            finally
            {
                Destroy(cueMap, binding, prefab, owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void FlagOn_MissingBinding_DiagnosticNoSpawn()
        {
            var owner = new GameObject("BoxSlideTrailMissingBinding");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxSlideTrail = true;

                runtime.Present(CreateExtensionContext(CreateMotion()));

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
        public void Flag_IndependentFromBoxExitAndFlipFlags()
        {
            var owner = new GameObject("BoxSlideTrailFlagIndependence");
            try
            {
                var runtime = owner.AddComponent<GameplayVfxProductionRuntime>();
                runtime.EnableGameplayVfxBoxSlideTrail = false;
                runtime.EnableGameplayVfxBoxDestroySmokeMigration = true;
                runtime.EnableGameplayVfxFlipImpactBurstMigration = true;
                runtime.EnableGameplayVfxFlipDestroySelfMotionMigration = true;

                runtime.Present(CreateExtensionContext(CreateMotion()));

                Assert.That(runtime.EnableGameplayVfxBoxSlideTrail, Is.False);
                Assert.That(runtime.LastPlannedRequestCount, Is.Zero);
            }
            finally
            {
                Destroy(owner);
            }
        }

        [Test]
        [Category("Extended")]
        public void NoLegacyPresenterSuppressGate()
        {
            var runtimeSource = ReadRepoFile(ProductionRuntimePath);

            Assert.That(runtimeSource, Does.Not.Contain("SuppressLegacyBoxSlide"));
            Assert.That(runtimeSource, Does.Not.Contain("SuppressLegacy"));
        }

        [Test]
        [Category("Extended")]
        public void SlideTrail_CompletesAndReleasesAfterTail()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0.20f);
            try
            {
                var command = new ParameterizedMotionVfxCommand(
                    GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail),
                    sourceEntityId: 30,
                    sequenceId: 7,
                    presentationSeed: 7,
                    sourceLocalPosition: Vector3.zero,
                    sourceLocalRotation: Quaternion.identity,
                    targetLocalPosition: new Vector3(1f, 0f, 0f),
                    targetLocalRotation: Quaternion.identity,
                    durationSeconds: 0.5f,
                    arcHeight: 0f,
                    breakStartSeconds: 0.5f,
                    fadeDurationSeconds: 0.20f,
                    ParameterizedMotionVfxFadeMode.AlphaOnly,
                    ParameterizedMotionVfxCloneMode.PrefabOnly,
                    ParameterizedMotionVfxSamplerMode.Linear);

                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);

                fixture.TimeProvider.TimeSeconds = 0.5f;
                fixture.Pool.Advance(0.5f);
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));

                fixture.TimeProvider.TimeSeconds = 0.71f;
                fixture.Pool.Advance(0.21f);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
                Assert.That(fixture.Pool.PooledCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SlideTrailRuntime_ParameterizedInstanceMovesLinearly()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0.20f);
            try
            {
                var command = new ParameterizedMotionVfxCommand(
                    GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail),
                    sourceEntityId: 30,
                    sequenceId: 7,
                    presentationSeed: 7,
                    sourceLocalPosition: Vector3.zero,
                    sourceLocalRotation: Quaternion.identity,
                    targetLocalPosition: new Vector3(2f, 0f, 0f),
                    targetLocalRotation: Quaternion.identity,
                    durationSeconds: 0.5f,
                    arcHeight: 1f,
                    breakStartSeconds: 0.5f,
                    fadeDurationSeconds: 0.20f,
                    ParameterizedMotionVfxFadeMode.AlphaOnly,
                    ParameterizedMotionVfxCloneMode.PrefabOnly,
                    ParameterizedMotionVfxSamplerMode.Linear);

                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 0.25f;
                fixture.Pool.Advance(0.25f);

                Assert.That(Vector3.Distance(instance.localPosition, new Vector3(1f, 0f, 0f)), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(instance.localPosition.z, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void BoxSlideDustTrailPrefab_PassesValidation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlideTrailPrefabPath);

            Assert.That(prefab, Is.Not.Null, SlideTrailPrefabPath);
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
        public void BoxSlideDustTrailBinding_Validates()
        {
            var binding = AssetDatabase.LoadAssetAtPath<VfxBindingDefinitionAsset>(SlideTrailBindingPath);

            Assert.That(binding, Is.Not.Null, SlideTrailBindingPath);
            Assert.That(binding.CueId, Is.EqualTo(GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail)));
            Assert.That(binding.Requirement, Is.EqualTo(VfxBindingRequirement.DiagnosticIfMissing));
            Assert.That(binding.MissingAnchorPolicy, Is.EqualTo(VfxMissingAnchorPolicy.ReportDiagnostic));
            Assert.That(binding.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(binding.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(binding.TailSeconds, Is.EqualTo(0.20f).Within(0.001f));
            Assert.That(binding.InitialPoolSize, Is.EqualTo(4));
            Assert.That(binding.MaxConcurrentInstances, Is.EqualTo(12));
            Assert.That(binding.ValidateAuthoring().HasErrors, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void HostDefaultMap_ResolvesSlideDustTrail()
        {
            var cueMap = AssetDatabase.LoadAssetAtPath<VfxCueMapAsset>(HostDefaultCueMapPath);

            Assert.That(cueMap, Is.Not.Null, HostDefaultCueMapPath);
            Assert.That(
                cueMap.BuildRuntimeMap().TryResolve(GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail), out var policy),
                Is.True);
            Assert.That(policy.PlaybackMode, Is.EqualTo(VfxPlaybackMode.OneShot));
            Assert.That(policy.StopPolicy, Is.EqualTo(VfxStopPolicy.AuthoredDuration));
            Assert.That(policy.MaxConcurrentInstances, Is.EqualTo(12));
        }

        [Test]
        [Category("Extended")]
        public void Boundary_NoAuthorityCarrierOrUnitProjectileTrailExpansion()
        {
            var slideAdapterSource = ReadRepoFile(CommandBuilderPath);
            var parameterizedSamplerSource = ReadRepoFile(ParameterizedMotionCommandPath);
            var runtimeSource = ReadRepoFile(ProductionRuntimePath);
            var tickPresentationDataSource = ReadRepoFile(TickPresentationDataPath);
            var cueSource = ReadRepoFile(VfxEnumsPath);
            var source = slideAdapterSource + "\n" + parameterizedSamplerSource + "\n" + runtimeSource;
            var forbiddenTokens = new[]
            {
                "WorldState",
                "WorldSnapshot",
                "TickPipeline",
                "ProjectedWorld",
                "FinalizationBatch",
                "DeterminismHashBuilder",
                "MotionTrack",
                "MotionClip",
                "SampleLinearConstant",
                "UnitMovementTrail",
                "ProjectileTrail",
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), token);
            }

            Assert.That(slideAdapterSource, Does.Not.Contain("ActionPlanId"));
            Assert.That(tickPresentationDataSource, Does.Not.Contain("SlideDustTrail"));
            Assert.That(tickPresentationDataSource, Does.Not.Contain("BoxSlideTrail"));
            Assert.That(Enum.GetNames(typeof(BoxVfxCue)), Has.Length.EqualTo(12));
            Assert.That(cueSource, Does.Not.Contain("ExactLinear"));
            Assert.That(runtimeSource, Does.Not.Contain("EnableGameplayVfxExactLinear"));
        }

        private static BuilderFixture CreateBuilderFixture(
            CubeTopologyState? topology = null,
            float boxSlideStepIntervalSeconds = 0.2f)
        {
            var resolvedTopology = topology ?? new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(resolvedTopology);
            stateStore.EntityTypesByEntityId[30] = EntityType.Box;
            stateStore.EntityTypesByEntityId[31] = EntityType.Box;
            var trackState = new GameplayPresentationTrackState();
            var timingProfile = CreateTimingProfile(boxSlideStepIntervalSeconds);
            return new BuilderFixture(
                resolvedTopology,
                timingProfile,
                new GameplayCubeProjector(
                    new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 8)),
                    1f),
                new GameplayPoseResolver(stateStore, trackState),
                new GameplayMotionTimingResolver(stateStore, trackState));
        }

        private static GameplayTimingProfile CreateTimingProfile(float boxSlideStepIntervalSeconds)
        {
            return new GameplayTimingProfile(
                GameplayTimingProfile.DefaultSimulationTicksPerSecond,
                GameplayTimingProfile.DefaultInitialMoveDelaySeconds,
                GameplayTimingProfile.DefaultRepeatedMoveIntervalSeconds,
                boxSlideStepIntervalSeconds,
                GameplayTimingProfile.DefaultProjectileStepIntervalSeconds,
                GameplayTimingProfile.DefaultMoveMotionDurationSeconds,
                GameplayTimingProfile.DefaultPushMotionDurationSeconds,
                GameplayTimingProfile.DefaultTopologyMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipMotionDurationSeconds,
                GameplayTimingProfile.DefaultFlipArcHeightInCells,
                GameplayTimingProfile.DefaultMaxTicksPerFrame);
        }

        private static TickEntityMotion CreateMotion(
            int entityId = 30,
            TickEntityMotionKind motionKind = TickEntityMotionKind.BoxSlide,
            SurfaceCell? sourceCell = null,
            SurfaceCell? destinationCell = null,
            CubeTopologyState? sourceTopology = null,
            CubeTopologyState? destinationTopology = null,
            Direction? sourceFacing = null,
            Direction? destinationFacing = null)
        {
            return new TickEntityMotion(
                entityId,
                motionKind,
                sourceCell ?? new SurfaceCell(FaceId.Floor, 1, 1),
                destinationCell ?? new SurfaceCell(FaceId.Floor, 2, 1),
                sourceTopology,
                destinationTopology,
                sourceFacing,
                destinationFacing);
        }

        private static GameplayTickPresentationExtensionContext CreateExtensionContext(
            params TickEntityMotion[] motions)
        {
            var topology = new CubeTopologyState(FaceId.Floor);
            var stateStore = new GameplayPresentationStateStore();
            stateStore.ResetSession(topology);
            stateStore.EntityTypesByEntityId[30] = EntityType.Box;
            var projector = new GameplayCubeProjector(
                new BoardBounds(new Vector2Int(0, 0), new Vector2Int(8, 8)),
                1f);

            return new GameplayTickPresentationExtensionContext(
                CreateResult(new TickPresentationData(motions), topology),
                topology,
                stateStore,
                projector,
                timingProfile: CreateTimingProfile(0.2f));
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

        private static VfxBindingDefinitionAsset CreateBinding(GameObject prefab)
        {
            var binding = ScriptableObject.CreateInstance<VfxBindingDefinitionAsset>();
            SetField(binding, "family", GameplayVfxFamily.Box);
            SetField(binding, "cueCode", (int)BoxVfxCue.SlideDustTrail);
            SetField(binding, "prefab", prefab);
            SetField(binding, "requirement", VfxBindingRequirement.DiagnosticIfMissing);
            SetField(binding, "missingAnchorPolicy", VfxMissingAnchorPolicy.ReportDiagnostic);
            SetField(binding, "playbackMode", VfxPlaybackMode.OneShot);
            SetField(binding, "stopPolicy", VfxStopPolicy.AuthoredDuration);
            SetField(binding, "defaultLifetimeSeconds", 0f);
            SetField(binding, "tailSeconds", 0.20f);
            SetField(binding, "initialPoolSize", 4);
            SetField(binding, "maxConcurrentInstances", 12);
            return binding;
        }

        private static VfxCueMapAsset CreateCueMap(params VfxBindingDefinitionAsset[] bindings)
        {
            var cueMap = ScriptableObject.CreateInstance<VfxCueMapAsset>();
            SetField(cueMap, "bindings", bindings);
            return cueMap;
        }

        private static PoolFixture CreatePoolFixture(float tailSeconds)
        {
            var owner = new GameObject("BoxSlideTrailPoolOwner");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = GameplayVfxParameterizedMotionRuntimeTests.CreateRuntimePrefab("BoxSlideTrailPoolPrefab");
            var prefabProvider = new GameplayVfxParameterizedMotionRuntimeTests.SinglePrefabProvider(prefab);
            var timeProvider = new GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(root, prefabProvider, timeProvider);
            var cueId = GameplayVfxCueId.From(BoxVfxCue.SlideDustTrail);
            var request = new GameplayVfxRequest(
                1,
                7,
                7,
                sourceEntityId: 30,
                cueId,
                VfxAnchor.ForCell(
                    new SurfaceCell(FaceId.Floor, 1, 1),
                    new CubeTopologyState(FaceId.Floor),
                    VfxAnchorSlot.CellCenter),
                VfxTimingKind.ImmediateOnTickPresentation);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 12);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter,
                Vector3.zero,
                Quaternion.identity);
            return new PoolFixture(
                owner,
                prefab,
                root,
                pool,
                timeProvider,
                new ResolvedVfxPlaybackCommand(request, policy, anchor));
        }

        private static bool HasComponentTypeNamed(GameObject root, string typeName)
        {
            var components = root.GetComponentsInChildren<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadRepoFile(string path)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), path));
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
                CubeTopologyState topology,
                GameplayTimingProfile timingProfile,
                GameplayCubeProjector projector,
                GameplayPoseResolver poseResolver,
                GameplayMotionTimingResolver motionTimingResolver)
            {
                Topology = topology;
                TimingProfile = timingProfile;
                Projector = projector;
                PoseResolver = poseResolver;
                MotionTimingResolver = motionTimingResolver;
            }

            public CubeTopologyState Topology { get; }

            public GameplayTimingProfile TimingProfile { get; }

            public GameplayCubeProjector Projector { get; }

            public GameplayPoseResolver PoseResolver { get; }

            public GameplayMotionTimingResolver MotionTimingResolver { get; }
        }

        private readonly struct PoolFixture
        {
            public PoolFixture(
                GameObject owner,
                GameObject prefab,
                GameplayVfxRuntimeRoot root,
                GameplayVfxGameObjectPool pool,
                GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider timeProvider,
                ResolvedVfxPlaybackCommand playbackCommand)
            {
                Owner = owner;
                Prefab = prefab;
                Root = root;
                Pool = pool;
                TimeProvider = timeProvider;
                PlaybackCommand = playbackCommand;
            }

            public GameObject Owner { get; }

            public GameObject Prefab { get; }

            public GameplayVfxRuntimeRoot Root { get; }

            public GameplayVfxGameObjectPool Pool { get; }

            public GameplayVfxParameterizedMotionRuntimeTests.FakeTimeProvider TimeProvider { get; }

            public ResolvedVfxPlaybackCommand PlaybackCommand { get; }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxParameterizedMotionRuntimeTests.Destroy(Prefab, Owner);
            }
        }
    }
}
