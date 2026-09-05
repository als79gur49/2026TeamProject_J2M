using System.Reflection;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SurfaceBeltButtonBadgeViewTests
    {
        private const string AllIn1UiMaskShaderName = "AllIn1SpriteShader/AllIn1SpriteShaderUiMask";

        [TestCase(0, 0)]
        [TestCase(2, 0)]
        [TestCase(0, 3)]
        [TestCase(2, 3)]
        public void Bind_UsesEachButtonTypeRemainderIndependently(int normalRemaining, int moonBlockOnlyRemaining)
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();
            Material shineMaterial = null;

            try
            {
                var group = CreateGroup(
                    root.transform,
                    out var badge,
                    out var frame,
                    out var background,
                    out var motionRoot,
                    out shineMaterial);

                group.Bind(
                    new SurfaceBeltButtonRemainderViewModel(0, normalRemaining, moonBlockOnlyRemaining),
                    styleProfile);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(badge.gameObject.activeSelf, Is.True);
                var normalColor = normalRemaining > 0
                    ? styleProfile.NormalButton.Active.BackgroundColor
                    : new Color(0.5f, 0.5f, 0.5f, 1.0f);
                var moonColor = moonBlockOnlyRemaining > 0
                    ? styleProfile.MoonButton.Active.BackgroundColor
                    : new Color(0.5f, 0.5f, 0.5f, 1.0f);
                Assert.That(frame.color, Is.EqualTo(normalColor));
                Assert.That(background.color, Is.EqualTo(normalColor));
                Assert.That(group.MoonBadge.gameObject.activeSelf, Is.True);
                Assert.That(GetFieldValue<Image>(group.MoonBadge, "_frame").color, Is.EqualTo(moonColor));
                Assert.That(GetFieldValue<Image>(group.MoonBadge, "_background").color, Is.EqualTo(moonColor));
                Assert.That(motionRoot.localScale, Is.EqualTo(
                    Vector3.one * (normalRemaining > 0 ? 1.0f : styleProfile.Transition.InactiveScale)));
                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shineMaterial);
                Object.DestroyImmediate(styleProfile);
            }
        }

        [Test]
        public void Bind_UsesInactiveStyleWhenNoButtonRemains()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();
            Material shineMaterial = null;

            try
            {
                var group = CreateGroup(
                    root.transform,
                    out var badge,
                    out var frame,
                    out var background,
                    out var motionRoot,
                    out shineMaterial);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 0, 0), styleProfile);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(badge.gameObject.activeSelf, Is.True);
                Assert.That(frame.color, Is.EqualTo(styleProfile.NormalButton.Inactive.BackgroundColor));
                Assert.That(background.color, Is.EqualTo(styleProfile.NormalButton.Inactive.BackgroundColor));
                Assert.That(
                    motionRoot.localScale,
                    Is.EqualTo(Vector3.one * styleProfile.Transition.InactiveScale));
                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shineMaterial);
                Object.DestroyImmediate(styleProfile);
            }
        }

        [Test]
        public void Bind_InactiveToActive_StartsPopAndShineAndDisableSettlesAtActiveStyle()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();
            Material shineMaterial = null;

            try
            {
                var group = CreateGroup(
                    root.transform,
                    out var badge,
                    out var frame,
                    out var background,
                    out var motionRoot,
                    out shineMaterial);
                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 0, 0), styleProfile);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 1, 0), styleProfile);

                var sequence = GetFieldValue<object>(badge, "_transitionSequence");
                var runtimeMaterial = GetFieldValue<Material>(badge, "_shineMaterialInstance");
                Assert.That(sequence, Is.Not.Null);
                Assert.That(runtimeMaterial, Is.Not.Null);
                Assert.That(runtimeMaterial, Is.Not.SameAs(shineMaterial));
                Assert.That(runtimeMaterial.shader.name, Is.EqualTo(AllIn1UiMaskShaderName));

                InvokePrivate(badge, "OnDisable");

                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);
                Assert.That(frame.color, Is.EqualTo(styleProfile.NormalButton.Active.BackgroundColor));
                Assert.That(background.color, Is.EqualTo(styleProfile.NormalButton.Active.BackgroundColor));
                Assert.That(motionRoot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(runtimeMaterial.GetFloat("_ShineGlow"), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shineMaterial);
                Object.DestroyImmediate(styleProfile);
            }
        }

        [Test]
        public void Bind_ActiveSlotChange_PlaysConfirmationWithoutReplayingSameBinding()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();
            Material shineMaterial = null;

            try
            {
                var group = CreateGroup(
                    root.transform,
                    out var badge,
                    out _,
                    out _,
                    out _,
                    out shineMaterial);
                var slotZeroActive = new SurfaceBeltButtonRemainderViewModel(0, 1, 0);
                group.Bind(slotZeroActive, styleProfile);

                group.Bind(slotZeroActive, styleProfile);
                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(1, 1, 0), styleProfile);
                var confirmation = GetFieldValue<object>(badge, "_transitionSequence");
                Assert.That(confirmation, Is.Not.Null);

                InvokePrivate(badge, "OnDisable");
                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shineMaterial);
                Object.DestroyImmediate(styleProfile);
            }
        }

        [Test]
        public void Disable_DuringDeactivation_KillsTweenAndAppliesInactiveStableState()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();
            Material shineMaterial = null;

            try
            {
                var group = CreateGroup(
                    root.transform,
                    out var badge,
                    out var frame,
                    out var background,
                    out var motionRoot,
                    out shineMaterial);
                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 1, 0), styleProfile);
                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 0, 0), styleProfile);
                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Not.Null);

                InvokePrivate(badge, "OnDisable");

                Assert.That(GetFieldValue<object>(badge, "_transitionSequence"), Is.Null);
                Assert.That(frame.color, Is.EqualTo(styleProfile.NormalButton.Inactive.BackgroundColor));
                Assert.That(background.color, Is.EqualTo(styleProfile.NormalButton.Inactive.BackgroundColor));
                Assert.That(
                    motionRoot.localScale,
                    Is.EqualTo(Vector3.one * styleProfile.Transition.InactiveScale));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(shineMaterial);
                Object.DestroyImmediate(styleProfile);
            }
        }

        [Test]
        public void BadgeContracts_KeepSeparateBadgesWithoutNumericLabels()
        {
            Assert.That(GetPrivateField<SurfaceBeltButtonBadgeView>("_countText"), Is.Null);
            Assert.That(GetPrivateField<SurfaceBeltButtonBadgeGroupView>("_moonBadge"), Is.Not.Null);
            Assert.That(typeof(ButtonBadgeVisualStateStyle).GetProperty("TextColor"), Is.Null);
        }

        private static SurfaceBeltButtonBadgeGroupView CreateGroup(
            Transform parent,
            out SurfaceBeltButtonBadgeView badge,
            out Image frame,
            out Image background,
            out RectTransform motionRoot,
            out Material shineMaterial)
        {
            var groupObject = new GameObject(
                "ButtonBadgeGroup",
                typeof(RectTransform),
                typeof(SurfaceBeltButtonBadgeGroupView));
            groupObject.transform.SetParent(parent, false);

            var badgeObject = new GameObject(
                "NormalBadge",
                typeof(RectTransform),
                typeof(SurfaceBeltButtonBadgeView));
            badgeObject.transform.SetParent(groupObject.transform, false);
            var backgroundObject = new GameObject("BadgeImage", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(badgeObject.transform, false);

            frame = badgeObject.AddComponent<Image>();
            background = backgroundObject.GetComponent<Image>();
            motionRoot = frame.rectTransform;
            var shader = Shader.Find(AllIn1UiMaskShaderName);
            Assert.That(shader, Is.Not.Null);
            shineMaterial = new Material(shader);
            badge = badgeObject.GetComponent<SurfaceBeltButtonBadgeView>();
            SetField(badge, "_frame", frame);
            SetField(badge, "_background", background);
            SetField(badge, "_motionRoot", motionRoot);
            SetField(badge, "_shineMaterialTemplate", shineMaterial);

            var group = groupObject.GetComponent<SurfaceBeltButtonBadgeGroupView>();
            SetField(group, "_normalBadge", badge);
            var moonBadge = Object.Instantiate(badge, groupObject.transform);
            moonBadge.name = "MoonBadge";
            SetField(group, "_moonBadge", moonBadge);
            return group;
        }

        private static TValue GetFieldValue<TValue>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} should define {fieldName}.");
            return (TValue)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name} should define {methodName}.");
            method.Invoke(target, null);
        }

        private static FieldInfo GetPrivateField<TTarget>(string fieldName)
        {
            return typeof(TTarget).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} should define {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
