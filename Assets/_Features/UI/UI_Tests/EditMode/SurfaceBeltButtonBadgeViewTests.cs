using System.Reflection;
using Game.Feature.UI.HUD;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class SurfaceBeltButtonBadgeViewTests
    {
        [Test]
        public void Bind_ShowsPositiveAndZeroBadgesWithStateColors()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();

            try
            {
                var group = CreateGroup(root.transform, out var normalBadge, out var normalBackground, out var normalText, out var moonBadge, out _, out _);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 2, 0), styleProfile, showBadges: true);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(normalBadge.gameObject.activeSelf, Is.True);
                Assert.That(normalText.text, Is.EqualTo("2"));
                Assert.That(normalBackground.color, Is.EqualTo(styleProfile.NormalButton.Active.BackgroundColor));
                Assert.That(normalText.color, Is.EqualTo(styleProfile.NormalButton.Active.TextColor));
                Assert.That(moonBadge.gameObject.activeSelf, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(styleProfile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bind_ShowsZeroBadgesWithInactiveColors()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();

            try
            {
                var group = CreateGroup(root.transform, out var normalBadge, out var normalBackground, out var normalText, out var moonBadge, out var moonBackground, out var moonText);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 0, 0), styleProfile, showBadges: true);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(normalBadge.gameObject.activeSelf, Is.True);
                Assert.That(normalText.text, Is.EqualTo("0"));
                Assert.That(normalBackground.color, Is.EqualTo(styleProfile.NormalButton.Inactive.BackgroundColor));
                Assert.That(normalText.color, Is.EqualTo(styleProfile.NormalButton.Inactive.TextColor));
                Assert.That(moonBadge.gameObject.activeSelf, Is.True);
                Assert.That(moonText.text, Is.EqualTo("0"));
                Assert.That(moonBackground.color, Is.EqualTo(styleProfile.MoonBlockOnlyButton.Inactive.BackgroundColor));
                Assert.That(moonText.color, Is.EqualTo(styleProfile.MoonBlockOnlyButton.Inactive.TextColor));
            }
            finally
            {
                Object.DestroyImmediate(styleProfile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bind_UsesMoonBlockOnlyProfileForMoonBlockOnlyBadge()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();

            try
            {
                var group = CreateGroup(root.transform, out var normalBadge, out _, out _, out var moonBadge, out var moonBackground, out var moonText);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 0, 3), styleProfile, showBadges: true);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(normalBadge.gameObject.activeSelf, Is.True);
                Assert.That(moonBadge.gameObject.activeSelf, Is.True);
                Assert.That(moonText.text, Is.EqualTo("3"));
                Assert.That(moonBackground.color, Is.EqualTo(styleProfile.MoonBlockOnlyButton.Active.BackgroundColor));
                Assert.That(moonText.color, Is.EqualTo(styleProfile.MoonBlockOnlyButton.Active.TextColor));
            }
            finally
            {
                Object.DestroyImmediate(styleProfile);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Bind_HidesBadgesWhenAuthoredCellShouldNotShowBadges()
        {
            var root = new GameObject("SurfaceBeltButtonBadgeViewTests");
            var styleProfile = ScriptableObject.CreateInstance<SurfaceBeltButtonBadgeStyleProfile>();

            try
            {
                var group = CreateGroup(root.transform, out var normalBadge, out _, out _, out var moonBadge, out _, out _);

                group.Bind(new SurfaceBeltButtonRemainderViewModel(0, 2, 3), styleProfile, showBadges: false);

                Assert.That(group.gameObject.activeSelf, Is.True);
                Assert.That(normalBadge.gameObject.activeSelf, Is.False);
                Assert.That(moonBadge.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(styleProfile);
                Object.DestroyImmediate(root);
            }
        }

        private static SurfaceBeltButtonBadgeGroupView CreateGroup(
            Transform parent,
            out GameObject normalBadgeRoot,
            out Image normalBackground,
            out TMP_Text normalText,
            out GameObject moonBadgeRoot,
            out Image moonBackground,
            out TMP_Text moonText)
        {
            var groupObject = new GameObject("ButtonBadgeGroup", typeof(RectTransform), typeof(SurfaceBeltButtonBadgeGroupView));
            groupObject.transform.SetParent(parent, false);
            var group = groupObject.GetComponent<SurfaceBeltButtonBadgeGroupView>();
            var normalBadge = CreateBadge(groupObject.transform, "NormalBadge", out normalBadgeRoot, out normalBackground, out normalText);
            var moonBadge = CreateBadge(groupObject.transform, "MoonBlockOnlyBadge", out moonBadgeRoot, out moonBackground, out moonText);

            SetField(group, "_normalBadge", normalBadge);
            SetField(group, "_moonBlockOnlyBadge", moonBadge);
            return group;
        }

        private static SurfaceBeltButtonBadgeView CreateBadge(
            Transform parent,
            string name,
            out GameObject badgeRoot,
            out Image background,
            out TMP_Text countText)
        {
            badgeRoot = new GameObject(name, typeof(RectTransform), typeof(SurfaceBeltButtonBadgeView));
            badgeRoot.transform.SetParent(parent, false);
            var backgroundObject = new GameObject("BadgeImage", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(badgeRoot.transform, false);
            background = backgroundObject.GetComponent<Image>();
            var textObject = new GameObject("CountText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(badgeRoot.transform, false);
            countText = textObject.GetComponent<TMP_Text>();
            var view = badgeRoot.GetComponent<SurfaceBeltButtonBadgeView>();
            SetField(view, "_background", background);
            SetField(view, "_countText", countText);
            return view;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} should define {fieldName}.");
            field.SetValue(target, value);
        }

    }
}
