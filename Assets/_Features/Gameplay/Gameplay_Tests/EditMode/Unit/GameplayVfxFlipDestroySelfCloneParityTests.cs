using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxSourceCloneRuntimeTests
    {
        private const string InactiveBlendProperty = "_InactiveBlend";
        private const string InactiveNoiseRevealProperty = "_InactiveNoiseReveal";
        private const string DesaturateStrengthProperty = "_DesaturateStrength";
        private const string EmissionSuppressionProperty = "_EmissionSuppression";
        private const string InactiveTintProperty = "_InactiveTint";

        [Test]
        [Category("Extended")]
        public void CloneSourceProvider_ResolvesSourceModelRoot()
        {
            var stateStore = new GameplayPresentationStateStore();
            var viewObject = new GameObject("SourceView");
            try
            {
                stateStore.ResetSession(new CubeTopologyState(FaceId.Floor));
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(30);
                var modelRoot = view.EnsureModelRoot();
                var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(child.GetComponent<Collider>());
                child.transform.SetParent(modelRoot, worldPositionStays: false);
                stateStore.ViewsByEntityId[30] = view;
                var provider = new GameplayVfxStateStoreCloneSourceProvider(stateStore);

                var resolved = provider.TryResolveCloneSource(new GameplayVfxCloneSourceKey(30, 1), out var source);

                Assert.That(resolved, Is.True);
                Assert.That(source.ModelRoot, Is.SameAs(modelRoot));
                Assert.That(source.LocalScale, Is.EqualTo(modelRoot.localScale));
            }
            finally
            {
                Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void CloneSourceProvider_SequenceOverrideWinsOnlyForMatchingToken()
        {
            var stateStore = new GameplayPresentationStateStore();
            var viewObject = new GameObject("LiveSourceView");
            var overrideObject = new GameObject("MoonBlockGhostSource");
            try
            {
                stateStore.ResetSession(new CubeTopologyState(FaceId.Floor));
                var view = viewObject.AddComponent<GameplayEntityView>();
                view.Initialize(30);
                var liveModelRoot = view.EnsureModelRoot();
                var liveChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(liveChild.GetComponent<Collider>());
                liveChild.transform.SetParent(liveModelRoot, worldPositionStays: false);

                var overrideChild = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(overrideChild.GetComponent<Collider>());
                overrideChild.transform.SetParent(overrideObject.transform, worldPositionStays: false);
                stateStore.ViewsByEntityId[30] = view;
                stateStore.VfxCloneSourceOverridesByKey[new GameplayVfxCloneSourceKey(30, 9002)] = overrideObject.transform;
                var provider = new GameplayVfxStateStoreCloneSourceProvider(stateStore);

                Assert.That(
                    provider.TryResolveCloneSource(new GameplayVfxCloneSourceKey(30, 9002), out var overrideSource),
                    Is.True);
                Assert.That(overrideSource.ModelRoot, Is.SameAs(overrideObject.transform));

                Assert.That(
                    provider.TryResolveCloneSource(new GameplayVfxCloneSourceKey(30, 9003), out var liveSource),
                    Is.True);
                Assert.That(liveSource.ModelRoot, Is.SameAs(liveModelRoot));
            }
            finally
            {
                Object.DestroyImmediate(overrideObject);
                Object.DestroyImmediate(viewObject);
            }
        }

        [Test]
        [Category("Extended")]
        public void PrefabWithSourceClone_UsesFallbackPrefab_WhenSourceViewIsUnavailable()
        {
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: MissingCloneSourceProvider.Instance,
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                var instance = fixture.Root.OneShotRoot.GetChild(0);

                Assert.That(instance.Find("ParameterizedMotionCloneRoot"), Is.Null);
                Assert.That(instance.GetComponentInChildren<Renderer>(includeInactive: true).enabled, Is.True);
                Assert.That(fixture.Pool.MissingSourceViewCount, Is.EqualTo(1));
                Assert.That(fixture.Pool.MissingPrefabCount, Is.Zero);
            }
            finally
            {
                fixture.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_CopiesFrontFaceInactiveVisualState()
        {
            var expectedTint = new Color(0.18f, 0.27f, 0.39f, 1f);
            var source = CreateInactiveCompatibleCloneSource("SourceViewInactiveState");
            var fixture = CreateEnemyDeathMotionFixture(source.ModelRoot);
            try
            {
                ApplyInactivePropertyBlock(
                    source.Renderer,
                    inactiveBlend: 1f,
                    inactiveNoiseReveal: 1f,
                    desaturateStrength: 0.37f,
                    emissionSuppression: 0.68f,
                    expectedTint);

                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, CreateEnemyDeathMotionCommand());

                var cloneMaterial = ResolveCloneRenderer(fixture).sharedMaterial;
                Assert.That(cloneMaterial, Is.Not.SameAs(source.SharedMaterial));
                Assert.That(cloneMaterial.GetFloat(InactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(InactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(DesaturateStrengthProperty), Is.EqualTo(0.37f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(EmissionSuppressionProperty), Is.EqualTo(0.68f).Within(0.0001f));
                AssertColorApproximately(expectedTint, cloneMaterial.GetColor(InactiveTintProperty));
                Assert.That(source.SharedMaterial.GetFloat(InactiveBlendProperty), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_InactiveSnapshot_DoesNotBreakDeathAlphaFade()
        {
            var expectedTint = new Color(0.42f, 0.31f, 0.2f, 1f);
            var source = CreateInactiveCompatibleCloneSource("SourceViewInactiveAlphaFade");
            var fixture = CreateEnemyDeathMotionFixture(source.ModelRoot);
            try
            {
                var sourceAlpha = GetMaterialAlpha(source.SharedMaterial);
                ApplyInactivePropertyBlock(
                    source.Renderer,
                    inactiveBlend: 1f,
                    inactiveNoiseReveal: 1f,
                    desaturateStrength: 0.44f,
                    emissionSuppression: 0.55f,
                    expectedTint);

                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, CreateEnemyDeathMotionCommand());
                fixture.TimeProvider.TimeSeconds = 0.9f;
                fixture.Pool.Advance(0.9f);

                var cloneMaterial = ResolveCloneRenderer(fixture).sharedMaterial;
                Assert.That(GetMaterialAlpha(cloneMaterial), Is.LessThan(1f));
                Assert.That(cloneMaterial.GetFloat(InactiveBlendProperty), Is.EqualTo(1f).Within(0.0001f));
                Assert.That(cloneMaterial.GetFloat(InactiveNoiseRevealProperty), Is.EqualTo(1f).Within(0.0001f));
                AssertColorApproximately(expectedTint, cloneMaterial.GetColor(InactiveTintProperty));
                Assert.That(GetMaterialAlpha(source.SharedMaterial), Is.EqualTo(sourceAlpha).Within(0.0001f));
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_OriginalViewImmutability_DoesNotMoveSourceView()
        {
            var source = CreateCloneSource("SourceViewNoMove");
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                var originalPosition = source.ModelRoot.localPosition;
                var originalRotation = source.ModelRoot.localRotation;
                var originalScale = source.ModelRoot.localScale;

                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                fixture.TimeProvider.TimeSeconds = 0.8f;
                fixture.Pool.Advance(0.8f);

                Assert.That(source.ModelRoot.localPosition, Is.EqualTo(originalPosition));
                Assert.That(source.ModelRoot.localRotation, Is.EqualTo(originalRotation));
                Assert.That(source.ModelRoot.localScale, Is.EqualTo(originalScale));
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_OriginalViewImmutability_DoesNotModifySharedMaterial()
        {
            var source = CreateCloneSource("SourceViewSharedMaterial");
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                var originalColor = source.SharedMaterial.color;

                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                fixture.TimeProvider.TimeSeconds = 0.9f;
                fixture.Pool.Advance(0.9f);

                Assert.That(source.SharedMaterial.color, Is.EqualTo(originalColor));
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_AlphaFade_UsesInstancedMaterials()
        {
            var source = CreateCloneSource("SourceViewAlphaFade");
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                fixture.TimeProvider.TimeSeconds = 0.9f;
                fixture.Pool.Advance(0.9f);

                var cloneRenderer = ResolveCloneRenderer(fixture);
                Assert.That(cloneRenderer.sharedMaterial, Is.Not.SameAs(source.SharedMaterial));
                Assert.That(cloneRenderer.sharedMaterial.color.a, Is.LessThan(1f));
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_AlphaFade_MissingMaterialDoesNotCrash()
        {
            var source = CreateCloneSource("SourceViewNullMaterial", assignMaterial: false);
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));

                Assert.DoesNotThrow(() =>
                {
                    fixture.TimeProvider.TimeSeconds = 0.9f;
                    fixture.Pool.Advance(0.9f);
                });
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_HardCleanup_DestroysMaterialInstances()
        {
            var source = CreateCloneSource("SourceViewHardCleanup");
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                fixture.Pool.PlayParameterizedMotion(
                    fixture.PlaybackCommand,
                    GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                        cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone));
                var clone = ResolveClone(fixture).gameObject;
                var instancedMaterial = ResolveCloneRenderer(fixture).sharedMaterial;

                fixture.Pool.HardCleanupAll();

                Assert.That(clone == null, Is.True);
                Assert.That(instancedMaterial == null, Is.True);
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        [Test]
        [Category("Extended")]
        public void SourceClone_RepeatedPlayback_DoesNotReuseDestroyedClone()
        {
            var source = CreateCloneSource("SourceViewRepeated");
            var fixture = GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(source.ModelRoot),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
            try
            {
                var command = GameplayVfxParameterizedMotionRuntimeTests.CreateCommand(
                    cloneMode: ParameterizedMotionVfxCloneMode.PrefabWithSourceClone);
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var firstClone = ResolveClone(fixture).gameObject;

                fixture.TimeProvider.TimeSeconds = 1f;
                fixture.Pool.Advance(1f);
                Assert.That(firstClone == null, Is.True);

                fixture.TimeProvider.TimeSeconds = 2f;
                fixture.Pool.PlayParameterizedMotion(fixture.PlaybackCommand, command);
                var secondClone = ResolveClone(fixture).gameObject;

                Assert.That(secondClone, Is.Not.Null);
                Assert.That(secondClone == firstClone, Is.False);
            }
            finally
            {
                fixture.Destroy();
                source.Destroy();
            }
        }

        private static Transform ResolveClone(GameplayVfxParameterizedMotionRuntimeTests.PoolFixture fixture)
        {
            var instance = fixture.Root.OneShotRoot.childCount > 0
                ? fixture.Root.OneShotRoot.GetChild(0)
                : fixture.Root.PoolRoot.GetChild(0);
            var clone = instance.Find("ParameterizedMotionCloneRoot");
            Assert.That(clone, Is.Not.Null);
            return clone;
        }

        private static Renderer ResolveCloneRenderer(GameplayVfxParameterizedMotionRuntimeTests.PoolFixture fixture)
        {
            return ResolveClone(fixture).GetComponentInChildren<Renderer>(includeInactive: true);
        }

        private static GameplayVfxParameterizedMotionRuntimeTests.PoolFixture CreateEnemyDeathMotionFixture(Transform sourceRoot)
        {
            return GameplayVfxParameterizedMotionRuntimeTests.CreatePoolFixture(
                tailSeconds: 0f,
                cloneSourceProvider: new SingleCloneSourceProvider(sourceRoot),
                cueId: GameplayVfxCueId.From(EnemyVfxCue.DeathMotion),
                visualSourceMode: VfxVisualSourceMode.PrefabWithSourceClone);
        }

        private static CloneSourceFixture CreateCloneSource(string name, bool assignMaterial = true)
        {
            var viewObject = new GameObject(name);
            var view = viewObject.AddComponent<GameplayEntityView>();
            view.Initialize(30);
            var modelRoot = view.EnsureModelRoot();
            modelRoot.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            modelRoot.localRotation = Quaternion.AngleAxis(15f, Vector3.up);
            modelRoot.localScale = new Vector3(1.2f, 0.9f, 1.1f);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(modelRoot, worldPositionStays: false);
            var renderer = visual.GetComponent<Renderer>();
            Material sharedMaterial = null;
            if (assignMaterial)
            {
                sharedMaterial = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit"));
                sharedMaterial.color = Color.white;
                renderer.sharedMaterial = sharedMaterial;
            }
            else
            {
                renderer.sharedMaterial = null;
            }

            return new CloneSourceFixture(viewObject, modelRoot, sharedMaterial);
        }

        private static CloneSourceFixture CreateInactiveCompatibleCloneSource(string name)
        {
            var source = CreateCloneSource(name, assignMaterial: false);
            var shader = Shader.Find("Game/Enemy/CustomEnemyLit");
            Assert.That(shader, Is.Not.Null, "Game/Enemy/CustomEnemyLit shader is required for inactive visual snapshot tests.");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = Color.white;
            }

            source.Renderer.sharedMaterial = material;
            return new CloneSourceFixture(source.Owner, source.ModelRoot, material, source.Renderer);
        }

        private static ParameterizedMotionVfxCommand CreateEnemyDeathMotionCommand()
        {
            return new ParameterizedMotionVfxCommand(
                GameplayVfxCueId.From(EnemyVfxCue.DeathMotion),
                sourceEntityId: 30,
                sequenceId: 7,
                presentationSeed: 7,
                sourceLocalPosition: Vector3.zero,
                sourceLocalRotation: Quaternion.identity,
                targetLocalPosition: new Vector3(0f, 0f, -2f),
                targetLocalRotation: Quaternion.identity,
                durationSeconds: 1f,
                arcHeight: 0.2f,
                breakStartSeconds: 0.12f,
                fadeDurationSeconds: 0.88f,
                ParameterizedMotionVfxFadeMode.EnemyDeathFade,
                ParameterizedMotionVfxCloneMode.PrefabWithSourceClone,
                ParameterizedMotionVfxSamplerMode.EnemyDeathFlyAway,
                arcLocalDirection: Vector3.up,
                spinDegrees: 360f,
                spinAxisLocal: Vector3.forward);
        }

        private static void ApplyInactivePropertyBlock(
            Renderer renderer,
            float inactiveBlend,
            float inactiveNoiseReveal,
            float desaturateStrength,
            float emissionSuppression,
            Color inactiveTint)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetFloat(InactiveBlendProperty, inactiveBlend);
            block.SetFloat(InactiveNoiseRevealProperty, inactiveNoiseReveal);
            block.SetFloat(DesaturateStrengthProperty, desaturateStrength);
            block.SetFloat(EmissionSuppressionProperty, emissionSuppression);
            block.SetColor(InactiveTintProperty, inactiveTint);
            renderer.SetPropertyBlock(block);
        }

        private static float GetMaterialAlpha(Material material)
        {
            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor").a;
            }

            return material.HasProperty("_Color")
                ? material.color.a
                : 1f;
        }

        private static void AssertColorApproximately(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        private readonly struct CloneSourceFixture
        {
            public CloneSourceFixture(GameObject owner, Transform modelRoot, Material sharedMaterial, Renderer renderer = null)
            {
                Owner = owner;
                ModelRoot = modelRoot;
                SharedMaterial = sharedMaterial;
                Renderer = renderer != null ? renderer : modelRoot.GetComponentInChildren<Renderer>(includeInactive: true);
            }

            public GameObject Owner { get; }

            public Transform ModelRoot { get; }

            public Material SharedMaterial { get; }

            public Renderer Renderer { get; }

            public void Destroy()
            {
                GameplayVfxParameterizedMotionRuntimeTests.Destroy(SharedMaterial, Owner);
            }
        }

        private sealed class SingleCloneSourceProvider : IGameplayVfxCloneSourceProvider
        {
            private readonly Transform modelRoot;

            public SingleCloneSourceProvider(Transform modelRoot)
            {
                this.modelRoot = modelRoot;
            }

            public bool TryResolveCloneSource(GameplayVfxCloneSourceKey key, out GameplayVfxCloneSource source)
            {
                source = new GameplayVfxCloneSource(modelRoot);
                return true;
            }
        }

        private sealed class MissingCloneSourceProvider : IGameplayVfxCloneSourceProvider
        {
            public static readonly MissingCloneSourceProvider Instance = new();

            public bool TryResolveCloneSource(GameplayVfxCloneSourceKey key, out GameplayVfxCloneSource source)
            {
                source = default;
                return false;
            }
        }
    }
}
