using System;
using System.Linq;
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
                var kboLight = UiTestPrefabAssetUtility.LoadKboDiaGothicLightFont();
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

                Assert.That(header.font, Is.SameAs(kboLight));
                Assert.That(header.fontSharedMaterial, Is.SameAs(kboLight.material));
                Assert.That(header.fontStyle, Is.EqualTo(FontStyles.Normal));
                Assert.That(rowLabel.font, Is.SameAs(kboLight));
                Assert.That(rowLabel.fontSharedMaterial, Is.SameAs(kboLight.material));
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

        [TestCase(false)]
        [TestCase(true)]
        public void ObjectiveHud_HiddenLocaleChanges_DeferRowsUntilLatestStateIsShown(bool initiallyEmpty)
        {
            using var fixture = new LocaleHudFixture();
            if (!initiallyEmpty) fixture.Show("ko-KR", "출구로 이동하기 (0/1)");
            fixture.Root.SetActive(false);
            // EditMode does not drive MonoBehaviour lifecycle callbacks for this prefab.
            typeof(ObjectiveHudView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.View, null);
            var rowCount = fixture.View.GetComponentsInChildren<ObjectiveHudRowView>(true).Length;

            foreach (var locale in new[] { "ja-JP", "zh-CN", "en-US", "ko-KR" })
            {
                fixture.Show(locale, locale == "ko-KR" ? "출구로 이동하기 (0/1)" : "脱出エリアに到達する（0/1）");
                Assert.That(fixture.Rows, Is.Empty, "Hidden callbacks must not enter or bind rows.");
                Assert.That(fixture.View.GetComponentsInChildren<ObjectiveHudRowView>(true).Length,
                    Is.EqualTo(rowCount));
            }

            fixture.Root.SetActive(true);
            Assert.That(fixture.View.gameObject.activeInHierarchy, Is.True);
            typeof(ObjectiveHudView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.View, null);
            Assert.That(fixture.Rows, Has.Length.EqualTo(1));
            fixture.AssertRendered(fixture.Rows[0], "출구로 이동하기 (0/1)");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ObjectiveHud_LocaleChangeDuringTransition_RefreshesContentWithoutRestart(bool exiting)
        {
            using var fixture = new LocaleHudFixture();
            fixture.Show("ja-JP", "脱出エリアに到達する（0/1）");
            var row = fixture.Rows.Single();
            if (exiting)
            {
                for (var i = 0; i < 60; i++) row.Tick(0.02f);
                fixture.Show("ja-JP", "脱出エリアに到達する（1/1）", satisfied: true);
                typeof(ObjectiveHudView).GetMethod("ProcessTransitionAdvance", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(fixture.View, new object[] { float.MaxValue });
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
            }
            else
            {
                row.Tick(0.02f);
                Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Entering));
            }

            var state = row.VisualState;
            var elapsed = GetField<float>(row, "_heightElapsed");
            var text = exiting ? "출구로 이동하기 (1/1)" : "출구로 이동하기 (0/1)";
            fixture.Show("ko-KR", text, satisfied: exiting);

            Assert.That(fixture.Rows.Single(), Is.SameAs(row));
            Assert.That(row.VisualState, Is.EqualTo(state));
            Assert.That(GetField<float>(row, "_heightElapsed"), Is.EqualTo(elapsed));
            fixture.AssertRendered(row, text);
        }

        [Test]
        public void ObjectiveHud_RemovedExitingRow_KeepsMatchingTextAndFont()
        {
            using var fixture = new LocaleHudFixture();
            fixture.Show("ja-JP", "脱出エリアに到達する（0/1）");
            var row = fixture.Rows.Single();
            for (var i = 0; i < 60; i++) row.Tick(0.02f);
            var label = GetField<TMP_Text>(row, "_label");
            var font = label.font;
            fixture.Resolver.SetLocale("ko-KR");
            fixture.Model.SetState(true, "objective", string.Empty, Array.Empty<ObjectiveConditionHudViewModel>());
            typeof(ObjectiveHudView).GetMethod("ProcessTransitionAdvance", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.View, new object[] { float.MaxValue });
            Assert.That(row.VisualState, Is.EqualTo(ObjectiveRowVisualState.Completing));
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.text, Is.EqualTo("脱出エリアに到達する（0/1）"));
            label.ForceMeshUpdate();
        }

        [Test]
        public void ObjectiveHud_EmptyRoot_CanWakeForNewObjective()
        {
            using var fixture = new LocaleHudFixture();
            fixture.Model.Reset();
            Assert.That(fixture.View.gameObject.activeSelf, Is.False);
            fixture.Show("ko-KR", "출구로 이동하기 (0/1)");
            Assert.That(fixture.View.isActiveAndEnabled, Is.True);
            fixture.AssertRendered(fixture.Rows.Single(), "출구로 이동하기 (0/1)");
        }

        private sealed class LocaleHudFixture : IDisposable
        {
            public readonly GameObject Root = new("ObjectiveLocaleLifecycle", typeof(RectTransform), typeof(Canvas));
            public readonly MutableLocaleResolver Resolver = new("ja-JP");
            public readonly ObjectiveHudViewModel Model = new();
            public readonly ObjectiveHudView View;
            public ObjectiveHudRowView[] Rows => View.GetComponentsInChildren<ObjectiveHudRowView>(true)
                .Where(row => !string.IsNullOrEmpty(row.StableId)).ToArray();

            public LocaleHudFixture()
            {
                Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var hud = UiTestPrefabAssetUtility.InstantiateHudPrefab(Root.GetComponent<RectTransform>());
                View = hud.ObjectiveHudView;
                var binding = View.GetComponent<ObjectiveHudTypographyBinding>();
                binding.Initialize(Resolver);
                View.ConfigureTypography(binding);
                View.Bind(Model);
            }

            public void Show(string locale, string text, bool satisfied = false)
            {
                Resolver.SetLocale(locale);
                Model.SetState(true, "objective", string.Empty,
                    new[] { new ObjectiveConditionHudViewModel("exit", text, satisfied, satisfied) });
            }

            public void AssertRendered(ObjectiveHudRowView row, string text)
            {
                var label = GetField<TMP_Text>(row, "_label");
                Assert.That(label.text, Is.EqualTo(text));
                Assert.That(label.font, Is.SameAs(UiTestPrefabAssetUtility.LoadKboDiaGothicLightFont()));
                Canvas.ForceUpdateCanvases();
                label.ForceMeshUpdate();
                Assert.That(label.textInfo.characterCount, Is.GreaterThan(0));
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
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
