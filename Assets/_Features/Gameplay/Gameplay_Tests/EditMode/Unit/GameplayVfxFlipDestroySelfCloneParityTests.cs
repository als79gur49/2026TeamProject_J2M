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

                var resolved = provider.TryResolveCloneSource(30, out var source);

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
            }
            finally
            {
                fixture.Destroy();
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

        private readonly struct CloneSourceFixture
        {
            public CloneSourceFixture(GameObject owner, Transform modelRoot, Material sharedMaterial)
            {
                Owner = owner;
                ModelRoot = modelRoot;
                SharedMaterial = sharedMaterial;
            }

            public GameObject Owner { get; }

            public Transform ModelRoot { get; }

            public Material SharedMaterial { get; }

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

            public bool TryResolveCloneSource(int sourceEntityId, out GameplayVfxCloneSource source)
            {
                source = new GameplayVfxCloneSource(modelRoot);
                return true;
            }
        }

        private sealed class MissingCloneSourceProvider : IGameplayVfxCloneSourceProvider
        {
            public static readonly MissingCloneSourceProvider Instance = new();

            public bool TryResolveCloneSource(int sourceEntityId, out GameplayVfxCloneSource source)
            {
                source = default;
                return false;
            }
        }
    }
}
