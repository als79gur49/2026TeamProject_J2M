using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.UIAccess.Models;
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
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Tests
{
    public sealed class UnityLocalizationStringTableIntegrationTests
    {
        private Locale _selectedLocaleBeforeTest;

        [SetUp]
        public void PreserveSelectedLocale()
        {
            _selectedLocaleBeforeTest = LocalizationSettings.SelectedLocale;
        }

        [TearDown]
        public void RestoreSelectedLocale()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocale = _selectedLocaleBeforeTest;
            }
        }

        [Test]
        public void LocalizationAssets_ContainRequiredLocalesAndUiStringTable()
        {
            Assert.That(LocalizationEditorSettings.GetLocale("en-US"), Is.Not.Null);
            Assert.That(LocalizationEditorSettings.GetLocale("ko-KR"), Is.Not.Null);
            Assert.That(LocalizationEditorSettings.ActiveLocalizationSettings, Is.Not.Null);
            var smartFormatter = LocalizationSettings.StringDatabase?.SmartFormatter;
            Assert.That(smartFormatter, Is.Not.Null);
            Assert.That(smartFormatter.SourceExtensions, Is.Not.Empty);
            Assert.That(smartFormatter.SourceExtensions, Has.None.Null);
            Assert.That(smartFormatter.FormatterExtensions, Is.Not.Empty);
            Assert.That(smartFormatter.FormatterExtensions, Has.None.Null);

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
        public void SettingsLocalizationContract_IsCompleteUniqueAndWellFormed()
        {
            var entries = SettingsLocalizationContract.Entries;
            var declaredIds = Enum.GetValues(typeof(SettingsLocalizationEntryId))
                .Cast<SettingsLocalizationEntryId>()
                .ToArray();

            Assert.That(entries, Has.Count.EqualTo(49));
            Assert.That(
                entries.Count(entry => entry.Coverage.HasFlag(SettingsLocalizationCoverage.StaticDescriptor)),
                Is.EqualTo(28));
            Assert.That(
                entries.Count(entry => entry.Coverage.HasFlag(SettingsLocalizationCoverage.DynamicDescriptor)),
                Is.EqualTo(21));
            Assert.That(entries.Count(entry => entry.IsSmart), Is.EqualTo(8));
            Assert.That(
                entries.Count(entry => entry.FormatKind == SettingsLocalizationFormatKind.PercentArgument),
                Is.EqualTo(2));
            Assert.That(
                entries.Count(entry => entry.FormatKind == SettingsLocalizationFormatKind.PositionalArgument),
                Is.EqualTo(6));
            Assert.That(
                entries.Count(entry => entry.FormatKind == SettingsLocalizationFormatKind.None),
                Is.EqualTo(41));
            Assert.That(
                entries.Select(entry => entry.Id).ToArray(),
                Is.EquivalentTo(declaredIds),
                "Every stable Settings localization ID must have exactly one contract entry.");
            Assert.That(
                entries.Select(entry => entry.Id).Distinct().Count(),
                Is.EqualTo(entries.Count),
                "Settings localization contract IDs must be unique.");
            Assert.That(
                entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(entries.Count),
                "Settings localization contract keys must be unique.");

            var requiredProjectionCoverage =
                SettingsLocalizationCoverage.Bootstrap |
                SettingsLocalizationCoverage.PackageFreeFallback |
                SettingsLocalizationCoverage.InvariantFallback;
            foreach (var entry in entries)
            {
                Assert.That(entry.Table, Is.EqualTo("UI"), entry.Id.ToString());
                Assert.That(entry.Key, Is.Not.Null.And.Not.Empty, entry.Id.ToString());
                Assert.That(
                    entry.Coverage.HasFlag(SettingsLocalizationCoverage.StaticDescriptor) ^
                    entry.Coverage.HasFlag(SettingsLocalizationCoverage.DynamicDescriptor),
                    Is.True,
                    $"{entry.Key} must belong to exactly one descriptor projection.");
                Assert.That(
                    entry.Coverage.HasFlag(requiredProjectionCoverage),
                    Is.True,
                    $"{entry.Key} must participate in every required fallback/bootstrap projection.");
                Assert.That(
                    entry.IsSmart,
                    Is.EqualTo(entry.FormatKind != SettingsLocalizationFormatKind.None),
                    $"{entry.Key} Smart metadata must match its runtime formatting contract.");
            }

            Assert.That(
                entries
                    .Where(entry => entry.FormatKind == SettingsLocalizationFormatKind.PercentArgument)
                    .Select(entry => entry.Key)
                    .ToArray(),
                Is.EquivalentTo(new[]
                {
                    SettingsLocalizationContract.Keys.AudioVolumeValue,
                    SettingsLocalizationContract.Keys.AudioVolumeValueMuted,
                }),
                "Only audio percentage entries use the percent argument contract.");
            Assert.That(
                entries
                    .Where(entry => entry.FormatKind == SettingsLocalizationFormatKind.PositionalArgument)
                    .Select(entry => entry.Key)
                    .ToArray(),
                Is.EquivalentTo(new[]
                {
                    SettingsLocalizationContract.Keys.DisplayResolutionValue,
                    SettingsLocalizationContract.Keys.DisplayPreviewCountdown,
                    SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus,
                    SettingsLocalizationContract.Keys.InputActionConflict,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody,
                }),
                "Only display value/countdown/confirmation entries use the positional argument contract.");
        }

        [Test]
        public void SettingsLocalizationContract_MatchesDescriptorAndBootstrapKeyProjections()
        {
            var staticDescriptorKeys = typeof(SettingsStaticTextDescriptors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(LocalizedTextDescriptor))
                .Select(field => ((LocalizedTextDescriptor)field.GetValue(null)).Key);
            var dynamicDescriptorKeys = typeof(SettingsDynamicTextDescriptors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field =>
                    field.FieldType == typeof(string) &&
                    field.IsLiteral &&
                    field.Name.EndsWith("Key", StringComparison.Ordinal))
                .Select(field => (string)field.GetRawConstantValue());
            var descriptorKeys = staticDescriptorKeys.Concat(dynamicDescriptorKeys);
            var contractKeys = SettingsLocalizationContract.Entries.Select(entry => entry.Key);
            var dynamicContractKeys = SettingsLocalizationContract.Entries
                .Where(entry => entry.Coverage.HasFlag(SettingsLocalizationCoverage.DynamicDescriptor))
                .Select(entry => entry.Key);
            var dynamicFactoryKeys = CreateDynamicDescriptorFactoryInventory()
                .Select(descriptor => descriptor.Key);
            var bootstrapKeys = SettingsLocalizationAssetBootstrap.Entries
                .Select(entry => entry.Key)
                .Where(IsManagedSettingsKey);

            AssertExactKeySet(contractKeys, descriptorKeys, "descriptor");
            AssertExactKeySet(dynamicContractKeys, dynamicFactoryKeys, "dynamic descriptor factory");
            AssertExactKeySet(contractKeys, bootstrapKeys, "bootstrap");

            var contractByKey = SettingsLocalizationContract.Entries.ToDictionary(
                entry => entry.Key,
                StringComparer.Ordinal);
            foreach (var bootstrapEntry in SettingsLocalizationAssetBootstrap.Entries.Where(
                         entry => IsManagedSettingsKey(entry.Key)))
            {
                Assert.That(
                    bootstrapEntry.IsSmart,
                    Is.EqualTo(contractByKey[bootstrapEntry.Key].IsSmart),
                    $"Bootstrap Smart metadata drift: {bootstrapEntry.Key}");
            }
        }

        [Test]
        public void PackageFreeSettingsCatalog_RejectsLocaleProjectionOmissionBeforeFallback()
        {
            var english = SettingsLocalizationContract.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Key,
                StringComparer.Ordinal);
            var korean = SettingsLocalizationContract.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Key,
                StringComparer.Ordinal);
            korean.Remove(SettingsLocalizationContract.Keys.InputRebindPushPrompt);
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> incompleteCatalog =
                new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    [PackageFreeLocalizedTextResolver.DefaultLocaleCode] = english,
                    [PackageFreeLocalizedTextResolver.KoreanLocaleCode] = korean,
                };
            var validationMethod = typeof(PackageFreeLocalizedTextResolver).GetMethod(
                "ValidateSettingsCatalog",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(validationMethod, Is.Not.Null);

            var invocationException = Assert.Throws<TargetInvocationException>(
                () => validationMethod.Invoke(null, new object[] { incompleteCatalog }));
            Assert.That(invocationException.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(
                invocationException.InnerException.Message,
                Does.Contain(PackageFreeLocalizedTextResolver.KoreanLocaleCode));
            Assert.That(
                invocationException.InnerException.Message,
                Does.Contain(SettingsLocalizationContract.Keys.InputRebindPushPrompt));
        }

        [Test]
        public void SettingsLocalizationContract_FallbackAndBootstrapCopiesMatchCommittedTables()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(SettingsLocalizationContract.Table);
            Assert.That(collection, Is.Not.Null);
            var englishTable = collection.GetTable(PackageFreeLocalizedTextResolver.DefaultLocaleCode) as StringTable;
            var koreanTable = collection.GetTable(PackageFreeLocalizedTextResolver.KoreanLocaleCode) as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);
            Assert.That(collection.SharedData.Entries, Has.Count.EqualTo(109));

            var contractKeys = SettingsLocalizationContract.Entries.Select(entry => entry.Key).ToArray();
            var sharedManagedKeys = collection.SharedData.Entries
                .Select(entry => entry.Key)
                .Where(IsManagedSettingsKey);
            AssertExactKeySet(contractKeys, sharedManagedKeys, "committed shared table");

            var bootstrapByKey = SettingsLocalizationAssetBootstrap.Entries.ToDictionary(
                entry => entry.Key,
                StringComparer.Ordinal);
            var packageFreeResolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var invariantResolverType = typeof(SettingsInputPresenter).Assembly.GetType(
                "Game.Feature.UI.Application.InvariantSettingsLocalizedTextResolver");
            Assert.That(invariantResolverType, Is.Not.Null);
            var invariantResolver = invariantResolverType
                .GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as ILocalizedTextResolver;
            Assert.That(invariantResolver, Is.Not.Null);

            foreach (var contractEntry in SettingsLocalizationContract.Entries)
            {
                var englishEntry = englishTable.GetEntry(contractEntry.Key);
                var koreanEntry = koreanTable.GetEntry(contractEntry.Key);
                Assert.That(englishEntry, Is.Not.Null, $"en-US missing: {contractEntry.Key}");
                Assert.That(koreanEntry, Is.Not.Null, $"ko-KR missing: {contractEntry.Key}");
                Assert.That(
                    englishEntry.KeyId,
                    Is.EqualTo(koreanEntry.KeyId),
                    $"Locale table ID parity: {contractEntry.Key}");
                Assert.That(englishEntry.IsSmart, Is.EqualTo(contractEntry.IsSmart), contractEntry.Key);
                Assert.That(koreanEntry.IsSmart, Is.EqualTo(contractEntry.IsSmart), contractEntry.Key);

                Assert.That(bootstrapByKey.ContainsKey(contractEntry.Key), Is.True, contractEntry.Key);
                var bootstrapEntry = bootstrapByKey[contractEntry.Key];
                Assert.That(
                    bootstrapEntry.English,
                    Is.EqualTo(englishEntry.LocalizedValue),
                    $"Bootstrap en-US drift: {contractEntry.Key}");
                Assert.That(
                    bootstrapEntry.Korean,
                    Is.EqualTo(koreanEntry.LocalizedValue),
                    $"Bootstrap ko-KR drift: {contractEntry.Key}");

                var descriptor = new LocalizedTextDescriptor(contractEntry.Table, contractEntry.Key);
                Assert.That(
                    packageFreeResolver.Resolve(descriptor),
                    Is.EqualTo(englishEntry.LocalizedValue),
                    $"Package-free en-US drift: {contractEntry.Key}");
                var representativeDescriptor = CreateRepresentativeDescriptor(contractEntry);
                Assert.That(
                    invariantResolver.Resolve(representativeDescriptor),
                    Is.EqualTo(FormatRepresentativeValue(contractEntry, englishEntry.LocalizedValue)),
                    $"Invariant en-US drift: {contractEntry.Key}");
            }

            packageFreeResolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            foreach (var contractEntry in SettingsLocalizationContract.Entries)
            {
                var descriptor = new LocalizedTextDescriptor(contractEntry.Table, contractEntry.Key);
                Assert.That(
                    packageFreeResolver.Resolve(descriptor),
                    Is.EqualTo(koreanTable.GetEntry(contractEntry.Key).LocalizedValue),
                    $"Package-free ko-KR drift: {contractEntry.Key}");
            }
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
        public void UiStringTable_ContainsSettingsAudioSmartStringEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);

            AssertAudioSmartEntries(collection.GetTable("en-US") as StringTable, "{0}%", "{0}% (Muted)");
            AssertAudioSmartEntries(collection.GetTable("ko-KR") as StringTable, "{0}%", "{0}% (음소거)");
        }

        [Test]
        public void UiStringTable_ContainsSettingsDisplaySmartStringEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);

            AssertDisplaySmartEntries(
                collection.GetTable("en-US") as StringTable,
                "{0}",
                "Reverting in {0}s",
                "Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {0} seconds.");
            AssertDisplaySmartEntries(
                collection.GetTable("ko-KR") as StringTable,
                "{0}",
                "{0}초 후 되돌림",
                "화면 설정을 미리 적용했습니다. {0}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.");
        }

        [Test]
        public void UiStringTable_ContainsSelectedSettingsInputDynamicEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);

            AssertInputDynamicEntries(
                collection.GetTable("en-US") as StringTable,
                "Key reassignment canceled.",
                "Input settings reset.",
                "This key cannot be used.",
                "Movement keys cannot overlap.",
                "Another key is already being reassigned.",
                "This key is already used by {0}.",
                "This key cannot be used.",
                "Press a key for Push...",
                "Press a key for Flip...");
            AssertInputDynamicEntries(
                collection.GetTable("ko-KR") as StringTable,
                "키 재지정을 취소했습니다.",
                "키 설정을 초기화했습니다.",
                "이 키는 사용할 수 없습니다.",
                "이동 키는 서로 중복될 수 없습니다.",
                "이미 다른 키를 재지정하고 있습니다.",
                "이 키는 이미 {0}에 할당되어 있습니다.",
                "이 키는 사용할 수 없습니다.",
                "밀기 키를 누르세요…",
                "뒤집기 키를 누르세요…");
        }

        [Test]
        public void UiStringTable_ContainsExactObjectiveHudSchemaInBothLocales()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(ObjectiveHudLocalization.Table);
            Assert.That(collection, Is.Not.Null);

            AssertObjectiveHudEntries(
                collection.GetTable("en-US") as StringTable,
                "Objectives",
                "Reach the Exit Zone ({0}/{1})",
                "Reach the designated zone ({0}/{1})",
                "Place a pushable box on the button ({0}/{1})",
                "Place the moon-marked box on the button ({0}/{1})");
            AssertObjectiveHudEntries(
                collection.GetTable("ko-KR") as StringTable,
                "목표",
                "탈출 지점에 도달하기 ({0}/{1})",
                "지정 구역에 도달하기 ({0}/{1})",
                "밀 수 있는 상자를 버튼 위에 놓기 ({0}/{1})",
                "달 문양 상자를 버튼 위에 놓기 ({0}/{1})");
        }

        [Test]
        public void UiStringTable_PriorityKoreanCopyPolishPass1_HasExactTwelveValuesAndPreservesEnglishAndPlaceholders()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);
            var englishTable = collection.GetTable("en-US") as StringTable;
            var koreanTable = collection.GetTable("ko-KR") as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var expected = new[]
            {
                (Key: "ui.hud.objectives.title", English: "Objectives", Korean: "목표"),
                (Key: "ui.hud.objective.reach_exit", English: "Reach the Exit Zone ({0}/{1})", Korean: "탈출 지점에 도달하기 ({0}/{1})"),
                (Key: "ui.hud.objective.activate_button", English: "Place a pushable box on the button ({0}/{1})", Korean: "밀 수 있는 상자를 버튼 위에 놓기 ({0}/{1})"),
                (Key: "ui.hud.objective.activate_moon_button", English: "Place the moon-marked box on the button ({0}/{1})", Korean: "달 문양 상자를 버튼 위에 놓기 ({0}/{1})"),
                (Key: "ui.hud.objective.reach_zone", English: "Reach the designated zone ({0}/{1})", Korean: "지정 구역에 도달하기 ({0}/{1})"),
                (Key: "ui.settings.input.reset_confirm.body", English: "Reset input settings to defaults?", Korean: "키 설정을 기본값으로 초기화할까요?"),
                (Key: "ui.settings.display.preview_confirm.title", English: "Confirm Display Preview", Korean: "화면 설정을 유지할까요?"),
                (Key: "ui.settings.display.preview_confirm.fullscreen_body", English: "Preview {0} x {1} in Borderless Fullscreen. These changes are temporary and will revert in {2} seconds unless you confirm.", Korean: "{0} × {1} 테두리 없는 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다."),
                (Key: "ui.settings.display.preview_confirm.windowed_body", English: "Preview {0} x {1} in Windowed mode. These changes are temporary and will revert in {2} seconds unless you confirm.", Korean: "{0} × {1} 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다."),
                (Key: "ui.main_menu.slot.confirm.restart.warning", English: "Existing progress will be replaced.", Korean: "기존 진행 상황이 초기화됩니다."),
                (Key: "ui.main_menu.slot.confirm.overwrite.warning", English: "Existing progress will be overwritten.", Korean: "기존 진행 상황을 덮어씁니다."),
                (Key: "ui.main_menu.quit_confirm.body", English: "Quit to desktop?", Korean: "게임을 종료하고 바탕 화면으로 나갈까요?"),
            };

            Assert.That(expected, Has.Length.EqualTo(12));
            Assert.That(expected.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(12));

            foreach (var entry in expected)
            {
                var english = englishTable.GetEntry(entry.Key);
                var korean = koreanTable.GetEntry(entry.Key);
                var shared = collection.SharedData.Entries.Single(candidate =>
                    string.Equals(candidate.Key, entry.Key, StringComparison.Ordinal));

                Assert.That(english, Is.Not.Null, $"en-US missing: {entry.Key}");
                Assert.That(korean, Is.Not.Null, $"ko-KR missing: {entry.Key}");
                Assert.That(english.KeyId, Is.EqualTo(shared.Id), entry.Key);
                Assert.That(korean.KeyId, Is.EqualTo(shared.Id), entry.Key);
                Assert.That(english.LocalizedValue, Is.EqualTo(entry.English), entry.Key);
                Assert.That(korean.LocalizedValue, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(
                    ExtractPlaceholderTokens(korean.LocalizedValue),
                    Is.EqualTo(ExtractPlaceholderTokens(english.LocalizedValue)),
                    entry.Key);
            }
        }

        [Test]
        public void UiStringTable_PriorityKoreanCopyPolishPass2_HasExactFiveValuesPreservesEnglishAndExcludesCurrentDisplay()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);
            var englishTable = collection.GetTable("en-US") as StringTable;
            var koreanTable = collection.GetTable("ko-KR") as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var expected = new[]
            {
                (Key: "ui.settings.input.already_rebinding", English: "Another key is already being reassigned.", Korean: "이미 다른 키를 재지정하고 있습니다."),
                (Key: "ui.settings.display.fullscreen_window", English: "Borderless Fullscreen", Korean: "테두리 없는 창 모드"),
                (Key: "ui.settings.input.rebind_push_prompt", English: "Press a key for Push...", Korean: "밀기 키를 누르세요…"),
                (Key: "ui.settings.input.rebind_flip_prompt", English: "Press a key for Flip...", Korean: "뒤집기 키를 누르세요…"),
                (Key: "ui.settings.input.action_conflict", English: "This key is already used by {0}.", Korean: "이 키는 이미 {0}에 할당되어 있습니다."),
            };

            Assert.That(expected, Has.Length.EqualTo(5));
            Assert.That(expected.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(5));

            foreach (var entry in expected)
            {
                var english = englishTable.GetEntry(entry.Key);
                var korean = koreanTable.GetEntry(entry.Key);
                var shared = collection.SharedData.Entries.Single(candidate =>
                    string.Equals(candidate.Key, entry.Key, StringComparison.Ordinal));

                Assert.That(english, Is.Not.Null, $"en-US missing: {entry.Key}");
                Assert.That(korean, Is.Not.Null, $"ko-KR missing: {entry.Key}");
                Assert.That(english.KeyId, Is.EqualTo(shared.Id), entry.Key);
                Assert.That(korean.KeyId, Is.EqualTo(shared.Id), entry.Key);
                Assert.That(english.LocalizedValue, Is.EqualTo(entry.English), entry.Key);
                Assert.That(korean.LocalizedValue, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(
                    ExtractPlaceholderTokens(korean.LocalizedValue),
                    Is.EqualTo(ExtractPlaceholderTokens(english.LocalizedValue)),
                    entry.Key);
            }

            Assert.That(
                koreanTable.GetEntry("ui.settings.display.current")?.LocalizedValue,
                Is.EqualTo("현재 화면 설정"),
                "Pass 2 explicitly excludes Current Display.");
            Assert.That(
                englishTable.GetEntry("ui.settings.display.current")?.LocalizedValue,
                Is.EqualTo("Current Display"),
                "Pass 2 must not change the excluded English entry.");
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesObjectiveHudCountsAndLocaleRoundTrip()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            AssertObjectiveHudResolvedText(
                resolver,
                "Objectives",
                "Reach the Exit Zone",
                "Reach the designated zone",
                "Place a pushable box on the button",
                "Place the moon-marked box on the button");

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            AssertObjectiveHudResolvedText(
                resolver,
                "목표",
                "탈출 지점에 도달하기",
                "지정 구역에 도달하기",
                "밀 수 있는 상자를 버튼 위에 놓기",
                "달 문양 상자를 버튼 위에 놓기");

            Assert.That(resolver.TrySetLocale("en-US"), Is.True);
            AssertObjectiveHudResolvedText(
                resolver,
                "Objectives",
                "Reach the Exit Zone",
                "Reach the designated zone",
                "Place a pushable box on the button",
                "Place the moon-marked box on the button");
        }

        [Test]
        public void UiStringTable_SmartFlagsMatchParsedArgumentExpressionsAcrossRequiredLocales()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("UI");
            Assert.That(collection, Is.Not.Null);
            var englishTable = collection.GetTable("en-US") as StringTable;
            var koreanTable = collection.GetTable("ko-KR") as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var smartFormatter = LocalizationSettings.StringDatabase?.SmartFormatter;
            Assert.That(smartFormatter, Is.Not.Null);

            foreach (var sharedEntry in collection.SharedData.Entries)
            {
                var englishEntry = englishTable.GetEntry(sharedEntry.Key);
                var koreanEntry = koreanTable.GetEntry(sharedEntry.Key);
                Assert.That(englishEntry, Is.Not.Null, $"en-US: {sharedEntry.Key}");
                Assert.That(koreanEntry, Is.Not.Null, $"ko-KR: {sharedEntry.Key}");
                Assert.That(
                    koreanEntry.IsSmart,
                    Is.EqualTo(englishEntry.IsSmart),
                    $"Smart metadata parity: {sharedEntry.Key}");
                AssertSmartFlagMatchesParsedArguments(smartFormatter, englishEntry, "en-US");
                AssertSmartFlagMatchesParsedArguments(smartFormatter, koreanEntry, "ko-KR");
            }
        }

        [Test]
        public void StageStringTable_ContainsCompleteStageDisplayNameEntries()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection("Stage");
            Assert.That(collection, Is.Not.Null);
            var activeStageEntries = LoadActiveStageDisplayNameEntries();
            var englishTable = collection.GetTable("en-US") as StringTable;
            var koreanTable = collection.GetTable("ko-KR") as StringTable;

            Assert.That(
                activeStageEntries.Select(entry => entry.Key).ToArray(),
                Is.EquivalentTo(StageDisplayNameEntries.Select(entry => entry.Key).ToArray()));
            Assert.That(activeStageEntries, Has.Length.EqualTo(13));
            Assert.That(
                activeStageEntries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(activeStageEntries.Length));
            AssertStageTable(englishTable, StageDisplayNameEntries, useKoreanValues: false);
            AssertStageTable(koreanTable, StageDisplayNameEntries, useKoreanValues: true);

            foreach (var entry in StageDisplayNameEntries)
            {
                var english = englishTable.GetEntry(entry.Key);
                var korean = koreanTable.GetEntry(entry.Key);
                var shared = collection.SharedData.Entries.Single(candidate =>
                    string.Equals(candidate.Key, entry.Key, StringComparison.Ordinal));
                Assert.That(english.KeyId, Is.EqualTo(shared.Id), entry.Key);
                Assert.That(english.KeyId, Is.EqualTo(korean.KeyId), entry.Key);
                Assert.That(
                    ExtractPlaceholderTokens(korean.LocalizedValue),
                    Is.EqualTo(ExtractPlaceholderTokens(english.LocalizedValue)),
                    entry.Key);
            }

            var legacyEnglish = englishTable.GetEntry(LegacyStageDisplayNameKey);
            var legacyKorean = koreanTable.GetEntry(LegacyStageDisplayNameKey);
            var legacyShared = collection.SharedData.Entries.Single(candidate =>
                string.Equals(candidate.Key, LegacyStageDisplayNameKey, StringComparison.Ordinal));
            Assert.That(legacyEnglish, Is.Not.Null);
            Assert.That(legacyKorean, Is.Not.Null);
            Assert.That(legacyEnglish.KeyId, Is.EqualTo(legacyShared.Id));
            Assert.That(legacyEnglish.KeyId, Is.EqualTo(legacyKorean.KeyId));
            Assert.That(legacyEnglish.LocalizedValue, Is.EqualTo(LegacyStageDisplayNameEnglishValue));
            Assert.That(legacyKorean.LocalizedValue, Is.EqualTo(LegacyStageDisplayNameKoreanValue));
            Assert.That(
                activeStageEntries.Select(entry => entry.Key),
                Does.Not.Contain(LegacyStageDisplayNameKey));
        }

        [Test]
        public void FirstLaunch_PreservesUnitySelectedKoreanLocale_WhenNoPreferenceExists()
        {
            var koreanLocale = RequireAvailableLocale("ko-KR");
            var store = new FakeUiLocalePreferenceStore();
            using var resolver = CreateUnityResolver(
                store,
                koreanLocale);

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(koreanLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void CommandLineSelectedLocale_IsNotOverwritten_WhenNoPreferenceExists()
        {
            var koreanLocale = RequireAvailableLocale("ko-KR");
            LocalizationSettings.SelectedLocale = koreanLocale;
            var selectedLocaleEventCount = 0;
            void HandleSelectedLocaleChanged(Locale _) => selectedLocaleEventCount++;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;

            var store = new FakeUiLocalePreferenceStore();
            try
            {
                using var resolver = CreateUnityResolverFromCurrentSelection(store);
                var resolverEventCount = 0;
                resolver.LocaleChanged += () => resolverEventCount++;

                Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(koreanLocale));
                Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
                Assert.That(selectedLocaleEventCount, Is.EqualTo(0));
                Assert.That(store.SaveCallCount, Is.EqualTo(0));

                Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
                Assert.That(resolverEventCount, Is.EqualTo(0));
                Assert.That(store.SaveCallCount, Is.EqualTo(0));
            }
            finally
            {
                LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            }
        }

        [Test]
        public void StartupLocale_NoPreference_PreservesUnitySelectedEnglishLocale()
        {
            var englishLocale = RequireAvailableLocale("en-US");
            var store = new FakeUiLocalePreferenceStore();
            using var resolver = CreateUnityResolver(
                store,
                englishLocale);

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(englishLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StartupLocale_SavedEnglishPreference_OverridesUnitySelectedKoreanLocale()
        {
            var englishLocale = RequireAvailableLocale("en-US");
            var store = new FakeUiLocalePreferenceStore("en-US");
            using var resolver = CreateUnityResolver(
                store,
                RequireAvailableLocale("ko-KR"));

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(englishLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StartupLocale_SavedKoreanPreference_OverridesUnitySelectedEnglishLocale()
        {
            var koreanLocale = RequireAvailableLocale("ko-KR");
            var store = new FakeUiLocalePreferenceStore("ko-KR");
            using var resolver = CreateUnityResolver(
                store,
                RequireAvailableLocale("en-US"));

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(koreanLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StartupLocale_InvalidSavedPreference_PreservesUnitySelectedKoreanLocale()
        {
            var koreanLocale = RequireAvailableLocale("ko-KR");
            var store = new FakeUiLocalePreferenceStore("fr-FR");
            using var resolver = CreateUnityResolver(
                store,
                koreanLocale);

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(koreanLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StartupLocale_MalformedSavedPreference_PreservesUnitySelectedKoreanLocale()
        {
            var koreanLocale = RequireAvailableLocale("ko-KR");
            using var resolver = CreateUnityResolver(
                new FakeUiLocalePreferenceStore("   "),
                koreanLocale);

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(koreanLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
        }

        [Test]
        public void StartupLocale_NoPreferenceAndNoUnitySelection_FallsBackToEnglish()
        {
            var englishLocale = RequireAvailableLocale("en-US");
            var store = new FakeUiLocalePreferenceStore();
            using var resolver = CreateUnityResolver(
                store,
                selectedLocale: null);

            Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(englishLocale));
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
            Assert.That(store.SaveCallCount, Is.EqualTo(0));
        }

        [Test]
        public void StartupLocale_UnsupportedUnitySelection_FallsBackToEnglish()
        {
            var unsupportedLocale = Locale.CreateLocale("fr-FR");
            try
            {
                var englishLocale = RequireAvailableLocale("en-US");
                var store = new FakeUiLocalePreferenceStore();
                using var resolver = CreateUnityResolver(
                    store,
                    unsupportedLocale);

                Assert.That(LocalizationSettings.SelectedLocale, Is.SameAs(englishLocale));
                Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("en-US"));
                Assert.That(store.SaveCallCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unsupportedLocale);
            }
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
                Is.EqualTo("연구실-01"));
            Assert.That(eventCount, Is.EqualTo(1));

            Assert.That(resolver.TrySetLocale("fr-FR"), Is.False);
            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(resolver.Resolve(new LocalizedTextDescriptor("UI", "ui.settings.missing")), Is.EqualTo("[UI:ui.settings.missing]"));
        }

        [Test]
        public void StageDisplayNameDescriptors_ResolveAllOfficialNamesAcrossLocaleRoundTrip()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            AssertStageDescriptorsResolve(resolver, useKoreanValues: false);
            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            AssertStageDescriptorsResolve(resolver, useKoreanValues: true);
            Assert.That(resolver.TrySetLocale("en-US"), Is.True);
            AssertStageDescriptorsResolve(resolver, useKoreanValues: false);

            Assert.That(
                resolver.Resolve(StageDisplayNameTextDescriptors.Create(LegacyStageDisplayNameKey)),
                Is.EqualTo(LegacyStageDisplayNameEnglishValue));
        }

        [Test]
        public void PackageFreeStageFallback_MatchesCompleteLocaleTablesAndPreservesUnknownKeys()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(StageDisplayNameKeys.Table);
            var englishTable = collection?.GetTable(PackageFreeLocalizedTextResolver.DefaultLocaleCode) as StringTable;
            var koreanTable = collection?.GetTable(PackageFreeLocalizedTextResolver.KoreanLocaleCode) as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var fallbackResolver = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            using var unityResolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            var stageKeys = StageDisplayNameEntries
                .Select(entry => entry.Key)
                .Append(LegacyStageDisplayNameKey)
                .ToArray();
            Assert.That(stageKeys, Has.Length.EqualTo(14));

            var englishFallbackValues = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in stageKeys)
            {
                var descriptor = StageDisplayNameTextDescriptors.Create(key);
                var expected = englishTable.GetEntry(key)?.LocalizedValue;
                Assert.That(expected, Is.Not.Null.And.Not.Empty, $"en-US table missing {key}");
                Assert.That(fallbackResolver.Resolve(descriptor), Is.EqualTo(expected), $"en-US fallback {key}");
                Assert.That(unityResolver.Resolve(descriptor), Is.EqualTo(expected), $"en-US String Table {key}");
                englishFallbackValues.Add(key, fallbackResolver.Resolve(descriptor));
            }

            var unknown = StageDisplayNameTextDescriptors.Create("stage.unknown.display_name");
            Assert.That(fallbackResolver.Resolve(unknown), Is.EqualTo("[Stage:stage.unknown.display_name]"));
            Assert.That(unityResolver.Resolve(unknown), Is.EqualTo("[Stage:stage.unknown.display_name]"));

            fallbackResolver.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            Assert.That(unityResolver.TrySetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode), Is.True);
            foreach (var key in stageKeys)
            {
                var descriptor = StageDisplayNameTextDescriptors.Create(key);
                var expected = koreanTable.GetEntry(key)?.LocalizedValue;
                Assert.That(expected, Is.Not.Null.And.Not.Empty, $"ko-KR table missing {key}");
                Assert.That(fallbackResolver.Resolve(descriptor), Is.EqualTo(expected), $"ko-KR fallback {key}");
                Assert.That(unityResolver.Resolve(descriptor), Is.EqualTo(expected), $"ko-KR String Table {key}");
                if (!string.Equals(expected, englishTable.GetEntry(key)?.LocalizedValue, StringComparison.Ordinal))
                {
                    Assert.That(fallbackResolver.Resolve(descriptor), Is.Not.EqualTo(englishFallbackValues[key]), key);
                }
            }

            Assert.That(fallbackResolver.Resolve(unknown), Is.EqualTo("[Stage:stage.unknown.display_name]"));
            Assert.That(unityResolver.Resolve(unknown), Is.EqualTo("[Stage:stage.unknown.display_name]"));
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesSettingsAudioSmartStringArguments()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.AudioVolumeValue(50, isMuted: false)),
                Is.EqualTo("50%"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.AudioVolumeValue(50, isMuted: true)),
                Is.EqualTo("50% (Muted)"));

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.AudioVolumeValue(50, isMuted: false)),
                Is.EqualTo("50%"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.AudioVolumeValue(50, isMuted: true)),
                Is.EqualTo("50% (음소거)"));
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesSettingsDisplaySmartStringArguments()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayResolutionValue("1920 x 1080")),
                Is.EqualTo("1920 x 1080"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewCountdown(10)),
                Is.EqualTo("Reverting in 10s"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewActiveStatus(10)),
                Is.EqualTo("Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in 10 seconds."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewRevertedStatus()),
                Is.EqualTo("Preview reverted to the previous saved display settings."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplaySavedStatus()),
                Is.EqualTo("Display settings saved."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayExternalDriftStatus()),
                Is.EqualTo("Current display changed outside saved settings. Saved settings remain unchanged until you apply again."));

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayResolutionValue("1920 x 1080")),
                Is.EqualTo("1920 x 1080"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewCountdown(10)),
                Is.EqualTo("10초 후 되돌림"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewActiveStatus(10)),
                Is.EqualTo("화면 설정을 미리 적용했습니다. 10초 안에 확인하지 않으면 이전 설정으로 돌아갑니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayPreviewRevertedStatus()),
                Is.EqualTo("화면 설정이 이전에 저장된 값으로 돌아갔습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplaySavedStatus()),
                Is.EqualTo("화면 설정이 저장되었습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.DisplayExternalDriftStatus()),
                Is.EqualTo("현재 화면 설정이 저장된 설정과 다릅니다. 다시 적용하기 전까지 저장된 설정은 변경되지 않습니다."));
        }

        [Test]
        public void UnityStringTableTextResolver_ResolvesSelectedSettingsInputStatus()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindCanceled()),
                Is.EqualTo("Key reassignment canceled."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputResetComplete()),
                Is.EqualTo("Input settings reset."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputReservedKey()),
                Is.EqualTo("This key cannot be used."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputMovementConflict()),
                Is.EqualTo("Movement keys cannot overlap."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputAlreadyRebinding()),
                Is.EqualTo("Another key is already being reassigned."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputActionConflict(
                    SettingsStaticTextDescriptors.Flip)),
                Is.EqualTo("This key is already used by Flip."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputUnsupportedKey()),
                Is.EqualTo("This key cannot be used."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Push)),
                Is.EqualTo("Press a key for Push..."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Flip)),
                Is.EqualTo("Press a key for Flip..."));

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindCanceled()),
                Is.EqualTo("키 재지정을 취소했습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputResetComplete()),
                Is.EqualTo("키 설정을 초기화했습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputReservedKey()),
                Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputMovementConflict()),
                Is.EqualTo("이동 키는 서로 중복될 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputAlreadyRebinding()),
                Is.EqualTo("이미 다른 키를 재지정하고 있습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputActionConflict(
                    SettingsStaticTextDescriptors.Flip)),
                Is.EqualTo("이 키는 이미 뒤집기에 할당되어 있습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputUnsupportedKey()),
                Is.EqualTo("이 키는 사용할 수 없습니다."));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Push)),
                Is.EqualTo("밀기 키를 누르세요…"));
            Assert.That(
                resolver.Resolve(SettingsDynamicTextDescriptors.InputRebindPrompt(KeyboardBindableAction.Flip)),
                Is.EqualTo("뒤집기 키를 누르세요…"));
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
            var climateCrisisKr = UiTestPrefabAssetUtility.LoadClimateCrisisKrFont();
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            using var harness = SettingsProductionLocalizationRuntimeTests.GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            var titleLabel = GetText(harness.SettingsView, "_titleLabel");

            Assert.That(titleLabel.text, Is.EqualTo("Settings"));
            Assert.That(harness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("Language"));
            Assert.That(harness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("English"));

            harness.SettingsView.ClickDisplayTab();
            harness.SettingsView.DisplayView.ClickLanguageCycle();

            Assert.That(resolver.CurrentLocaleCode, Is.EqualTo("ko-KR"));
            Assert.That(titleLabel.text, Is.EqualTo("설정"));
            Assert.That(titleLabel.font, Is.SameAs(climateCrisisKr));
            Assert.That(harness.SettingsView.DisplayView.LanguageLabelText, Is.EqualTo("언어"));
            Assert.That(harness.SettingsView.DisplayView.CurrentLanguageText, Is.EqualTo("한국어"));
        }

        [Test]
        public void RuntimeSettings_RequiredStaticShellUsesUnityTableBindingsAndRawKeyNamesRemainUnlocalized()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            using var harness = SettingsProductionLocalizationRuntimeTests.GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();

            AssertSettingsStaticShell(
                harness.SettingsView,
                "Master",
                "Background Music",
                "Effects",
                "Mute",
                "Current Display",
                "Resolution",
                "Only automatically detected resolutions are shown.",
                "Borderless Fullscreen",
                "Apply",
                "Revert");
            Assert.That(
                GetTextAtPath(
                    harness.SettingsView,
                    "SettingsSectionHost/SettingsDisplaySection/FullscreenRow/FullscreenToggle/Label").text,
                Is.EqualTo("Off"));
            AssertRawInputNames(harness.SettingsView, "J", "K");

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

            AssertSettingsStaticShell(
                harness.SettingsView,
                "전체 음량",
                "배경 음악",
                "효과음",
                "음소거",
                "현재 화면 설정",
                "해상도",
                "자동으로 감지된 해상도만 표시됩니다.",
                "테두리 없는 창 모드",
                "적용",
                "되돌리기");
            Assert.That(
                GetTextAtPath(
                    harness.SettingsView,
                    "SettingsSectionHost/SettingsDisplaySection/FullscreenRow/FullscreenToggle/Label").text,
                Is.EqualTo("꺼짐"));
            AssertRawInputNames(harness.SettingsView, "J", "K");
        }

        [Test]
        public void RuntimeSettings_StaticShellLocaleSwitchPreservesAuthoredLayoutGeometry()
        {
            using var resolver = CreateUnityResolver(new FakeUiLocalePreferenceStore());
            using var harness = SettingsProductionLocalizationRuntimeTests.GameplaySettingsHarness.Create(resolver);

            harness.ShowSettings();
            ForceSettingsLayout(harness.SettingsView);
            var englishGeometry = CaptureSettingsGeometry(harness.SettingsView);

            Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);
            ForceSettingsLayout(harness.SettingsView);
            var koreanGeometry = CaptureSettingsGeometry(harness.SettingsView);

            foreach (var pair in englishGeometry)
            {
                Assert.That(koreanGeometry[pair.Key].x, Is.EqualTo(pair.Value.x).Within(0.01f), $"{pair.Key} width");
                Assert.That(koreanGeometry[pair.Key].y, Is.EqualTo(pair.Value.y).Within(0.01f), $"{pair.Key} height");
            }
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

                AssertPauseLabels(view, "Paused", "Resume", "Settings", "Retry", "Main Menu");

                Assert.That(resolver.TrySetLocale("ko-KR"), Is.True);

                AssertPauseLabels(view, "일시 정지", "계속하기", "설정", "다시 시도", "메인 메뉴");
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
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(
                TypographyThemeValidator.ThemeAssetPath);
            Assert.That(theme, Is.Not.Null);

            try
            {
                view.BindStaticLocalization(
                    MainMenuStaticTextPayload.Default,
                    resolver,
                    DefaultLocalizedTypographyResolver.Instance,
                    typographyTheme: theme);

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
            var resolver = UiSettingsBridgeAssembly.CreatePersistentSettingsLocalizedTextResolver(
                new FakeUiLocalePreferenceStore("en-US"));

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
            Assert.That(gameplayInstallerSource, Does.Contain("localizedTextResolver: _localizedTextResolver"));
            Assert.That(gameplayInstallerSource, Does.Contain("new StageInfoPresenter(_localizedTextResolver)"));
            Assert.That(gameplayInstallerSource, Does.Contain("(_localizedTextResolver as IDisposable)?.Dispose()"));

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

        private static UnityStringTableTextResolver CreateUnityResolver(
            FakeUiLocalePreferenceStore store)
        {
            return CreateUnityResolver(store, RequireAvailableLocale("en-US"));
        }

        private static UnityStringTableTextResolver CreateUnityResolver(
            FakeUiLocalePreferenceStore store,
            Locale selectedLocale)
        {
            LocalizationSettings.SelectedLocale = selectedLocale;
            return CreateUnityResolverFromCurrentSelection(store);
        }

        private static UnityStringTableTextResolver CreateUnityResolverFromCurrentSelection(
            FakeUiLocalePreferenceStore store)
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

        private static Locale RequireAvailableLocale(string localeCode)
        {
            var locale = LocalizationSettings.AvailableLocales?.GetLocale(localeCode);
            Assert.That(locale, Is.Not.Null, $"Required test Locale is unavailable: {localeCode}");
            return locale;
        }

        private static bool IsManagedSettingsKey(string key)
        {
            return key != null &&
                   (key.StartsWith("ui.settings.", StringComparison.Ordinal) ||
                    string.Equals(key, SettingsLocalizationContract.Keys.Back, StringComparison.Ordinal) ||
                    string.Equals(key, SettingsLocalizationContract.Keys.Cancel, StringComparison.Ordinal));
        }

        private static IReadOnlyList<LocalizedTextDescriptor> CreateDynamicDescriptorFactoryInventory()
        {
            var descriptors = new List<LocalizedTextDescriptor>();
            var factoryMethods = typeof(SettingsDynamicTextDescriptors)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.ReturnType == typeof(LocalizedTextDescriptor));

            foreach (var factoryMethod in factoryMethods)
            {
                var argumentSets = new List<object[]> { new object[factoryMethod.GetParameters().Length] };
                var parameters = factoryMethod.GetParameters();
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    var parameter = parameters[parameterIndex];
                    if (parameter.ParameterType == typeof(int))
                    {
                        foreach (var arguments in argumentSets)
                        {
                            arguments[parameterIndex] = 7;
                        }
                    }
                    else if (parameter.ParameterType == typeof(string))
                    {
                        foreach (var arguments in argumentSets)
                        {
                            arguments[parameterIndex] = "1920 x 1080";
                        }
                    }
                    else if (parameter.ParameterType == typeof(bool))
                    {
                        var falseArgumentSets = argumentSets
                            .Select(arguments => (object[])arguments.Clone())
                            .ToArray();
                        foreach (var arguments in argumentSets)
                        {
                            arguments[parameterIndex] = true;
                        }

                        foreach (var arguments in falseArgumentSets)
                        {
                            arguments[parameterIndex] = false;
                            argumentSets.Add(arguments);
                        }
                    }
                    else if (parameter.ParameterType == typeof(KeyboardBindableAction))
                    {
                        var flipArgumentSets = argumentSets
                            .Select(arguments => (object[])arguments.Clone())
                            .ToArray();
                        foreach (var arguments in argumentSets)
                        {
                            arguments[parameterIndex] = KeyboardBindableAction.Push;
                        }

                        foreach (var arguments in flipArgumentSets)
                        {
                            arguments[parameterIndex] = KeyboardBindableAction.Flip;
                            argumentSets.Add(arguments);
                        }
                    }
                    else if (parameter.ParameterType == typeof(LocalizedTextDescriptor))
                    {
                        foreach (var arguments in argumentSets)
                        {
                            arguments[parameterIndex] = SettingsStaticTextDescriptors.Flip;
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Settings dynamic descriptor factory '{factoryMethod.Name}' has unsupported " +
                            $"representative parameter '{parameter.Name}' ({parameter.ParameterType.Name}).");
                    }
                }

                foreach (var arguments in argumentSets)
                {
                    descriptors.Add((LocalizedTextDescriptor)factoryMethod.Invoke(null, arguments));
                }
            }

            return descriptors;
        }

        private static LocalizedTextDescriptor CreateRepresentativeDescriptor(
            SettingsLocalizationContractEntry entry)
        {
            if (string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.InputActionConflict,
                    StringComparison.Ordinal))
            {
                return new LocalizedTextDescriptor(
                    entry.Table,
                    entry.Key,
                    arguments: new object[] { SettingsStaticTextDescriptors.Flip });
            }

            if (string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody,
                    StringComparison.Ordinal) ||
                string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody,
                    StringComparison.Ordinal))
            {
                return new LocalizedTextDescriptor(
                    entry.Table,
                    entry.Key,
                    arguments: new object[] { 7, 8, 9 });
            }

            return entry.FormatKind == SettingsLocalizationFormatKind.None
                ? new LocalizedTextDescriptor(entry.Table, entry.Key)
                : new LocalizedTextDescriptor(
                    entry.Table,
                    entry.Key,
                    arguments: new object[] { 7 });
        }

        private static string FormatRepresentativeValue(
            SettingsLocalizationContractEntry entry,
            string value)
        {
            if (string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.InputActionConflict,
                    StringComparison.Ordinal))
            {
                return value.Replace("{0}", "Flip");
            }

            if (string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody,
                    StringComparison.Ordinal) ||
                string.Equals(
                    entry.Key,
                    SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody,
                    StringComparison.Ordinal))
            {
                return value
                    .Replace("{0}", "7")
                    .Replace("{1}", "8")
                    .Replace("{2}", "9");
            }

            return entry.FormatKind == SettingsLocalizationFormatKind.None
                ? value
                : value.Replace("{0}", "7");
        }

        private static void AssertExactKeySet(
            IEnumerable<string> expectedKeys,
            IEnumerable<string> actualKeys,
            string projectionName)
        {
            var expected = new HashSet<string>(expectedKeys, StringComparer.Ordinal);
            var actual = new HashSet<string>(actualKeys, StringComparer.Ordinal);
            var missing = expected.Except(actual).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            var unexpected = actual.Except(expected).OrderBy(key => key, StringComparer.Ordinal).ToArray();

            Assert.That(
                missing,
                Is.Empty,
                $"{projectionName} projection missing Settings localization keys: {string.Join(", ", missing)}");
            Assert.That(
                unexpected,
                Is.Empty,
                $"{projectionName} projection contains unmanaged Settings localization keys: {string.Join(", ", unexpected)}");
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
                Assert.That(tableEntry.IsSmart, Is.EqualTo(entry.IsSmart), entry.Key);
            }
        }

        private static void AssertAudioSmartEntries(StringTable table, string volumeValue, string mutedValue)
        {
            Assert.That(table, Is.Not.Null);
            AssertAudioSmartEntry(table, SettingsDynamicTextDescriptors.AudioVolumeValueKey, volumeValue);
            AssertAudioSmartEntry(table, SettingsDynamicTextDescriptors.AudioVolumeValueMutedKey, mutedValue);
        }

        private static void AssertAudioSmartEntry(StringTable table, string key, string expectedValue)
        {
            var entry = table.GetEntry(key);
            Assert.That(entry, Is.Not.Null, key);
            Assert.That(entry.LocalizedValue, Is.EqualTo(expectedValue));
            Assert.That(entry.LocalizedValue, Does.Contain("{0}"));
            Assert.That(entry.IsSmart, Is.True, key);
        }

        private static void AssertDisplaySmartEntries(
            StringTable table,
            string resolutionValue,
            string previewCountdownValue,
            string previewActiveStatusValue)
        {
            Assert.That(table, Is.Not.Null);
            var entry = table.GetEntry(SettingsDynamicTextDescriptors.DisplayResolutionValueKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.DisplayResolutionValueKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(resolutionValue));
            Assert.That(entry.LocalizedValue, Does.Contain("{0}"));
            Assert.That(entry.IsSmart, Is.True, SettingsDynamicTextDescriptors.DisplayResolutionValueKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.DisplayPreviewCountdownKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.DisplayPreviewCountdownKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(previewCountdownValue));
            Assert.That(entry.LocalizedValue, Does.Contain("{0}"));
            Assert.That(entry.IsSmart, Is.True, SettingsDynamicTextDescriptors.DisplayPreviewCountdownKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.DisplayPreviewActiveStatusKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.DisplayPreviewActiveStatusKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(previewActiveStatusValue));
            Assert.That(entry.LocalizedValue, Does.Contain("{0}"));
            Assert.That(entry.IsSmart, Is.True, SettingsDynamicTextDescriptors.DisplayPreviewActiveStatusKey);
        }

        private static void AssertInputDynamicEntries(
            StringTable table,
            string rebindCanceledValue,
            string resetCompleteValue,
            string reservedKeyValue,
            string movementConflictValue,
            string alreadyRebindingValue,
            string actionConflictValue,
            string unsupportedKeyValue,
            string rebindPushPromptValue,
            string rebindFlipPromptValue)
        {
            Assert.That(table, Is.Not.Null);
            var entry = table.GetEntry(SettingsDynamicTextDescriptors.InputRebindCanceledKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputRebindCanceledKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(rebindCanceledValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputRebindCanceledKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputResetCompleteKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputResetCompleteKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(resetCompleteValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputResetCompleteKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputReservedKeyKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputReservedKeyKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(reservedKeyValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputReservedKeyKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputMovementConflictKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputMovementConflictKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(movementConflictValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputMovementConflictKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputAlreadyRebindingKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputAlreadyRebindingKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(alreadyRebindingValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputAlreadyRebindingKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputActionConflictKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputActionConflictKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(actionConflictValue));
            Assert.That(entry.LocalizedValue, Does.Contain("{0}"));
            Assert.That(entry.IsSmart, Is.True, SettingsDynamicTextDescriptors.InputActionConflictKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputUnsupportedKeyKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputUnsupportedKeyKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(unsupportedKeyValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputUnsupportedKeyKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputRebindPushPromptKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputRebindPushPromptKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(rebindPushPromptValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputRebindPushPromptKey);

            entry = table.GetEntry(SettingsDynamicTextDescriptors.InputRebindFlipPromptKey);
            Assert.That(entry, Is.Not.Null, SettingsDynamicTextDescriptors.InputRebindFlipPromptKey);
            Assert.That(entry.LocalizedValue, Is.EqualTo(rebindFlipPromptValue));
            Assert.That(entry.LocalizedValue, Is.Not.Empty);
            Assert.That(entry.IsSmart, Is.False, SettingsDynamicTextDescriptors.InputRebindFlipPromptKey);
        }

        private static void AssertObjectiveHudEntries(
            StringTable table,
            string header,
            string reachExit,
            string reachZone,
            string activateButton,
            string activateMoonButton)
        {
            Assert.That(table, Is.Not.Null);
            AssertObjectiveHudEntry(table, ObjectiveHudLocalization.Keys.Header, header, isSmart: false);
            AssertObjectiveHudEntry(table, ObjectiveHudLocalization.Keys.ReachExit, reachExit, isSmart: true);
            AssertObjectiveHudEntry(table, ObjectiveHudLocalization.Keys.ReachZone, reachZone, isSmart: true);
            AssertObjectiveHudEntry(table, ObjectiveHudLocalization.Keys.ActivateButton, activateButton, isSmart: true);
            AssertObjectiveHudEntry(table, ObjectiveHudLocalization.Keys.ActivateMoonButton, activateMoonButton, isSmart: true);
        }

        private static void AssertObjectiveHudEntry(
            StringTable table,
            string key,
            string expected,
            bool isSmart)
        {
            var entry = table.GetEntry(key);
            Assert.That(entry, Is.Not.Null, key);
            Assert.That(entry.LocalizedValue, Is.EqualTo(expected), key);
            Assert.That(entry.IsSmart, Is.EqualTo(isSmart), key);
        }

        private static void AssertObjectiveHudResolvedText(
            ILocalizedTextResolver resolver,
            string header,
            string reachExit,
            string reachZone,
            string activateButton,
            string activateMoonButton)
        {
            Assert.That(
                resolver.Resolve(ObjectiveHudLocalization.HeaderDescriptor),
                Is.EqualTo(header));
            AssertObjectiveHudResolvedCounts(
                resolver,
                GameplayObjectivePresentationKind.ReachExit,
                reachExit);
            AssertObjectiveHudResolvedCounts(
                resolver,
                GameplayObjectivePresentationKind.ReachZone,
                reachZone);
            AssertObjectiveHudResolvedCounts(
                resolver,
                GameplayObjectivePresentationKind.ActivateButton,
                activateButton);
            AssertObjectiveHudResolvedCounts(
                resolver,
                GameplayObjectivePresentationKind.ActivateMoonButton,
                activateMoonButton);
        }

        private static void AssertObjectiveHudResolvedCounts(
            ILocalizedTextResolver resolver,
            GameplayObjectivePresentationKind kind,
            string expectedTitle)
        {
            var counts = new[]
            {
                (Completed: 1, Required: 1),
                (Completed: 9, Required: 10),
                (Completed: 99, Required: 99),
            };
            foreach (var count in counts)
            {
                Assert.That(
                    ObjectiveHudLocalization.TryCreateConditionDescriptor(
                        kind,
                        count.Completed,
                        count.Required,
                        out var descriptor),
                    Is.True);
                Assert.That(
                    resolver.Resolve(descriptor),
                    Is.EqualTo($"{expectedTitle} ({count.Completed}/{count.Required})"));
            }
        }

        private static void AssertSmartFlagMatchesParsedArguments(
            SmartFormatter smartFormatter,
            StringTableEntry entry,
            string localeCode)
        {
            var parsedFormat = smartFormatter.Parser.ParseFormat(
                entry.LocalizedValue,
                smartFormatter.GetNotEmptyFormatterExtensionNames());
            var hasArgumentExpression = parsedFormat.Items.Any(item => item is Placeholder);

            Assert.That(
                entry.IsSmart,
                Is.EqualTo(hasArgumentExpression),
                $"{localeCode}: {entry.Key} must be Smart if and only if its parsed value contains an argument expression.");
        }

        private static void AssertStageTable(
            StringTable table,
            IReadOnlyList<(string StageId, string Key, string English, string Korean)> expectedEntries,
            bool useKoreanValues)
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
                Assert.That(
                    tableEntry.LocalizedValue,
                    Is.EqualTo(useKoreanValues ? entry.Korean : entry.English),
                    entry.StageId);
                Assert.That(tableEntry.LocalizedValue, Is.Not.Empty, entry.StageId);
            }
        }

        private static void AssertStageDescriptorsResolve(
            ILocalizedTextResolver resolver,
            bool useKoreanValues)
        {
            foreach (var entry in StageDisplayNameEntries)
            {
                var stageId = StageId.CreateOrThrow(entry.StageId);
                var descriptor = StageDisplayNameTextDescriptors.ForStage(stageId);
                Assert.That(descriptor.Key, Is.EqualTo(entry.Key), entry.StageId);
                Assert.That(
                    resolver.Resolve(descriptor),
                    Is.EqualTo(useKoreanValues ? entry.Korean : entry.English),
                    entry.StageId);
            }
        }

        private static string[] ExtractPlaceholderTokens(string value)
        {
            var tokens = new List<string>();
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] != '{')
                {
                    continue;
                }

                var closeIndex = value.IndexOf('}', index + 1);
                if (closeIndex < 0)
                {
                    tokens.Add(value.Substring(index));
                    break;
                }

                tokens.Add(value.Substring(index, closeIndex - index + 1));
                index = closeIndex;
            }

            return tokens.ToArray();
        }

        private static (string StageId, string Key)[] LoadActiveStageDisplayNameEntries()
        {
            var expectedEntries = StageDisplayNameEntries.ToDictionary(
                entry => entry.Key,
                entry => entry.StageId,
                StringComparer.Ordinal);
            var contentEntries = AssetDatabase
                .FindAssets($"t:{nameof(StageContentEntry)}", new[] { StageContentPaths.CampaignLevel01StagesRoot })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StageContentEntry>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(entry => entry != null && entry.StageId.IsValid)
                .ToDictionary(entry => entry.StageId.Value, StringComparer.Ordinal);
            var sequence = AssetDatabase.LoadAssetAtPath<CampaignStageSequenceDefinition>(
                StageContentPaths.CampaignStageSequenceAssetPath);
            Assert.That(sequence, Is.Not.Null);
            Assert.That(sequence.Entries.Count, Is.EqualTo(13));

            var entries = sequence.Entries
                .Select(sequenceEntry =>
                {
                    Assert.That(
                        contentEntries.TryGetValue(sequenceEntry.StageId.Value, out var entry),
                        Is.True,
                        sequenceEntry.StageId.Value);
                    Assert.That(entry.PresentationDefinition, Is.Not.Null, entry.StageId.Value);
                    var key = entry.PresentationDefinition.DisplayNameKey;
                    Assert.That(key, Is.EqualTo(StageDisplayNameKeys.ForStage(entry.StageId)), entry.StageId.Value);
                    Assert.That(expectedEntries.TryGetValue(key, out var expectedStageId), Is.True, entry.StageId.Value);
                    Assert.That(expectedStageId, Is.EqualTo(entry.StageId.Value), entry.StageId.Value);
                    return (entry.StageId.Value, key);
                })
                .ToArray();

            return entries;
        }

        private static void AssertNoAssemblyReference(Assembly assembly, string referenceName)
        {
            Assert.That(
                assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray(),
                Does.Not.Contain(referenceName));
        }

        private static readonly (string StageId, string Key, string English, string Korean)[] StageDisplayNameEntries =
        {
            ("stage-0-1", "stage.stage-0-1.display_name", "Lab-01", "연구실-01"),
            ("stage-0-2", "stage.stage-0-2.display_name", "Lab-02", "연구실-02"),
            ("stage-0-3", "stage.stage-0-3.display_name", "Lab-03", "연구실-03"),
            ("stage-1-1", "stage.stage-1-1.display_name", "Lobby-01", "로비-01"),
            ("stage-1-2", "stage.stage-1-2.display_name", "Lobby-02", "로비-02"),
            ("stage-2-1", "stage.stage-2-1.display_name", "Ward[A]-01", "A병동-01"),
            ("stage-2-2", "stage.stage-2-2.display_name", "Ward[A]-02", "A병동-02"),
            ("stage-3-1", "stage.stage-3-1.display_name", "Ward[B]-01", "B병동-01"),
            ("stage-3-2", "stage.stage-3-2.display_name", "Ward[B]-02", "B병동-02"),
            ("stage-3-3", "stage.stage-3-3.display_name", "Ward[B]-03", "B병동-03"),
            ("stage-4-1", "stage.stage-4-1.display_name", "Morgue-01", "영안실-01"),
            ("stage-4-2", "stage.stage-4-2.display_name", "Morgue-02", "영안실-02"),
            ("stage-4-3", "stage.stage-4-3.display_name", "Morgue-03", "영안실-03"),
        };

        private const string LegacyStageDisplayNameKey =
            "stage.legacy-stage-5-1.display_name";
        private const string LegacyStageDisplayNameEnglishValue = "Legacy 5-1";
        private const string LegacyStageDisplayNameKoreanValue = "레거시 5-1";

        private static TMP_Text GetText(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} must exist.");
            var value = field.GetValue(target) as TMP_Text;
            Assert.That(value, Is.Not.Null);
            return value;
        }

        private static void AssertSettingsStaticShell(
            SettingsScreenView view,
            string audioMain,
            string audioBgm,
            string audioSfx,
            string mute,
            string currentDisplay,
            string resolution,
            string resolutionHint,
            string fullscreenWindow,
            string apply,
            string revert)
        {
            const string audioRoot = "SettingsSectionHost/SettingsAudioSection";
            Assert.That(GetTextAtPath(view, $"{audioRoot}/MainAudioRow/Label").text, Is.EqualTo(audioMain));
            Assert.That(GetTextAtPath(view, $"{audioRoot}/BgmAudioRow/Label").text, Is.EqualTo(audioBgm));
            Assert.That(GetTextAtPath(view, $"{audioRoot}/SfxAudioRow/Label").text, Is.EqualTo(audioSfx));
            Assert.That(GetTextAtPath(view, $"{audioRoot}/MainAudioRow/MuteToggle/Label").text, Is.EqualTo(mute));
            Assert.That(GetTextAtPath(view, $"{audioRoot}/BgmAudioRow/MuteToggle/Label").text, Is.EqualTo(mute));
            Assert.That(GetTextAtPath(view, $"{audioRoot}/SfxAudioRow/MuteToggle/Label").text, Is.EqualTo(mute));

            const string displayRoot = "SettingsSectionHost/SettingsDisplaySection";
            Assert.That(GetTextAtPath(view, $"{displayRoot}/CurrentDisplayRow/CurrentDisplayLabel").text, Is.EqualTo(currentDisplay));
            Assert.That(GetTextAtPath(view, $"{displayRoot}/ResolutionRow/ResolutionLabel").text, Is.EqualTo(resolution));
            Assert.That(GetTextAtPath(view, $"{displayRoot}/ResolutionHoverHint/ResolutionHoverHintText").text, Is.EqualTo(resolutionHint));
            Assert.That(GetTextAtPath(view, $"{displayRoot}/FullscreenRow/FullscreenLabel").text, Is.EqualTo(fullscreenWindow));
            Assert.That(GetTextAtPath(view, $"{displayRoot}/DisplayActionRow/DisplayApplyButton_New/MASK/Item/FlipChangeLabel").text, Is.EqualTo(apply));
            Assert.That(GetTextAtPath(view, $"{displayRoot}/DisplayActionRow/DisplayRevertButton_New/MASK/Item/FlipChangeLabel").text, Is.EqualTo(revert));
        }

        private static void AssertRawInputNames(
            SettingsScreenView view,
            string push,
            string flip)
        {
            Assert.That(view.InputView.IsMovementUsingArrowKeys, Is.False);
            Assert.That(GetText(view.InputView, "_pushCurrentText").text, Is.EqualTo(push));
            Assert.That(GetText(view.InputView, "_flipCurrentText").text, Is.EqualTo(flip));
        }

        private static TMP_Text GetTextAtPath(SettingsScreenView view, string path)
        {
            var target = view.transform.Find(path);
            Assert.That(target, Is.Not.Null, path);
            var text = target.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, path);
            return text;
        }

        private static Dictionary<string, UnityEngine.Vector2> CaptureSettingsGeometry(SettingsScreenView view)
        {
            var paths = new[]
            {
                "SettingsTabRow",
                "SettingsSectionHost",
                "SettingsSectionHost/SettingsAudioSection",
                "SettingsSectionHost/SettingsAudioSection/MainAudioRow",
                "SettingsSectionHost/SettingsAudioSection/MainAudioRow/Label",
                "SettingsSectionHost/SettingsAudioSection/MainAudioRow/Slider",
                "SettingsSectionHost/SettingsAudioSection/MainAudioRow/Value",
                "SettingsSectionHost/SettingsAudioSection/MainAudioRow/MuteToggle",
            };
            var geometry = new Dictionary<string, UnityEngine.Vector2>(StringComparer.Ordinal)
            {
                ["SettingsScreen"] = ((UnityEngine.RectTransform)view.transform).rect.size,
            };
            foreach (var path in paths)
            {
                var target = view.transform.Find(path) as UnityEngine.RectTransform;
                Assert.That(target, Is.Not.Null, path);
                geometry[path] = target.rect.size;
            }

            return geometry;
        }

        private static void ForceSettingsLayout(SettingsScreenView view)
        {
            UnityEngine.Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((UnityEngine.RectTransform)view.transform);
            UnityEngine.Canvas.ForceUpdateCanvases();
        }

        private static void AssertPauseLabels(
            PausePopupView view,
            string title,
            string resume,
            string settings,
            string retry,
            string mainMenu)
        {
            Assert.That(GetText(view, "_titleLabel").text, Is.EqualTo(title));
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
