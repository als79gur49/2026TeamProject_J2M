using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Host
{
    public sealed class GameplayTransientEffectPresenter
    {
        private readonly List<IGameplayTransientEffectTrack> _activeTracks = new();
        private readonly GameplayPresentationEffectFactory _effectFactory = new();

        public int ActiveEffectCount => _activeTracks.Count;

        public bool HasActiveEffects => _activeTracks.Count > 0;

        public void Initialize(Transform parent, float cellSize)
        {
            Clear();
            _effectFactory.Initialize(parent, cellSize);
        }

        public void Clear()
        {
            for (var i = _activeTracks.Count - 1; i >= 0; i--)
            {
                _activeTracks[i].Dispose();
            }

            _activeTracks.Clear();
        }

        public void PlayExitEffect(
            TickEntityExitPresentationSignal signal,
            GameplayEntityView sourceView,
            GameplayEntityPose localPose,
            float durationSeconds)
        {
            var track = _effectFactory.CreateExitEffect(signal, sourceView, localPose, durationSeconds);
            if (track != null)
            {
                _activeTracks.Add(track);
            }
        }

        public void Update(float deltaTime)
        {
            for (var i = _activeTracks.Count - 1; i >= 0; i--)
            {
                var track = _activeTracks[i];
                track.Advance(deltaTime);
                if (!track.IsComplete)
                {
                    continue;
                }

                track.Dispose();
                _activeTracks.RemoveAt(i);
            }
        }
    }

    internal interface IGameplayTransientEffectTrack : IDisposable
    {
        bool IsComplete { get; }

        void Advance(float deltaTime);
    }

    // Exit effects are transient-only echoes. They never extend authoritative view
    // lifetime after gameplay has already committed the entity exit.
    public sealed class EntityExitEffectTrack : IGameplayTransientEffectTrack
    {
        private readonly TickEntityExitCause _exitCause;
        private readonly float _durationSeconds;
        private readonly Material[][] _instancedMaterials;
        private readonly Renderer[] _renderers;
        private readonly Transform _root;
        private readonly Vector3 _rootStartLocalPosition;
        private readonly Vector3 _rootStartScale;
        private float _elapsedSeconds;

        public EntityExitEffectTrack(
            TickEntityExitCause exitCause,
            Transform root,
            Renderer[] renderers,
            Material[][] instancedMaterials,
            float durationSeconds)
        {
            _exitCause = exitCause;
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _renderers = renderers ?? Array.Empty<Renderer>();
            _instancedMaterials = instancedMaterials ?? Array.Empty<Material[]>();
            _durationSeconds = Mathf.Max(0.0001f, durationSeconds);
            _rootStartLocalPosition = root.localPosition;
            _rootStartScale = root.localScale;
            ApplyVisualState(normalizedTime: 0f);
        }

        public bool IsComplete => _elapsedSeconds >= _durationSeconds - 0.0001f;

        public void Advance(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                _elapsedSeconds = Mathf.Min(_durationSeconds, _elapsedSeconds + deltaTime);
            }

            ApplyVisualState(Mathf.Clamp01(_elapsedSeconds / _durationSeconds));
        }

        public void Dispose()
        {
            for (var rendererIndex = 0; rendererIndex < _instancedMaterials.Length; rendererIndex++)
            {
                var materials = _instancedMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    SafeDestroy(materials[materialIndex]);
                }
            }

            SafeDestroy(_root != null ? _root.gameObject : null);
        }

        private void ApplyVisualState(float normalizedTime)
        {
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 2f);
            var alpha = Mathf.Lerp(1f, 0f, easedTime);
            var scale = ResolveScale(easedTime);
            _root.localScale = Vector3.Scale(_rootStartScale, scale);
            _root.localPosition = ResolveLocalPositionOffset(easedTime);

            for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
            {
                var materials = _instancedMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    SetMaterialAlpha(materials[materialIndex], alpha);
                }
            }
        }

        private Vector3 ResolveLocalPositionOffset(float easedTime)
        {
            return _exitCause switch
            {
                TickEntityExitCause.BoxDestroy => _rootStartLocalPosition + (_root.localRotation * Vector3.back * Mathf.Lerp(0f, 0.08f, easedTime)),
                _ => _rootStartLocalPosition,
            };
        }

        private Vector3 ResolveScale(float easedTime)
        {
            return _exitCause switch
            {
                TickEntityExitCause.BoxDestroy => new Vector3(
                    Mathf.Lerp(1f, 1.18f, easedTime),
                    Mathf.Lerp(1f, 1.18f, easedTime),
                    Mathf.Lerp(1f, 0.22f, easedTime)),
                _ => Vector3.one * Mathf.Lerp(1f, 0.55f, easedTime),
            };
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 0f);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void SafeDestroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }

        private static void SetMaterialAlpha(Material material, float alpha)
        {
            if (material == null)
            {
                return;
            }

            ConfigureTransparentMaterial(material);

            if (material.HasProperty("_BaseColor"))
            {
                var baseColor = material.GetColor("_BaseColor");
                baseColor.a = alpha;
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_Color"))
            {
                var color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }
    }

    public sealed class GameplayPresentationEffectFactory
    {
        private readonly Dictionary<EntityType, Material> _fallbackMaterialsByEntityType = new();
        private float _cellSize = 1f;
        private Transform _parent;
        private Shader _shader;

        public void Initialize(Transform parent, float cellSize)
        {
            _parent = parent;
            _cellSize = Mathf.Max(0.0001f, cellSize);
            _shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        }

        public EntityExitEffectTrack CreateExitEffect(
            TickEntityExitPresentationSignal signal,
            GameplayEntityView sourceView,
            GameplayEntityPose localPose,
            float durationSeconds)
        {
            if (_parent == null)
            {
                return null;
            }

            var effectRoot = new GameObject($"TransientEntityExitEffect_{signal.ExitedEntityId}_{signal.ExitCause}");
            effectRoot.transform.SetParent(_parent, worldPositionStays: false);
            effectRoot.transform.localPosition = localPose.Position;
            effectRoot.transform.localRotation = localPose.Rotation;
            effectRoot.transform.localScale = Vector3.one;

            var visualRoot = CreateVisualRoot(effectRoot.transform, sourceView, signal.EntityType);
            visualRoot.gameObject.SetActive(true);

            var colliders = visualRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(colliders[i]);
                }
                else
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }

            var renderers = visualRoot.GetComponentsInChildren<Renderer>(includeInactive: true);
            return new EntityExitEffectTrack(
                signal.ExitCause,
                effectRoot.transform,
                renderers,
                CreateInstancedMaterials(renderers),
                durationSeconds);
        }

        private Transform CreateFallbackVisualRoot(Transform effectRoot, EntityType entityType)
        {
            var visualProfile = GameplayEntityVisualProfile.Create(entityType, _cellSize);
            var modelRoot = new GameObject("TransientModelRoot").transform;
            modelRoot.SetParent(effectRoot, worldPositionStays: false);
            modelRoot.localPosition = visualProfile.ModelLocalPosition;
            modelRoot.localRotation = visualProfile.ModelLocalRotation;
            modelRoot.localScale = Vector3.one;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(modelRoot, worldPositionStays: false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = visualProfile.ModelLocalScale;

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ResolveFallbackMaterial(entityType);
            }

            return modelRoot;
        }

        private Material[][] CreateInstancedMaterials(Renderer[] renderers)
        {
            var instancedMaterials = new Material[renderers.Length][];

            for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                var renderer = renderers[rendererIndex];
                if (renderer == null)
                {
                    instancedMaterials[rendererIndex] = Array.Empty<Material>();
                    continue;
                }

                var sharedMaterials = renderer.sharedMaterials;
                var clonedMaterials = new Material[sharedMaterials.Length];
                for (var materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
                {
                    var sharedMaterial = sharedMaterials[materialIndex];
                    if (sharedMaterial != null)
                    {
                        clonedMaterials[materialIndex] = new Material(sharedMaterial);
                    }
                }

                renderer.sharedMaterials = clonedMaterials;
                instancedMaterials[rendererIndex] = clonedMaterials;
            }

            return instancedMaterials;
        }

        private Transform CreateVisualRoot(
            Transform effectRoot,
            GameplayEntityView sourceView,
            EntityType entityType)
        {
            if (sourceView != null &&
                sourceView.ModelRoot != null &&
                sourceView.ModelRoot.childCount > 0)
            {
                var clonedModelRoot = Object.Instantiate(sourceView.ModelRoot.gameObject, effectRoot);
                clonedModelRoot.name = "TransientModelRoot";
                clonedModelRoot.transform.localPosition = sourceView.ModelRoot.localPosition;
                clonedModelRoot.transform.localRotation = sourceView.ModelRoot.localRotation;
                clonedModelRoot.transform.localScale = sourceView.ModelRoot.localScale;
                return clonedModelRoot.transform;
            }

            return CreateFallbackVisualRoot(effectRoot, entityType);
        }

        private Material ResolveFallbackMaterial(EntityType entityType)
        {
            if (_fallbackMaterialsByEntityType.TryGetValue(entityType, out var material) &&
                material != null)
            {
                return material;
            }

            if (_shader == null)
            {
                return null;
            }

            var createdMaterial = new Material(_shader)
            {
                color = entityType switch
                {
                    EntityType.Box => new Color(0.72f, 0.5f, 0.24f, 1f),
                    EntityType.Projectile => new Color(0.9f, 0.4f, 0.2f, 1f),
                    EntityType.None => new Color(0.25f, 0.28f, 0.33f, 1f),
                    _ => new Color(0.75f, 0.75f, 0.82f, 1f),
                },
            };
            _fallbackMaterialsByEntityType[entityType] = createdMaterial;
            return createdMaterial;
        }
    }
}
