using System;
using System.Linq;
using System.Reflection;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Tests
{
    public sealed class UnityLocalizationStringTableIntegrationTests
    {
        [Test]
        public void LocalizationAssets_ContainRequiredLocalesAndUiStringTable()
        {
            Assert.That(LocalizationEditorSettings.GetLocale("en-US"), Is.Not.Null);
            Assert.That(LocalizationEditorSettings.GetLocale("ko-KR"), Is.Not.Null);
            Assert.That(LocalizationEditorSettings.ActiveLocalizationSettings, Is.Not.Null);

            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);
            Assert.That(collection.GetTable("en-US"), Is.Not.Null);
            Assert.That(collection.GetTable("ko-KR"), Is.Not.Null);
        }

        [Test]
        public void UiStringTable_ContainsCompleteSettingsStaticEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);

            AssertTable(collection.GetTable("en-US") as StringTable, useKorean: false);
            AssertTable(collection.GetTable("ko-KR") as StringTable, useKorean: true);
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesAndSwitchesSupportedLocales()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("Settings"));

            var eventCount = 0;
            resolver.LocaleChanged += () => eventCount++;

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("설정"));
            Assert.That(eventCount, Is.EqualTo(1));

            Assert.That(resolver.TrySetLocale("fr-FR"), Is.False);
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(new LocalizedTextDescriptor("UI", "ui.settings.missing")), Is.EqualTo("[UI:ui.settings.missing]"));
        }

        [Test]
        public void UnityStringTableTextResolver_UsesExistingPersistencePolicy()
        {
            var store = new FakeUiLocalePreferenceStore("ko-KR");
            using var restored = CreateUnityResolver(store);

            Assert.That(restored.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(restored.Resolve(SettingsStaticTextDescriptors.Language), Is.EqualTo("언어"));

            Assert.That(restored.TrySetLocale("fr-FR"), Is.False);
            Assert.That(store.SaveCallCount, Is.EqualTo(0));

            Assert.That(restored.TrySetLocale("en-US"), Is.True);
            Assert.That(store.LastSavedLocaleCode, Is.EqualTo("en-US"));

            using var fallback = CreateUnityResolver(new FakeUiLocalePreferenceStore("fr-FR"));
            Assert.That(fallback.CurrentLocaleCode, Is.EqualTo("en-US"));
        }

        [Test]
        public void RuntimeSettings_LanguageRowSwitchesThroughUnityAdapterAndKeepsKoreanFont()
        {
            var nanumGothic = LoadNanumGothic();
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            var fontResolver = new DefaultLocalizedTmpFontResolver(nanumGothic);
            using var harness = SettingsProductionLocalizationRuntimeTests.GameplaySettingsHarness.Create(
                resolver,
                fontResolver: fontResolver);

            harness.ShowSettings();
            var titleLabel = GetText(harness.SettingsView, "_titleLabel");

            Assert.That(titleLabel.text, Is.EqualTo("Settings"));
            Assert.That(harness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("Language"));
            Assert.That(harness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("English"));

            harness.SettingsView.ClickDisplayTab();
            harness.SettingsView.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(nanumGothic));
            Assert.That(harness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(harness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
        }

        [Test]
        public void UiSettingsBridgeAssembly_UsesUnityAdapterWhenLocalizationAssetsAreAvailable()
        {
            var resolver = UiSettingsBridgeAssembly.CreatePersistentSettingsLocalizedTextResolver(new FakeUiLocalePreferenceStore());

            try
            {
                Assert.That(resolver.GetType().Name, Is.EqualTo("UnityStringTableTextResolver"));
                Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("Settings"));
            }
            finally
            {
                (resolver as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void ArchitectureBoundary_RemainsConstrainedToCompositionAndTests()
        {
            AssertNoAssemblyReference(typeof(Game.Feature.UI.Application.SettingsScreenPresenter).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(Game.Feature.UI.Application.SettingsScreenPresenter).Assembly, "Unity.Addressables");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.Addressables");
            AssertNoAssemblyReference(typeof(Game.Feature.UI.Application.SettingsScreenPresenter).Assembly, "Unity.TextMeshPro");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.TextMeshPro");

            Assert.That(typeof(UnityStringTableTextResolver).Assembly.GetName().Name, Is.EqualTo("Game.Feature.UI.Composition"));
        }

        [Test]
        public void AddressablesSettings_AreLocalDefaultAndDoNotIntroduceRemoteGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings), Does.StartWith("Assets/AddressableAssetsData/"));
            Assert.That(settings.groups.Any(group => group != null && string.Equals(group.Name, "Default Local Group", StringComparison.Ordinal)), Is.True);
            Assert.That(settings.BuildRemoteCatalog, Is.False);

            foreach (var group in settings.groups.Where(group => group != null))
            {
                var bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
                if (bundledSchema == null)
                {
                    continue;
                }

                Assert.That(
                    bundledSchema.BuildPath.GetName(settings),
                    Does.Not.Contain("Remote"),
                    group.Name);
                Assert.That(
                    bundledSchema.LoadPath.GetName(settings),
                    Does.Not.Contain("Remote"),
                    group.Name);
            }
        }

        private static UnityStringTableTextResolver CreateUnityResolver(FakeUiLocalePreferenceStore store)
        {
            Assert.That(
                UnityStringTableTextResolver.TryCreateSettingsDefault(
                    store,
                    out var resolver,
                    out var reason),
                Is.True,
                reason);
            return resolver;
        }

        private static void AssertTable(StringTable table, bool useKorean)
        {
            Assert.That(table, Is.Not.Null);
            foreach (var entry in SettingsLocalizationAssetBootstrap.Entries)
            {
                var tableEntry = table.GetEntry(entry.Key);
                Assert.That(tableEntry, Is.Not.Null, entry.Key);
                Assert.That(tableEntry.LocalizedValue, Is.EqualTo(useKorean ? entry.Korean : entry.English));
                Assert.That(tableEntry.LocalizedValue, Is.Not.Empty);
            }
        }

        private static void AssertNoAssemblyReference(Assembly assembly, string referenceName)
        {
            Assert.That(
                assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray(),
                Does.Not.Contain(referenceName));
        }

        private static TMP_FontAsset LoadNanumGothic()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Shared/UI/Fonts/NanumGothic SDF.asset");
            Assert.That(asset, Is.Not.Null);
            return asset;
        }

        private static TMP_Text GetText(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            var value = field.GetValue(target) as TMP_Text;
            Assert.That(value, Is.Not.Null);
            return value;
        }

        public sealed class FakeUiLocalePreferenceStore : IUiLocalePreferenceStore
        {
            private string _localeCode;

            public FakeUiLocalePreferenceStore(string localeCode = null)
            {
                _localeCode = localeCode;
            }

            public int SaveCallCount { get; private set; }

            public string LastSavedLocaleCode { get; private set; }

            public bool TryLoad(out string localeCode)
            {
                localeCode = _localeCode;
                return !string.IsNullOrWhiteSpace(localeCode);
            }

            public void Save(string localeCode)
            {
                SaveCallCount++;
                LastSavedLocaleCode = localeCode;
                _localeCode = localeCode;
            }
        }
    }
}
