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
        public void LegacyEnemyDeathSampler_UsesEaseOutArcAndSpin()
        {
            var command = new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(EnemyVfxCue.DeathMotion),
                sourceEntityId: 40,
                sequenceId: 9127,
                presentationSeed: 9127,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                targetLocalPosition: new Vector3(0f, 0f, -2f),
                targetLocalRotation: Quaternion.identity,
                durationSeconds: 1f,
                arcHeight: 0.2f,
                breakStartSeconds: 0.12f,
                fadeDurationSeconds: 0.88f,
                ParameterizedMotionVfxFadeMode.LegacyEnemyDeath,
                ParameterizedMotionVfxCloneMode.SourceViewCloneWithPrefabFallback,
                ParameterizedMotionVfxSamplerMode.LegacyEnemyDeathFlyAway,
                arcLocalDirection: Vector3.up,
                spinDegrees: 360f,
                spinAxisLocal: Vector3.forward);

            var sample = ParameterizedMotionVfxSampler.Sample(command, 0.5f);
            var expectedEasedTime = 1f - Mathf.Pow(0.5f, 3f);
            var expectedPosition = Vector3.LerpUnclamped(
                command.SourceLocalPosition,
                command.TargetLocalPosition,
                expectedEasedTime) + Vector3.up * command.ArcHeight;

            Assert.That(Vector3.Distance(sample.LocalPosition, expectedPosition), Is.LessThanOrEqualTo(0.0001f));
            Assert.That(Quaternion.Angle(sample.LocalRotation, Quaternion.AngleAxis(360f * expectedEasedTime, Vector3.forward)), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void LegacyEnemyDeathFade_StartsAtTwelvePercent()
        {
            var command = new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(EnemyVfxCue.DeathMotion),
                sourceEntityId: 40,
                sequenceId: 9127,
                presentationSeed: 9127,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                targetLocalPosition: Vector3.forward,
                targetLocalRotation: Quaternion.identity,
                durationSeconds: 1f,
                arcHeight: 0.2f,
                breakStartSeconds: 0.12f,
                fadeDurationSeconds: 0.88f,
                ParameterizedMotionVfxFadeMode.LegacyEnemyDeath,
                ParameterizedMotionVfxCloneMode.PrefabOnly,
                ParameterizedMotionVfxSamplerMode.LegacyEnemyDeathFlyAway);

            Assert.That(ParameterizedMotionVfxSampler.Sample(command, 0.11f).FadeProgress, Is.Zero);
            Assert.That(ParameterizedMotionVfxSampler.Sample(command, 1f).FadeProgress, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        [Category("Extended")]
        public void DestroyShrinkEase_PopsThenShrinksAndIsNotLinear()
        {
            var fixture = CreatePoolFixture(
                tailSeconds: 0f,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink));
            try
            {
                var command = CreateDestroyShrinkCommand();
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                fixture.TimeProvider.TimeSeconds = 0.08f;
                fixture.Pool.Advance(0.08f);
                Assert.That(instance.localScale.x, Is.GreaterThan(1f));

                fixture.TimeProvider.TimeSeconds = 0.5f;
                fixture.Pool.Advance(0.42f);
                Assert.That(instance.localScale.x, Is.LessThan(0.95f));
                Assert.That(instance.localScale.y, Is.EqualTo(instance.localScale.x).Within(0.0001f));
                Assert.That(instance.localScale.z, Is.EqualTo(instance.localScale.x).Within(0.0001f));
                Assert.That(instance.localScale.x, Is.Not.EqualTo(0.53f).Within(0.04f));
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void DestroyShrinkEase_AlphaFadesThroughMaterialInstance()
        {
            var fixture = CreatePoolFixture(
                tailSeconds: 0f,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink));
            try
            {
                var command = CreateDestroyShrinkCommand();
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var renderer = fixture.Root.OneShotRoot.GetChild(0).GetComponentInChildren<Renderer>();
                var originalSharedMaterial = fixture.Prefab.GetComponentInChildren<Renderer>().sharedMaterial;

                fixture.TimeProvider.TimeSeconds = 0.9f;
                fixture.Pool.Advance(0.9f);

                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(originalSharedMaterial));
                Assert.That(renderer.sharedMaterial.color.a, Is.LessThan(0.2f));
                Assert.That(originalSharedMaterial.color.a, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
            }
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

        [Test]
        [Category("Core")]
        public void DestroyShrink_Plays_WithSourceClone_WhenPrefabIsMissing()
        {
            var sourceProvider = CreateCloneSourceProvider(out var sourceRoot);
            var fixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: sourceProvider,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);
            try
            {
                var handle = fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));

                Assert.That(handle, Is.Not.Null);
                Assert.That(fixture.Pool.ActiveCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(fixture.Pool.MissingSourceViewCount, Is.Zero);
                Assert.That(fixture.Root.OneShotRoot.GetChild(0).Find("ParameterizedMotionCloneRoot"), Is.Not.Null);
            }
            finally
            {
                fixture.Destroy();
                Destroy(sourceRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void DestroyShrink_ReportsMissingSourceView_WhenSourceViewIsUnavailable()
        {
            var fixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);
            try
            {
                var handle = fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));

                Assert.That(handle, Is.Null);
                Assert.That(fixture.Pool.MissingSourceViewCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.MissingPrefabCount, Is.Zero);
                Assert.That(fixture.Pool.CommonHostUnavailableCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void PrefabOnlyCue_ReportsMissingPrefab_WhenPrefabIsNull()
        {
            var fixture = CreatePoolFixture(tailSeconds: 0f, useNullPrefab: true);
            try
            {
                var handle = fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, CreateCommand());

                Assert.That(handle, Is.Null);
                Assert.That(fixture.Pool.MissingPrefabCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.MissingSourceViewCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotion_ReleasesHost_AfterLifetimeAndTail()
        {
            var sourceProvider = CreateCloneSourceProvider(out var sourceRoot);
            var fixture = CreatePoolFixture(
                tailSeconds: 0.25f,
                cloneSourceProvider: sourceProvider,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));

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
                Destroy(sourceRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotion_RespectsMaxConcurrency()
        {
            var sourceProvider = CreateCloneSourceProvider(out var sourceRoot);
            var fixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: sourceProvider,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed,
                maxConcurrentInstances: 1);
            try
            {
                var first = fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));
                var second = fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.Null);
                Assert.That(fixture.Pool.DroppedByLimitCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Destroy();
                Destroy(sourceRoot);
            }
        }

        [Test]
        [Category("Core")]
        public void SourceCloneMotion_DoesNotMutateOriginalView()
        {
            var sourceProvider = CreateCloneSourceProvider(out var sourceRoot);
            var originalScale = sourceRoot.transform.localScale;
            var originalPosition = sourceRoot.transform.localPosition;
            var fixture = CreatePoolFixture(
                tailSeconds: 0.18f,
                cloneSourceProvider: sourceProvider,
                cueId: GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                visualSourceMode: VfxVisualSourceMode.SourceCloneMotion,
                hostRequirement: GameplayVfxHostRequirement.CommonHostAllowed);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    CreateDestroyShrinkCommand(ParameterizedMotionVfxCloneMode.SourceCloneMotion));

                fixture.TimeProvider.TimeSeconds = 0.9f;
                fixture.Pool.Advance(0.9f);

                Assert.That(sourceRoot.transform.localScale, Is.EqualTo(originalScale));
                Assert.That(sourceRoot.transform.localPosition, Is.EqualTo(originalPosition));
            }
            finally
            {
                fixture.Destroy();
                Destroy(sourceRoot);
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

        private static ParameterizedMotionVfxCommand CreateDestroyShrinkCommand(
            ParameterizedMotionVfxCloneMode cloneMode = ParameterizedMotionVfxCloneMode.PrefabOnly)
        {
            return new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(BoxVfxCue.DestroyShrink),
                sourceEntityId: 30,
                sequenceId: 7,
                presentationSeed: 7,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                targetLocalPosition: Vector3.zero,
                targetLocalRotation: Quaternion.identity,
                durationSeconds: 1f,
                arcHeight: 0f,
                breakStartSeconds: 0f,
                fadeDurationSeconds: 1f,
                ParameterizedMotionVfxFadeMode.DestroyShrinkEase,
                cloneMode,
                ParameterizedMotionVfxSamplerMode.Linear);
        }

        internal static PoolFixture CreatePoolFixture(
            float tailSeconds,
            bool persistent = false,
            IGameplayVfxCloneSourceProvider cloneSourceProvider = null,
            GameplayVfxCueId cueId = default,
            VfxVisualSourceMode visualSourceMode = VfxVisualSourceMode.PrefabOnly,
            GameplayVfxHostRequirement hostRequirement = GameplayVfxHostRequirement.ExplicitPrefabRequired,
            bool useNullPrefab = false,
            int maxConcurrentInstances = 8)
        {
            var owner = new GameObject("ParameterizedMotionPoolOwner");
            var root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            var prefab = useNullPrefab ? null : CreateRuntimePrefab("ParameterizedMotionPoolPrefab");
            var prefabProvider = new SinglePrefabProvider(prefab);
            var timeProvider = new FakeTimeProvider();
            var pool = new GameplayVfxGameObjectPool(root, prefabProvider, timeProvider, cloneSourceProvider);
            cueId = cueId.Equals(default(GameplayVfxCueId))
                ? GameplayVfxCueId.From(BoxVfxCue.FlipDestroySelfMotion)
                : cueId;
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
                maxConcurrentInstances: maxConcurrentInstances,
                visualSourceMode: visualSourceMode,
                hostRequirement: hostRequirement);
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

        private static SingleCloneSourceProvider CreateCloneSourceProvider(out GameObject sourceRoot)
        {
            sourceRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(sourceRoot.GetComponent<Collider>());
            sourceRoot.name = "OriginalSourceView";
            sourceRoot.transform.localPosition = new Vector3(2f, 3f, 4f);
            sourceRoot.transform.localScale = new Vector3(1.5f, 1.25f, 0.75f);
            return new SingleCloneSourceProvider(sourceRoot.transform);
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

        private sealed class SingleCloneSourceProvider : IGameplayVfxCloneSourceProvider
        {
            private readonly Transform modelRoot;

            public SingleCloneSourceProvider(Transform modelRoot)
            {
                this.modelRoot = modelRoot;
            }

            public bool TryResolveCloneSource(int sourceEntityId, out GameplayVfxCloneSource source)
            {
                if (sourceEntityId == 30 && modelRoot != null)
                {
                    source = new GameplayVfxCloneSource(modelRoot);
                    return true;
                }

                source = default;
                return false;
            }
        }

        internal sealed class FakeTimeProvider : IGameplayVfxTimeProvider
        {
            public float TimeSeconds { get; set; }
        }
    }
}
