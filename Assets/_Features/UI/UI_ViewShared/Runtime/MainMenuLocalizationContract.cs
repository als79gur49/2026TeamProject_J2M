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
        SlotErrorUnsupportedTitle,
        SlotErrorUnsupportedDetail,
        SlotErrorCorruptTitle,
        SlotErrorCorruptDetail,
        SlotErrorPermissionTitle,
        SlotErrorPermissionDetail,
        SlotErrorLoadFailedTitle,
        SlotErrorLoadFailedDetail,
        SlotErrorNeedsRepairTitle,
        SlotErrorNeedsRepairDetail,
        SlotErrorRecoveryPendingTitle,
        SlotErrorRecoveryPendingDetail,
        SaveRecoveryRetry,
        SaveRecoveryReset,
        SaveRecoveryResetTitle,
        SaveRecoveryResetBody,
        SaveRecoveryResetWarning,
        SaveRecoveryResetConfirm,
        ParticipantResetTitle,
        ParticipantResetBody,
        ParticipantResetWarning,
        ParticipantResetConfirm,
        ParticipantResetUnavailable,
        ParticipantResetBusy,
        ParticipantResetRestart,
        ParticipantResetClose,
        ParticipantResetLegacyBody,
        ParticipantResetLegacyAction,
        ParticipantResetLegacyConfirmBody,
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
            public const string ParticipantResetTitle = "ui.main_menu.participant_reset.title";
            public const string ParticipantResetBody = "ui.main_menu.participant_reset.body";
            public const string ParticipantResetWarning = "ui.main_menu.participant_reset.warning";
            public const string ParticipantResetConfirm = "ui.main_menu.participant_reset.confirm";
            public const string ParticipantResetUnavailable = "ui.main_menu.participant_reset.unavailable";
            public const string ParticipantResetBusy = "ui.main_menu.participant_reset.busy";
            public const string ParticipantResetRestart = "ui.main_menu.participant_reset.restart";
            public const string ParticipantResetClose = "ui.main_menu.participant_reset.close";
            public const string ParticipantResetLegacyBody = "ui.main_menu.participant_reset.legacy.body";
            public const string ParticipantResetLegacyAction = "ui.main_menu.participant_reset.legacy.action";
            public const string ParticipantResetLegacyConfirmBody = "ui.main_menu.participant_reset.legacy.confirm.body";

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
            public const string SlotErrorUnsupportedTitle = "ui.main_menu.slot.error.unsupported.title";
            public const string SlotErrorUnsupportedDetail = "ui.main_menu.slot.error.unsupported.detail";
            public const string SlotErrorCorruptTitle = "ui.main_menu.slot.error.corrupt.title";
            public const string SlotErrorCorruptDetail = "ui.main_menu.slot.error.corrupt.detail";
            public const string SlotErrorPermissionTitle = "ui.main_menu.slot.error.permission.title";
            public const string SlotErrorPermissionDetail = "ui.main_menu.slot.error.permission.detail";
            public const string SlotErrorLoadFailedTitle = "ui.main_menu.slot.error.load_failed.title";
            public const string SlotErrorLoadFailedDetail = "ui.main_menu.slot.error.load_failed.detail";
            public const string SlotErrorNeedsRepairTitle = "ui.main_menu.slot.error.needs_repair.title";
            public const string SlotErrorNeedsRepairDetail = "ui.main_menu.slot.error.needs_repair.detail";
            public const string SlotErrorRecoveryPendingTitle = "ui.main_menu.slot.error.recovery_pending.title";
            public const string SlotErrorRecoveryPendingDetail = "ui.main_menu.slot.error.recovery_pending.detail";
            public const string SaveRecoveryRetry = "ui.main_menu.save_recovery.retry";
            public const string SaveRecoveryReset = "ui.main_menu.save_recovery.reset";
            public const string SaveRecoveryResetTitle = "ui.main_menu.save_recovery.confirm.title";
            public const string SaveRecoveryResetBody = "ui.main_menu.save_recovery.confirm.body";
            public const string SaveRecoveryResetWarning = "ui.main_menu.save_recovery.confirm.warning";
            public const string SaveRecoveryResetConfirm = "ui.main_menu.save_recovery.confirm.confirm";
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
                Entry(MainMenuLocalizationEntryId.SlotChances, Keys.SlotChances, "Chances {0}", "남은 목숨: {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotDeaths, Keys.SlotDeaths, "Deaths {0}", "사망 횟수: {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotPlayed, Keys.SlotPlayed, "Last Played: {0}", "마지막 플레이: {0}", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.SlotRestart, Keys.SlotRestart, "Restart", "다시 시작", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotContinue, Keys.SlotContinue, "Continue", "계속", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotDelete, Keys.SlotDelete, "Delete", "삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.DeleteTitle, Keys.DeleteTitle, "Delete Slot", "슬롯 삭제", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.DeleteBody, Keys.DeleteBody, "Delete slot {0}?", "{0}번 슬롯을 삭제할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.DeleteWarning, Keys.DeleteWarning, "This action cannot be undone.", "이 작업은 되돌릴 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.DeleteConfirm, Keys.DeleteConfirm, "Delete", "삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.RestartTitle, Keys.RestartTitle, "Restart Slot", "슬롯 처음부터 시작", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.RestartBody, Keys.RestartBody, "Restart slot {0} from the beginning?", "{0}번 슬롯을 처음부터 다시 시작할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.RestartWarning, Keys.RestartWarning, "Existing progress will be replaced.", "기존 진행 상황이 초기화됩니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.RestartConfirm, Keys.RestartConfirm, "Restart", "다시 시작", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.OverwriteTitle, Keys.OverwriteTitle, "Overwrite Slot", "슬롯 덮어쓰기", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.OverwriteBody, Keys.OverwriteBody, "Start a new game in slot {0}?", "{0}번 슬롯에서 새 게임을 시작할까요?", LocalizedTextRole.Body, LocalizedTextWeight.Regular, true),
                Entry(MainMenuLocalizationEntryId.OverwriteWarning, Keys.OverwriteWarning, "Existing progress will be overwritten.", "기존 진행 상황을 덮어씁니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.OverwriteConfirm, Keys.OverwriteConfirm, "New Game", "새 게임", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.QuitTitle, Keys.QuitTitle, "Quit Game", "게임 종료", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.QuitBody, Keys.QuitBody, "Quit to desktop?", "게임을 종료하고 바탕 화면으로 나갈까요?", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.QuitWarning, Keys.QuitWarning, "Unsaved progress may be lost.", "저장되지 않은 진행 상황은 사라질 수 있습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.QuitConfirm, Keys.QuitConfirm, "Quit", "종료", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SlotErrorUnsupportedTitle, Keys.SlotErrorUnsupportedTitle, "Unsupported Save", "호환되지 않는 저장 데이터", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorUnsupportedDetail, Keys.SlotErrorUnsupportedDetail, "This save was created by an unsupported version.", "지원되지 않는 버전에서 생성된 저장 데이터입니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SlotErrorCorruptTitle, Keys.SlotErrorCorruptTitle, "Save Data Damaged", "손상된 저장 데이터", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorCorruptDetail, Keys.SlotErrorCorruptDetail, "This save data could not be read.", "저장 데이터를 읽을 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SlotErrorPermissionTitle, Keys.SlotErrorPermissionTitle, "Save Access Failed", "저장 데이터 접근 실패", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorPermissionDetail, Keys.SlotErrorPermissionDetail, "The save data could not be accessed. Check file permissions.", "저장 데이터 접근 권한을 확인하세요.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SlotErrorLoadFailedTitle, Keys.SlotErrorLoadFailedTitle, "Save Load Failed", "저장 데이터 불러오기 실패", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorLoadFailedDetail, Keys.SlotErrorLoadFailedDetail, "The save data could not be loaded.", "저장 데이터를 불러올 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SlotErrorNeedsRepairTitle, Keys.SlotErrorNeedsRepairTitle, "Save Data Unavailable", "저장 데이터 사용 불가", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorNeedsRepairDetail, Keys.SlotErrorNeedsRepairDetail, "This save cannot be used in its current state.", "현재 상태에서는 이 저장 데이터를 사용할 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SlotErrorRecoveryPendingTitle, Keys.SlotErrorRecoveryPendingTitle, "Save Reset Incomplete", "저장 데이터 초기화 미완료", LocalizedTextRole.Label, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SlotErrorRecoveryPendingDetail, Keys.SlotErrorRecoveryPendingDetail, "The save reset did not finish. Retry to continue.", "저장 데이터 초기화가 완료되지 않았습니다. 다시 시도하세요.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryRetry, Keys.SaveRecoveryRetry, "Retry", "다시 시도", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryReset, Keys.SaveRecoveryReset, "Delete All Save Data", "모든 저장 데이터 삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryResetTitle, Keys.SaveRecoveryResetTitle, "Delete All Save Data", "모든 저장 데이터 삭제", LocalizedTextRole.Title, LocalizedTextWeight.Bold),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryResetBody, Keys.SaveRecoveryResetBody, "Delete the incompatible save data and every save slot, then start over?", "호환되지 않는 저장 데이터와 모든 저장 슬롯을 삭제하고 새로 시작할까요?", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryResetWarning, Keys.SaveRecoveryResetWarning, "All progress will be deleted.", "모든 진행 상황이 삭제됩니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.SaveRecoveryResetConfirm, Keys.SaveRecoveryResetConfirm, "Delete All", "모두 삭제", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetTitle, Keys.ParticipantResetTitle, "Prepare Next Participant", "다음 참가자 준비", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetBody, Keys.ParticipantResetBody, "Reset every slot, active progress and achievement ledger at the current save location, and the target achievements of the current Steam account, then restart?", "현재 저장 경로의 모든 슬롯·활성 진행·업적 장부와 현재 Steam 계정의 대상 업적을 초기화하고 재시작할까요?", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.ParticipantResetWarning, Keys.ParticipantResetWarning, "Settings are preserved. Progress cannot be recovered.", "설정은 보존합니다. 삭제한 진행은 복구할 수 없습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.ParticipantResetConfirm, Keys.ParticipantResetConfirm, "Reset and Restart", "초기화 후 재시작", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetUnavailable, Keys.ParticipantResetUnavailable, "Prepare Next Participant\n(Steam required)", "다음 참가자 준비\n(Steam 연결 필요)", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetBusy, Keys.ParticipantResetBusy, "Preparing the next participant. Please wait.", "다음 참가자를 준비하고 있습니다. 잠시 기다려 주세요.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.ParticipantResetRestart, Keys.ParticipantResetRestart, "Restart", "다시 실행", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetClose, Keys.ParticipantResetClose, "Close", "닫기", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetLegacyBody, Keys.ParticipantResetLegacyBody, "An unfinished reset from a previous test version cannot be resumed. You can start a new full reset for this version.", "이전 버전의 미완료 초기화는 이어서 진행할 수 없습니다. 현재 버전의 전체 초기화를 새로 시작할 수 있습니다.", LocalizedTextRole.Body),
                Entry(MainMenuLocalizationEntryId.ParticipantResetLegacyAction, Keys.ParticipantResetLegacyAction, "Start New Full Reset", "새 전체 초기화", LocalizedTextRole.Button),
                Entry(MainMenuLocalizationEntryId.ParticipantResetLegacyConfirmBody, Keys.ParticipantResetLegacyConfirmBody, "Reset all 18 achievements of the current Steam account, every campaign slot, active progress and the achievement ledger at this save location, then restart?", "현재 Steam 계정의 18가지 업적 전체와 현재 저장 경로의 모든 슬롯·활성 진행·업적 장부를 삭제하고 재시작할까요?", LocalizedTextRole.Body),
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
