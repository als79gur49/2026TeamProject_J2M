using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Vfx;
using Game.Feature.Gameplay.Vfx.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayVfxGameObjectPoolTests
    {
        private GameObject owner;
        private GameObject prefab;
        private GameplayVfxRuntimeRoot root;
        private FakePrefabProvider prefabProvider;
        private FakeTimeProvider timeProvider;
        private GameplayVfxGameObjectPool pool;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("GameplayVfxPoolTestOwner");
            root = GameplayVfxRuntimeRoot.CreateUnder(owner.transform);
            prefab = new GameObject("TestVfxPrefab");
            prefab.AddComponent<ParticleSystem>();
            prefabProvider = new FakePrefabProvider(prefab);
            timeProvider = new FakeTimeProvider();
            pool = new GameplayVfxGameObjectPool(root, prefabProvider, timeProvider);
        }

        [TearDown]
        public void TearDown()
        {
            pool?.HardCleanupAll();
            Destroy(owner);
            Destroy(prefab);
        }

        [Test]
        [Category("Extended")]
        public void PlayTransient_LeasesPrefabUnderOneShotRoot()
        {
            var handle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(root.OneShotRoot.childCount, Is.EqualTo(1));
            Assert.That(root.OneShotRoot.GetChild(0).gameObject.activeSelf, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void Release_ReturnsInstanceToPoolAndReusesIt()
        {
            var firstHandle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));
            var firstInstance = root.OneShotRoot.GetChild(0).gameObject;

            pool.Release(firstHandle);

            Assert.That(firstHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(firstInstance.activeSelf, Is.False);
            Assert.That(firstInstance.transform.parent, Is.EqualTo(root.PoolRoot));
            Assert.That(pool.PooledCount, Is.EqualTo(1));

            var secondHandle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration, sequenceId: 2));
            var reusedInstance = root.OneShotRoot.GetChild(0).gameObject;

            Assert.That(secondHandle, Is.Not.Null);
            Assert.That(reusedInstance, Is.SameAs(firstInstance));
            Assert.That(pool.PooledCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void StopEmittingThenRelease_WaitsForTailSeconds()
        {
            var command = CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                isPersistent: true,
                tailSeconds: 1f);
            var handle = pool.StartPersistent(command);
            var runner = new VfxLifetimeRunner();

            runner.Stop(handle, VfxStopPolicy.StopEmittingThenRelease);

            timeProvider.TimeSeconds = 0.5f;
            pool.Advance(0.5f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.PooledCount, Is.EqualTo(0));

            timeProvider.TimeSeconds = 1f;
            pool.Advance(0.5f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.PooledCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void DetachThenStopEmitting_MovesInstanceToTailRootUntilTailCompletes()
        {
            var command = CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.DetachThenStopEmittingThenRelease,
                isPersistent: true,
                tailSeconds: 1f);
            var handle = pool.StartPersistent(command);
            var instance = root.PersistentRoot.GetChild(0).gameObject;
            var runner = new VfxLifetimeRunner();

            runner.Stop(handle, VfxStopPolicy.DetachThenStopEmittingThenRelease);

            Assert.That(instance.transform.parent, Is.EqualTo(root.TailRoot));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));

            timeProvider.TimeSeconds = 0.5f;
            pool.Advance(0.5f);

            Assert.That(instance.transform.parent, Is.EqualTo(root.TailRoot));
            Assert.That(instance.activeSelf, Is.True);

            timeProvider.TimeSeconds = 1f;
            pool.Advance(0.5f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(instance.transform.parent, Is.EqualTo(root.PoolRoot));
            Assert.That(instance.activeSelf, Is.False);
        }

        [Test]
        [Category("Extended")]
        public void MaxConcurrent_SkipsTransientOverLimit()
        {
            var first = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f,
                maxConcurrentInstances: 1));
            var second = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                sequenceId: 2,
                defaultLifetimeSeconds: 10f,
                maxConcurrentInstances: 1));

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
            Assert.That(pool.DroppedByLimitCount, Is.EqualTo(1));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void PersistentOverLimit_ReturnsNullWithoutRegistryRegistration()
        {
            var registry = new VfxPersistentHandleRegistry();
            var first = CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                isPersistent: true,
                maxConcurrentInstances: 1,
                persistentEntityId: 7);
            var second = CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                sequenceId: 2,
                isPersistent: true,
                maxConcurrentInstances: 1,
                persistentEntityId: 8);

            var firstHandle = registry.GetOrStart(first, pool);
            var secondHandle = registry.GetOrStart(second, pool);

            Assert.That(firstHandle, Is.Not.Null);
            Assert.That(secondHandle, Is.Null);
            Assert.That(registry.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.DroppedByLimitCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void HardCleanupAll_DestroysActiveAndPooledInstances()
        {
            var activeHandle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f));
            var pooledHandle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                sequenceId: 2,
                defaultLifetimeSeconds: 10f));
            var activeInstance = root.OneShotRoot.GetChild(0).gameObject;
            var pooledInstance = root.OneShotRoot.GetChild(1).gameObject;

            pool.Release(pooledHandle);
            pool.HardCleanupAll();

            Assert.That(activeHandle.State, Is.EqualTo(VfxLifetimeState.HardCleanup));
            Assert.That(pooledHandle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(activeInstance == null, Is.True);
            Assert.That(pooledInstance == null, Is.True);
            Assert.That(pool.ActiveCount, Is.EqualTo(0));
            Assert.That(pool.PooledCount, Is.EqualTo(0));
        }

        [Test]
        [Category("Extended")]
        public void PoolPlayReleaseAndAdvance_DoNotCreateSnapshots()
        {
            SnapshotMaterializationCounts counts;
            using (var capture = SnapshotMaterializationDiagnostics.BeginCapture())
            {
                var handle = pool.PlayTransient(CreateCommand(
                    VfxPlaybackMode.OneShot,
                    VfxStopPolicy.AuthoredDuration,
                    defaultLifetimeSeconds: 0f,
                    tailSeconds: 0f));
                pool.Advance(0f);
                pool.Release(handle);
                counts = capture.Counts;
            }

            Assert.That(counts.WorldStateCreateSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldMaterializedSnapshotCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldApplyBatchCount, Is.EqualTo(0));
            Assert.That(counts.ProjectedWorldCacheHitCount, Is.EqualTo(0));
        }

        private static ResolvedVfxPlaybackCommand CreateCommand(
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            int sequenceId = 1,
            bool isPersistent = false,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            int persistentEntityId = 7)
        {
            var cueId = GameplayVfxCueId.From(EnemyVfxCue.Spawn);
            var request = new GameplayVfxRequest(
                tickIndex: 1,
                sequenceId,
                presentationSeed: 31 + sequenceId,
                cueId,
                VfxAnchor.ForEntity(persistentEntityId),
                VfxTimingKind.ImmediateOnTickPresentation,
                isPersistent,
                isPersistent
                    ? new VfxPersistentKey(cueId, VfxAnchorKind.Entity, persistentEntityId)
                    : VfxPersistentKey.None);
            var policy = new VfxBindingRuntimePolicy(
                cueId,
                VfxBindingRequirement.Optional,
                VfxMissingAnchorPolicy.SkipOptional,
                playbackMode,
                stopPolicy,
                defaultLifetimeSeconds,
                tailSeconds,
                maxConcurrentInstances);
            var anchor = VfxResolvedAnchor.ForCell(
                new SurfaceCell(FaceId.Floor, 1, 1),
                new CubeTopologyState(FaceId.Floor),
                VfxAnchorSlot.CellCenter);
            return new ResolvedVfxPlaybackCommand(request, policy, anchor);
        }

        private static void Destroy(Object unityObject)
        {
            if (unityObject != null)
            {
                Object.DestroyImmediate(unityObject);
            }
        }

        private sealed class FakePrefabProvider : IVfxPrefabProvider
        {
            private readonly GameObject prefab;

            public FakePrefabProvider(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public bool TryResolvePrefab(in ResolvedVfxPlaybackCommand command, out GameObject resolvedPrefab)
            {
                resolvedPrefab = prefab;
                return resolvedPrefab != null;
            }
        }

        private sealed class FakeTimeProvider : IGameplayVfxTimeProvider
        {
            public float TimeSeconds { get; set; }
        }
    }
}
