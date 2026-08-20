using System;
using System.Collections.Generic;
using Game.Feature.UI.ViewShared;
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
            (SettingsLocalizationContract.Keys.Title, "Settings", "설정", false),
            (SettingsLocalizationContract.Keys.AudioTab, "Audio", "오디오", false),
            (SettingsLocalizationContract.Keys.DisplayTab, "Display", "화면", false),
            (SettingsLocalizationContract.Keys.InputTab, "Input", "조작", false),
            (SettingsLocalizationContract.Keys.AudioMain, "Master", "전체 음량", false),
            (SettingsLocalizationContract.Keys.AudioBgm, "Background Music", "배경 음악", false),
            (SettingsLocalizationContract.Keys.AudioSfx, "Effects", "효과음", false),
            (SettingsLocalizationContract.Keys.AudioMute, "Mute", "음소거", false),
            (SettingsLocalizationContract.Keys.DisplayCurrent, "Current Display", "현재 화면 설정", false),
            (SettingsLocalizationContract.Keys.DisplayResolution, "Resolution", "해상도", false),
            (SettingsLocalizationContract.Keys.DisplayResolutionHint, "Only automatically detected resolutions are shown.", "자동으로 감지된 해상도만 표시됩니다.", false),
            (SettingsLocalizationContract.Keys.DisplayFullscreenWindow, "Borderless Fullscreen", "테두리 없는 창 모드", false),
            (SettingsLocalizationContract.Keys.DisplayFullscreenOn, "On", "켜짐", false),
            (SettingsLocalizationContract.Keys.DisplayFullscreenOff, "Off", "꺼짐", false),
            (SettingsLocalizationContract.Keys.DisplayApply, "Apply", "적용", false),
            (SettingsLocalizationContract.Keys.DisplayRevert, "Revert", "되돌리기", false),
            (SettingsLocalizationContract.Keys.InputMovementKeys, "Movement Keys", "이동 키", false),
            (SettingsLocalizationContract.Keys.InputPush, "Push", "밀기", false),
            (SettingsLocalizationContract.Keys.InputFlip, "Flip", "뒤집기", false),
            (SettingsLocalizationContract.Keys.InputReset, "Reset Input", "키 설정 초기화", false),
            (SettingsLocalizationContract.Keys.Back, "Back", "뒤로", false),
            (SettingsLocalizationContract.Keys.Language, "Language", "언어", false),
            (SettingsLocalizationContract.Keys.LanguageEnglish, "English", "영어", false),
            (SettingsLocalizationContract.Keys.LanguageKorean, "Korean", "한국어", false),
            (SettingsLocalizationContract.Keys.AudioVolumeValue, "{0}%", "{0}%", true),
            (SettingsLocalizationContract.Keys.AudioVolumeValueMuted, "{0}% (Muted)", "{0}% (음소거)", true),
            (SettingsLocalizationContract.Keys.DisplayResolutionValue, "{0}", "{0}", true),
            (SettingsLocalizationContract.Keys.DisplayPreviewCountdown, "Reverting in {0}s", "{0}초 후 되돌림", true),
            (SettingsLocalizationContract.Keys.DisplayPreviewActiveStatus, "Preview active. Current display is temporary and not saved. Confirm to keep it, or it will revert in {0} seconds.", "화면 설정을 미리 적용했습니다. {0}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.", true),
            (SettingsLocalizationContract.Keys.DisplayPreviewRevertedStatus, "Preview reverted to the previous saved display settings.", "화면 설정이 이전에 저장된 값으로 돌아갔습니다.", false),
            (SettingsLocalizationContract.Keys.DisplaySavedStatus, "Display settings saved.", "화면 설정이 저장되었습니다.", false),
            (SettingsLocalizationContract.Keys.DisplayExternalDriftStatus, "Current display changed outside saved settings. Saved settings remain unchanged until you apply again.", "현재 화면 설정이 저장된 설정과 다릅니다. 다시 적용하기 전까지 저장된 설정은 변경되지 않습니다.", false),
            (SettingsLocalizationContract.Keys.InputRebindCanceled, "Key reassignment canceled.", "키 재지정을 취소했습니다.", false),
            (SettingsLocalizationContract.Keys.InputResetComplete, "Input settings reset.", "키 설정을 초기화했습니다.", false),
            (SettingsLocalizationContract.Keys.InputReservedKey, "This key cannot be used.", "이 키는 사용할 수 없습니다.", false),
            (SettingsLocalizationContract.Keys.InputMovementConflict, "Movement keys cannot overlap.", "이동 키는 서로 중복될 수 없습니다.", false),
            (SettingsLocalizationContract.Keys.InputAlreadyRebinding, "Another key is already being reassigned.", "이미 다른 키를 재지정하고 있습니다.", false),
            (SettingsLocalizationContract.Keys.InputActionConflict, "This key is already used by {0}.", "이 키는 이미 {0}에 할당되어 있습니다.", true),
            (SettingsLocalizationContract.Keys.InputUnsupportedKey, "This key cannot be used.", "이 키는 사용할 수 없습니다.", false),
            (SettingsLocalizationContract.Keys.InputRebindPushPrompt, "Press a key for Push...", "밀기 키를 누르세요…", false),
            (SettingsLocalizationContract.Keys.InputRebindFlipPrompt, "Press a key for Flip...", "뒤집기 키를 누르세요…", false),
            (SettingsLocalizationContract.Keys.InputResetConfirmTitle, "Reset Input Settings", "키 설정 초기화", false),
            (SettingsLocalizationContract.Keys.InputResetConfirmBody, "Reset input settings to defaults?", "키 설정을 기본값으로 초기화할까요?", false),
            (SettingsLocalizationContract.Keys.InputResetConfirmLabel, "Reset", "초기화", false),
            (SettingsLocalizationContract.Keys.Cancel, "Cancel", "취소", false),
            (SettingsLocalizationContract.Keys.DisplayPreviewConfirmTitle, "Confirm Display Preview", "화면 설정을 유지할까요?", false),
            (SettingsLocalizationContract.Keys.DisplayPreviewConfirmFullscreenBody, "Preview {0} x {1} in Borderless Fullscreen. These changes are temporary and will revert in {2} seconds unless you confirm.", "{0} × {1} 테두리 없는 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.", true),
            (SettingsLocalizationContract.Keys.DisplayPreviewConfirmWindowedBody, "Preview {0} x {1} in Windowed mode. These changes are temporary and will revert in {2} seconds unless you confirm.", "{0} × {1} 창 모드를 적용했습니다. {2}초 안에 확인하지 않으면 이전 설정으로 돌아갑니다.", true),
            (SettingsLocalizationContract.Keys.DisplayPreviewConfirmKeep, "Keep", "유지", false),
            ("ui.common.settings", "Settings", "설정", false),
            ("ui.main_menu.start", "Start", "시작", false),
            ("ui.main_menu.quit", "Quit", "종료", false),
            ("ui.pause.title", "Paused", "일시 정지", false),
            ("ui.pause.resume", "Resume", "계속하기", false),
            ("ui.pause.retry", "Retry", "다시 시도", false),
            ("ui.pause.main_menu", "Main Menu", "메인 메뉴", false),
            (TerminalResultLocalizationContract.Keys.Continue, "Continue", "계속", false),
            (TerminalResultLocalizationContract.Keys.StageClearTitle, "Stage Clear", "스테이지 클리어", false),
            (TerminalResultLocalizationContract.Keys.LevelFailedTitle, "Stage Failed", "스테이지 실패", false),
            (TerminalResultLocalizationContract.Keys.RestartStage, "Restart Stage", "다시 시작", false),
            (TerminalResultLocalizationContract.Keys.MainMenu, "Main Menu", "메인 메뉴", false),
            (TerminalResultLocalizationContract.Keys.GameClearTitle, "Game Clear", "게임 클리어", false),
            (SceneTransitionLocalizationContract.Keys.RemainingChances, "Remaining Chances", "남은 기회", false),
            (SceneTransitionLocalizationContract.Keys.Loading, "Loading...", "불러오는 중...", false),
            (HudWorldGuideLocalizationKeys.Movement, "Move", "이동", false),
            (HudWorldGuideLocalizationKeys.Push, "Push", "밀기", false),
            (HudWorldGuideLocalizationKeys.Flip, "Flip", "뒤집기", false),
            (MainMenuLocalizationContract.Keys.SlotLabel, "Slot {0}", "슬롯 {0}", true),
            (MainMenuLocalizationContract.Keys.SlotEmpty, "Empty", "비어 있음", false),
            (MainMenuLocalizationContract.Keys.SlotNewGame, "New Game", "새 게임", false),
            (MainMenuLocalizationContract.Keys.SlotCompleted, "Completed", "완료", false),
            (MainMenuLocalizationContract.Keys.SlotStage, "Stage {0}", "스테이지 {0}", true),
            (MainMenuLocalizationContract.Keys.SlotChances, "Chances {0}", "남은 기회: {0}", true),
            (MainMenuLocalizationContract.Keys.SlotDeaths, "Deaths {0}", "사망 횟수: {0}", true),
            (MainMenuLocalizationContract.Keys.SlotPlayed, "Last Played: {0}", "마지막 플레이: {0}", true),
            (MainMenuLocalizationContract.Keys.SlotRestart, "Restart", "다시 시작", false),
            (MainMenuLocalizationContract.Keys.SlotContinue, "Continue", "계속", false),
            (MainMenuLocalizationContract.Keys.SlotDelete, "Delete", "삭제", false),
            (MainMenuLocalizationContract.Keys.DeleteTitle, "Delete Slot", "슬롯 삭제", false),
            (MainMenuLocalizationContract.Keys.DeleteBody, "Delete slot {0}?", "{0}번 슬롯을 삭제할까요?", true),
            (MainMenuLocalizationContract.Keys.DeleteWarning, "This action cannot be undone.", "이 작업은 되돌릴 수 없습니다.", false),
            (MainMenuLocalizationContract.Keys.DeleteConfirm, "Delete", "삭제", false),
            (MainMenuLocalizationContract.Keys.RestartTitle, "Restart Slot", "슬롯 처음부터 시작", false),
            (MainMenuLocalizationContract.Keys.RestartBody, "Restart slot {0} from the beginning?", "{0}번 슬롯을 처음부터 다시 시작할까요?", true),
            (MainMenuLocalizationContract.Keys.RestartWarning, "Existing progress will be replaced.", "기존 진행 상황이 초기화됩니다.", false),
            (MainMenuLocalizationContract.Keys.RestartConfirm, "Restart", "다시 시작", false),
            (MainMenuLocalizationContract.Keys.OverwriteTitle, "Overwrite Slot", "슬롯 덮어쓰기", false),
            (MainMenuLocalizationContract.Keys.OverwriteBody, "Start a new game in slot {0}?", "{0}번 슬롯에서 새 게임을 시작할까요?", true),
            (MainMenuLocalizationContract.Keys.OverwriteWarning, "Existing progress will be overwritten.", "기존 진행 상황을 덮어씁니다.", false),
            (MainMenuLocalizationContract.Keys.OverwriteConfirm, "New Game", "새 게임", false),
            (MainMenuLocalizationContract.Keys.QuitTitle, "Quit Game", "게임 종료", false),
            (MainMenuLocalizationContract.Keys.QuitBody, "Quit to desktop?", "게임을 종료하고 바탕 화면으로 나갈까요?", false),
            (MainMenuLocalizationContract.Keys.QuitWarning, "Unsaved progress may be lost.", "저장되지 않은 진행 상황은 사라질 수 있습니다.", false),
            (MainMenuLocalizationContract.Keys.QuitConfirm, "Quit", "종료", false),
            (MainMenuLocalizationContract.Keys.SlotErrorUnsupportedTitle, "Unsupported Save", "호환되지 않는 저장 데이터", false),
            (MainMenuLocalizationContract.Keys.SlotErrorUnsupportedDetail, "This save was created by an unsupported version.", "지원되지 않는 버전에서 생성된 저장 데이터입니다.", false),
            (MainMenuLocalizationContract.Keys.SlotErrorCorruptTitle, "Save Data Damaged", "손상된 저장 데이터", false),
            (MainMenuLocalizationContract.Keys.SlotErrorCorruptDetail, "This save data could not be read.", "저장 데이터를 읽을 수 없습니다.", false),
            (MainMenuLocalizationContract.Keys.SlotErrorPermissionTitle, "Save Access Failed", "저장 데이터 접근 실패", false),
            (MainMenuLocalizationContract.Keys.SlotErrorPermissionDetail, "The save data could not be accessed. Check file permissions.", "저장 데이터 접근 권한을 확인하세요.", false),
            (MainMenuLocalizationContract.Keys.SlotErrorLoadFailedTitle, "Save Load Failed", "저장 데이터 불러오기 실패", false),
            (MainMenuLocalizationContract.Keys.SlotErrorLoadFailedDetail, "The save data could not be loaded.", "저장 데이터를 불러올 수 없습니다.", false),
            (MainMenuLocalizationContract.Keys.SlotErrorNeedsRepairTitle, "Save Data Unavailable", "저장 데이터 사용 불가", false),
            (MainMenuLocalizationContract.Keys.SlotErrorNeedsRepairDetail, "This save cannot be used in its current state.", "현재 상태에서는 이 저장 데이터를 사용할 수 없습니다.", false),
            (MainMenuLocalizationContract.Keys.SlotErrorRecoveryPendingTitle, "Save Reset Incomplete", "저장 데이터 초기화 미완료", false),
            (MainMenuLocalizationContract.Keys.SlotErrorRecoveryPendingDetail, "The save reset did not finish. Retry to continue.", "저장 데이터 초기화가 완료되지 않았습니다. 다시 시도하세요.", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryRetry, "Retry", "다시 시도", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryReset, "Delete All Save Data", "모든 저장 데이터 삭제", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryResetTitle, "Delete All Save Data", "모든 저장 데이터 삭제", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryResetBody, "Delete the incompatible save data and every save slot, then start over?", "호환되지 않는 저장 데이터와 모든 저장 슬롯을 삭제하고 새로 시작할까요?", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryResetWarning, "All progress will be deleted.", "모든 진행 상황이 삭제됩니다.", false),
            (MainMenuLocalizationContract.Keys.SaveRecoveryResetConfirm, "Delete All", "모두 삭제", false),
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
