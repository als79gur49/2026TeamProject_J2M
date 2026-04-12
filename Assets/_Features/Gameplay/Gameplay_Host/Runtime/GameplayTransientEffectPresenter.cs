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
        private Camera _outputCamera;

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

        public void ConfigureOutputCamera(Camera outputCamera)
        {
            _outputCamera = outputCamera;
        }

        public void PlayExitEffect(
            TickEntityExitPresentationSignal signal,
            GameplayEntityView sourceView,
            GameplayEntityPose localPose,
            GameplayEntityPose? targetLocalPose,
            float durationSeconds)
        {
            var track = _effectFactory.CreateExitEffect(
                signal,
                sourceView,
                localPose,
                targetLocalPose,
                durationSeconds,
                _outputCamera);
            if (track != null)
            {
                _activeTracks.Add(track);
            }
        }

        public void PlayHitEffect(
            int entityId,
            GameplayEntityPose localPose,
            EntityEffectPresentationSnapshot effectSnapshot,
            float fallbackDurationSeconds)
        {
            var track = _effectFactory.CreateHitEffect(
                entityId,
                localPose,
                effectSnapshot,
                fallbackDurationSeconds);
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

    internal static class GameplayTransientEffectTrackUtility
    {
        public static void ConfigureTransparentMaterial(Material material)
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

        public static void SafeDestroy(Object target)
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

        public static void SetMaterialAlpha(Material material, float alpha)
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
                    GameplayTransientEffectTrackUtility.SafeDestroy(materials[materialIndex]);
                }
            }

            GameplayTransientEffectTrackUtility.SafeDestroy(_root != null ? _root.gameObject : null);
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
                    GameplayTransientEffectTrackUtility.SetMaterialAlpha(materials[materialIndex], alpha);
                }
            }
        }

        private Vector3 ResolveLocalPositionOffset(float easedTime)
        {
            return _exitCause switch
            {
                TickEntityExitCause.DestroyedByImpact => _rootStartLocalPosition + (_root.localRotation * Vector3.back * Mathf.Lerp(0f, 0.08f, easedTime)),
                _ => _rootStartLocalPosition,
            };
        }

        private Vector3 ResolveScale(float easedTime)
        {
            return _exitCause switch
            {
                TickEntityExitCause.DestroyedByImpact => new Vector3(
                    Mathf.Lerp(1f, 1.18f, easedTime),
                    Mathf.Lerp(1f, 1.18f, easedTime),
                    Mathf.Lerp(1f, 0.22f, easedTime)),
                _ => Vector3.one * Mathf.Lerp(1f, 0.55f, easedTime),
            };
        }

    }

    internal sealed class EnemyDeathExitEffectTrack : IGameplayTransientEffectTrack
    {
        private readonly float _arcHeight;
        private readonly Vector3 _arcLocalDirection;
        private readonly Material[][] _instancedMaterials;
        private readonly Renderer[] _renderers;
        private readonly Transform _root;
        private readonly Vector3 _rootStartLocalPosition;
        private readonly Quaternion _rootStartLocalRotation;
        private readonly Vector3 _rootStartLocalScale;
        private readonly Vector3 _spinAxisLocal;
        private readonly float _spinDegrees;
        private readonly Vector3 _targetLocalPosition;
        private readonly float _durationSeconds;
        private float _elapsedSeconds;

        public EnemyDeathExitEffectTrack(
            Transform root,
            Renderer[] renderers,
            Material[][] instancedMaterials,
            float durationSeconds,
            in EnemyDeathExitEffectPlan plan)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _renderers = renderers ?? Array.Empty<Renderer>();
            _instancedMaterials = instancedMaterials ?? Array.Empty<Material[]>();
            _durationSeconds = Mathf.Max(0.0001f, durationSeconds);
            _rootStartLocalPosition = root.localPosition;
            _rootStartLocalRotation = root.localRotation;
            _rootStartLocalScale = root.localScale;
            _targetLocalPosition = plan.TargetLocalPosition;
            _arcLocalDirection = plan.ArcLocalDirection.sqrMagnitude > 0.000001f
                ? plan.ArcLocalDirection.normalized
                : Vector3.up;
            _arcHeight = Mathf.Max(0f, plan.ArcHeight);
            _spinDegrees = plan.SpinDegrees;
            _spinAxisLocal = plan.SpinAxisLocal.sqrMagnitude > 0.000001f
                ? plan.SpinAxisLocal.normalized
                : Vector3.forward;
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
                    GameplayTransientEffectTrackUtility.SafeDestroy(materials[materialIndex]);
                }
            }

            GameplayTransientEffectTrackUtility.SafeDestroy(_root != null ? _root.gameObject : null);
        }

        private void ApplyVisualState(float normalizedTime)
        {
            var easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            var fadeT = Mathf.Clamp01((normalizedTime - 0.12f) / 0.88f);
            var alpha = 1f - (fadeT * fadeT);
            var arcOffset = _arcLocalDirection * (_arcHeight * Mathf.Sin(normalizedTime * Mathf.PI));
            _root.localPosition = Vector3.LerpUnclamped(_rootStartLocalPosition, _targetLocalPosition, easedTime) + arcOffset;
            _root.localRotation = Quaternion.AngleAxis(_spinDegrees * easedTime, _spinAxisLocal) * _rootStartLocalRotation;
            _root.localScale = _rootStartLocalScale * Mathf.Lerp(1f, 0.88f, normalizedTime);

            for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
            {
                var materials = _instancedMaterials[rendererIndex];
                if (materials == null)
                {
                    continue;
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    GameplayTransientEffectTrackUtility.SetMaterialAlpha(materials[materialIndex], alpha);
                }
            }
        }
    }

    internal readonly struct EnemyDeathExitEffectPlan
    {
        public EnemyDeathExitEffectPlan(
            Vector3 targetLocalPosition,
            Vector3 arcLocalDirection,
            float arcHeight,
            float spinDegrees,
            Vector3 spinAxisLocal)
        {
            TargetLocalPosition = targetLocalPosition;
            ArcLocalDirection = arcLocalDirection;
            ArcHeight = arcHeight;
            SpinDegrees = spinDegrees;
            SpinAxisLocal = spinAxisLocal;
        }

        public Vector3 TargetLocalPosition { get; }

        public Vector3 ArcLocalDirection { get; }

        public float ArcHeight { get; }

        public float SpinDegrees { get; }

        public Vector3 SpinAxisLocal { get; }
    }

    internal static class EnemyDeathExitEffectPlanBuilder
    {
        private const float MinimumCellSize = 0.0001f;
        private const float CameraNearPlanePaddingInCells = 0.12f;
        private const float CameraPlaneJitterInCells = 0.18f;
        private const float CameraPlaneBiasWeight = 0.2f;
        private const float CameraPlaneBiasMaxInCells = 0.18f;
        private const float FallbackForwardDistanceInCells = 2.6f;
        private const float FallbackForwardDistanceJitterInCells = 0.6f;
        private const float FallbackPlaneJitterInCells = 0.18f;

        public static EnemyDeathExitEffectPlan Build(
            Transform parent,
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            float cellSize,
            int presentationSeed)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var seededValues = new SeededValueSequence(presentationSeed);
            var resolvedCellSize = Mathf.Max(MinimumCellSize, cellSize);
            var planeJitter = new Vector2(
                seededValues.NextRange(-CameraPlaneJitterInCells, CameraPlaneJitterInCells),
                seededValues.NextRange(-CameraPlaneJitterInCells, CameraPlaneJitterInCells)) * resolvedCellSize;
            var arcHeight = seededValues.NextRange(0.1f, 0.2f) * resolvedCellSize;
            var spinDegrees = seededValues.NextSignedRange(240f, 420f);

            if (outputCamera != null &&
                TryResolveCameraForwardTargetLocalPosition(
                    parent,
                    sourceLocalPose,
                    targetLocalPose,
                    outputCamera,
                    resolvedCellSize,
                    planeJitter,
                    out var cameraForwardTargetLocalPosition,
                    out var cameraUpLocalDirection,
                    out var cameraForwardLocalDirection))
            {
                return new EnemyDeathExitEffectPlan(
                    cameraForwardTargetLocalPosition,
                    cameraUpLocalDirection,
                    arcHeight,
                    spinDegrees,
                    cameraForwardLocalDirection);
            }

            var fallbackForwardDirection = sourceLocalPose.Rotation * Vector3.back;
            if (fallbackForwardDirection.sqrMagnitude <= 0.000001f)
            {
                fallbackForwardDirection = Vector3.back;
            }

            var fallbackRightDirection = sourceLocalPose.Rotation * Vector3.right;
            var fallbackUpDirection = sourceLocalPose.Rotation * Vector3.up;
            var fallbackPlaneOffset = ResolveFallbackPlaneOffset(
                sourceLocalPose,
                targetLocalPose,
                fallbackRightDirection,
                fallbackUpDirection,
                resolvedCellSize,
                planeJitter);
            var fallbackDistance = (FallbackForwardDistanceInCells + seededValues.NextRange(0f, FallbackForwardDistanceJitterInCells)) *
                                   resolvedCellSize;
            var fallbackTargetLocalPosition =
                sourceLocalPose.Position +
                (fallbackForwardDirection.normalized * fallbackDistance) +
                fallbackPlaneOffset;
            return new EnemyDeathExitEffectPlan(
                fallbackTargetLocalPosition,
                fallbackUpDirection,
                arcHeight,
                spinDegrees,
                fallbackForwardDirection);
        }

        private static bool TryResolveCameraForwardTargetLocalPosition(
            Transform parent,
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            float cellSize,
            Vector2 planeJitter,
            out Vector3 targetLocalPosition,
            out Vector3 arcLocalDirection,
            out Vector3 spinAxisLocal)
        {
            var startWorldPosition = parent.TransformPoint(sourceLocalPose.Position);
            var startCameraLocalPosition = outputCamera.transform.InverseTransformPoint(startWorldPosition);
            if (!IsValidCameraLocalPoint(startCameraLocalPosition))
            {
                targetLocalPosition = default;
                arcLocalDirection = default;
                spinAxisLocal = default;
                return false;
            }

            var targetCameraLocalPosition = startCameraLocalPosition;
            targetCameraLocalPosition.z = outputCamera.nearClipPlane + Mathf.Max(0.05f, CameraNearPlanePaddingInCells * cellSize);
            var planeOffset = ResolveCameraPlaneOffset(
                parent,
                targetLocalPose,
                outputCamera,
                startCameraLocalPosition,
                cellSize,
                planeJitter);
            targetCameraLocalPosition.x += planeOffset.x;
            targetCameraLocalPosition.y += planeOffset.y;

            var targetWorldPosition = outputCamera.transform.TransformPoint(targetCameraLocalPosition);
            if (!IsFinite(targetWorldPosition))
            {
                targetLocalPosition = default;
                arcLocalDirection = default;
                spinAxisLocal = default;
                return false;
            }

            targetLocalPosition = parent.InverseTransformPoint(targetWorldPosition);
            arcLocalDirection = parent.InverseTransformDirection(outputCamera.transform.up).normalized;
            spinAxisLocal = parent.InverseTransformDirection(outputCamera.transform.forward).normalized;
            return true;
        }

        private static Vector2 ResolveCameraPlaneOffset(
            Transform parent,
            GameplayEntityPose? targetLocalPose,
            Camera outputCamera,
            Vector3 startCameraLocalPosition,
            float cellSize,
            Vector2 planeJitter)
        {
            var planeOffset = planeJitter;
            if (!targetLocalPose.HasValue)
            {
                return planeOffset;
            }

            var targetWorldPosition = parent.TransformPoint(targetLocalPose.Value.Position);
            var targetCameraLocalPosition = outputCamera.transform.InverseTransformPoint(targetWorldPosition);
            if (!IsFinite(targetCameraLocalPosition))
            {
                return planeOffset;
            }

            var cameraPlaneDirection = new Vector2(
                targetCameraLocalPosition.x - startCameraLocalPosition.x,
                targetCameraLocalPosition.y - startCameraLocalPosition.y);
            if (cameraPlaneDirection.sqrMagnitude <= 0.000001f)
            {
                return planeOffset;
            }

            var biasMagnitude = Mathf.Min(cameraPlaneDirection.magnitude * CameraPlaneBiasWeight, CameraPlaneBiasMaxInCells * cellSize);
            return planeOffset + (cameraPlaneDirection.normalized * biasMagnitude);
        }

        private static Vector3 ResolveFallbackPlaneOffset(
            GameplayEntityPose sourceLocalPose,
            GameplayEntityPose? targetLocalPose,
            Vector3 fallbackRightDirection,
            Vector3 fallbackUpDirection,
            float cellSize,
            Vector2 planeJitter)
        {
            var playerBias = Vector2.zero;
            if (targetLocalPose.HasValue)
            {
                var toTarget = targetLocalPose.Value.Position - sourceLocalPose.Position;
                playerBias = new Vector2(
                    Vector3.Dot(toTarget, fallbackRightDirection.normalized),
                    Vector3.Dot(toTarget, fallbackUpDirection.normalized));
                if (playerBias.sqrMagnitude > 0.000001f)
                {
                    playerBias = playerBias.normalized * Mathf.Min(playerBias.magnitude * 0.2f, FallbackPlaneJitterInCells * cellSize);
                }
            }

            var combinedPlaneOffset = playerBias + planeJitter;
            return (fallbackRightDirection.normalized * combinedPlaneOffset.x) +
                   (fallbackUpDirection.normalized * combinedPlaneOffset.y);
        }

        private static bool IsValidCameraLocalPoint(Vector3 point)
        {
            return point.z > 0.0001f && IsFinite(point);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }
    }

    internal struct SeededValueSequence
    {
        private uint _state;

        public SeededValueSequence(int seed)
        {
            _state = seed != 0
                ? unchecked((uint)seed)
                : 0x9E3779B9u;
        }

        public float NextFloat01()
        {
            return (NextState() & 0x00FFFFFFu) / 16777215f;
        }

        public float NextRange(float min, float max)
        {
            return Mathf.Lerp(min, max, NextFloat01());
        }

        public float NextSignedRange(float minMagnitude, float maxMagnitude)
        {
            var magnitude = NextRange(minMagnitude, maxMagnitude);
            return NextFloat01() < 0.5f
                ? -magnitude
                : magnitude;
        }

        private uint NextState()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value != 0u ? value : 0xA511E9B3u;
            return _state;
        }
    }

    internal sealed class TimedGameObjectEffectTrack : IGameplayTransientEffectTrack
    {
        private readonly float _durationSeconds;
        private readonly GameObject _root;
        private float _elapsedSeconds;

        public TimedGameObjectEffectTrack(GameObject root, float durationSeconds)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _durationSeconds = Mathf.Max(0.0001f, durationSeconds);
        }

        public bool IsComplete => _elapsedSeconds >= _durationSeconds - 0.0001f;

        public void Advance(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                _elapsedSeconds = Mathf.Min(_durationSeconds, _elapsedSeconds + deltaTime);
            }
        }

        public void Dispose()
        {
            if (_root == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_root);
            }
            else
            {
                Object.DestroyImmediate(_root);
            }
        }
    }

    internal sealed class GameplayPresentationEffectFactory
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

        public IGameplayTransientEffectTrack CreateExitEffect(
            TickEntityExitPresentationSignal signal,
            GameplayEntityView sourceView,
            GameplayEntityPose localPose,
            GameplayEntityPose? targetLocalPose,
            float durationSeconds,
            Camera outputCamera)
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
            var instancedMaterials = CreateInstancedMaterials(renderers);
            if (signal.ExitCause == TickEntityExitCause.Killed)
            {
                var plan = EnemyDeathExitEffectPlanBuilder.Build(
                    _parent,
                    localPose,
                    targetLocalPose,
                    outputCamera,
                    _cellSize,
                    signal.PresentationSeed);
                return new EnemyDeathExitEffectTrack(
                    effectRoot.transform,
                    renderers,
                    instancedMaterials,
                    durationSeconds,
                    plan);
            }

            return new EntityExitEffectTrack(
                signal.ExitCause,
                effectRoot.transform,
                renderers,
                instancedMaterials,
                durationSeconds);
        }

        internal IGameplayTransientEffectTrack CreateHitEffect(
            int entityId,
            GameplayEntityPose localPose,
            EntityEffectPresentationSnapshot effectSnapshot,
            float fallbackDurationSeconds)
        {
            if (_parent == null ||
                !effectSnapshot.HasHitVfxPrefab)
            {
                return null;
            }

            var effectInstance = Object.Instantiate(effectSnapshot.HitVfxPrefab, _parent, worldPositionStays: false);
            effectInstance.name = $"TransientPlayerHitEffect_{entityId}";
            effectInstance.transform.localPosition = localPose.Position;
            effectInstance.transform.localRotation = localPose.Rotation;
            effectInstance.transform.localScale = Vector3.one;
            effectInstance.SetActive(true);

            var durationSeconds = effectSnapshot.HasHitEffectDurationOverride
                ? effectSnapshot.HitEffectDurationSeconds
                : Mathf.Max(0.0001f, fallbackDurationSeconds);
            return new TimedGameObjectEffectTrack(effectInstance, durationSeconds);
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
