using System;
using System.Collections.Generic;
using Game.Feature.Gameplay.BoardState;
using Game.Feature.Gameplay.Entities;
using Game.Feature.Gameplay.Debug;
using Game.Feature.Gameplay.Host;
using Game.Feature.Gameplay.Loop;
using Game.Feature.Gameplay.Model.Phases;
using Game.Feature.Gameplay.PlayerControl;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    [Category("Full")]
    public sealed class GameplayAnimationDriverCacheTests
    {
        private GameObject _root;
        private GameplayAnimationSyncCoordinator _sync;
        private Dictionary<int, GameplayEntityView> _views;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(nameof(GameplayAnimationDriverCacheTests));
            _sync = new GameplayAnimationSyncCoordinator { DriverCacheDiagnosticsEnabled = true };
            _views = new Dictionary<int, GameplayEntityView>();
        }

        [TearDown]
        public void TearDown()
        {
            _sync.Reset();
            Object.DestroyImmediate(_root);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(7)]
        public void RepeatedExplicitAndLazyLookup_CachesPositiveAndNegativeResults(int mask)
        {
            var view = CreateView(mask);
            _sync.CacheDrivers(40, view);
            for (var i = 0; i < 20; i++)
            {
                AssertDrivers(mask, view);
                _sync.CacheDrivers(40, view);
            }
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(3));
            Assert.That(_sync.DriverCacheResolveCount, Is.EqualTo(1));
            Assert.That(_sync.DriverCacheHitCount, Is.EqualTo(80));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void LazyLookupFirst_ResolvesAllThreeOnce(int first)
        {
            var view = CreateView(7);
            if (first == 0) _sync.TryGetEnemyAnimatorDriver(40, _views, out _);
            else if (first == 1) _sync.TryGetPlayerAnimatorDriver(40, _views, out _);
            else _sync.TryGetEnemyScalePulseDriver(40, _views, out _);
            _sync.CacheDrivers(40, view);
            AssertDrivers(7, view);
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(3));
        }

        [Test]
        public void MissingView_DoesNotPreventLaterSupply()
        {
            Assert.That(_sync.TryGetEnemyAnimatorDriver(40, _views, out _), Is.False);
            _sync.CacheDrivers(40, null);
            Assert.That(_sync.DriverComponentLookupCount, Is.Zero);
            var view = CreateView(7);
            AssertDrivers(7, view);
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(3));
            _views.Clear();
            Assert.That(_sync.TryGetEnemyAnimatorDriver(40, _views, out _), Is.False);
            _views[40] = view;
            AssertDrivers(7, view);
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(6));
        }

        [TestCase(7, 0)]
        [TestCase(0, 7)]
        [TestCase(7, 7)]
        public void SameIdReplacement_LazyLookupReturnsOnlyCurrentView(int oldMask, int newMask)
        {
            var oldView = CreateView(oldMask);
            _sync.CacheDrivers(40, oldView);
            var view = CreateView(newMask);
            AssertDrivers(newMask, view);
            _sync.CacheDrivers(40, view);
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(6));
        }

        [Test]
        public void SameViewHit_DoesNotRestartPulse_ReplacementRestoresOldScale()
        {
            var oldView = CreateView(5);
            var oldPulse = oldView.GetComponent<EnemySummonScalePulsePresentationDriver>();
            var originalScale = oldView.ModelRoot.localScale;
            _sync.CacheDrivers(40, oldView);
            oldPulse.Apply(SummonState(windup: true));
            _sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
            var progressed = oldPulse.CurrentScaleMultiplier;
            Assert.That(progressed, Is.GreaterThan(1f));
            _sync.CacheDrivers(40, oldView);
            Assert.That(oldPulse.CurrentScaleMultiplier, Is.EqualTo(progressed));
            var view = CreateView(5);
            AssertDrivers(5, view);
            Assert.That(oldView.ModelRoot.localScale, Is.EqualTo(originalScale));
            Assert.That(oldPulse.IsPlaying, Is.False);
            var replacementPulse = view.GetComponent<EnemySummonScalePulsePresentationDriver>();
            Assert.That(replacementPulse.IsPlaying, Is.False);
            _sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
            Assert.That(oldView.ModelRoot.localScale, Is.EqualTo(originalScale));
            Assert.That(replacementPulse.CurrentScaleMultiplier, Is.EqualTo(1f));
            replacementPulse.Apply(SummonState(windup: true));
            _sync.AdvanceEnemyAutonomousPresentationAfterSemantic(0.4f);
            Assert.That(replacementPulse.CurrentScaleMultiplier, Is.GreaterThan(1f));
        }

        [TestCase(false, 0)]
        [TestCase(false, 7)]
        [TestCase(true, 0)]
        [TestCase(true, 7)]
        public void ReleaseAndReset_StartNewLifetimeOnSameView(bool reset, int mask)
        {
            var view = CreateView(mask);
            _sync.CacheDrivers(40, view);
            if (reset) _sync.Reset(); else _sync.ReleaseEntity(40);
            if (mask == 0) view.gameObject.AddComponent<PlayerAnimatorDriver>();
            AssertDrivers(mask == 0 ? 2 : mask, view);
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(6));
        }

        [Test]
        public void FrozenConfiguration_AdditionsWaitForNewLifetime_DestroyedPositivesAreRetiredOnce()
        {
            var view = CreateView(0);
            _sync.CacheDrivers(40, view);
            view.gameObject.AddComponent<PlayerAnimatorDriver>();
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out _), Is.False);
            Assert.That(_sync.DestroyedCachedDriverCount, Is.Zero);
            _sync.ReleaseEntity(40);
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out var player), Is.True);
            Object.DestroyImmediate(player);
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out _), Is.False);
            view.gameObject.AddComponent<PlayerAnimatorDriver>();
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out _), Is.False);
            Assert.That(_sync.DestroyedCachedDriverCount, Is.EqualTo(1));
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(6));
        }

        [Test]
        public void DestroyedDrivers_DirectAdvanceResetAndHiddenSyncAreSafe()
        {
            var view = CreateView(7);
            _sync.CacheDrivers(40, view);
            Object.DestroyImmediate(view.GetComponent<EnemyAnimatorDriver>());
            Object.DestroyImmediate(view.GetComponent<PlayerAnimatorDriver>());
            Object.DestroyImmediate(view.GetComponent<EnemySummonScalePulsePresentationDriver>());
            Assert.DoesNotThrow(() => _sync.AdvancePresentation(0.2f));
            Assert.DoesNotThrow(() => _sync.ApplyInitialPlayerPresentation(new Dictionary<int, GameplayEntityPose>()));
            Assert.DoesNotThrow(() => _sync.SyncHiddenDrivers(new HashSet<int>(), null, null, _views));
            _sync.CacheDrivers(40, view);
            Assert.That(_sync.DestroyedCachedDriverCount, Is.EqualTo(3));
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(3));
            Assert.DoesNotThrow(() => _sync.Reset());
        }

        [Test]
        public void HiddenSync_CanReplaceAndRemoveEntriesDuringTraversal()
        {
            _sync.CacheDrivers(40, CreateView(7));
            var view = CreateView(2);
            Assert.DoesNotThrow(() => _sync.SyncHiddenDrivers(new HashSet<int>(), null, null, _views));
            AssertDrivers(2, view);
            Assert.That(view.GetComponent<PlayerAnimatorDriver>().IsVisible, Is.False);
            _views.Clear();
            Assert.DoesNotThrow(() => _sync.SyncHiddenDrivers(new HashSet<int>(), null, null, _views));
        }

        [Test]
        public void EnemyReplacement_RestoresPersistentStateWithoutReplayingOneShots()
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            var state = SummonState(windup: true);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(state);
            var view = CreateView(1);
            _sync.CacheDrivers(40, view);
            var driver = view.GetComponent<EnemyAnimatorDriver>();
            Assert.That(driver.LastPresentationState.EntityId, Is.EqualTo(40));
            Assert.That(driver.LastPresentationState.AiMode, Is.EqualTo(state.AiMode));
            Assert.That(driver.WindupSignalCount, Is.Zero);
            Assert.That(driver.AttackSignalCount, Is.Zero);
            Assert.That(driver.HitSignalCount, Is.Zero);
        }

        [Test]
        public void ReplacementPreparationFailure_DoesNotPublishCandidate_AndSameCandidateCanRetry()
        {
            var oldView = CreateView(5);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(windup: true, dead: true));
            var replacement = CreateView(1);
            var invalidBinding = AddInvalidBinding(replacement);

            var failure = Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
            Assert.That(failure.Message, Does.Contain("at least one binding"));
            Assert.That(_sync.DriverCacheResolveCount, Is.EqualTo(1), "A failed candidate is not published.");
            _sync.CacheDrivers(40, oldView);
            Assert.That(_sync.DriverCacheHitCount, Is.EqualTo(1));

            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            var driver = replacement.GetComponent<EnemyAnimatorDriver>();
            Assert.That(driver.LastPresentationState.DidDie, Is.True);
            Assert.That(driver.DeathSignalCount, Is.Zero);
            Assert.That(driver.LastPresentationState.StartedSummonWindupThisTick, Is.False,
                "The recovery store must erase event flags, not preserve replayable commands.");
            Assert.That(_sync.DriverCacheResolveCount, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedReplacement_OldViewDestroyed_RetryRestoresValueSnapshot(bool observeMissingView)
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(windup: true, dead: true));
            var replacement = CreateView(1);
            var invalidBinding = AddInvalidBinding(replacement);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
            Object.DestroyImmediate(oldView.gameObject);
            if (observeMissingView) _sync.CacheDrivers(40, null);

            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            var driver = replacement.GetComponent<EnemyAnimatorDriver>();
            Assert.That(driver.LastPresentationState.DidDie, Is.True);
            Assert.That(driver.LastPresentationState.StartedSummonWindupThisTick, Is.False);
            Assert.That(driver.DeathSignalCount, Is.Zero);
            Assert.That(driver.UtilityWindupSignalCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedReplacement_ReleaseOrReset_DoesNotLeakSnapshotIntoReusedId(bool reset)
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(dead: true));
            var failedCandidate = CreateView(1);
            AddInvalidBinding(failedCandidate);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, failedCandidate));
            Object.DestroyImmediate(oldView.gameObject);
            if (reset) _sync.Reset(); else _sync.ReleaseEntity(40);

            var nextLifetime = CreateView(1);
            _sync.CacheDrivers(40, nextLifetime);
            Assert.That(nextLifetime.GetComponent<EnemyAnimatorDriver>().LastPresentationState.DidDie, Is.False);
            Assert.That(nextLifetime.GetComponent<EnemyAnimatorDriver>().LastPresentationState.EntityId, Is.Zero);
        }

        [Test]
        public void FailedReplacement_NewerSemanticInputOverridesSnapshotAndOldDriver()
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(dead: true));
            var replacement = CreateView(1);
            var invalidBinding = AddInvalidBinding(replacement);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));

            Assert.Throws<InvalidOperationException>(() => _sync.ApplyTickPresentation(
                Result(new[] { Enemy(40) }, TickPresentationData.Empty), _views, (_, _) => 0f));
            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().LastPresentationState.DidDie, Is.False,
                "New semantic input wins even when the previous cached driver is still alive.");
        }

        [Test]
        public void FailedReplacement_NewerDirectDeathSurvivesAnotherFailedAttempt()
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState());
            var replacement = CreateView(1);
            var invalidBinding = AddInvalidBinding(replacement);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
            Object.DestroyImmediate(oldView.gameObject);
            Assert.Throws<InvalidOperationException>(() => _sync.BeginEnemyDeathPresentation(40, _views));

            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().LastPresentationState.DidDie, Is.True);
            Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().DeathSignalCount, Is.Zero);
        }

        [Test]
        public void OldNormalizationFailure_PublishesPreparedReplacement_AndRethrowsOriginalException()
        {
            var oldView = CreateView(5);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(dead: true));
            var replacement = CreateView(1);
            var expected = new InvalidOperationException("old normalization failure");
            _sync.BeforeDriverBindingNormalizationForTests = _ => throw expected;
            try
            {
                var actual = Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
                Assert.That(actual, Is.SameAs(expected));
                Assert.That(actual.StackTrace, Does.Contain(nameof(GameplayAnimationSyncCoordinator)));
                Assert.That(_sync.DriverCacheResolveCount, Is.EqualTo(2));
                _sync.CacheDrivers(40, replacement);
                Assert.That(_sync.DriverCacheHitCount, Is.EqualTo(1));
                Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().LastPresentationState.DidDie, Is.True);
                Assert.That(_sync.TryGetEnemyScalePulseDriver(40, _views, out _), Is.False);
            }
            finally { _sync.BeforeDriverBindingNormalizationForTests = null; }
        }

        [Test]
        public void ReleaseNormalizationFailure_ClearsBindingsTracksHoldsAndFailureSnapshot()
        {
            var oldView = CreateView(7);
            _sync.CacheDrivers(40, oldView);
            _sync.ApplyTickPresentation(PlayerResult(dead: false, attempt: true), _views, (_, _) => 1f);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState(dead: true));
            var failedCandidate = CreateView(1);
            AddInvalidBinding(failedCandidate);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, failedCandidate));
            var expected = new InvalidOperationException("release normalization failure");
            _sync.BeforeDriverBindingNormalizationForTests = _ => throw expected;
            try
            {
                Assert.That(Assert.Throws<InvalidOperationException>(() => _sync.ReleaseEntity(40)), Is.SameAs(expected));
                Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
                Assert.DoesNotThrow(() => _sync.ReleaseEntity(40));
                var nextLifetime = CreateView(1);
                _sync.CacheDrivers(40, nextLifetime);
                Assert.That(nextLifetime.GetComponent<EnemyAnimatorDriver>().LastPresentationState.EntityId, Is.Zero);
            }
            finally { _sync.BeforeDriverBindingNormalizationForTests = null; }
        }

        [Test]
        public void FailedReplacement_NewerPlayerDeathRetiresOldHoldBeforeRetry()
        {
            var oldView = CreateView(3);
            _sync.CacheDrivers(40, oldView);
            _sync.ApplyTickPresentation(PlayerResult(dead: false, attempt: true), _views, (_, _) => 1f);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState());
            Assert.That(_sync.HasActivePlayerVisualHold, Is.True);
            var replacement = CreateView(3);
            var invalidBinding = AddInvalidBinding(replacement);

            // This input creates the first failure snapshot, then must supersede it.
            Assert.Throws<InvalidOperationException>(() => _sync.ApplyTickPresentation(
                PlayerResult(dead: true, attempt: false), _views, (_, _) => 1f));
            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Death));
            Object.DestroyImmediate(oldView.gameObject);
            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            var driver = replacement.GetComponent<PlayerAnimatorDriver>();
            Assert.That(driver.LastPresentationState.DidDie, Is.True);
            Assert.That(driver.LastPresentationState.DidDieThisTick, Is.False);
            Assert.That(driver.ActionStartSignalCount, Is.Zero);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Death));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedReplacement_NewerPlayerCancelRetiresActionHoldButPreservesDeathOverride(bool wasDead)
        {
            var oldView = CreateView(3);
            _sync.CacheDrivers(40, oldView);
            _sync.ApplyTickPresentation(PlayerResult(dead: false, attempt: true), _views, (_, _) => 1f);
            if (wasDead)
            {
                _sync.ApplyTickPresentation(PlayerResult(dead: true, attempt: false), _views, (_, _) => 1f);
            }
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState());
            var replacement = CreateView(3);
            var invalidBinding = AddInvalidBinding(replacement);
            Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
            Assert.Throws<InvalidOperationException>(() => _sync.ApplyTickPresentation(
                PlayerResult(dead: false, attempt: false, canceled: true), _views, (_, _) => 1f));

            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            var expected = wasDead ? PlayerViewAnimationState.Death : PlayerViewAnimationState.Idle;
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(expected));
            Object.DestroyImmediate(oldView.gameObject);
            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(expected));
            Assert.That(replacement.GetComponent<PlayerAnimatorDriver>().LastPresentationState.CanceledThisTick, Is.False);
            Assert.That(replacement.GetComponent<PlayerAnimatorDriver>().ActionStartSignalCount, Is.Zero);
        }

        private static EnemyAnimationBindingAuthoring AddInvalidBinding(GameplayEntityView view)
        {
            var binding = view.gameObject.AddComponent<EnemyAnimationBindingAuthoring>();
            binding.ConfigureForTests(Array.Empty<EnemyAnimationCueBinding>(), -1f);
            return binding;
        }

        [Test]
        public void PlayerHoldAndDeath_SurviveReplacement_AndMissingDriverClearsState()
        {
            var oldView = CreateView(2);
            _sync.CacheDrivers(40, oldView);
            _sync.ApplyTickPresentation(PlayerResult(dead: false, attempt: true), _views, (_, _) => 1f);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.True);
            _sync.AdvancePresentationBeforeEnemySemantic(0.1f);
            var before = _sync.ResolvePlayerAnimationPlayback(40, false, false);
            var view = CreateView(2);
            _sync.CacheDrivers(40, view);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.True);
            Assert.That(_sync.ResolvePlayerAnimationPlayback(40, false, false).State, Is.EqualTo(before.State));
            Assert.That(view.GetComponent<PlayerAnimatorDriver>().ActionStartSignalCount, Is.Zero);
            _sync.ApplyTickPresentation(PlayerResult(dead: true, attempt: false), _views, (_, _) => 1f);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Death));
            _sync.CacheDrivers(40, CreateView(2));
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Death));
            var missing = CreateView(0);
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out _), Is.False);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Idle));
            _sync.CacheDrivers(40, missing);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Idle));
        }

        [Test]
        public void FrozenNegativePlayer_MapperSeesAddition_LazyCleanupDoesNotInvalidateIteration()
        {
            var view = CreateView(0);
            _sync.CacheDrivers(40, view);
            view.gameObject.AddComponent<PlayerAnimatorDriver>();
            Assert.DoesNotThrow(() => _sync.ApplyTickPresentation(PlayerResult(false, true), _views, (_, _) => 1f));
            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Idle));
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(3));
        }

        [Test]
        public void CampaignCatalog_AllTenPrefabs_ResolveAndApplyInitialAndTick()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyPresentationCatalog>(GameplayDriverCachePerformanceTests.CatalogPath);
            Assert.That(catalog.Entries.Length, Is.EqualTo(10));
            var prefabs = new Dictionary<int, GameplayEntityView>();
            var entities = new List<EntityState>();
            for (var i = 0; i < catalog.Entries.Length; i++) prefabs.Add(40 + i, catalog.Entries[i].ViewPrefab);
            var factory = new DefaultGameplayEntityViewFactory(_root.transform, 1f, 10, enemyViewPrefabsByEntityId: prefabs);
            var pulseCount = 0;
            for (var i = 0; i < catalog.Entries.Length; i++)
            {
                var entity = Enemy(40 + i);
                var view = factory.CreateView(entity);
                entities.Add(entity);
                _views[entity.entityId] = view;
                Assert.That(view.GetComponent<EnemyAnimatorDriver>(), Is.Not.Null, catalog.Entries[i].PresentationId);
                Assert.That(view.GetComponent<PlayerAnimatorDriver>(), Is.Null);
                var pulse = view.GetComponent<EnemySummonScalePulsePresentationDriver>();
                if (pulse != null)
                {
                    pulseCount++;
                    Assert.That(catalog.Entries[i].ViewPrefab.name, Is.EqualTo("EnemyView_JPeter"));
                }
                _sync.CacheDrivers(entity.entityId, view);
            }
            Assert.That(pulseCount, Is.EqualTo(1));
            _sync.ApplyInitialEnemyPresentation(entities, new Dictionary<int, GameplayEntityPose>(), _views);
            _sync.ApplyTickPresentation(Result(entities, TickPresentationData.Empty), _views, (_, _) => 1f);
            foreach (var entity in entities)
            {
                _sync.CacheDrivers(entity.entityId, _views[entity.entityId]);
                Assert.That(_views[entity.entityId].GetComponent<EnemyAnimatorDriver>().LastPresentationState.EntityId,
                    Is.EqualTo(entity.entityId));
            }
            Assert.That(_sync.DriverComponentLookupCount, Is.EqualTo(30));
        }

        [Test]
        public void StationaryEnemyReplacement_AppliesPoseVisibilityAndSemanticBeforeSkip()
        {
            var presenter = _root.AddComponent<GameplayTickViewPresenter>();
            GameplayPresentationTestCompositionBuilder.BindPresenter(presenter);
            var registry = _root.AddComponent<GameplayEntityViewRegistry>();
            var oldView = CreateView(1);
            registry.Register(oldView);
            var topology = new CubeTopologyState(FaceId.Floor);
            presenter.Initialize(new GameplayEntityViewBinder(registry, null),
                new BoardBounds(Vector2Int.zero, new Vector2Int(1, 1)), topology, 1f, new GameplayTimingProfile(60, 0f, 0.4f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.65f, 8));
            var enemy = Enemy(40);
            enemy.position = new SurfaceCell(FaceId.Front, 0, 0);
            presenter.PresentInitial(new[] { enemy }, topology);
            presenter.UpdatePresentation(0f);
            Assert.That(presenter.DebugLastEntityPresentationApplyDiagnostics.EnemySkippedCount, Is.EqualTo(1));
            var expectedPosition = oldView.transform.localPosition;
            var view = CreateView(1);
            var recorder = view.gameObject.AddComponent<CacheSemanticRecorder>();
            view.transform.localPosition = Vector3.one * 99f;
            view.gameObject.SetActive(false);
            registry.Register(view);
            presenter.Present(Result(new[] { enemy }, TickPresentationData.Empty));
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(recorder.ApplyCount, Is.GreaterThan(0));
            Assert.That(recorder.State.ShouldPauseAutonomousPresentation, Is.True);
            Assert.That(presenter.DebugLastEntityPresentationApplyDiagnostics.EnemySkippedCount, Is.Zero);
            presenter.UpdatePresentation(0f);
            Assert.That(presenter.DebugLastEntityPresentationApplyDiagnostics.EnemySkippedCount, Is.EqualTo(1));
        }

        [Test]
        public void EnemyUtilityTrack_ReplacementPreservesRemainingTimeWithoutReplayingWindup()
        {
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 0.3f, 1f));
            try
            {
                var oldView = CreateView(1);
                ConfigureUtilityTiming(oldView, clip);
                _sync.CacheDrivers(40, oldView);
                var data = new TickPresentationData(
                    Array.Empty<TickEntityMotion>(), null, Array.Empty<TickVisibilityChange>(), Array.Empty<TickTransitionVisibilityChange>(),
                    Array.Empty<TickPlayerActionPresentationSignal>(), Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                    Array.Empty<TickPlayerDamagePresentationSignal>(), Array.Empty<TickPlayerDeathPresentationSignal>(),
                    Array.Empty<TickEnemyDamagePresentationSignal>(), Array.Empty<TickEnemyActionPresentationSignal>(),
                    Array.Empty<TickEnemyJumpPresentationSignal>(), Array.Empty<TickEntityExitPresentationSignal>(),
                    Array.Empty<FlipImpactPresentationSignal>(),
                    enemyUtilitySignals: new[] { new TickEnemyUtilityPresentationSignal(40,
                        EnemyUtilityPresentationKind.GravityFieldAura, EnemyUtilityPresentationPhase.WindupStarted,
                        startTick: 1, executeTick: 3, durationTicks: 2, effectIndex: 0, activationSequence: 5) });
                _sync.ApplyTickPresentation(Result(new[] { Enemy(40) }, data), _views, (_, _) => 0f);
                _sync.AdvancePresentationBeforeEnemySemantic(0.2f);
                _sync.ApplyTickPresentation(Result(new[] { Enemy(40) }, TickPresentationData.Empty), _views, (_, _) => 0f);
                var view = CreateView(1);
                ConfigureUtilityTiming(view, clip);
                _sync.CacheDrivers(40, view);
                var driver = view.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(driver.UtilityWindupSignalCount, Is.Zero);
                _sync.AdvancePresentationBeforeEnemySemantic(0.29f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                _sync.AdvancePresentationBeforeEnemySemantic(0.02f);
                Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void FailedUtilityReplacement_RetryUsesCurrentTrackTimeAndHonorsExpiryOrNewCancel(bool expire, bool cancel)
        {
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 0.3f, 1f));
            try
            {
                var oldView = CreateView(1);
                ConfigureUtilityTiming(oldView, clip);
                _sync.CacheDrivers(40, oldView);
                _sync.ApplyTickPresentation(UtilityResult(EnemyUtilityPresentationPhase.WindupStarted), _views, (_, _) => 0f);
                _sync.AdvancePresentationBeforeEnemySemantic(0.1f);
                var replacement = CreateView(1);
                ConfigureUtilityTiming(replacement, clip);
                var invalidBinding = AddInvalidBinding(replacement);
                Assert.Throws<InvalidOperationException>(() => _sync.CacheDrivers(40, replacement));
                Object.DestroyImmediate(oldView.gameObject);
                _sync.AdvancePresentationBeforeEnemySemantic(expire ? 0.5f : 0.2f);
                if (cancel)
                {
                    Assert.Throws<InvalidOperationException>(() => _sync.ApplyTickPresentation(
                        UtilityResult(EnemyUtilityPresentationPhase.Canceled), _views, (_, _) => 0f));
                }

                Object.DestroyImmediate(invalidBinding);
                _sync.CacheDrivers(40, replacement);
                var driver = replacement.GetComponent<EnemyAnimatorDriver>();
                Assert.That(driver.UtilityWindupSignalCount, Is.Zero);
                Assert.That(driver.LastPresentationState.StartedUtilityWindupThisTick, Is.False);
                if (expire || cancel)
                {
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero);
                }
                else
                {
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                    _sync.AdvancePresentationBeforeEnemySemantic(0.19f);
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.EqualTo(0.5f).Within(0.0001f));
                    _sync.AdvancePresentationBeforeEnemySemantic(0.02f);
                    Assert.That(driver.CurrentPresentationDurationSeconds, Is.Zero,
                        "The retry uses the continuing track, not the elapsed time at failure.");
                }
            }
            finally { Object.DestroyImmediate(clip); }
        }

        [Test]
        public void IncomingDeathOnFirstFailedReplacement_RetryKeepsDeathAfterOldViewIsDestroyed()
        {
            var oldView = CreateView(1);
            _sync.CacheDrivers(40, oldView);
            oldView.GetComponent<EnemyAnimatorDriver>().Apply(SummonState());
            var replacement = CreateView(1);
            var invalidBinding = AddInvalidBinding(replacement);
            Assert.Throws<InvalidOperationException>(() => _sync.BeginEnemyDeathPresentation(40, _views));
            Object.DestroyImmediate(oldView.gameObject);
            Object.DestroyImmediate(invalidBinding);
            _sync.CacheDrivers(40, replacement);
            Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().LastPresentationState.DidDie, Is.True);
            Assert.That(replacement.GetComponent<EnemyAnimatorDriver>().DeathSignalCount, Is.Zero);
        }

        private static TickResult UtilityResult(EnemyUtilityPresentationPhase phase)
        {
            var data = new TickPresentationData(
                Array.Empty<TickEntityMotion>(), null, Array.Empty<TickVisibilityChange>(), Array.Empty<TickTransitionVisibilityChange>(),
                Array.Empty<TickPlayerActionPresentationSignal>(), Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(), Array.Empty<TickPlayerDeathPresentationSignal>(),
                Array.Empty<TickEnemyDamagePresentationSignal>(), Array.Empty<TickEnemyActionPresentationSignal>(),
                Array.Empty<TickEnemyJumpPresentationSignal>(), Array.Empty<TickEntityExitPresentationSignal>(),
                Array.Empty<FlipImpactPresentationSignal>(),
                enemyUtilitySignals: new[] { new TickEnemyUtilityPresentationSignal(40,
                    EnemyUtilityPresentationKind.GravityFieldAura, phase,
                    startTick: 1, executeTick: 3, durationTicks: 2, effectIndex: 0, activationSequence: 5) });
            return Result(new[] { Enemy(40) }, data);
        }

        private static void ConfigureUtilityTiming(GameplayEntityView view, AnimationClip clip)
        {
            var authoring = view.gameObject.AddComponent<EnemyAnimationTimingAuthoring>();
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("attackWindupAnimatorDurationSeconds").floatValue = 0.5f;
            serialized.FindProperty("attackWindupReferenceClip").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void HiddenMissingView_ClearsPlayerHoldEvenWhenEnemyLookupRunsFirst()
        {
            _sync.CacheDrivers(40, CreateView(7));
            _sync.ApplyTickPresentation(PlayerResult(false, true), _views, (_, _) => 1f);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.True);
            _views.Clear();
            _sync.SyncHiddenDrivers(new HashSet<int>(), null, null, _views);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Idle));
        }

        [Test]
        public void LazyEnemyLookupOfPlayerlessReplacement_ClearsPlayerState()
        {
            _sync.CacheDrivers(40, CreateView(2));
            _sync.ApplyTickPresentation(PlayerResult(false, true), _views, (_, _) => 1f);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.True);
            CreateView(1);
            Assert.That(_sync.TryGetEnemyAnimatorDriver(40, _views, out _), Is.True);
            Assert.That(_sync.HasActivePlayerVisualHold, Is.False);
            Assert.That(_sync.ResolvePlayerAnimationState(40, false, false), Is.EqualTo(PlayerViewAnimationState.Idle));
        }

        private GameplayEntityView CreateView(int mask)
        {
            var obj = new GameObject("CacheView");
            obj.transform.SetParent(_root.transform, false);
            var view = obj.AddComponent<GameplayEntityView>();
            view.Initialize(40);
            if ((mask & 1) != 0) obj.AddComponent<EnemyAnimatorDriver>();
            if ((mask & 2) != 0)
            {
                obj.AddComponent<PlayerAnimationTimingAuthoring>();
                obj.AddComponent<PlayerAnimatorDriver>();
            }
            if ((mask & 4) != 0) obj.AddComponent<EnemySummonScalePulsePresentationDriver>();
            _views[40] = view;
            return view;
        }

        private void AssertDrivers(int mask, GameplayEntityView view)
        {
            Assert.That(_sync.TryGetEnemyAnimatorDriver(40, _views, out var enemy), Is.EqualTo((mask & 1) != 0));
            Assert.That(enemy, Is.SameAs(view.GetComponent<EnemyAnimatorDriver>()));
            Assert.That(_sync.TryGetPlayerAnimatorDriver(40, _views, out var player), Is.EqualTo((mask & 2) != 0));
            Assert.That(player, Is.SameAs(view.GetComponent<PlayerAnimatorDriver>()));
            Assert.That(_sync.TryGetEnemyScalePulseDriver(40, _views, out var pulse), Is.EqualTo((mask & 4) != 0));
            Assert.That(pulse, Is.SameAs(view.GetComponent<EnemySummonScalePulsePresentationDriver>()));
        }

        private static EntityState Enemy(int id) => new EntityState
        {
            entityId = id, type = EntityType.Unit, unitRole = UnitRole.Enemy,
            position = new SurfaceCell(FaceId.Floor, 0, 0), hp = 1, maxHp = 1,
            boardPresence = EntityBoardPresence.Occupying, facing = Direction.Right,
            aiMode = EnemyAiMode.Patrol,
        };

        internal static EnemyViewPresentationState SummonState(bool windup = false, bool recover = false,
            bool canceled = false, bool dead = false) => new EnemyViewPresentationState(
                40, 1, EnemyAiMode.Patrol, EnemyActionKind.None, EnemyJumpPhase.None, EnemyChargePhase.None,
                false, false, false, false, false, false, false, false, false, false, false, false, dead,
                startedSummonWindupThisTick: windup, startedSummonRecoverThisTick: recover, summonCanceledThisTick: canceled);

        private static TickResult PlayerResult(bool dead, bool attempt, bool canceled = false)
        {
            var entity = Enemy(40);
            entity.unitRole = UnitRole.Player;
            entity.hp = dead ? 0 : 1;
            return Result(new[] { entity }, new TickPresentationData(
                Array.Empty<TickEntityMotion>(), null, Array.Empty<TickVisibilityChange>(), Array.Empty<TickTransitionVisibilityChange>(),
                canceled ? new[] { new TickPlayerActionPresentationSignal(40, PlayerActionKind.None,
                    activeActionSequence: 1, startedThisTick: false, completedThisTick: false, canceledThisTick: true) }
                    : Array.Empty<TickPlayerActionPresentationSignal>(), Array.Empty<TickPlayerLocomotionPresentationSignal>(),
                Array.Empty<TickPlayerDamagePresentationSignal>(), Array.Empty<TickEnemyDamagePresentationSignal>(),
                Array.Empty<TickEnemyActionPresentationSignal>(), Array.Empty<TickEnemyJumpPresentationSignal>(),
                Array.Empty<TickEntityExitPresentationSignal>(),
                playerActionAttemptSignals: attempt ? new[] { new TickPlayerActionAttemptPresentationSignal(40,
                    PlayerActionKind.Push, Direction.Right, PlayerActionAttemptFeedbackKind.NoTarget) } : null));
        }

        private static TickResult Result(IEnumerable<EntityState> entities, TickPresentationData data) => new TickResult(
            1, Array.Empty<TickPhase>(), Array.Empty<string>(), MovementPhaseResult.Empty, AttackPhaseResult.Empty,
            entities, Array.Empty<string>(), new CubeTopologyState(FaceId.Floor), data, string.Empty, TickTrace.Empty);

        private sealed class CacheSemanticRecorder : MonoBehaviour, IEnemyVisualSemanticPresentationDriver
        {
            public int ApplyCount { get; private set; }
            public EnemyVisualSemanticState State { get; private set; }
            public void ApplyEnemyVisualSemanticState(in EnemyVisualSemanticState state) { ApplyCount++; State = state; }
        }
    }
}
