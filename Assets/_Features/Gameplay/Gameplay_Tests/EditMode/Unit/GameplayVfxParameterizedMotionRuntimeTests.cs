using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxParameterizedMotionRuntimeTests
    {
        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_StartsAtSourcePose()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f);
            try
            {
                var command = CreateCommand();
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                Assert.That(Vector3.Distance(instance.localPosition, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(instance.localRotation, command.SourceLocalRotation), Is.LessThanOrEqualTo(0.001f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_ReachesTargetPoseAtEnd()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0.2f);
            try
            {
                var command = CreateCommand(durationSeconds: 1f);
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);

                Assert.That(Vector3.Distance(instance.localPosition, command.TargetLocalPosition), Is.LessThanOrEqualTo(0.0001f));
                Assert.That(Quaternion.Angle(instance.localRotation, command.TargetLocalRotation), Is.LessThanOrEqualTo(0.001f));
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_AppliesArcHeight()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f);
            try
            {
                var command = new ParameterizedMotionVfxCommand(
                    GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion),
                    sourceEntityId: 30,
                    sequenceId: 7,
                    presentationSeed: 7,
                    sourceLocalPosition: Vector3.zero,
                    sourceLocalRotation: Quaternion.identity,
                    targetLocalPosition: new Vector3(2f, 0f, 0f),
                    targetLocalRotation: Quaternion.identity,
                    durationSeconds: 1f,
                    arcHeight: 0.6f,
                    breakStartSeconds: 0.7f,
                    fadeDurationSeconds: 0.3f,
                    ParameterizedMotionVfxFadeMode.ScaleAndAlpha,
                    ParameterizedMotionVfxCloneMode.PrefabOnly);
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 0.5f;
                fixture.Pool.Advance(0.5f);

                Assert.That(instance.localPosition.z, Is.LessThan(-0.1f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_StartsAtSourcePosition()
        {
            var command = CreateCommand(samplerMode: ParameterizedMotionVfxSamplerMode.Linear);

            var sample = ParameterizedMotionVfxSampler.Sample(command, elapsedSeconds: 0f);

            Assert.That(Vector3.Distance(sample.LocalPosition, command.SourceLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalRotation, command.SourceLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_ReachesTargetPositionAtEnd()
        {
            var command = CreateCommand(samplerMode: ParameterizedMotionVfxSamplerMode.Linear);

            var sample = ParameterizedMotionVfxSampler.Sample(command, command.DurationSeconds);

            Assert.That(Vector3.Distance(sample.LocalPosition, command.TargetLocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalRotation, command.TargetLocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_MidpointIsExactLinearMidpoint()
        {
            var command = CreateCommand(
                arcHeight: 0f,
                samplerMode: ParameterizedMotionVfxSamplerMode.Linear);

            var sample = ParameterizedMotionVfxSampler.Sample(command, command.DurationSeconds * 0.5f);

            Assert.That(Vector3.Distance(sample.LocalPosition, new Vector3(1f, 0f, 0f)), Is.LessThanOrEqualTo(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_IgnoresArcHeight()
        {
            var lowArcCommand = CreateCommand(
                arcHeight: 0f,
                samplerMode: ParameterizedMotionVfxSamplerMode.Linear);
            var highArcCommand = CreateCommand(
                arcHeight: 10f,
                samplerMode: ParameterizedMotionVfxSamplerMode.Linear);

            var lowArcSample = ParameterizedMotionVfxSampler.Sample(lowArcCommand, lowArcCommand.DurationSeconds * 0.5f);
            var highArcSample = ParameterizedMotionVfxSampler.Sample(highArcCommand, highArcCommand.DurationSeconds * 0.5f);

            Assert.That(Vector3.Distance(highArcSample.LocalPosition, lowArcSample.LocalPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(highArcSample.LocalRotation, lowArcSample.LocalRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_DoesNotUseFlipArcClamp()
        {
            var command = CreateCommand(
                arcHeight: 0f,
                samplerMode: ParameterizedMotionVfxSamplerMode.Linear);

            var sample = ParameterizedMotionVfxSampler.Sample(command, command.DurationSeconds * 0.5f);
            var flipArcSample = FlipArcSampler.Sample(command.SourcePose, command.TargetPose, 0.5f, command.ArcHeight);

            Assert.That(sample.LocalPosition.z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(Vector3.Distance(sample.LocalPosition, flipArcSample.Position), Is.GreaterThan(0.00001f));
        }

        [Test]
        [Category("Extended")]
        public void LinearSampler_SlerpsRotation()
        {
            var command = CreateCommand(samplerMode: ParameterizedMotionVfxSamplerMode.Linear);
            var expectedRotation = Quaternion.Slerp(
                command.SourceLocalRotation,
                command.TargetLocalRotation,
                0.5f);

            var sample = ParameterizedMotionVfxSampler.Sample(command, command.DurationSeconds * 0.5f);

            Assert.That(Quaternion.Angle(sample.LocalRotation, expectedRotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void FlipArcSampler_Regression_UnchangedForFlipDestroySelf()
        {
            var command = CreateCommand(samplerMode: ParameterizedMotionVfxSamplerMode.FlipArc);
            var expected = FlipArcSampler.Sample(command.SourcePose, command.TargetPose, 0.5f, command.ArcHeight);

            var sample = ParameterizedMotionVfxSampler.Sample(command, command.DurationSeconds * 0.5f);

            Assert.That(Vector3.Distance(sample.LocalPosition, expected.Position), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalRotation, expected.Rotation), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_BreakFade_StartsAtBreakStart()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f);
            try
            {
                var command = CreateCommand(durationSeconds: 1f, breakStartSeconds: 0.7f, fadeDurationSeconds: 0.3f);
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 0.69f;
                fixture.Pool.Advance(0.69f);
                Assert.That(instance.localScale, Is.EqualTo(Vector3.one));

                fixture.TimeProvider.TimeSeconds = 0.75f;
                fixture.Pool.Advance(0.06f);
                Assert.That(instance.localScale.x, Is.GreaterThan(1f));
                Assert.That(instance.localScale.z, Is.LessThan(1f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_ReleasesAfterTail()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0.25f);
            try
            {
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, CreateCommand(durationSeconds: 1f));

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));

                fixture.TimeProvider.TimeSeconds = 1.25f;
                fixture.Pool.Advance(0.25f);
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
        public void ParameterizedMotion_HardCleanup_ClearsActiveInstances()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0.25f);
            try
            {
                var handle = fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, CreateCommand());
                var instance = fixture.Root.OneShotRoot.GetChild(0).gameObject;

                fixture.Pool.HardCleanupAll();

                Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.HardCleanup));
                Assert.That(instance == null, Is.True);
                Assert.That(fixture.Pool.ActiveCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_DoesNotBreakOneShotPool()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f);
            try
            {
                var handle = fixture.Pool.PlayTransient(fixture.PlaybackCommand);

                Assert.That(handle, Is.Not.Null);
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.Root.OneShotRoot.GetChild(0).localPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void ParameterizedMotion_DoesNotBreakPersistentPool()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f, persistent: true);
            try
            {
                var handle = fixture.Pool.StartPersistent(fixture.PlaybackCommand);

                fixture.TimeProvider.TimeSeconds = 10f;
                fixture.Pool.Advance(10f);

                Assert.That(handle, Is.Not.Null);
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        internal static ParameterizedMotionVfxCommand CreateCommand(
            float durationSeconds = 1f,
            float arcHeight = 0.6f,
            float breakStartSeconds = 0.7f,
            float fadeDurationSeconds = 0.3f,
            ParameterizedMotionVfxCloneMode cloneMode = ParameterizedMotionVfxCloneMode.PrefabOnly,
            ParameterizedMotionVfxSamplerMode samplerMode = ParameterizedMotionVfxSamplerMode.FlipArc)
        {
            return new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion),
                sourceEntityId: 30,
                sequenceId: 7,
                presentationSeed: 7,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                targetLocalPosition: new Vector3(2f, 0f, 0f),
                targetLocalRotation: Quaternion.AngleAxis(180f, Vector3.up),
                durationSeconds,
                arcHeight,
                breakStartSeconds,
                fadeDurationSeconds,
                ParameterizedMotionVfxFadeMode.ScaleAndAlpha,
                cloneMode,
                samplerMode);
        }

        internal static PoolFixture CreatePoolFixture(
            float tailSeconds,
            bool persistent = false,
            IGameplayVfxCloneSourceProvider cloneSourceProvider = null)
        {
            var owner = new GameObject("ParameterizedMotionPoolOwner");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = CreateRuntimePrefab("ParameterizedMotionPoolPrefab");
            var prefabProvider = new SinglePrefabProvider(prefab);
            var timeProvider = new FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(root, prefabProvider, timeProvider, cloneSourceProvider);
            var cueId = GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion);
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
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent: persistent,
                persistentKey: persistent
                    ? new VfxPersistentKey(cueId, VfxAnchorKind.Entity, entityId: 30)
                    : VfxPersistentKey.None);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.DiagnosticIfMissing,
                VfxMissingAnchorPolicy.ReportDiagnostic,
                persistent ? VfxPlaybackMode.Loop : VfxPlaybackMode.OneShot,
                persistent ? VfxStopPolicy.ManualStopRequired : VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0f,
                tailSeconds: tailSeconds,
                maxConcurrentInstances: 8);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter,
                Vector3.zero,
                Quaternion.identity);
            return new PoolFixture(owner, prefab, root, pool, timeProvider, new ResolvedVfxPlaybackCommand(request, policy, anchor));
        }

        internal static GameObject CreateRuntimePrefab(string name)
        {
            var prefab = new GameObject(name);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(prefab.transform, worldPositionStays: false);
            visual.transform.localScale = Vector3.one * 0.35f;
            return prefab;
        }

        internal static void Destroy(params Object[] unityObjects)
        {
            for (var i = 0; i < unityObjects.Length; i++)
            {
                if (unityObjects[i] != null)
                {
                    Object.DestroyImmediate(unityObjects[i]);
                }
            }
        }

        internal readonly struct PoolFixture
        {
            public PoolFixture(
                GameObject owner,
                GameObject prefab,
                GameplayVfxRuntimeRoot root,
                GameplayVfxGameObjectPool pool,
                FakeTimeProvider timeProvider,
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

            public FakeTimeProvider TimeProvider { get; }

            public ResolvedVfxPlaybackCommand PlaybackCommand { get; }

            public void Destroy()
            {
                Pool?.HardCleanupAll();
                GameplayVfxParameterizedMotionRuntimeTests.Destroy(Prefab, Owner);
            }
        }

        internal sealed class SinglePrefabProvider : IVfxPrefabProvider
        {
            private readonly GameObject prefab;

            public SinglePrefabProvider(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject resolvedPrefab)
            {
                resolvedPrefab = prefab;
                return resolvedPrefab != null;
            }
        }

        internal sealed class FakeTimeProvider : IGameplayVfxTimeProvider
        {
            public float TimeSeconds { get; set; }
        }
    }
}
