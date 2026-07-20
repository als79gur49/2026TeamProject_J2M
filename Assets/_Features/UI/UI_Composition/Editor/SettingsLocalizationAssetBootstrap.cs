using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Game.Feature.UI.Composition.Editor
{
    public static class SettingsLocalizationAssetBootstrap
    {
        private const string LocalizationRootPath = "Assets/Localization";
        private const string LocalePath = "Assets/Localization/Locales";
        private const string StringTablePath = "Assets/Localization/StringTables/UI";
        private const string LocalizationSettingsPath = "Assets/Localization/Localization Settings.asset";
        private const string TableCollectionName = "UI";

        private static readonly (string Key, string English, string Korean, bool IsSmart)[] RequiredEntries =
        {
            ("ui.settings.title", "Settings", "설정", false),
            ("ui.settings.audio", "Audio", "오디오", false),
            ("ui.settings.display", "Display", "디스플레이", false),
            ("ui.settings.input", "Input", "입력", false),
            ("ui.settings.audio.main", "Main", "마스터", false),
            ("ui.settings.audio.bgm", "Background Music", "배경 음악", false),
            ("ui.settings.audio.sfx", "Effects", "효과음", false),
            ("ui.settings.audio.mute", "Mute", "음소거", false),
            ("ui.settings.display.current", "Current Display", "현재 디스플레이", false),
            ("ui.settings.display.resolution", "Resolution", "해상도", false),
            ("ui.settings.display.resolution_hint", "Only automatically detected resolutions are shown.", "자동으로 감지된 해상도만 표시됩니다.", false),
            ("ui.settings.display.fullscreen_window", "Fullscreen Window", "전체 화면 창", false),
            ("ui.settings.display.fullscreen_on", "On", "켜짐", false),
            ("ui.settings.display.apply", "Apply", "적용", false),
            ("ui.settings.display.revert", "Revert", "되돌리기", false),
            ("ui.settings.input.movement_keys", "Movement Keys", "이동 키", false),
            ("ui.settings.input.use_arrow_keys", "Use Arrow Keys", "화살표 키 사용", false),
            ("ui.settings.input.push", "Push", "밀기", false),
            ("ui.settings.input.flip", "Flip", "뒤집기", false),
            ("ui.settings.input.change", "Change", "변경", false),
            ("ui.settings.input.reset_input", "Reset Input", "입력 초기화", false),
            ("ui.common.back", "Back", "뒤로", false),
            ("ui.settings.language", "Language", "언어", false),
            ("ui.settings.language.english", "English", "영어", false),
            ("ui.settings.language.korean", "Korean", "한국어", false),
            ("ui.settings.audio.volume_value", "{0}%", "{0}%", true),
            ("ui.settings.audio.volume_value_muted", "{0}% (Muted)", "{0}% (음소거)", true),
            ("ui.settings.display.resolution_value", "{0}", "{0}", true),
            ("ui.settings.display.preview_countdown", "Reverting in {0}s", "{0}초 후 되돌림", true),
            ("ui.settings.display.status.preview_active", "Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {0} seconds.", "미리 보기 중입니다. 현재 화면 설정은 임시 상태이며 저장되지 않았습니다. 유지하려면 확인하세요. 그렇지 않으면 {0}초 후 되돌아갑니다.", true),
            ("ui.settings.display.status.preview_reverted", "Preview reverted to the previous saved display settings.", "미리 보기가 이전에 저장된 화면 설정으로 되돌아갔습니다.", false),
            ("ui.settings.display.status.saved", "Display settings saved.", "화면 설정이 저장되었습니다.", false),
            ("ui.settings.display.status.external_drift", "Current display changed outside saved settings. Saved settings remain unchanged until you apply again.", "현재 화면이 저장된 설정과 다릅니다. 다시 적용하기 전까지 저장된 설정은 변경되지 않습니다.", false),
            ("ui.settings.input.rebind_canceled", "Rebind canceled.", "키 변경 취소됨", false),
            ("ui.settings.input.reset_complete", "Input settings reset.", "입력 설정이 초기화되었습니다.", false),
            ("ui.settings.input.reserved_key", "This key is reserved.", "이 키는 예약되어 있습니다.", false),
            ("ui.settings.input.movement_conflict", "This key conflicts with movement keys.", "이 키는 이동 키와 충돌합니다.", false),
            ("ui.common.settings", "Settings", "설정", false),
            ("ui.main_menu.start", "Start", "시작", false),
            ("ui.main_menu.quit", "Quit", "종료", false),
            ("ui.pause.title", "Paused", "일시 정지", false),
            ("ui.pause.description", "Pausing modal popup", "일시 정지 팝업", false),
            ("ui.pause.resume", "Resume", "계속하기", false),
            ("ui.pause.retry", "Retry", "다시 시도", false),
            ("ui.pause.main_menu", "Main Menu", "메인 메뉴", false),
        };

        public static void EnsureSettingsLocalizationAssetsAndQuit()
        {
            try
            {
                EnsureSettingsLocalizationAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void EnsureSettingsLocalizationAssets()
        {
            EnsureFolder(LocalizationRootPath);
            EnsureFolder(LocalePath);
            EnsureFolder("Assets/Localization/StringTables");
            EnsureFolder(StringTablePath);
            AddressableAssetSettingsDefaultObject.GetSettings(true);

            var englishLocale = EnsureLocale("en-US");
            var koreanLocale = EnsureLocale("ko-KR");
            EnsureLocalizationSettings(englishLocale);
            var collection = EnsureStringTableCollection(englishLocale, koreanLocale);

            ApplyEntries(collection.GetTable(englishLocale.Identifier) as StringTable, localeCode: "en-US");
            ApplyEntries(collection.GetTable(koreanLocale.Identifier) as StringTable, localeCode: "ko-KR");
            foreach (var table in collection.StringTables)
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static IReadOnlyList<(string Key, string English, string Korean, bool IsSmart)> Entries => RequiredEntries;

        private static Locale EnsureLocale(string localeCode)
        {
            var locale = LocalizationEditorSettings.GetLocale(localeCode);
            if (locale != null)
            {
                return locale;
            }

            var assetPath = $"{LocalePath}/{localeCode}.asset";
            locale = AssetDatabase.LoadAssetAtPath<Locale>(assetPath);
            if (locale == null)
            {
                locale = Locale.CreateLocale(localeCode);
                AssetDatabase.CreateAsset(locale, assetPath);
            }

            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void EnsureLocalizationSettings(Locale projectLocale)
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(LocalizationSettingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                    AssetDatabase.CreateAsset(settings, LocalizationSettingsPath);
                }

                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            LocalizationSettings.Instance = settings;
            LocalizationSettings.ProjectLocale = projectLocale;
            LocalizationSettings.InitializeSynchronously = true;
            LocalizationSettings.PreloadBehavior = PreloadBehavior.PreloadSelectedLocaleAndFallbacks;
            EditorUtility.SetDirty(settings);
        }

        private static StringTableCollection EnsureStringTableCollection(Locale englishLocale, Locale koreanLocale)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableCollectionName);
            if (collection == null)
            {
                collection = LocalizationEditorSettings.CreateStringTableCollection(
                    TableCollectionName,
                    StringTablePath,
                    new List<Locale> { englishLocale, koreanLocale });
            }

            if (collection.GetTable(englishLocale.Identifier) == null)
            {
                collection.AddNewTable(englishLocale.Identifier);
            }

            if (collection.GetTable(koreanLocale.Identifier) == null)
            {
                collection.AddNewTable(koreanLocale.Identifier);
            }

            return collection;
        }

        private static void ApplyEntries(StringTable table, string localeCode)
        {
            if (table == null)
            {
                throw new InvalidOperationException($"Missing UI String Table for locale '{localeCode}'.");
            }

            foreach (var entry in RequiredEntries)
            {
                var tableEntry = table.AddEntry(
                    entry.Key,
                    string.Equals(localeCode, "ko-KR", StringComparison.Ordinal)
                        ? entry.Korean
                        : entry.English);
                tableEntry.IsSmart = entry.IsSmart;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
