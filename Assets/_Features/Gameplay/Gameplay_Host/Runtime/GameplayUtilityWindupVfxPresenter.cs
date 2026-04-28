using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Loop;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Host
{
    internal sealed class GameplayUtilityWindupVfxPresenter
    {
        private readonly Dictionary<SummonWarningKey, SummonWarningRuntime> _summonWarningsByKey = new();
        private readonly HashSet<SummonWarningKey> _seenSummonWarningKeys = new();
        private readonly List<SummonWarningKey> _summonWarningRemovalBuffer = new();
        private Transform _parent;

        internal int SummonWarningInstanceCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _summonWarningsByKey)
                {
                    if (pair.Value.WarningInstance != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialize(Transform parent)
        {
            Clear();
            _parent = parent;
        }

        public void Clear()
        {
            foreach (var pair in _summonWarningsByKey)
            {
                pair.Value.Dispose();
            }

            _summonWarningsByKey.Clear();
            _seenSummonWarningKeys.Clear();
            _summonWarningRemovalBuffer.Clear();
        }

        public void RefreshSummonWarnings(
            IReadOnlyList<TickSummonWindupWarningSignal> signals,
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

            _seenSummonWarningKeys.Clear();
            for (var i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                var key = new SummonWarningKey(
                    signal.SourceEntityId,
                    signal.EffectIndex,
                    signal.ActivationSequence);
                _seenSummonWarningKeys.Add(key);
                if (_summonWarningsByKey.TryGetValue(key, out var existingRuntime))
                {
                    existingRuntime.LastSeenTick = signal.TickIndex;
                    existingRuntime.SourceCell = signal.SourceCell;
                    existingRuntime.Topology = signal.Topology;
                    UpdateWarningPlacement(existingRuntime, signal, stateStore, projector);
                    continue;
                }

                if (!TryCreateSummonWarningRuntime(signal, stateStore, projector, out var runtime))
                {
                    continue;
                }

                _summonWarningsByKey[key] = runtime;
            }

            _summonWarningRemovalBuffer.Clear();
            foreach (var pair in _summonWarningsByKey)
            {
                if (!_seenSummonWarningKeys.Contains(pair.Key))
                {
                    _summonWarningRemovalBuffer.Add(pair.Key);
                }
            }

            for (var i = 0; i < _summonWarningRemovalBuffer.Count; i++)
            {
                var key = _summonWarningRemovalBuffer[i];
                if (!_summonWarningsByKey.TryGetValue(key, out var runtime))
                {
                    continue;
                }

                runtime.Dispose();
                _summonWarningsByKey.Remove(key);
            }
        }

        private bool TryCreateSummonWarningRuntime(
            TickSummonWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            out SummonWarningRuntime runtime)
        {
            runtime = null;
            if (!stateStore.ViewsByEntityId.TryGetValue(signal.SourceEntityId, out var sourceView) ||
                sourceView == null)
            {
                return false;
            }

            var authoring = EnemyUtilityWindupPresentationAuthoring.GetOptionalValidatedAuthoring(sourceView);
            if (authoring == null)
            {
                return false;
            }

            var snapshot = authoring.CreateSnapshot();
            if (!snapshot.HasSummonWindupWarningPrefab)
            {
                return false;
            }

            var instance = CreateWarningInstance(signal, snapshot, sourceView, stateStore, projector);
            if (instance == null)
            {
                return false;
            }

            runtime = new SummonWarningRuntime(
                signal.SourceEntityId,
                signal.EffectIndex,
                signal.ActivationSequence,
                signal.SourceCell,
                signal.Topology,
                snapshot,
                instance);
            runtime.LastSeenTick = signal.TickIndex;
            return true;
        }

        private GameObject CreateWarningInstance(
            TickSummonWindupWarningSignal signal,
            EnemyUtilityWindupPresentationSnapshot snapshot,
            GameplayEntityView sourceView,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (snapshot.AttachSummonWarningToSourceView)
            {
                var attachParent = ResolveAttachParent(sourceView, snapshot);
                return InstantiateVfx(
                    snapshot.SummonWindupWarningPrefab,
                    attachParent,
                    $"SummonWindupWarning_{signal.SourceEntityId}_{signal.EffectIndex}_{signal.ActivationSequence}",
                    snapshot.LocalOffset,
                    Quaternion.identity,
                    useLocalTransform: true);
            }

            if (_parent == null ||
                !TryResolveSourcePose(signal, stateStore, projector, out var pose))
            {
                return null;
            }

            return InstantiateVfx(
                snapshot.SummonWindupWarningPrefab,
                _parent,
                $"SummonWindupWarning_{signal.SourceEntityId}_{signal.EffectIndex}_{signal.ActivationSequence}",
                pose.Position + (pose.Rotation * snapshot.LocalOffset),
                pose.Rotation,
                useLocalTransform: true);
        }

        private void UpdateWarningPlacement(
            SummonWarningRuntime runtime,
            TickSummonWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector)
        {
            if (runtime.WarningInstance == null ||
                runtime.Snapshot.AttachSummonWarningToSourceView ||
                !TryResolveSourcePose(signal, stateStore, projector, out var pose))
            {
                return;
            }

            runtime.WarningInstance.transform.localPosition =
                pose.Position + (pose.Rotation * runtime.Snapshot.LocalOffset);
            runtime.WarningInstance.transform.localRotation = pose.Rotation;
        }

        private static bool TryResolveSourcePose(
            TickSummonWindupWarningSignal signal,
            GameplayPresentationStateStore stateStore,
            GameplayCubeProjector projector,
            out GameplayEntityPose pose)
        {
            if (stateStore.CommittedLocalTargetPoses.TryGetValue(signal.SourceEntityId, out pose))
            {
                return true;
            }

            if (!projector.TryProjectEntityCell(signal.SourceCell, signal.Topology, EntityType.Unit, out var projectedPose))
            {
                pose = default;
                return false;
            }

            pose = new GameplayEntityPose(projectedPose.LocalPosition, projectedPose.LocalRotation);
            return true;
        }

        private static Transform ResolveAttachParent(
            GameplayEntityView sourceView,
            EnemyUtilityWindupPresentationSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.AttachSlot) &&
                TryFindDescendant(sourceView.transform, snapshot.AttachSlot, out var slot))
            {
                return slot;
            }

            return sourceView.transform;
        }

        private static bool TryFindDescendant(Transform root, string name, out Transform result)
        {
            if (root == null)
            {
                result = null;
                return false;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                result = root;
                return true;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                if (TryFindDescendant(root.GetChild(i), name, out result))
                {
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static GameObject InstantiateVfx(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
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
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = localRotation;
            }
            else
            {
                instance.transform.SetPositionAndRotation(localPosition, localRotation);
            }

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

        private readonly struct SummonWarningKey : IEquatable<SummonWarningKey>
        {
            public SummonWarningKey(int sourceEntityId, int effectIndex, int activationSequence)
            {
                SourceEntityId = sourceEntityId;
                EffectIndex = effectIndex;
                ActivationSequence = activationSequence;
            }

            public int SourceEntityId { get; }

            public int EffectIndex { get; }

            public int ActivationSequence { get; }

            public bool Equals(SummonWarningKey other)
            {
                return SourceEntityId == other.SourceEntityId &&
                       EffectIndex == other.EffectIndex &&
                       ActivationSequence == other.ActivationSequence;
            }

            public override bool Equals(object obj)
            {
                return obj is SummonWarningKey other && Equals(other);
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

        private sealed class SummonWarningRuntime : IDisposable
        {
            public SummonWarningRuntime(
                int sourceEntityId,
                int effectIndex,
                int activationSequence,
                SurfaceCell sourceCell,
                CubeTopologyState topology,
                EnemyUtilityWindupPresentationSnapshot snapshot,
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

            public EnemyUtilityWindupPresentationSnapshot Snapshot { get; }

            public GameObject WarningInstance { get; }

            public void Dispose()
            {
                GameplayTransientEffectTrackUtility.SafeDestroy(WarningInstance);
            }
        }
    }
}
