using Game.Feature.Gameplay.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Feature.Gameplay.Tests.Unit
{
    [Category("Extended")]
    public sealed class GameplayPlayerActionCountEffectDriverTests
    {
        private const string PrefabPath =
            "Assets/_Features/Gameplay/Gameplay_Host/Prefabs/PlayerActionCountView.prefab";

        [Test]
        public void IncrementEffect_AdvancesDeterministicallyAndNormalizesAtCompletion()
        {
            var instance = InstantiateView();
            try
            {
                var view = instance.GetComponent<GameplayPlayerActionCountView>();
                var driver = instance.GetComponent<GameplayPlayerActionCountEffectDriver>();

                view.ShowCountWithIncrementEffect(1);

                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(driver.DebugPlayCount, Is.EqualTo(1));
                Assert.That(driver.DebugMotionAnchoredPosition.y, Is.EqualTo(-6f).Within(0.001f));
                Assert.That(driver.DebugMotionLocalScale.x, Is.EqualTo(0.70f).Within(0.001f));
                Assert.That(driver.DebugGlowLocalScale.x, Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(driver.DebugGlowColor.a, Is.EqualTo(0.90f).Within(0.001f));

                driver.Advance(0.12f);

                Assert.That(driver.IsPlaying, Is.True);
                Assert.That(driver.DebugMotionAnchoredPosition.y, Is.EqualTo(6f).Within(0.02f));
                Assert.That(driver.DebugMotionLocalScale.x, Is.EqualTo(1.12f).Within(0.02f));
                Assert.That(driver.DebugGlowLocalScale.x, Is.GreaterThan(0.65f));
                Assert.That(driver.DebugGlowColor.a, Is.LessThan(0.90f));

                driver.Advance(0.12f);

                Assert.That(driver.IsPlaying, Is.False);
                Assert.That(driver.DebugElapsedSeconds, Is.Zero);
                Assert.That(driver.DebugMotionAnchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(driver.DebugMotionLocalScale, Is.EqualTo(Vector3.one));
                Assert.That(driver.DebugGlowLocalScale, Is.EqualTo(Vector3.one));
                Assert.That(driver.DebugGlowColor.a, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RapidIncrement_RestartsFromAuthoredStateWithoutAccumulation()
        {
            var instance = InstantiateView();
            try
            {
                var view = instance.GetComponent<GameplayPlayerActionCountView>();
                var driver = instance.GetComponent<GameplayPlayerActionCountEffectDriver>();

                view.ShowCountWithIncrementEffect(1);
                driver.Advance(0.06f);
                view.ShowCountWithIncrementEffect(2);

                Assert.That(driver.DebugPlayCount, Is.EqualTo(2));
                Assert.That(driver.DebugElapsedSeconds, Is.Zero);
                Assert.That(driver.DebugMotionAnchoredPosition.y, Is.EqualTo(-6f).Within(0.001f));
                Assert.That(driver.DebugMotionLocalScale.x, Is.EqualTo(0.70f).Within(0.001f));

                view.ShowCount(2);
                Assert.That(driver.DebugPlayCount, Is.EqualTo(2), "A passive refresh must not replay the effect.");

                view.Hide();
                Assert.That(instance.activeSelf, Is.False);
                Assert.That(driver.IsPlaying, Is.False);
                Assert.That(driver.DebugMotionAnchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(driver.DebugMotionLocalScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NegativeAdvance_IsRejected()
        {
            var instance = InstantiateView();
            try
            {
                var view = instance.GetComponent<GameplayPlayerActionCountView>();
                var driver = instance.GetComponent<GameplayPlayerActionCountEffectDriver>();
                view.ShowCountWithIncrementEffect(1);

                Assert.That(() => driver.Advance(-0.01f), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GameObject InstantiateView()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            Assert.That(instance.GetComponentInChildren<Image>(includeInactive: true), Is.Not.Null);
            return instance;
        }
    }
}
