using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Popups;
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

            var stageCollection = LocalizationEditorSettings.GetStringTableCollection("Stage");
            Assert.That(stageCollection, Is.Not.Null);
            Assert.That(stageCollection.GetTable("en-US"), Is.Not.Null);
            Assert.That(stageCollection.GetTable("ko-KR"), Is.Not.Null);
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
        public void StageStringTable_ContainsCompleteStageDisplayNameEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("Stage");
            Assert.That(collection, Is.Not.Null);
            var activeStageEntries = LoadActiveStageDisplayNameEntries();

            Assert.That(
                activeStageEntries.Select(entry => entry.Key).ToArray(),
                Is.EquivalentTo(StageDisplayNameEntries.Select(entry => entry.Key).ToArray()));
            Assert.That(
                activeStageEntries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(activeStageEntries.Length));
            AssertStageTable(collection.GetTable("en-US") as StringTable, activeStageEntries);
            AssertStageTable(collection.GetTable("ko-KR") as StringTable, activeStageEntries);
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesAndSwitchesSupportedLocales()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(PauseStaticTextDescriptors.Title), Is.EqualTo("Paused"));
            Assert.That(resolver.Resolve(PauseStaticTextDescriptors.Resume), Is.EqualTo("Resume"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Start), Is.EqualTo("Start"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Settings), Is.EqualTo("Settings"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Quit), Is.EqualTo("Quit"));
            Assert.That(
                resolver.Resolve(StageDisplayNameTextDescriptors.Create("stage.stage-0-1.display_name")),
                Is.EqualTo("Lab-01"));

            var eventCount = 0;
            resolver.LocaleChanged += () => eventCount++;

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(SettingsStaticTextDescriptors.Title), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(PauseStaticTextDescriptors.Title), Is.EqualTo("일시 정지"));
            Assert.That(resolver.Resolve(PauseStaticTextDescriptors.Resume), Is.EqualTo("계속하기"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Start), Is.EqualTo("시작"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Settings), Is.EqualTo("설정"));
            Assert.That(resolver.Resolve(MainMenuStaticTextDescriptors.Quit), Is.EqualTo("종료"));
            Assert.That(
                resolver.Resolve(StageDisplayNameTextDescriptors.Create("stage.stage-0-1.display_name")),
                Is.EqualTo("Lab-01"));
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
        public void RuntimePausePopup_ResolvesUnityTableLabelsAndRefreshesWhenLocaleChanges()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            var prefab = UiTestPrefabAssetUtility.LoadPopupPrefab<PausePopupView>(
                UiTestPrefabAssetUtility.PausePopupPrefabPath);
            var view = UnityEngine.Object.Instantiate(prefab);
            var presenter = new PausePopupPresenter(resolver);

            try
            {
                presenter.Apply(PausePopupPayload.Default);
                view.Bind(presenter.ViewModel);
                view.BindStaticLocalization(
                    PausePopupPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance);
                view.IsVisible = true;

                AssertPauseLabels(view, "Paused", "Pausing modal popup", "Resume", "Settings", "Retry", "Main Menu");

                Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

                AssertPauseLabels(view, "일시 정지", "일시 정지 팝업", "계속하기", "설정", "다시 시도", "메인 메뉴");
            }
            finally
            {
                view.UnbindStaticLocalization();
                view.Bind(null);
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void RuntimeMainMenuShell_ResolvesUnityTableLabelsAndRefreshesWhenLocaleChanges()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<MainMenuScreenView>(
                UiTestPrefabAssetUtility.MainMenuScreenPrefabPath);
            var view = UnityEngine.Object.Instantiate(prefab);

            try
            {
                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance);

                AssertMainMenuLabels(view, "Start", "Settings", "Quit");

                Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

                AssertMainMenuLabels(view, "시작", "설정", "종료");
            }
            finally
            {
                view.UnbindStaticLocalization();
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
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
        public void UiSettingsBridgeAssembly_FailsFastWhenUnityAdapterCannotBeCreated()
        {
            UiSettingsBridgeAssembly.TryCreateLocalizedTextResolver failingUnityAdapter =
                (IUiLocalePreferenceStore _,
                    out ILocalizedTextResolver resolver,
                    out string failureReason) =>
                {
                    resolver = null;
                    failureReason = "simulated missing UI String Table";
                    return false;
                };

            var exception = Assert.Throws<InvalidOperationException>(
                () => UiSettingsBridgeAssembly.CreatePersistentSettingsLocalizedTextResolver(
                    new FakeUiLocalePreferenceStore(),
                    failingUnityAdapter));

            Assert.That(exception.Message, Does.Contain("Unity Localization production setup is required"));
            Assert.That(exception.Message, Does.Contain("simulated missing UI String Table"));
        }

        [Test]
        public void ProductionUiComposition_RequestsUnityResolverAndPassesItToRuntimeFactories()
        {
            var gameplayInstallerSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayUiFlowInstaller.cs");
            var mainMenuInstallerSource = System.IO.File.ReadAllText(
                "Assets/_Features/UI/UI_Composition/Runtime/MainMenuUiFlowInstaller.cs");

            Assert.That(gameplayInstallerSource, Does.Contain("CreatePersistentSettingsLocalizedTextResolver()"));
            Assert.That(gameplayInstallerSource, Does.Contain("localizedTextResolver: localizedTextResolver"));
            Assert.That(gameplayInstallerSource, Does.Contain("new StageInfoPresenter(localizedTextResolver)"));

            Assert.That(mainMenuInstallerSource, Does.Contain("CreatePersistentSettingsLocalizedTextResolver()"));
            Assert.That(mainMenuInstallerSource, Does.Contain("localizedTextResolver: _localizedTextResolver"));
            Assert.That(mainMenuInstallerSource, Does.Contain("BindStaticLocalization"));
        }

        [Test]
        public void ProductionRuntimeSource_DoesNotReferencePackageFreeResolver()
        {
            var roots = new[]
            {
                "Assets/_Features/UI/UI_Application/Runtime",
                "Assets/_Features/UI/UI_Composition/Runtime",
                "Assets/_Features/UI/UI_Popups/Runtime",
                "Assets/_Features/UI/UI_Screens/Runtime",
            };

            foreach (var file in roots.SelectMany(root =>
                         System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories)))
            {
                Assert.That(
                    System.IO.File.ReadAllText(file),
                    Does.Not.Contain("PackageFreeLocalizedTextResolver"),
                    file);
            }
        }

        [Test]
        public void ArchitectureBoundary_RemainsConstrainedToCompositionAndTests()
        {
            AssertNoAssemblyReference(typeof(Game.Feature.UI.Application.SettingsScreenPresenter).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(Game.Feature.UI.Application.SettingsScreenPresenter).Assembly, "Unity.Addressables");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.Localization");
            AssertNoAssemblyReference(typeof(LocalizedTextDescriptor).Assembly, "Unity.Addressables");
            AssertNoAssemblyReference(typeof(StagePresentationDefinition).Assembly, "Unity.Localization");
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

        private static void AssertStageTable(
            StringTable table,
            IReadOnlyList<(string StageId, string Key, string Value)> expectedEntries)
        {
            Assert.That(table, Is.Not.Null);
            var stageDisplayNameKeys = table.SharedData.Entries
                .Select(entry => entry.Key)
                .Where(key => key.StartsWith("stage.", StringComparison.Ordinal) &&
                              key.EndsWith(StageDisplayNameKeys.Suffix, StringComparison.Ordinal))
                .ToArray();
            Assert.That(
                stageDisplayNameKeys.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(stageDisplayNameKeys.Length),
                table.LocaleIdentifier.Code);
            foreach (var entry in expectedEntries)
            {
                var tableEntry = table.GetEntry(entry.Key);
                Assert.That(tableEntry, Is.Not.Null, entry.Key);
                Assert.That(tableEntry.LocalizedValue, Is.EqualTo(entry.Value), entry.StageId);
                Assert.That(tableEntry.LocalizedValue, Is.Not.Empty);
            }
        }

        private static (string StageId, string Key, string Value)[] LoadActiveStageDisplayNameEntries()
        {
            var expectedValues = StageDisplayNameEntries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
                StringComparer.Ordinal);
            var entries = AssetDatabase
                .FindAssets($"t:{nameof(StageContentEntry)}", new[] { StageContentPaths.CampaignLevel01StagesRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageContentEntry>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(entry => entry != null && entry.StageId.IsValid)
                .OrderBy(entry => entry.StageId.Value, StringComparer.Ordinal)
                .Select(entry =>
                {
                    Assert.That(entry.PresentationDefinition, Is.Not.Null, entry.StageId.Value);
                    var key = entry.PresentationDefinition.DisplayNameKey;
                    Assert.That(key, Is.EqualTo(StageDisplayNameKeys.ForStage(entry.StageId)), entry.StageId.Value);
                    Assert.That(expectedValues.TryGetValue(key, out var value), Is.True, entry.StageId.Value);
                    return (entry.StageId.Value, key, value);
                })
                .ToArray();

            Assert.That(entries, Is.Not.Empty);
            return entries;
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

        private static readonly (string Key, string Value)[] StageDisplayNameEntries =
        {
            ("stage.stage-0-1.display_name", "Lab-01"),
            ("stage.stage-0-2.display_name", "Lab-02"),
            ("stage.stage-1-1.display_name", "Lobby-01"),
            ("stage.stage-2-1.display_name", "Ward[A]-01"),
            ("stage.stage-2-2.display_name", "Ward[A]-02"),
            ("stage.stage-3-1.display_name", "Ward[B]-01"),
            ("stage.stage-3-2.display_name", "Ward[B]-02"),
            ("stage.stage-4-1.display_name", "Morgue-01"),
            ("stage.stage-4-2.display_name", "Morgue-02"),
            ("stage.legacy-stage-5-1.display_name", "Legacy 5-1"),
        };

        private static TMP_Text GetText(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            var value = field.GetValue(target) as TMP_Text;
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static void AssertPauseLabels(
            PausePopupView view,
            string title,
            string description,
            string resume,
            string settings,
            string retry,
            string mainMenu)
        {
            Assert.That(GetText(view, "_titleLabel").text, Is.EqualTo(title));
            Assert.That(GetText(view, "_descriptionLabel").text, Is.EqualTo(description));
            Assert.That(GetText(view, "_resumeButtonLabel").text, Is.EqualTo(resume));
            Assert.That(GetText(view, "_settingsButtonLabel").text, Is.EqualTo(settings));
            Assert.That(GetText(view, "_retryButtonLabel").text, Is.EqualTo(retry));
            Assert.That(GetText(view, "_mainMenuButtonLabel").text, Is.EqualTo(mainMenu));
        }

        private static void AssertMainMenuLabels(
            MainMenuScreenView view,
            string start,
            string settings,
            string quit)
        {
            Assert.That(GetText(view, "_startButtonLabel").text, Is.EqualTo(start));
            Assert.That(GetText(view, "_settingsButtonLabel").text, Is.EqualTo(settings));
            Assert.That(GetText(view, "_quitButtonLabel").text, Is.EqualTo(quit));
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
