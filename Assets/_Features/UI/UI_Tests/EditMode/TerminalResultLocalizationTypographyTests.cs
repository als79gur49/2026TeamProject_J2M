using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Feature.Gameplay.UIAccess.Models;
using Game.Feature.Stages;
using Game.Feature.UI.Application;
using Game.Feature.UI.Composition;
using Game.Feature.UI.Composition.Editor;
using Game.Feature.UI.Flow;
using Game.Feature.UI.Popups;
using Game.Feature.UI.Screens;
using Game.Feature.UI.ViewShared;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Game.Feature.UI.Tests
{
    public sealed class TerminalResultLocalizationTypographyTests
    {
        private const string ThemePath =
            "Assets/_Features/UI/UI_Composition/Authoring/Typography/GameplayUiTypographyTheme.asset";
        private const string ClimateSdfPath =
            "Assets/_Shared/UI/Fonts/ClimateCrisisKR-2000 SDF.asset";
        private byte[] _climateSerializedBaseline;

        [OneTimeSetUp]
        public void SnapshotClimateSerializedBaseline()
        {
            _climateSerializedBaseline = File.ReadAllBytes(ClimateSdfPath);
        }

        [OneTimeTearDown]
        public void RestoreClimateSerializedBaseline()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ClimateSdfPath))
            {
                EditorUtility.ClearDirty(asset);
            }
            File.WriteAllBytes(ClimateSdfPath, _climateSerializedBaseline);
        }

        [Test]
        public void TerminalResultContract_HasExactSixEntryCopyAndZeroArguments()
        {
            var expected = new[]
            {
                (TerminalResultLocalizationContract.Keys.Continue, "Continue", "계속"),
                (TerminalResultLocalizationContract.Keys.LevelFailedTitle, "Stage Failed", "스테이지 실패"),
                (
                    TerminalResultLocalizationContract.Keys.ChancesExhaustedDetail,
                    "All chances have been used. Restart the stage or return to the main menu.",
                    "모든 기회를 소진했습니다. 스테이지를 다시 시작하거나 메인 메뉴로 돌아가세요."),
                (TerminalResultLocalizationContract.Keys.RestartStage, "Restart Stage", "스테이지 다시 시작"),
                (TerminalResultLocalizationContract.Keys.MainMenu, "Main Menu", "메인 메뉴"),
                (TerminalResultLocalizationContract.Keys.GameClearTitle, "Game Clear", "게임 클리어"),
            };

            Assert.That(TerminalResultLocalizationContract.Entries.Count, Is.EqualTo(6));
            Assert.That(
                TerminalResultLocalizationContract.Entries.Select(entry => entry.Id),
                Is.EquivalentTo(Enum.GetValues(typeof(TerminalResultLocalizationEntryId))));
            Assert.That(
                TerminalResultLocalizationContract.Entries.Select(entry => entry.Key).Distinct().Count(),
                Is.EqualTo(6));

            foreach (var item in expected)
            {
                var entry = TerminalResultLocalizationContract.Entries.Single(
                    candidate => candidate.Key == item.Item1);
                Assert.That(entry.Table, Is.EqualTo("UI"));
                Assert.That(entry.English, Is.EqualTo(item.Item2));
                Assert.That(entry.Korean, Is.EqualTo(item.Item3));
                Assert.That(entry.IsSmart, Is.False);
                Assert.That(CountPlaceholders(entry.English), Is.Zero, entry.Key);
                Assert.That(CountPlaceholders(entry.Korean), Is.Zero, entry.Key);
            }
        }

        [Test]
        public void TerminalResultDescriptors_MatchContractAndSemanticRoles()
        {
            var descriptors = new[]
            {
                TerminalResultTextDescriptors.Continue,
                TerminalResultTextDescriptors.LevelFailedTitle,
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                TerminalResultTextDescriptors.RestartStage,
                TerminalResultTextDescriptors.MainMenu,
                TerminalResultTextDescriptors.GameClearTitle,
            };

            Assert.That(
                descriptors.Select(descriptor => descriptor.Key),
                Is.EquivalentTo(TerminalResultLocalizationContract.Entries.Select(entry => entry.Key)));
            Assert.That(TerminalResultTextDescriptors.Continue.Role, Is.EqualTo(LocalizedTextRole.Button));
            Assert.That(TerminalResultTextDescriptors.LevelFailedTitle.Role, Is.EqualTo(LocalizedTextRole.Title));
            Assert.That(TerminalResultTextDescriptors.ChancesExhaustedDetail.Role, Is.EqualTo(LocalizedTextRole.Body));
            Assert.That(TerminalResultTextDescriptors.RestartStage.Role, Is.EqualTo(LocalizedTextRole.Button));
            Assert.That(TerminalResultTextDescriptors.MainMenu.Role, Is.EqualTo(LocalizedTextRole.Button));
            Assert.That(TerminalResultTextDescriptors.GameClearTitle.Role, Is.EqualTo(LocalizedTextRole.Title));
            Assert.That(descriptors, Has.All.Matches<LocalizedTextDescriptor>(
                descriptor => descriptor.Arguments.Count == 0));
        }

        [Test]
        public void TerminalResultCatalogs_HaveUnityPackageFreeAndInvariantParity()
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(
                TerminalResultLocalizationContract.Table);
            var englishTable = collection?.GetTable(PackageFreeLocalizedTextResolver.DefaultLocaleCode)
                as StringTable;
            var koreanTable = collection?.GetTable(PackageFreeLocalizedTextResolver.KoreanLocaleCode)
                as StringTable;
            Assert.That(englishTable, Is.Not.Null);
            Assert.That(koreanTable, Is.Not.Null);

            var packageFree = PackageFreeLocalizedTextResolver.CreateSettingsDefault();
            var invariant = ResolveInvariantResolver();
            var bootstrap = SettingsLocalizationAssetBootstrap.Entries.ToDictionary(
                entry => entry.Key,
                StringComparer.Ordinal);

            foreach (var entry in TerminalResultLocalizationContract.Entries)
            {
                var descriptor = new LocalizedTextDescriptor(entry.Table, entry.Key, entry.Role, entry.Weight);
                Assert.That(englishTable.GetEntry(entry.Key)?.LocalizedValue, Is.EqualTo(entry.English), entry.Key);
                Assert.That(koreanTable.GetEntry(entry.Key)?.LocalizedValue, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(bootstrap[entry.Key].English, Is.EqualTo(entry.English), entry.Key);
                Assert.That(bootstrap[entry.Key].Korean, Is.EqualTo(entry.Korean), entry.Key);
                Assert.That(bootstrap[entry.Key].IsSmart, Is.False, entry.Key);
                Assert.That(packageFree.Resolve(descriptor), Is.EqualTo(entry.English), entry.Key);
                Assert.That(invariant.Resolve(descriptor), Is.EqualTo(entry.English), entry.Key);
            }

            packageFree.SetLocale(PackageFreeLocalizedTextResolver.KoreanLocaleCode);
            foreach (var entry in TerminalResultLocalizationContract.Entries)
            {
                var descriptor = new LocalizedTextDescriptor(entry.Table, entry.Key, entry.Role, entry.Weight);
                Assert.That(packageFree.Resolve(descriptor), Is.EqualTo(entry.Korean), entry.Key);
            }
        }

        [Test]
        public void LevelFailedMapper_PreservesTypedReasonAndNavigation_UnknownFailsClosed()
        {
            var stageId = StageId.CreateOrThrow("stage-2-1");
            var request = new StageNavigationRequest(
                stageId,
                StageNavigationKind.Retry,
                "level-failed-restart-level",
                StageTransitionHint.ForKind(StageTransitionKind.LevelFailedRestart));
            var readModel = new GameplayLevelFailedReadModel(
                GameplayLevelFailureReason.ChancesExhausted,
                request);

            var payload = LevelFailedPayloadMapper.Map(readModel);

            Assert.That(readModel.Reason, Is.EqualTo(GameplayLevelFailureReason.ChancesExhausted));
            Assert.That(payload.DetailTextDescriptor, Is.EqualTo(TerminalResultTextDescriptors.ChancesExhaustedDetail));
            AssertNavigationEqual(request, payload.RestartLevelRequest);
            Assert.That(
                LevelFailedPayloadMapper.Map(new GameplayLevelFailedReadModel(
                    GameplayLevelFailureReason.None,
                    request)).DetailTextDescriptor,
                Is.EqualTo(default(LocalizedTextDescriptor)));
            Assert.That(
                LevelFailedPayloadMapper.Map(new GameplayLevelFailedReadModel(
                    (GameplayLevelFailureReason)999,
                    request)).DetailTextDescriptor,
                Is.EqualTo(default(LocalizedTextDescriptor)));
        }

        [Test]
        public void LevelFailedRuntime_ReentryUsesCurrentLocaleAndDisposesPriorProductionRuntime()
        {
            var request = new StageNavigationRequest(
                StageId.CreateOrThrow("stage-1-1"),
                StageNavigationKind.Retry,
                "level-failed-restart-level");
            var payload = new LevelFailedScreenPayload(
                TerminalResultTextDescriptors.ChancesExhaustedDetail,
                request);
            var resolver = new RecordingResolver("en-US");
            using var harness = TerminalRuntimeHarness.Create(resolver);
            var screenRequest = new ScreenRequest(
                ScreenId.LevelFailed,
                payload,
                "terminal-level-failed");

            Assert.That(harness.Controller.Show(screenRequest), Is.True);
            var englishEntry = harness.Controller.CurrentEntry.Value;
            var englishView = harness.ScreenLayer.FindScreenView<LevelFailedScreenView>();
            Assert.That(englishEntry.Policy.RetentionMode, Is.EqualTo(ScreenRetentionMode.DisposeOnHide));
            AssertLevelFailedRuntimeView(
                englishView,
                "en-US",
                "Stage Failed",
                "All chances have been used. Restart the stage or return to the main menu.",
                "Restart Stage",
                "Main Menu");
            Assert.That(resolver.SubscriptionCount, Is.Zero);

            harness.Controller.Clear();
            Assert.That(englishView == null, Is.True, "DisposeOnHide must destroy the old production view.");
            var resolvedAfterEnglishDispose = resolver.ResolveCallCount;
            resolver.SetLocale("ko-KR");
            Assert.That(
                resolver.ResolveCallCount,
                Is.EqualTo(resolvedAfterEnglishDispose),
                "Changing locale after hide must not update the disposed presenter/view.");
            Assert.That(resolver.SubscriptionCount, Is.Zero);

            Assert.That(harness.Controller.Show(screenRequest), Is.True);
            var koreanEntry = harness.Controller.CurrentEntry.Value;
            var koreanView = harness.ScreenLayer.FindScreenView<LevelFailedScreenView>();
            Assert.That(koreanEntry.InstanceId, Is.Not.EqualTo(englishEntry.InstanceId));
            Assert.That(koreanView, Is.Not.SameAs(englishView));
            AssertLevelFailedRuntimeView(
                koreanView,
                "ko-KR",
                "스테이지 실패",
                "모든 기회를 소진했습니다. 스테이지를 다시 시작하거나 메인 메뉴로 돌아가세요.",
                "스테이지 다시 시작",
                "메인 메뉴");
            Assert.That(resolver.SubscriptionCount, Is.Zero);

            harness.Controller.Clear();
            Assert.That(koreanView == null, Is.True);
            var resolvedAfterKoreanDispose = resolver.ResolveCallCount;
            resolver.SetLocale("en-US");
            Assert.That(resolver.ResolveCallCount, Is.EqualTo(resolvedAfterKoreanDispose));
            Assert.That(resolver.SubscriptionCount, Is.Zero);

            Assert.That(harness.Controller.Show(screenRequest), Is.True);
            var recreatedEnglishEntry = harness.Controller.CurrentEntry.Value;
            var recreatedEnglishView = harness.ScreenLayer.FindScreenView<LevelFailedScreenView>();
            Assert.That(recreatedEnglishEntry.InstanceId, Is.Not.EqualTo(koreanEntry.InstanceId));
            AssertLevelFailedRuntimeView(
                recreatedEnglishView,
                "en-US",
                "Stage Failed",
                "All chances have been used. Restart the stage or return to the main menu.",
                "Restart Stage",
                "Main Menu");
            Assert.That(resolver.SubscriptionCount, Is.Zero);
        }

        [Test]
        public void TerminalPrefabs_ApplySemanticTypographyAndPreserveAuthoredSizing()
        {
            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
            var climate = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ClimateSdfPath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(climate, Is.Not.Null);

            AssertTypography(
                UiTestPrefabAssetUtility.StageResultScreenPrefabPath,
                "_continueButtonLabel",
                TypographyStyleTag.Button,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath,
                "_titleLabel",
                TypographyStyleTag.HeaderLarge,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath,
                "_detailLabel",
                TypographyStyleTag.Body,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath,
                "_restartLevelButtonLabel",
                TypographyStyleTag.Button,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath,
                "_mainButtonLabel",
                TypographyStyleTag.Button,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.GameClearScreenPrefabPath,
                "_titleLabel",
                TypographyStyleTag.HeaderLarge,
                theme,
                climate);
            AssertTypography(
                UiTestPrefabAssetUtility.GameClearScreenPrefabPath,
                "_mainButtonLabel",
                TypographyStyleTag.Button,
                theme,
                climate);
        }

        [Test]
        public void TerminalPrefabAuthoring_HasSizedWrappedDetailAndNoStaleGameOver()
        {
            var levelFailed = File.ReadAllText(UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);
            var gameClear = File.ReadAllText(UiTestPrefabAssetUtility.GameClearScreenPrefabPath);
            Assert.That(levelFailed, Does.Not.Contain("Game Over"));
            Assert.That(gameClear, Does.Not.Contain("Game Over"));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);
            var view = prefab.GetComponent<LevelFailedScreenView>();
            var detail = GetField<TMP_Text>(view, "_detailLabel");
            Assert.That(detail.fontSize, Is.GreaterThan(0f));
            var serializedDetail = new SerializedObject(detail);
            Assert.That(
                serializedDetail.FindProperty("m_fontSizeBase").floatValue,
                Is.GreaterThan(0f));
            Assert.That(detail.textWrappingMode, Is.Not.EqualTo(TextWrappingModes.NoWrap));
            Assert.That(detail.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
        }

        [Test]
        public void GameplayBoundary_HasTypedReasonAndNoTerminalDisplayLocalizationDependency()
        {
            var readModelProperties = typeof(GameplayLevelFailedReadModel)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.That(readModelProperties.Select(property => property.Name), Is.EquivalentTo(new[]
            {
                nameof(GameplayLevelFailedReadModel.Reason),
                nameof(GameplayLevelFailedReadModel.RestartLevelRequest),
            }));
            Assert.That(readModelProperties.Select(property => property.PropertyType), Has.None.EqualTo(typeof(string)));

            var host = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs");
            var readModel = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_UIAccess/Runtime/Models/GameplayLevelFailedReadModel.cs");
            var asmdef = File.ReadAllText(
                "Assets/_Features/Gameplay/Gameplay_UIAccess/Gameplay.UIAccess.asmdef");
            foreach (var source in new[] { host, readModel })
            {
                Assert.That(source, Does.Not.Contain("\"Stage Failed\""));
                Assert.That(source, Does.Not.Contain("\"Level Failed\""));
                Assert.That(source, Does.Not.Contain("\"All chances"));
                Assert.That(source, Does.Not.Contain("\"Restart Stage\""));
                Assert.That(source, Does.Not.Contain("\"Main Menu\""));
                Assert.That(source, Does.Not.Contain("LocalizedTextDescriptor"));
                Assert.That(source, Does.Not.Contain("ui.result."));
            }

            Assert.That(asmdef, Does.Not.Contain("UI.ViewShared"));
            Assert.That(asmdef, Does.Not.Contain("Unity.Localization"));
        }

        [Test]
        public void TerminalImplementation_DoesNotBranchNavigationOnLocalizedStringsOrEnumToString()
        {
            var paths = new[]
            {
                "Assets/_Features/UI/UI_Application/Runtime/LevelFailedPayloadMapper.cs",
                "Assets/_Features/UI/UI_Application/Runtime/StageResult/StageResultScreenPresenters.cs",
                "Assets/_Features/UI/UI_Composition/Runtime/GameplayScreenRuntimeFactory.cs",
                "Assets/_Features/Gameplay/Gameplay_Host/Runtime/CampaignGameplayFlowController.cs",
            };
            var source = string.Join("\n", paths.Select(File.ReadAllText));
            Assert.That(
                source,
                Does.Not.Match(@"(?:==|Equals\s*\().*""(?:Continue|Main Menu)"""));
            Assert.That(source, Does.Not.Contain("Reason.ToString()"));
            Assert.That(source, Does.Not.Contain("NavigationKind.ToString()"));
        }

        [Test]
        public void TerminalResultVisualRunner_UsesProductionCompositionAndSeparatesDiagnostics()
        {
            var runner = File.ReadAllText("run_tests.sh");
            var utility = File.ReadAllText(
                "Assets/_Features/UI/UI_Tests/EditMode/TerminalResultVisualEvidenceUtility.cs");

            Assert.That(runner, Does.Contain("typography-result-visual"));
            Assert.That(
                runner,
                Does.Contain(
                    "Game.Feature.UI.Tests.TerminalResultVisualEvidenceUtility.CaptureFromCommandLine"));
            Assert.That(runner, Does.Contain("TERMINAL_RESULT_VISUAL_WIDTH=1920"));
            Assert.That(runner, Does.Contain("TERMINAL_RESULT_VISUAL_HEIGHT=1080"));
            Assert.That(runner, Does.Contain("verify_typography_visual_revision_gate"));
            Assert.That(runner, Does.Contain("visual_guard_cleanup_process_once"));
            Assert.That(runner, Does.Contain("observe_capture_assets_before_restore"));
            Assert.That(runner, Does.Contain("process_survivor_count="));

            Assert.That(utility, Does.Contain("GameplayUiCanvasRootShell"));
            Assert.That(utility, Does.Contain("new GameplayScreenRuntimeFactory("));
            Assert.That(utility, Does.Contain("new ScreenController(factory)"));
            Assert.That(utility, Does.Contain("LevelFailedPayloadMapper.Map("));
            Assert.That(utility, Does.Contain("UnityStringTableTextResolver.TryCreateSettingsDefault("));
            Assert.That(utility, Does.Contain("ScreenId.StageResult"));
            Assert.That(utility, Does.Contain("ScreenId.LevelFailed"));
            Assert.That(utility, Does.Contain("ScreenId.GameClear"));
            Assert.That(utility, Does.Contain("CaptureClassification.Canonical"));
            Assert.That(utility, Does.Contain("CaptureClassification.Diagnostic"));
            Assert.That(utility, Does.Contain("DiagnosticWidth = 960"));
            Assert.That(utility, Does.Contain("DiagnosticHeight = 540"));
            Assert.That(utility, Does.Contain("canonical_count="));
            Assert.That(utility, Does.Contain("diagnostic_count="));
            Assert.That(utility, Does.Contain("SettleScreenEnterMotion(viewRoot)"));
            Assert.That(utility, Does.Contain("\"StopRootEnterMotion\""));
            Assert.That(utility, Does.Contain("ValidateStageResultTitle(title, viewRoot, width, height)"));
            Assert.That(utility, Does.Contain("title.text != \"Level Clear\""));
            Assert.That(utility, Does.Contain("Game Over"));
        }

        private static void AssertTypography(
            string prefabPath,
            string fieldName,
            TypographyStyleTag role,
            GameplayUiTypographyTheme theme,
            TMP_FontAsset climate)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            try
            {
                var view = instance
                    .GetComponents<MonoBehaviour>()
                    .Single(component => component.GetType().GetMethod(
                        "ApplyLocalizedTypography",
                        BindingFlags.Instance | BindingFlags.Public) != null);
                var target = GetField<TMP_Text>(view, fieldName);
                var originalFont = target.font;
                var originalMaterial = target.fontSharedMaterial;
                var originalSize = target.fontSize;
                var originalAutoSize = target.enableAutoSizing;
                var originalMin = target.fontSizeMin;
                var originalMax = target.fontSizeMax;

                ApplyTypography(view, "en-US", theme);
                Assert.That(target.font, Is.SameAs(originalFont), $"{prefabPath}:{fieldName}");
                Assert.That(target.fontSharedMaterial, Is.SameAs(originalMaterial), $"{prefabPath}:{fieldName}");

                ApplyTypography(view, "ko-KR", theme);
                Assert.That(target.font, Is.SameAs(climate), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(target.fontStyle, Is.EqualTo(FontStyles.Normal), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(target.fontSize, Is.EqualTo(originalSize), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(target.enableAutoSizing, Is.EqualTo(originalAutoSize), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(target.fontSizeMin, Is.EqualTo(originalMin), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(target.fontSizeMax, Is.EqualTo(originalMax), $"{prefabPath}:{fieldName}:{role}");
                Assert.That(theme.ResolveOrThrow("ko-KR", role).SizingMode,
                    Is.EqualTo(TypographySizingMode.PreserveAuthored));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertLevelFailedRuntimeView(
            LevelFailedScreenView view,
            string localeCode,
            string expectedTitle,
            string expectedDetail,
            string expectedRestart,
            string expectedMain)
        {
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsVisible, Is.True);
            var title = GetField<TMP_Text>(view, "_titleLabel");
            var detail = GetField<TMP_Text>(view, "_detailLabel");
            var restart = GetField<TMP_Text>(view, "_restartLevelButtonLabel");
            var main = GetField<TMP_Text>(view, "_mainButtonLabel");
            Assert.That(title.text, Is.EqualTo(expectedTitle));
            Assert.That(detail.text, Is.EqualTo(expectedDetail));
            Assert.That(restart.text, Is.EqualTo(expectedRestart));
            Assert.That(main.text, Is.EqualTo(expectedMain));

            var theme = AssetDatabase.LoadAssetAtPath<GameplayUiTypographyTheme>(ThemePath);
            Assert.That(theme, Is.Not.Null);
            if (localeCode == "en-US")
            {
                var prefab = UiTestPrefabAssetUtility.LoadScreenPrefab<LevelFailedScreenView>(
                    UiTestPrefabAssetUtility.LevelFailedScreenPrefabPath);
                AssertTextStyle(title, GetField<TMP_Text>(prefab, "_titleLabel"));
                AssertTextStyle(detail, GetField<TMP_Text>(prefab, "_detailLabel"));
                AssertTextStyle(restart, GetField<TMP_Text>(prefab, "_restartLevelButtonLabel"));
                AssertTextStyle(main, GetField<TMP_Text>(prefab, "_mainButtonLabel"));
            }
            else
            {
                AssertTextStyle(title, theme.ResolveOrThrow(localeCode, TypographyStyleTag.HeaderLarge));
                AssertTextStyle(detail, theme.ResolveOrThrow(localeCode, TypographyStyleTag.Body));
                AssertTextStyle(restart, theme.ResolveOrThrow(localeCode, TypographyStyleTag.Button));
                AssertTextStyle(main, theme.ResolveOrThrow(localeCode, TypographyStyleTag.Button));
            }
        }

        private static void AssertTextStyle(TMP_Text text, ResolvedTmpTypographyStyle style)
        {
            Assert.That(text.font, Is.SameAs(style.FontAsset));
            Assert.That(text.fontSharedMaterial, Is.SameAs(style.MaterialPreset));
            Assert.That(text.fontStyle, Is.EqualTo(style.FontStyle));
        }

        private static void AssertTextStyle(TMP_Text text, TMP_Text authored)
        {
            Assert.That(text.font, Is.SameAs(authored.font));
            Assert.That(text.fontSharedMaterial, Is.SameAs(authored.fontSharedMaterial));
            Assert.That(text.fontStyle, Is.EqualTo(authored.fontStyle));
        }

        private static void ApplyTypography(
            MonoBehaviour view,
            string localeCode,
            GameplayUiTypographyTheme theme)
        {
            var method = view.GetType().GetMethod(
                "ApplyLocalizedTypography",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, view.GetType().FullName);
            method.Invoke(view, new object[] { localeCode, theme });
        }

        private static T GetField<T>(object instance, string fieldName) where T : class
        {
            var field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().Name}.{fieldName}");
            return field.GetValue(instance) as T;
        }

        private static ILocalizedTextResolver ResolveInvariantResolver()
        {
            var type = typeof(LevelFailedScreenPresenter).Assembly.GetType(
                "Game.Feature.UI.Application.InvariantSettingsLocalizedTextResolver");
            Assert.That(type, Is.Not.Null);
            var resolver = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as ILocalizedTextResolver;
            Assert.That(resolver, Is.Not.Null);
            return resolver;
        }

        private static int CountPlaceholders(string value)
        {
            return value.Count(character => character == '{');
        }

        private static void AssertNavigationEqual(
            StageNavigationRequest expected,
            StageNavigationRequest actual)
        {
            Assert.That(actual.StageId, Is.EqualTo(expected.StageId));
            Assert.That(actual.NavigationKind, Is.EqualTo(expected.NavigationKind));
            Assert.That(actual.Source, Is.EqualTo(expected.Source));
            Assert.That(actual.TransitionHint.Kind, Is.EqualTo(expected.TransitionHint.Kind));
            Assert.That(
                actual.TransitionHint.HasMinimumVisibleSecondsOverride,
                Is.EqualTo(expected.TransitionHint.HasMinimumVisibleSecondsOverride));
            Assert.That(
                actual.TransitionHint.MinimumVisibleSecondsOverride,
                Is.EqualTo(expected.TransitionHint.MinimumVisibleSecondsOverride));
        }

        private sealed class RecordingResolver : ILocalizedTextResolver
        {
            private readonly PackageFreeLocalizedTextResolver _inner;
            private int _subscriptionCount;

            public RecordingResolver(string localeCode)
            {
                _inner = PackageFreeLocalizedTextResolver.CreateSettingsDefault(localeCode);
            }

            public string CurrentLocaleCode => _inner.CurrentLocaleCode;

            public int SubscriptionCount => _subscriptionCount;

            public int ResolveCallCount { get; private set; }

            public event Action LocaleChanged
            {
                add
                {
                    _inner.LocaleChanged += value;
                    _subscriptionCount++;
                }
                remove
                {
                    _inner.LocaleChanged -= value;
                    _subscriptionCount--;
                }
            }

            public string Resolve(LocalizedTextDescriptor descriptor)
            {
                ResolveCallCount++;
                return _inner.Resolve(descriptor);
            }

            public void SetLocale(string localeCode)
            {
                _inner.SetLocale(localeCode);
            }
        }

        private sealed class TerminalRuntimeHarness : IDisposable
        {
            private readonly GameObject _root;
            private readonly PopupController _popupController;

            private TerminalRuntimeHarness(
                GameObject root,
                ScreenLayerView screenLayer,
                ScreenController controller,
                PopupController popupController)
            {
                _root = root;
                ScreenLayer = screenLayer;
                Controller = controller;
                _popupController = popupController;
            }

            public ScreenLayerView ScreenLayer { get; }

            public ScreenController Controller { get; }

            public static TerminalRuntimeHarness Create(ILocalizedTextResolver resolver)
            {
                var root = new GameObject("TerminalResultLocalizationTypographyTests_Runtime");
                var screenLayerRoot = new GameObject("ScreenLayerRoot", typeof(RectTransform));
                screenLayerRoot.transform.SetParent(root.transform, false);
                var screenContentRoot = new GameObject("ScreenContentRoot", typeof(RectTransform));
                screenContentRoot.transform.SetParent(screenLayerRoot.transform, false);
                var screenLayer = screenLayerRoot.AddComponent<ScreenLayerView>();
                screenLayer.Configure(
                    screenLayerRoot,
                    screenContentRoot.GetComponent<RectTransform>());

                var popupLayerRoot = new GameObject("PopupLayerRoot", typeof(RectTransform));
                popupLayerRoot.transform.SetParent(root.transform, false);
                var popupRoot = new GameObject("PopupRoot", typeof(RectTransform));
                popupRoot.transform.SetParent(popupLayerRoot.transform, false);
                var backdrop = new GameObject(
                    "Backdrop",
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(Image),
                    typeof(Button));
                backdrop.transform.SetParent(popupRoot.transform, false);
                var popupContentRoot = new GameObject("PopupContentRoot", typeof(RectTransform));
                popupContentRoot.transform.SetParent(popupRoot.transform, false);
                var popupLayer = popupLayerRoot.AddComponent<PopupLayerView>();
                popupLayer.Configure(
                    popupRoot,
                    backdrop.GetComponent<CanvasGroup>(),
                    backdrop.GetComponent<Image>(),
                    backdrop.GetComponent<Button>(),
                    popupContentRoot.GetComponent<RectTransform>());

                var popupController = new PopupController(new GameplayPopupRuntimeFactory(
                    popupLayer,
                    UiTestPrefabAssetUtility.LoadPopupCatalog(),
                    localizedTextResolver: resolver));
                var timeoutRelay = root.AddComponent<DisplayPreviewTimeoutRelay>();
                var lifecycleRelay = root.AddComponent<DisplaySettingsLifecycleRelay>();
                var previewHost = new DisplayPreviewSessionHost(popupController, timeoutRelay);
                var factory = new GameplayScreenRuntimeFactory(
                    screenLayer,
                    new FakeGameplayQueryFacade(
                        new GameplaySessionReadModel(1, false, true, false),
                        FakeGameplayQueryFacade.CreateDefaultPlayerHud(),
                        new GameplayObjectiveReadModel(true, true, false, false)),
                    new ManualGameplayUiPresentationSource(),
                    new FakeAudioSettingsPort(),
                    new FakeDisplaySettingsPort(),
                    NoOpKeyboardBindingSettingsPort.Instance,
                    new RecordingUiAudioPort(),
                    previewHost,
                    lifecycleRelay,
                    UiTestPrefabAssetUtility.LoadScreenCatalog(),
                    localizedTextResolver: resolver);
                return new TerminalRuntimeHarness(
                    root,
                    screenLayer,
                    new ScreenController(factory),
                    popupController);
            }

            public void Dispose()
            {
                Controller.Dispose();
                _popupController.Dispose();
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
