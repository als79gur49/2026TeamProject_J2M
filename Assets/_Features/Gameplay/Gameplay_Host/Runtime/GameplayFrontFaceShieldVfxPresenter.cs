using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayFrontFaceShieldVfxPresenter
    {
        private readonly Dictionary<FrontFaceShieldWarningKey, WarningShieldRuntime> _warningShieldsByKey = new();
        private readonly HashSet<FrontFaceShieldWarningKey> _seenWarningKeys = new();
        private readonly List<FrontFaceShieldWarningKey> _warningRemovalBuffer = new();
        private float _cellSize = 1f;
        private Transform _parent;

        internal int ActiveLoopInstanceCount => 0;

        internal int WarningInstanceCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _warningShieldsByKey)
                {
                    if (pair.Value.WarningInstance != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialize(Transform parent, float cellSize)
        {
            Clear();
            _parent = parent;
            _cellSize = Mathf.Max(0.0001f, cellSize);
        }

        public void Clear()
        {
            foreach (var pair in _warningShieldsByKey)
            {
                pair.Value.Dispose();
            }

            _warningShieldsByKey.Clear();

            _seenWarningKeys.Clear();
            _warningRemovalBuffer.Clear();
        }

        public void RefreshWindupWarnings(
            IReadOnlyList<TickFrontFaceShieldWindupWarningSignal> signals,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            _seenWarningKeys.Clear();
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                var key = new FrontFaceShieldWarningKey(
                    signal.SourceEntityId,
                    signal.EffectIndex,
                    signal.ActivationSequence);
                _seenWarningKeys.Add(key);
                if (_warningShieldsByKey.TryGetValue(key, out var existingRuntime))
                {
                    existingRuntime.LastSeenTick = signal.TickIndex;
                    existingRuntime.SourceCell = signal.SourceCell;
                    existingRuntime.Topology = signal.Topology;
                    UpdateWarningPlacement(existingRuntime, signal, stateStore, projector);
                    continue;
                }

                if (!TryCreateWarningRuntime(signal, stateStore, projector, out var runtime))
                {
                    continue;
                }

                _warningShieldsByKey[key] = runtime;
            }

            _warningRemovalBuffer.Clear();
            foreach (var pair in _warningShieldsByKey)
            {
                if (!_seenWarningKeys.Contains(pair.Key))
                {
                    _warningRemovalBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < _warningRemovalBuffer.Count; i++)
            {
                var key = _warningRemovalBuffer[i];
                if (!_warningShieldsByKey.TryGetValue(key, out var runtime))
                {
                    continue;
                }

                runtime.Dispose();
                _warningShieldsByKey.Remove(key);
            }
        }

        public void RefreshActiveSources(
            IReadOnlyList<TickFrontFaceShieldSourceSignal> signals,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (signals == null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            if (stateStore == null)
            {
                throw new ArgumentNullException(nameof(stateStore));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }
        }

        public void Update(float deltaTime)
        {
        }

        private bool TryCreateWarningRuntime(
            TickFrontFaceShieldWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            out WarningShieldRuntime runtime)
        {
            runtime = null;
            if (!TryResolveShieldSnapshot(signal.SourceEntityId, stateStore, out var snapshot) ||
                !snapshot.HasTelegraphPrefab ||
                _parent == null)
            {
                return false;
            }

            var warningInstance = CreateWarningInstance(signal, snapshot, stateStore, projector);
            if (warningInstance == null)
            {
                return false;
            }

            runtime = new WarningShieldRuntime(
                signal.SourceEntityId,
                signal.EffectIndex,
                signal.ActivationSequence,
                signal.SourceCell,
                signal.Topology,
                snapshot,
                warningInstance);
            runtime.LastSeenTick = signal.TickIndex;
            return true;
        }

        private GameObject CreateWarningInstance(
            TickFrontFaceShieldWindupWarningSignal signal,
            EnemyFrontFaceShieldPresentationSnapshot snapshot,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (!snapshot.HasTelegraphPrefab ||
                _parent == null ||
                !TryResolveSourcePose(signal, stateStore, projector, out var pose))
            {
                return null;
            }

            return InstantiateVfx(
                snapshot.TelegraphPrefab,
                _parent,
                $"FrontFaceShieldWindup_{signal.SourceEntityId}_{signal.EffectIndex}_{signal.ActivationSequence}",
                pose.Position + (pose.Rotation * snapshot.LocalOffset),
                pose.Rotation,
                ResolveRadiusScale(signal, snapshot),
                useLocalTransform: true);
        }

        private void UpdateWarningPlacement(
            WarningShieldRuntime runtime,
            TickFrontFaceShieldWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (runtime.WarningInstance == null ||
                !TryResolveSourcePose(signal, stateStore, projector, out var pose))
            {
                return;
            }

            runtime.WarningInstance.transform.localPosition =
                pose.Position + (pose.Rotation * runtime.Snapshot.LocalOffset);
            runtime.WarningInstance.transform.localRotation = pose.Rotation;
            runtime.WarningInstance.transform.localScale = ResolveRadiusScale(signal, runtime.Snapshot);
        }

        private bool TryResolveShieldSnapshot(
            int sourceEntityId,
            GameplayPresentationStateStore stateStore,
            out EnemyFrontFaceShieldPresentationSnapshot snapshot)
        {
            if (!stateStore.ViewsByEntityId.TryGetValue(sourceEntityId, out var sourceView) ||
                sourceView == null)
            {
                snapshot = default;
                return false;
            }

            var authoring = EnemyFrontFaceShieldPresentationAuthoring.GetOptionalValidatedAuthoring(sourceView);
            if (authoring == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = authoring.CreateSnapshot();
            return true;
        }

        private bool TryResolveSourcePose(
            TickFrontFaceShieldWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            out GameplayEntityPose pose)
        {
            return TryResolveSourcePose(
                signal.SourceEntityId,
                signal.SourceCell,
                signal.Topology,
                stateStore,
                projector,
                out pose);
        }

        private static bool TryResolveSourcePose(
            int sourceEntityId,
            SurfaceCell sourceCell,
            CubeTopologyState topology,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            out GameplayEntityPose pose)
        {
            if (stateStore.CommittedLocalTargetPoses.TryGetValue(sourceEntityId, out pose))
            {
                return true;
            }

            if (!projector.TryProjectEntityCell(sourceCell, topology, EntityType.Unit, out var projectedPose))
            {
                pose = default;
                return false;
            }

            pose = new GameplayEntityPose(projectedPose.LocalPosition, projectedPose.LocalRotation);
            return true;
        }

        private static bool TryResolveSurfacePose(
            GameplayCubeProjector projector,
            SurfaceCell cell,
            CubeTopologyState topology,
            out GameplayEntityPose pose)
        {
            if (!projector.TryProjectSurfaceCell(cell, topology, out var projectedPose))
            {
                pose = default;
                return false;
            }

            pose = new GameplayEntityPose(projectedPose.LocalPosition, projectedPose.LocalRotation);
            return true;
        }

        private Vector3 ResolveRadiusScale(
            TickFrontFaceShieldWindupWarningSignal signal,
            EnemyFrontFaceShieldPresentationSnapshot snapshot)
        {
            return ResolveRadiusScale(signal.Radius, signal.TargetPattern, snapshot);
        }

        private static Vector3 ResolveRadiusScale(
            int radius,
            FrontFaceShieldTargetPattern targetPattern,
            EnemyFrontFaceShieldPresentationSnapshot snapshot)
        {
            if (!snapshot.ScaleByRadius)
            {
                return Vector3.one;
            }

            return targetPattern switch
            {
                FrontFaceShieldTargetPattern.SquareRadius => Vector3.one * Mathf.Max(1f, (radius * 2) + 1),
                _ => Vector3.one * Mathf.Max(1f, radius),
            };
        }

        private static float ResolveDuration(float authoredDuration, float fallback)
        {
            return authoredDuration > 0f
                ? authoredDuration
                : fallback;
        }

        private static GameObject InstantiateVfx(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            bool useLocalTransform)
        {
            if (prefab == null ||
                parent == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, worldPositionStays: false);
            instance.name = name;
            if (useLocalTransform)
            {
                instance.transform.localPosition = position;
                instance.transform.localRotation = rotation;
            }
            else
            {
                instance.transform.SetPositionAndRotation(position, rotation);
            }

            instance.transform.localScale = scale;
            RemoveColliders(instance);
            instance.SetActive(true);
            return instance;
        }

        private static void RemoveColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(includeInactive: true);
            for (var i = 0; i < colliders.Length; i++)
            {
                GameplayTransientEffectTrackUtility.SafeDestroy(colliders[i]);
            }
        }

        private readonly struct FrontFaceShieldWarningKey : IEquatable<FrontFaceShieldWarningKey>
        {
            public FrontFaceShieldWarningKey(int sourceEntityId, int effectIndex, int activationSequence)
            {
                SourceEntityId = sourceEntityId;
                EffectIndex = effectIndex;
                ActivationSequence = activationSequence;
            }

            public int SourceEntityId { get; }

            public int EffectIndex { get; }

            public int ActivationSequence { get; }

            public bool Equals(FrontFaceShieldWarningKey other)
            {
                return SourceEntityId == other.SourceEntityId &&
                       EffectIndex == other.EffectIndex &&
                       ActivationSequence == other.ActivationSequence;
            }

            public override bool Equals(object obj)
            {
                return obj is FrontFaceShieldWarningKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = SourceEntityId;
                    hash = (hash * 397) ^ EffectIndex;
                    hash = (hash * 397) ^ ActivationSequence;
                    return hash;
                }
            }
        }

        private sealed class WarningShieldRuntime : IDisposable
        {
            public WarningShieldRuntime(
                int sourceEntityId,
                int effectIndex,
                int activationSequence,
                SurfaceCell sourceCell,
                CubeTopologyState topology,
                EnemyFrontFaceShieldPresentationSnapshot snapshot,
                GameObject warningInstance)
            {
                SourceEntityId = sourceEntityId;
                EffectIndex = effectIndex;
                ActivationSequence = activationSequence;
                SourceCell = sourceCell;
                Topology = topology;
                Snapshot = snapshot;
                WarningInstance = warningInstance;
            }

            public int SourceEntityId { get; }

            public int EffectIndex { get; }

            public int ActivationSequence { get; }

            public SurfaceCell SourceCell { get; set; }

            public CubeTopologyState Topology { get; set; }

            public int LastSeenTick { get; set; }

            public EnemyFrontFaceShieldPresentationSnapshot Snapshot { get; }

            public GameObject WarningInstance { get; }

            public void Dispose()
            {
                GameplayTransientEffectTrackUtility.SafeDestroy(WarningInstance);
            }
        }
    }
}
