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
        public void PlayTransient_AppliesResolvedAnchorPose()
        {
            var localPosition = new Vector3(0.25f, -0.5f, 0.75f);
            var localRotation = Quaternion.Euler(15f, 25f, 35f);

            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                resolvedLocalPosition: localPosition,
                resolvedLocalRotation: localRotation));
            var instance = root.OneShotRoot.GetChild(0);

            Assert.That(handle, Is.Not.Null);
            Assert.That(instance.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(instance.localRotation, localRotation), Is.LessThan(0.001f));
        }

        [Test]
        [Category("Extended")]
        public void PlayTransient_PreservesAuthoredModelRootPoseUnderRuntimeAnchor()
        {
            var modelRoot = new GameObject("ModelRoot").transform;
            modelRoot.SetParent(prefab.transform, worldPositionStays: false);
            modelRoot.localPosition = new Vector3(0.1f, 0.2f, 0.3f);
            modelRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            modelRoot.localScale = new Vector3(1.1f, 1.2f, 1.3f);
            var localPosition = new Vector3(0.25f, -0.5f, 0.75f);
            var localRotation = Quaternion.Euler(15f, 25f, 35f);

            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                resolvedLocalPosition: localPosition,
                resolvedLocalRotation: localRotation));
            var instance = root.OneShotRoot.GetChild(0);
            var instanceModelRoot = instance.Find("ModelRoot");

            Assert.That(handle, Is.Not.Null);
            Assert.That(instance.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(instance.localRotation, localRotation), Is.LessThan(0.001f));
            Assert.That(instanceModelRoot, Is.Not.Null);
            Assert.That(instanceModelRoot.localPosition, Is.EqualTo(modelRoot.localPosition));
            Assert.That(Quaternion.Angle(instanceModelRoot.localRotation, modelRoot.localRotation), Is.LessThan(0.001f));
            Assert.That(instanceModelRoot.localScale, Is.EqualTo(modelRoot.localScale));
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
        [Category("Core")]
        public void VFX_SpawnedWhileGameplayPresentationPaused_StartsSuspended()
        {
            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            var handle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));
            var particleSystem = root.OneShotRoot.GetChild(0).GetComponent<ParticleSystem>();

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
            Assert.That(particleSystem.isPaused, Is.True);
        }

        [Test]
        [Category("Core")]
        public void VFX_SpawnedWhilePaused_ResumesSameHandleAfterResume()
        {
            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            var handle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));
            var handleId = handle.HandleId;
            var instance = root.OneShotRoot.GetChild(0).gameObject;

            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.HandleId, Is.EqualTo(handleId));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(root.OneShotRoot.GetChild(0).gameObject, Is.SameAs(instance));
        }

        [Test]
        [Category("Core")]
        public void PooledVfx_ReusedAfterPause_DoesNotLeakOldSuspendReasons()
        {
            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            var firstHandle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));
            var firstInstance = root.OneShotRoot.GetChild(0).gameObject;
            pool.Release(firstHandle);
            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            var secondHandle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                sequenceId: 2));
            var reusedInstance = root.OneShotRoot.GetChild(0).gameObject;

            Assert.That(reusedInstance, Is.SameAs(firstInstance));
            Assert.That(secondHandle.State, Is.EqualTo(VfxLifetimeState.Active));
        }

        [Test]
        [Category("Core")]
        public void PooledVfx_LeasedWhilePauseStillActive_InheritsGameplayPauseReason()
        {
            var firstHandle = pool.PlayTransient(CreateCommand(VfxPlaybackMode.OneShot, VfxStopPolicy.AuthoredDuration));
            var firstInstance = root.OneShotRoot.GetChild(0).gameObject;
            pool.Release(firstHandle);
            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            var secondHandle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                sequenceId: 2));
            var reusedInstance = root.OneShotRoot.GetChild(0).gameObject;

            Assert.That(reusedInstance, Is.SameAs(firstInstance));
            Assert.That(secondHandle.State, Is.EqualTo(VfxLifetimeState.PresentationSuspended));
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_NonLoopingChildParticles_GameplayPauseResume_DoesNotClearResidualParticles()
        {
            var handle = pool.StartPersistent(CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                isPersistent: true));
            var particleSystem = root.PersistentRoot.GetChild(0).GetComponent<ParticleSystem>();
            var particleCountBeforePause = SeedStoppedResidualParticles(particleSystem);

            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(particleSystem.particleCount, Is.EqualTo(particleCountBeforePause));
            Assert.That(particleSystem.isPlaying, Is.False);
            Assert.That(particleSystem.isEmitting, Is.False);
        }

        [Test]
        [Category("Core")]
        public void PersistentVfx_GameplayPauseResume_DoesNotStartPreviouslyStoppedChildParticles()
        {
            pool.StartPersistent(CreateCommand(
                VfxPlaybackMode.Loop,
                VfxStopPolicy.StopEmittingThenRelease,
                isPersistent: true));
            var particleSystem = root.PersistentRoot.GetChild(0).GetComponent<ParticleSystem>();
            SeedStoppedResidualParticles(particleSystem);

            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(particleSystem.isPlaying, Is.False);
            Assert.That(particleSystem.isEmitting, Is.False);
            Assert.That(particleSystem.particleCount, Is.GreaterThan(0));
        }

        [Test]
        [Category("Core")]
        public void PooledVfx_PoolRelease_StillClearsParticles()
        {
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f));
            var instance = root.OneShotRoot.GetChild(0).gameObject;
            var particleSystem = instance.GetComponent<ParticleSystem>();
            SeedStoppedResidualParticles(particleSystem);

            pool.Release(handle);

            Assert.That(instance.activeSelf, Is.False);
            Assert.That(instance.transform.parent, Is.EqualTo(root.PoolRoot));
            Assert.That(particleSystem.particleCount, Is.Zero);
            Assert.That(particleSystem.IsAlive(true), Is.False);
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxPooledInstance_SameReasonSuspend_DoesNotOverwriteParticleSnapshot()
        {
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f));
            var particleSystem = root.OneShotRoot.GetChild(0).GetComponent<ParticleSystem>();
            Assert.That(particleSystem.isPlaying || particleSystem.isEmitting, Is.True);

            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pool.SuspendActivePresentation(VfxPresentationSuspendReason.GameplayPause);
            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(particleSystem.isPlaying || particleSystem.isEmitting, Is.True);
        }

        [Test]
        [Category("Core")]
        public void GameplayVfxPooledInstance_ResumeAbsentReason_IsNoOp()
        {
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f));
            var particleSystem = root.OneShotRoot.GetChild(0).GetComponent<ParticleSystem>();
            var particleCountBeforeResume = SeedStoppedResidualParticles(particleSystem);

            pool.ResumeActivePresentation(VfxPresentationSuspendReason.GameplayPause);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(particleSystem.particleCount, Is.EqualTo(particleCountBeforeResume));
            Assert.That(particleSystem.isPlaying, Is.False);
            Assert.That(particleSystem.isEmitting, Is.False);
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
        public void OneShotAuthoredDuration_ReleasesAfterLifetimeAndTail()
        {
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0.55f,
                tailSeconds: 0.25f));

            timeProvider.TimeSeconds = 0.54f;
            pool.Advance(0.54f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.PooledCount, Is.Zero);

            timeProvider.TimeSeconds = 0.55f;
            pool.Advance(0.01f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));

            timeProvider.TimeSeconds = 0.8f;
            pool.Advance(0.25f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.PooledCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Core")]
        public void TopologyTransitionStart_ClearsParticleAndTrailResiduals()
        {
            var prefabTrail = prefab.AddComponent<TrailRenderer>();
            prefabTrail.time = 10f;
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 10f));
            var instance = root.OneShotRoot.GetChild(0).gameObject;
            var trail = instance.GetComponent<TrailRenderer>();
            trail.AddPosition(Vector3.zero);
            trail.AddPosition(Vector3.one);
            Assert.That(trail.positionCount, Is.GreaterThan(0));

            pool.HardClearActiveForTopologyTransition();

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.PooledCount, Is.EqualTo(1));
            Assert.That(instance.activeSelf, Is.False);
            Assert.That(trail.positionCount, Is.Zero);
            Assert.That(instance.GetComponent<ParticleSystem>().IsAlive(true), Is.False);
        }

        [Test]
        [Category("Extended")]
        public void AuthoredDurationOneShot_DefaultLifetimeZero_BehaviorUnchanged()
        {
            var handle = pool.PlayTransient(CreateCommand(
                VfxPlaybackMode.OneShot,
                VfxStopPolicy.AuthoredDuration,
                defaultLifetimeSeconds: 0f,
                tailSeconds: 0f));

            pool.Advance(0f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));
            Assert.That(pool.ActiveCount, Is.EqualTo(1));

            pool.Advance(0f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.PooledCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Extended")]
        public void AttachedTransient_ControllerManagedDefaultLifetimeZero_RemainsAttachedAcrossAdvance()
        {
            var attachParent = new GameObject("AttachedFollowerParent").transform;
            attachParent.SetParent(owner.transform, worldPositionStays: false);
            var handle = pool.PlayAttachedTransient(
                CreateCommand(
                    VfxPlaybackMode.Follow,
                    VfxStopPolicy.DetachThenStopEmittingThenRelease,
                    defaultLifetimeSeconds: 0f,
                    tailSeconds: 0.25f),
                attachParent,
                controllerManagedLifetime: true);
            var instance = attachParent.GetChild(0);

            timeProvider.TimeSeconds = 1f;
            pool.Advance(1f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.Active));
            Assert.That(instance.parent, Is.EqualTo(attachParent));
            Assert.That(root.TailRoot.childCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.PooledCount, Is.Zero);
        }

        [Test]
        [Category("Extended")]
        public void ControllerManagedTail_ReleasesAfterControllerDetach()
        {
            var attachParent = new GameObject("AttachedFollowerParent").transform;
            attachParent.SetParent(owner.transform, worldPositionStays: false);
            var handle = pool.PlayAttachedTransient(
                CreateCommand(
                    VfxPlaybackMode.Follow,
                    VfxStopPolicy.DetachThenStopEmittingThenRelease,
                    defaultLifetimeSeconds: 0f,
                    tailSeconds: 0.25f),
                attachParent,
                controllerManagedLifetime: true);
            var instance = attachParent.GetChild(0);

            handle.Detach();
            handle.StopEmitting();
            handle.MarkTailPlaying();

            Assert.That(instance.parent, Is.EqualTo(root.TailRoot));
            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.TailPlaying));

            timeProvider.TimeSeconds = 0.30f;
            pool.Advance(0.30f);

            Assert.That(handle.State, Is.EqualTo(VfxLifetimeState.ReleasedToPool));
            Assert.That(instance.parent, Is.EqualTo(root.PoolRoot));
            Assert.That(pool.ActiveCount, Is.Zero);
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

        private static int SeedStoppedResidualParticles(ParticleSystem particleSystem, int count = 7)
        {
            Assert.That(particleSystem, Is.Not.Null);
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(false);
            var main = particleSystem.main;
            main.loop = false;
            main.duration = 0.1f;
            main.startLifetime = 30f;
            main.maxParticles = System.Math.Max(main.maxParticles, count);
            particleSystem.Play(false);
            particleSystem.Emit(
                new ParticleSystem.EmitParams
                {
                    applyShapeToPosition = false,
                    position = Vector3.zero,
                    startColor = Color.white,
                    startLifetime = 30f,
                    startSize = 0.1f
                },
                count);
            particleSystem.Pause(false);
            Assert.That(particleSystem.isPlaying, Is.False);
            Assert.That(particleSystem.isEmitting, Is.False);
            Assert.That(particleSystem.particleCount, Is.EqualTo(count));
            return particleSystem.particleCount;
        }

        private static ResolvedVfxPlaybackCommand CreateCommand(
            VfxPlaybackMode playbackMode,
            VfxStopPolicy stopPolicy,
            int sequenceId = 1,
            bool isPersistent = false,
            float defaultLifetimeSeconds = 0f,
            float tailSeconds = 0f,
            int maxConcurrentInstances = 0,
            int persistentEntityId = 7,
            Vector3? resolvedLocalPosition = null,
            Quaternion? resolvedLocalRotation = null)
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
            var anchorCell = new SurfaceCell(FaceId.Floor, 1, 1);
            var topology = new CubeTopologyState(FaceId.Floor);
            var anchor = resolvedLocalPosition.HasValue || resolvedLocalRotation.HasValue
                ? VfxResolvedAnchor.ForCell(
                    anchorCell,
                    topology,
                    VfxAnchorSlot.CellCenter,
                    resolvedLocalPosition ?? Vector3.zero,
                    resolvedLocalRotation ?? Quaternion.identity)
                : VfxResolvedAnchor.ForCell(
                    anchorCell,
                    topology,
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
