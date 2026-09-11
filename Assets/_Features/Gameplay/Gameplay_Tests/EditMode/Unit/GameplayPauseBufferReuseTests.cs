using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Unit
{
    public sealed class GameplayPauseBufferReuseTests
    {
        private sealed class PauseCacheObjects : IDisposable
        {
            private readonly List<GameObject> roots = new();
            public GameObject Root(string name = "PauseCacheRoot")
            {
                var root = new GameObject(name);
                roots.Add(root);
                return root;
            }
            public void Dispose()
            {
                foreach (var root in roots)
                    if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public class PauseCacheRecorder : MonoBehaviour
        {
            public readonly List<bool> Calls = new();
        }
        public sealed class PauseCacheFallback : PauseCacheRecorder
        {
            public Action<bool> Callback;
            public void SetPresentationPaused(bool paused) { Calls.Add(paused); Callback?.Invoke(paused); }
        }
        public sealed class PauseComparisonInterface : MonoBehaviour, IGameplayPresentationPausable
        {
            public readonly List<bool> Calls = new();
            public Action<bool> Callback;
            public Action OnEquals;
            public void SetPresentationPaused(bool paused) { Calls.Add(paused); Callback?.Invoke(paused); }
            public override bool Equals(object other)
            {
                var callback = OnEquals;
                OnEquals = null;
                callback?.Invoke();
                return ReferenceEquals(this, other);
            }
            public override int GetHashCode() => base.GetHashCode();
        }

        [Test]
        [Category("Integration")]
        public void PauseBuffers_PausedReentryPreservesThreeOuterTargetsAndOrder()
        {
            using var objects = new PauseCacheObjects();
            var outer = objects.Root(); var nested = objects.Root();
            var order = new List<string>();
            var first = outer.AddComponent<PauseCacheFallback>();
            var second = outer.AddComponent<PauseComparisonInterface>();
            var third = outer.AddComponent<PauseCacheFallback>();
            var inner = nested.AddComponent<PauseCacheFallback>();
            var registry = new GameplayPresentationPauseRegistry();
            registry.SetPresentationPaused(true);
            first.Callback = paused => { if (paused) { order.Add("first"); registry.RegisterRoot(nested); } };
            inner.Callback = paused => { if (paused) order.Add("inner"); };
            second.Callback = paused => { if (paused) order.Add("second"); };
            third.Callback = paused => { if (paused) order.Add("third"); };
            registry.RegisterRoot(outer);
            Assert.That(order, Is.EqualTo(new[] { "first", "inner", "second", "third" }));
            registry.SetPresentationPaused(false);
            Assert.That(first.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(second.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(third.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(inner.Calls, Is.EqualTo(new[] { true, false }));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        [Category("Integration")]
        public void PauseBuffers_ClearAndExceptionReleaseOwnedBuffers(bool throws, bool reflected)
        {
            using var objects = new PauseCacheObjects();
            var root = objects.Root(); var nested = objects.Root();
            var nestedProbe = nested.AddComponent<PauseCacheFallback>();
            var registry = new GameplayPresentationPauseRegistry();
            var sentinel = new InvalidOperationException("pause comparison sentinel");
            Action<bool> callback = paused =>
            {
                if (!paused) return;
                registry.Clear();
                registry.RegisterRoot(nested);
                if (throws) throw sentinel;
            };
            PauseComparisonInterface direct = null;
            PauseCacheFallback fallback = null;
            if (reflected) { fallback = root.AddComponent<PauseCacheFallback>(); fallback.Callback = callback; }
            else { direct = root.AddComponent<PauseComparisonInterface>(); direct.Callback = callback; }
            var second = root.AddComponent<PauseCacheFallback>();
            var third = root.AddComponent<PauseCacheFallback>();
            registry.SetPresentationPaused(true);
            if (!throws) registry.RegisterRoot(root);
            else if (reflected)
                Assert.That(Assert.Throws<TargetInvocationException>(() => registry.RegisterRoot(root)).InnerException, Is.SameAs(sentinel));
            else Assert.That(Assert.Throws<InvalidOperationException>(() => registry.RegisterRoot(root)), Is.SameAs(sentinel));
            Assert.That(registry.IsPaused, Is.False);
            Assert.That(second.Calls, Is.Empty);
            Assert.That(third.Calls, Is.Empty);
            // Every owned call must release references and ownership, including failure paths.
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var busy = typeof(GameplayPresentationPauseRegistry).GetField("registrationBuffersInUse", flags);
            Assert.That(busy, Is.Not.Null);
            Assert.That(busy.GetValue(registry), Is.False);
            foreach (var name in new[] { "behaviourBuffer", "animatorBuffer", "particleBuffer" })
            {
                var field = typeof(GameplayPresentationPauseRegistry).GetField(name, flags);
                Assert.That(field, Is.Not.Null);
                Assert.That(((System.Collections.ICollection)field.GetValue(registry)).Count, Is.Zero);
            }
            if (direct != null) direct.Callback = null;
            if (fallback != null) fallback.Callback = null;
            registry.RegisterRoot(root);
            registry.SetPresentationPaused(true);
            registry.SetPresentationPaused(false);
            Assert.That(second.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(third.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(nestedProbe.Calls, Is.EqualTo(new[] { true, false }));
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Integration")]
        public void PauseBuffers_CallbackAddedDisplaysAreDiscoveredInSameCall(bool duringEquals)
        {
            using var objects = new PauseCacheObjects();
            var priorRoot = objects.Root(); var root = objects.Root();
            var prior = priorRoot.AddComponent<PauseComparisonInterface>();
            var candidate = root.AddComponent<PauseComparisonInterface>();
            var registry = new GameplayPresentationPauseRegistry();
            registry.RegisterRoot(priorRoot);
            Animator addedAnimator = null; ParticleSystem addedParticles = null;
            Action mutate = () =>
            {
                var child = new GameObject("late displays"); child.transform.SetParent(root.transform, false);
                addedAnimator = child.AddComponent<Animator>(); addedAnimator.speed = 0.75f;
                addedParticles = child.AddComponent<ParticleSystem>(); addedParticles.Play();
            };
            if (duringEquals)
                prior.OnEquals = () => { mutate(); registry.SetPresentationPaused(true); };
            else
            {
                registry.SetPresentationPaused(true);
                candidate.Callback = paused => { if (paused) mutate(); };
            }
            registry.RegisterRoot(root);
            Assert.That(addedAnimator, Is.Not.Null);
            Assert.That(addedAnimator.speed, Is.Zero);
            Assert.That(addedParticles.isPaused, Is.True);
            Assert.That(candidate.Calls, Is.EqualTo(new[] { true }));
            candidate.Callback = null;
            registry.SetPresentationPaused(false);
            Assert.That(addedAnimator.speed, Is.EqualTo(0.75f));
            Assert.That(addedParticles.isPlaying, Is.True);
        }

        [Test]
        [Category("Integration")]
        public void PauseBuffers_EqualsClearAndReentryPreserveLaterCandidates()
        {
            using var objects = new PauseCacheObjects();
            var priorRoot = objects.Root(); var root = objects.Root(); var nested = objects.Root();
            var prior = priorRoot.AddComponent<PauseComparisonInterface>();
            var first = root.AddComponent<PauseComparisonInterface>();
            var second = root.AddComponent<PauseCacheFallback>();
            var third = root.AddComponent<PauseCacheFallback>();
            var inner = nested.AddComponent<PauseCacheFallback>();
            var registry = new GameplayPresentationPauseRegistry(); registry.RegisterRoot(priorRoot);
            prior.OnEquals = () => { registry.Clear(); registry.RegisterRoot(nested); registry.SetPresentationPaused(true); };
            registry.RegisterRoot(root);
            Assert.That(first.Calls, Is.EqualTo(new[] { true }));
            Assert.That(second.Calls, Is.EqualTo(new[] { true }));
            Assert.That(third.Calls, Is.EqualTo(new[] { true }));
            Assert.That(inner.Calls, Is.EqualTo(new[] { true }));
            registry.SetPresentationPaused(false);
            Assert.That(third.Calls, Is.EqualTo(new[] { true, false }));
        }


        [Test, Category("Integration")]
        public void PauseBuffers_InactiveAndLateTargetsRemainDiscoverableWithoutDuplicates()
        {
            using var objects = new PauseCacheObjects();
            var root = objects.Root();
            var child = objects.Root(); child.transform.SetParent(root.transform, false);
            child.SetActive(false);
            var direct = child.AddComponent<PauseComparisonInterface>();
            var registry = new GameplayPresentationPauseRegistry();
            registry.RegisterRoot(root);
            var late = child.AddComponent<PauseCacheFallback>();
            var animator = child.AddComponent<Animator>(); animator.speed = 0.75f;
            registry.RegisterRoot(root);
            registry.RegisterRoot(root);
            registry.SetPresentationPaused(true);
            Assert.That(direct.Calls, Is.EqualTo(new[] { true }));
            Assert.That(late.Calls, Is.EqualTo(new[] { true }));
            Assert.That(animator.speed, Is.Zero);
            registry.SetPresentationPaused(false);
            Assert.That(late.Calls, Is.EqualTo(new[] { true, false }));
            Assert.That(animator.speed, Is.EqualTo(0.75f));
        }

        [Test, Category("Integration")]
        public void PauseBuffers_DetachedPooledAndDestroyedTargetsPreserveRegistryLifetime()
        {
            using var objects = new PauseCacheObjects();
            var root = objects.Root(); var pool = objects.Root(); var child = objects.Root();
            child.transform.SetParent(root.transform, false);
            var target = child.AddComponent<PauseCacheFallback>();
            var registry = new GameplayPresentationPauseRegistry();
            registry.RegisterRoot(root);
            child.transform.SetParent(pool.transform, false);
            registry.SetPresentationPaused(true);
            Assert.That(target.Calls, Is.EqualTo(new[] { true }));
            registry.SetPresentationPaused(false);
            child.transform.SetParent(root.transform, false);
            registry.RegisterRoot(root);
            registry.SetPresentationPaused(true);
            Assert.That(target.Calls, Is.EqualTo(new[] { true, false, true }));
            UnityEngine.Object.DestroyImmediate(child);
            registry.SetPresentationPaused(false);
            var replacement = root.AddComponent<PauseCacheFallback>();
            registry.RegisterRoot(root);
            registry.SetPresentationPaused(true);
            Assert.That(replacement.Calls, Is.EqualTo(new[] { true }));
        }
    }
}
