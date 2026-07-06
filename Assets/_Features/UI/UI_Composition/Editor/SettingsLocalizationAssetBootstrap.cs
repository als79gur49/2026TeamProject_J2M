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

        private static readonly (string Key, string English, string Korean)[] RequiredEntries =
        {
            ("ui.settings.title", "Settings", "설정"),
            ("ui.settings.audio", "Audio", "오디오"),
            ("ui.settings.display", "Display", "디스플레이"),
            ("ui.settings.input", "Input", "입력"),
            ("ui.settings.input.movement_keys", "Movement Keys", "이동 키"),
            ("ui.settings.input.use_arrow_keys", "Use Arrow Keys", "화살표 키 사용"),
            ("ui.settings.input.push", "Push", "밀기"),
            ("ui.settings.input.flip", "Flip", "뒤집기"),
            ("ui.settings.input.change", "Change", "변경"),
            ("ui.settings.input.reset_input", "Reset Input", "입력 초기화"),
            ("ui.common.back", "Back", "뒤로"),
            ("ui.settings.language", "Language", "언어"),
            ("ui.settings.language.english", "English", "영어"),
            ("ui.settings.language.korean", "Korean", "한국어"),
            ("ui.common.settings", "Settings", "설정"),
            ("ui.pause.title", "Paused", "일시 정지"),
            ("ui.pause.description", "Pausing modal popup", "일시 정지 팝업"),
            ("ui.pause.resume", "Resume", "계속하기"),
            ("ui.pause.retry", "Retry", "다시 시도"),
            ("ui.pause.main_menu", "Main Menu", "메인 메뉴"),
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

        public static IReadOnlyList<(string Key, string English, string Korean)> Entries => RequiredEntries;

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
                table.AddEntry(
                    entry.Key,
                    string.Equals(localeCode, "ko-KR", StringComparison.Ordinal)
                        ? entry.Korean
                        : entry.English);
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
