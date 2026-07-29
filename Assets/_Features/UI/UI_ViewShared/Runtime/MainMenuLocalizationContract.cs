using System;
using System.Collections.Generic;

namespace Game.Feature.UI.ViewShared
{
    public enum MainMenuLocalizationEntryId
    {
        SlotLabel,
        SlotEmpty,
        SlotNewGame,
        SlotCompleted,
        SlotStage,
        SlotChances,
        SlotDeaths,
        SlotPlayed,
        SlotRestart,
        SlotContinue,
        SlotDelete,
        DeleteTitle,
        DeleteBody,
        DeleteWarning,
        DeleteConfirm,
        RestartTitle,
        RestartBody,
        RestartWarning,
        RestartConfirm,
        OverwriteTitle,
        OverwriteBody,
        OverwriteWarning,
        OverwriteConfirm,
        QuitTitle,
        QuitBody,
        QuitWarning,
        QuitConfirm,
    }

    public readonly struct MainMenuLocalizationContractEntry
    {
        public MainMenuLocalizationContractEntry(
            MainMenuLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight,
            bool isSmart = false)
        {
            Id = id;
            Key = key ?? string.Empty;
            English = english ?? string.Empty;
            Korean = korean ?? string.Empty;
            Role = role;
            Weight = weight;
            IsSmart = isSmart;
        }

        public MainMenuLocalizationEntryId Id { get; }

        public string Table => MainMenuLocalizationContract.Table;

        public string Key { get; }

        public string English { get; }

        public string Korean { get; }

        public LocalizedTextRole Role { get; }

        public LocalizedTextWeight Weight { get; }

        public bool IsSmart { get; }
    }

    public static class MainMenuLocalizationContract
    {
        public const string Table = "UI";

        public static class Keys
        {
            public const string SlotLabel = "ui.main_menu.slot.label";
            public const string SlotEmpty = "ui.main_menu.slot.empty";
            public const string SlotNewGame = "ui.main_menu.slot.new_game";
            public const string SlotCompleted = "ui.main_menu.slot.completed";
            public const string SlotStage = "ui.main_menu.slot.stage";
            public const string SlotChances = "ui.main_menu.slot.chances";
            public const string SlotDeaths = "ui.main_menu.slot.deaths";
            public const string SlotPlayed = "ui.main_menu.slot.played";
            public const string SlotRestart = "ui.main_menu.slot.action.restart";
            public const string SlotContinue = "ui.main_menu.slot.action.continue";
            public const string SlotDelete = "ui.main_menu.slot.action.delete";
            public const string DeleteTitle = "ui.main_menu.slot.confirm.delete.title";
            public const string DeleteBody = "ui.main_menu.slot.confirm.delete.body";
            public const string DeleteWarning = "ui.main_menu.slot.confirm.delete.warning";
            public const string DeleteConfirm = "ui.main_menu.slot.confirm.delete.confirm";
            public const string RestartTitle = "ui.main_menu.slot.confirm.restart.title";
            public const string RestartBody = "ui.main_menu.slot.confirm.restart.body";
            public const string RestartWarning = "ui.main_menu.slot.confirm.restart.warning";
            public const string RestartConfirm = "ui.main_menu.slot.confirm.restart.confirm";
            public const string OverwriteTitle = "ui.main_menu.slot.confirm.overwrite.title";
            public const string OverwriteBody = "ui.main_menu.slot.confirm.overwrite.body";
            public const string OverwriteWarning = "ui.main_menu.slot.confirm.overwrite.warning";
            public const string OverwriteConfirm = "ui.main_menu.slot.confirm.overwrite.confirm";
            public const string QuitTitle = "ui.main_menu.quit_confirm.title";
            public const string QuitBody = "ui.main_menu.quit_confirm.body";
            public const string QuitWarning = "ui.main_menu.quit_confirm.warning";
            public const string QuitConfirm = "ui.main_menu.quit_confirm.confirm";
            public const string Cancel = "ui.common.cancel";
        }

        private static readonly IReadOnlyList<MainMenuLocalizationContractEntry> ContractEntries =
            Array.AsReadOnly(new[]
            {
                Entry(MainMenuLocalizationEntryId.SlotLabel, Keys.SlotLabel, "Slot {0}", "슬롯 {0}", LocalizedTextRole.Title, LocalizedTextWeight.Bold, true),
                Entry(MainMenuLocalizationEntryId.SlotEmpty, Keys.SlotEmpty, "Empty", "비어 있음", LocalizedTextRole.Label),
                Entry(MainMenuLocalizationEntryId.SlotNewGame, Keys.SlotNewGame, "New Game", "새 게임", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotCompleted, Keys.SlotCompleted, "Completed", "완료", LocalizedTextRole.Label),
                Entry(MainMenuLocalizationEntryId.SlotStage, Keys.SlotStage, "Stage {0}", "스테이지 {0}", LocalizedTextRole.Label, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotChances, Keys.SlotChances, "Chances {0}", "기회 {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotDeaths, Keys.SlotDeaths, "Deaths {0}", "사망 {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotPlayed, Keys.SlotPlayed, "Played {0}", "최근 플레이 {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotRestart, Keys.SlotRestart, "Restart", "다시 시작", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotContinue, Keys.SlotContinue, "Continue", "계속", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotDelete, Keys.SlotDelete, "Delete", "삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.DeleteTitle, Keys.DeleteTitle, "Delete Slot", "슬롯 삭제", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.DeleteBody, Keys.DeleteBody, "Delete slot {0}?", "{0}번 슬롯을 삭제할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.DeleteWarning, Keys.DeleteWarning, "This action cannot be undone.", "이 작업은 되돌릴 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.DeleteConfirm, Keys.DeleteConfirm, "Delete", "삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.RestartTitle, Keys.RestartTitle, "Restart Slot", "슬롯 다시 시작", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.RestartBody, Keys.RestartBody, "Restart slot {0} from the beginning?", "{0}번 슬롯을 처음부터 다시 시작할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.RestartWarning, Keys.RestartWarning, "Existing progress will be replaced.", "기존 진행 상황은 교체됩니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.RestartConfirm, Keys.RestartConfirm, "Restart", "다시 시작", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.OverwriteTitle, Keys.OverwriteTitle, "Overwrite Slot", "슬롯 덮어쓰기", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.OverwriteBody, Keys.OverwriteBody, "Start a new game in slot {0}?", "{0}번 슬롯에서 새 게임을 시작할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.OverwriteWarning, Keys.OverwriteWarning, "Existing progress will be overwritten.", "기존 진행 상황은 덮어씌워집니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.OverwriteConfirm, Keys.OverwriteConfirm, "New Game", "새 게임", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.QuitTitle, Keys.QuitTitle, "Quit Game", "게임 종료", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.QuitBody, Keys.QuitBody, "Quit to desktop?", "데스크톱으로 종료할까요?", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.QuitWarning, Keys.QuitWarning, "Unsaved progress may be lost.", "저장되지 않은 진행 상황은 사라질 수 있습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.QuitConfirm, Keys.QuitConfirm, "Quit", "종료", LocalizedTextRole.Button),
            });

        public static IReadOnlyList<MainMenuLocalizationContractEntry> Entries => ContractEntries;

        public static MainMenuLocalizationContractEntry Get(MainMenuLocalizationEntryId id)
        {
            return ContractEntries[(int)id];
        }

        private static MainMenuLocalizationContractEntry Entry(
            MainMenuLocalizationEntryId id,
            string key,
            string english,
            string korean,
            LocalizedTextRole role,
            LocalizedTextWeight weight = LocalizedTextWeight.Regular,
            bool isSmart = false)
        {
            return new MainMenuLocalizationContractEntry(
                id,
                key,
                english,
                korean,
                role,
                weight,
                isSmart);
        }
    }
}
