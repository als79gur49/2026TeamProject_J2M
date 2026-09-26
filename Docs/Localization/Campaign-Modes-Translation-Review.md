# Campaign modes — translation review

Status: Approved by the user on 2026-09-25 ("실제 적용"). All 11 keys are applied to the four production UI String Tables. Korean, Japanese and Simplified Chinese Static atlases were rebuilt through ApprovedLocalizationDraftApplyUtility; English atlases already cover the approved text.
Existing shared Cancel, Main Menu and Quit strings are reused.

| UI key | en-US | ko-KR | ja-JP | zh-CN |
|---|---|---|---|---|
| ui.campaign.mode.title | Choose a Mode | 모드 선택 | モード選択 | 选择模式 |
| ui.campaign.mode.casual | Casual | 캐주얼 | カジュアル | 休闲 |
| ui.campaign.mode.hardcore | Hardcore | 하드코어 | ハードコア | 硬核 |
| ui.campaign.mode.casual_detail | You have 3 HP and unlimited retries. If you die, you return to the first stage of the current level. | HP는 3이며, 재도전 횟수에는 제한이 없습니다. 사망하면 현재 레벨의 첫 스테이지로 돌아갑니다. | HPは3で、何度でもリトライできます。死亡すると、現在のレベルの最初のステージに戻ります。 | 生命值为3，可无限次重试。死亡后，将返回当前关卡组的第一关。 |
| ui.campaign.mode.hardcore_detail | You start with 3 chances. When you run out, you return to the first stage of the campaign. | 목숨 3개로 시작합니다. 목숨을 모두 잃으면 캠페인의 첫 스테이지로 돌아갑니다. | 残機3でスタートします。残機がなくなると、キャンペーンの最初のステージに戻ります。 | 共有3次挑战机会。次数用尽后，将返回战役的第一关。 |
| ui.campaign.hp | HP {0}/{1} | HP {0}/{1} | HP {0}/{1} | 生命值 {0}/{1} |
| ui.campaign.return.level | You will return to the first stage of the current level. | 현재 레벨의 첫 스테이지로 돌아갑니다. | 現在のレベルの最初のステージに戻ります。 | 将返回当前关卡组的第一关。 |
| ui.campaign.return.campaign | You will return to the first stage of the campaign. | 캠페인의 첫 스테이지로 돌아갑니다. | キャンペーンの最初のステージに戻ります。 | 将返回战役的第一关。 |
| ui.campaign.restart.campaign | Restart Campaign | 캠페인 처음부터 | キャンペーンを最初から | 重新开始战役 |
| ui.campaign.save_error.title | Unable to Confirm Save Status | 저장 상태를 확인할 수 없습니다 | セーブデータの状態を確認できません | 无法确认存档状态 |
| ui.campaign.save_error.detail | Return to the main menu to check your save. Unsaved progress may be lost. | 메인 메뉴로 돌아가 저장 상태를 확인하세요. 저장되지 않은 진행 상황은 사라질 수 있습니다. | メインメニューに戻り、セーブデータの状態を確認してください。保存されていない進行状況は失われる場合があります。 | 请返回主菜单检查存档状态。未保存的进度可能会丢失。 |

Earlier Draft notes (superseded by the approved production application below):

2026-09-25 correction: removed the dedicated Reload label. The error popup reuses Main Menu (default selection) and Quit; the revised detail above remains Draft.

2026-09-25 terminology review: incorporated the user's recommended four-locale wording into this Draft. Hardcore uses the existing chance resource (Korean `남은 목숨`, Japanese `残機`, Chinese `剩余次数`); Japanese retry wording follows `ui.pause.retry`. The save-error warning reuses `ui.main_menu.quit_confirm.warning` in all four locales, and the title does not assert that persistence failed when only its response may have failed. Level-group and stage destinations remain distinct. Production String Tables and shipping atlases have not been updated; popup wrapping and glyph coverage still require validation when applied.

2026-09-25 production application: the user explicitly requested application of the recommendations above. The 11 CSV rows are Approved; the existing 142 rows are preserved. HP is a Smart String in all four locales. Apply passed with eight changed assets (two translated tables and six CJK font assets), preserved font identities and left Addressables unchanged. The authoring step added the shared keys and English/Korean entries before Apply. UI validation results are recorded in `../Architecture/Campaign-Casual-Hardcore-Implementation-Report.md`; actual Player wrapping remains unverified. Evidence: `/mnt/d/J2M/evidence/campaign-modes-implementation/localization-apply-20260925`.
