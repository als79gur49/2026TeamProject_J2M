using System;
using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Feature.Gameplay.Tests.Infrastructure
{
    [Category("Full")]
    public sealed class EnemyAnimationBindingAuthoringTests
    {
        [Test]
        public void RootEnabledSingleComponent_IsSelectedAndValidated()
        {
            using var fixture = Fixture.Create();
            var authoring = fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>();
            ConfigureValid(authoring);

            Assert.That(
                EnemyAnimationBindingAuthoring.GetOptionalValidatedRoot(fixture.View),
                Is.SameAs(authoring));
        }

        [Test]
        public void ChildComponent_IsHardFailure()
        {
            using var fixture = Fixture.Create();
            var child = new GameObject("Child");
            child.transform.SetParent(fixture.Root.transform, false);
            ConfigureValid(child.AddComponent<EnemyAnimationBindingAuthoring>());

            Assert.Throws<InvalidOperationException>(
                () => EnemyAnimationBindingAuthoring.GetOptionalValidatedRoot(fixture.View));
        }

        [Test]
        public void DisabledComponent_IsHardFailure()
        {
            using var fixture = Fixture.Create();
            var authoring = fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>();
            ConfigureValid(authoring);
            authoring.enabled = false;

            Assert.Throws<InvalidOperationException>(
                () => EnemyAnimationBindingAuthoring.GetOptionalValidatedRoot(fixture.View));
        }

        [Test]
        public void RootAndChildDuplicateComponents_AreHardFailure()
        {
            using var fixture = Fixture.Create();
            ConfigureValid(fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>());
            var child = new GameObject("DuplicateChild");
            child.transform.SetParent(fixture.Root.transform, false);
            ConfigureValid(child.AddComponent<EnemyAnimationBindingAuthoring>());

            Assert.Throws<InvalidOperationException>(
                () => EnemyAnimationBindingAuthoring.GetOptionalValidatedRoot(fixture.View));
        }

        [Test]
        public void ValidNewBinding_WinsAndInvalidLegacyTimingIsNotValidated()
        {
            using var fixture = Fixture.Create();
            var authoring = fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>();
            ConfigureValid(authoring);
            var legacy = fixture.Root.AddComponent<EnemyAnimationTimingAuthoring>();
            var serialized = new SerializedObject(legacy);
            serialized.FindProperty("stateTransitionCrossFadeDurationSeconds").floatValue = float.NaN;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.DoesNotThrow(
                () => EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(fixture.View, "test"));
        }

        [Test]
        public void InvalidNewBinding_DoesNotFallBackToValidLegacyTiming()
        {
            using var fixture = Fixture.Create();
            fixture.Root.AddComponent<EnemyAnimationTimingAuthoring>();
            var authoring = fixture.Root.AddComponent<EnemyAnimationBindingAuthoring>();
            authoring.ConfigureForTests(Array.Empty<EnemyAnimationCueBinding>(), -1f);

            Assert.Throws<InvalidOperationException>(
                () => EnemyViewPrefabRequirements.ValidateEnemyViewPrefab(fixture.View, "test"));
        }

        private static void ConfigureValid(EnemyAnimationBindingAuthoring authoring)
        {
            authoring.ConfigureForTests(
                new[]
                {
                    EnemyAnimationCueBinding.CreateForTests(
                        EnemyAnimationCue.ActionExecute,
                        EnemyAnimationDispatchMode.Trigger,
                        "Attack"),
                },
                -1f);
        }

        private sealed class Fixture : IDisposable
        {
            private Fixture(GameObject root, GameplayEntityView view)
            {
                Root = root;
                View = view;
            }

            public GameObject Root { get; }
            public GameplayEntityView View { get; }

            public static Fixture Create()
            {
                var root = new GameObject(nameof(EnemyAnimationBindingAuthoringTests));
                var view = root.AddComponent<GameplayEntityView>();
                root.AddComponent<EnemyAnimatorDriver>();
                return new Fixture(root, view);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
        }
    }
}
