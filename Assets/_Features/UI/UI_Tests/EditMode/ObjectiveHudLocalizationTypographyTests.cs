using System;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.HUD;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class ObjectiveHudLocalizationTypographyTests
    {
        private const string ThemeAssetPath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const int MaximumSemanticRowCount = 3;

        [Test]
        public void GameplayHudRoot_ObjectiveTypographyMetadata_UsesExistingSemanticRoles()
        {
            var hud = UiTestPrefabAssetUtility.LoadHudPrefab();
            var objectiveView = hud.ObjectiveHudView;
            var binding = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemeAssetPath);

            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.Theme, Is.SameAs(theme));
            Assert.That(binding.HeaderStyle, Is.EqualTo(TypographyStyleTag.HeaderSmall));
            Assert.That(binding.RowStyle, Is.EqualTo(TypographyStyleTag.BodySmall));
            Assert.That(
                Enum.GetValues(typeof(TypographyStyleTag)).Length,
                Is.EqualTo(19),
                "ObjectiveHud must reuse the existing typography vocabulary.");
            binding.ValidateAuthoredStructureOrThrow();
            objectiveView.ValidateAuthoredStructureOrThrow();
        }

        [Test]
        public void ObjectiveTypography_PersistentPrefabTarget_RejectsRuntimeMetadataMutation()
        {
            var hud = UiTestPrefabAssetUtility.LoadHudPrefab();
            var objectiveView = hud.ObjectiveHudView;
            var binding = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
            var header = objectiveView.HeaderLabel;
            var existingBinding = TypographyBinding.FindFor(header);
            var resolver = new MutableLocaleResolver("en-US");

            Assert.That(existingBinding, Is.Null);
            var exception = Assert.Throws<InvalidOperationException>(() => binding.Initialize(resolver));

            Assert.That(exception.Message, Does.Contain("persistent asset"));
            Assert.That(TypographyBinding.FindFor(header), Is.Null);
        }

        [Test]
        public void ObjectiveTypography_LocaleRoundTrip_ChangesIdentityAndPreservesAuthoredSizing()
        {
            var parentObject = new GameObject("ObjectiveTypographyTestRoot", typeof(RectTransform));
            var instance = UiTestPrefabAssetUtility.InstantiateHudPrefab(
                parentObject.GetComponent<RectTransform>());
            GameObject rowClone = null;
            try
            {
                var objectiveView = instance.ObjectiveHudView;
                var binding = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
                var template = GetField<RectTransform>(objectiveView, "_objectiveItemTemplate");
                rowClone = UnityEngine.Object.Instantiate(
                    template.gameObject,
                    template.parent,
                    false);
                rowClone.name = "ObjectiveTypographyRoundTripRow";
                rowClone.SetActive(true);
                var row = rowClone.GetComponent<ObjectiveHudRowView>();
                var header = objectiveView.HeaderLabel;
                var rowLabel = GetField<TMP_Text>(row, "_label");
                var headerSizing = TextSizingSnapshot.Capture(header);
                var rowSizing = TextSizingSnapshot.Capture(rowLabel);
                var climate = UiTestPrefabAssetUtility.LoadClimateCrisisKr2019Font();
                var resolver = new MutableLocaleResolver("en-US");

                binding.Initialize(resolver);
                objectiveView.ConfigureTypography(binding);
                row.ConfigureTypography(binding);

                headerSizing.AssertIdentityUnchanged(header);
                rowSizing.AssertIdentityUnchanged(rowLabel);
                headerSizing.AssertUnchanged(header);
                rowSizing.AssertUnchanged(rowLabel);

                resolver.SetLocale("ko-KR");
                objectiveView.ConfigureTypography(binding);
                binding.ApplyRow(rowLabel);

                Assert.That(header.font, Is.SameAs(climate));
                Assert.That(header.fontSharedMaterial, Is.SameAs(climate.material));
                Assert.That(header.fontStyle, Is.EqualTo(FontStyles.Normal));
                Assert.That(rowLabel.font, Is.SameAs(climate));
                Assert.That(rowLabel.fontSharedMaterial, Is.SameAs(climate.material));
                Assert.That(rowLabel.fontStyle, Is.EqualTo(FontStyles.Normal));
                headerSizing.AssertUnchanged(header);
                rowSizing.AssertUnchanged(rowLabel);

                resolver.SetLocale("en-US");
                objectiveView.ConfigureTypography(binding);
                binding.ApplyRow(rowLabel);

                headerSizing.AssertIdentityUnchanged(header);
                rowSizing.AssertIdentityUnchanged(rowLabel);
                headerSizing.AssertUnchanged(header);
                rowSizing.AssertUnchanged(rowLabel);
            }
            finally
            {
                if (rowClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(rowClone);
                }

                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [TestCase("en-US", "Place the moon-marked box on the button (99/99)")]
        [TestCase("ko-KR", "문블록으로 노란 버튼을 활성화하기 (99/99)")]
        public void ObjectiveRow_LongApprovedCopy_FitsAuthoredRow(string localeCode, string text)
        {
            var parentObject = new GameObject("ObjectiveTypographyTestRoot", typeof(RectTransform));
            var instance = UiTestPrefabAssetUtility.InstantiateHudPrefab(
                parentObject.GetComponent<RectTransform>());
            GameObject rowClone = null;
            try
            {
                var objectiveView = instance.ObjectiveHudView;
                var template = GetField<RectTransform>(objectiveView, "_objectiveItemTemplate");
                rowClone = UnityEngine.Object.Instantiate(template.gameObject, template.parent, false);
                rowClone.name = "ObjectiveLayoutProbe";
                rowClone.SetActive(true);
                var row = rowClone.GetComponent<ObjectiveHudRowView>();
                var binding = objectiveView.GetComponent<ObjectiveHudTypographyBinding>();
                var resolver = new MutableLocaleResolver(localeCode);
                binding.Initialize(resolver);
                row.ConfigureTypography(binding);

                var label = GetField<TMP_Text>(row, "_label");
                label.text = text;
                var objectiveWidth = objectiveView.GetComponent<LayoutElement>().preferredWidth;
                var availableWidth = objectiveWidth + label.rectTransform.sizeDelta.x;
                var availableHeight = rowClone.GetComponent<LayoutElement>().preferredHeight;

                Assert.That(availableWidth, Is.GreaterThan(0f));
                Assert.That(availableHeight, Is.GreaterThan(0f));
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(label.enableAutoSizing, Is.True);
                Assert.That(label.fontSizeMin, Is.EqualTo(12f));

                label.enableAutoSizing = false;
                label.fontSize = label.fontSizeMin;
                var minimumSizePreferred =
                    label.GetPreferredValues(text, Mathf.Infinity, Mathf.Infinity);

                Assert.That(minimumSizePreferred.x, Is.LessThanOrEqualTo(availableWidth + 0.01f));
                Assert.That(minimumSizePreferred.y, Is.LessThanOrEqualTo(availableHeight + 0.01f));
            }
            finally
            {
                if (rowClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(rowClone);
                }

                UnityEngine.Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void ObjectiveHud_MaximumSemanticStack_FitsAuthoredViewport()
        {
            var hud = UiTestPrefabAssetUtility.LoadHudPrefab();
            var objectiveView = hud.ObjectiveHudView;
            var template = GetField<RectTransform>(objectiveView, "_objectiveItemTemplate");
            var rowLayout = template.GetComponent<LayoutElement>();
            var objectiveLayout = objectiveView.GetComponent<LayoutElement>();
            var requiredHeight =
                objectiveView.HeaderLabel.rectTransform.rect.height +
                MaximumSemanticRowCount * rowLayout.preferredHeight;

            Assert.That(MaximumSemanticRowCount, Is.EqualTo(3));
            Assert.That(objectiveLayout.preferredHeight, Is.GreaterThanOrEqualTo(requiredHeight));
            Assert.That(objectiveView.HeaderLabel.rectTransform.rect.height, Is.GreaterThan(0f));
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName}");
            return (T)field.GetValue(target);
        }

        private sealed class MutableLocaleResolver : ILocalizedTextResolver
        {
            public MutableLocaleResolver(string localeCode)
            {
                CurrentLocaleCode = localeCode;
            }

            public string CurrentLocaleCode { get; private set; }

            public event Action LocaleChanged;

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                return descriptor.Key;
            }

            public void SetLocale(string localeCode)
            {
                CurrentLocaleCode = localeCode;
                LocaleChanged?.Invoke();
            }
        }

        private readonly struct TextSizingSnapshot
        {
            private TextSizingSnapshot(TMP_Text target)
            {
                Font = target.font;
                Material = target.fontSharedMaterial;
                FontStyle = target.fontStyle;
                FontSize = target.fontSize;
                EnableAutoSizing = target.enableAutoSizing;
                FontSizeMin = target.fontSizeMin;
                FontSizeMax = target.fontSizeMax;
                LineSpacing = target.lineSpacing;
                CharacterSpacing = target.characterSpacing;
            }

            private TMP_FontAsset Font { get; }

            private Material Material { get; }

            private FontStyles FontStyle { get; }

            private float FontSize { get; }

            private bool EnableAutoSizing { get; }

            private float FontSizeMin { get; }

            private float FontSizeMax { get; }

            private float LineSpacing { get; }

            private float CharacterSpacing { get; }

            public static TextSizingSnapshot Capture(TMP_Text target)
            {
                return new TextSizingSnapshot(target);
            }

            public void AssertUnchanged(TMP_Text target)
            {
                Assert.That(target.fontSize, Is.EqualTo(FontSize));
                Assert.That(target.enableAutoSizing, Is.EqualTo(EnableAutoSizing));
                Assert.That(target.fontSizeMin, Is.EqualTo(FontSizeMin));
                Assert.That(target.fontSizeMax, Is.EqualTo(FontSizeMax));
                Assert.That(target.lineSpacing, Is.EqualTo(LineSpacing));
                Assert.That(target.characterSpacing, Is.EqualTo(CharacterSpacing));
            }

            public void AssertIdentityUnchanged(TMP_Text target)
            {
                Assert.That(target.font, Is.SameAs(Font));
                Assert.That(target.fontSharedMaterial, Is.SameAs(Material));
                Assert.That(target.fontStyle, Is.EqualTo(FontStyle));
            }
        }
    }
}
