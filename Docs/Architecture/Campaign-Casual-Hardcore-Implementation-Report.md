# Campaign modes initial implementation — execution report

Status: implementation in progress; not ready for completion/sign-off.
Worktree: `/mnt/d/J2M/worktrees/save-structure-redesign`
Original base HEAD: `1719c6a4970313dd54506321ebe9b09513fc924d`.
Evidence: `/mnt/d/J2M/evidence/campaign-modes-implementation`.
Local implementation commits on 2026-09-26: `13502cfbe` (approved localization), `be200487d` (save/gameplay contract), and `4c0d7b9aa` (UI). No push, PR, or user-save failure experiment was performed.

## Commit packaging and validation — 2026-09-26

The approved localization, save/gameplay runtime, and UI were committed separately. Save and gameplay share one commit because the changed chance result and terminal port contracts require their consumers to change together. New script and prefab assets are paired with their `.meta` files. Generated trailing whitespace was removed from the authored prefab and new `.meta` diffs before staging; staged `git diff --check` passed for each commit.

The complete implementation worktree passed `./run_tests.sh core` (293 EditMode and 112 PlayMode selected, zero failures) and `./run_tests.sh ui` (1,637 EditMode selected, zero failures) before committing, using `/mnt/d/J2M/evidence/campaign-modes-commit-20260926`. Each of the three commit hooks also passed `./run_tests.sh core`. The first post-UI-commit `./run_tests.sh ui` rerun selected 1,637 cases and had one environment failure: `CampaignProductionEntryTests.ProductionSaveComposition_WiresPersistentDataRepositories_WithoutWritingSaveRoot` received a sharing violation for `Saves/exhibition-instance.lock` under the Windows persistent-data root while another Unity process was active. After that process exited, the same-revision full UI lane passed 1,637/1,637 with zero failures; its log is `/mnt/d/J2M/evidence/campaign-modes-commit-20260926/ui-committed-retry.log`. Broad unfiltered full and the remaining Player gameplay/save-failure walkthroughs remain unrun.

## User-confirmed Player manual checks — 2026-09-26

The user confirmed manual Player validation of Casual/Hardcore slot switching and screen layout. Those two manual checks are complete. The confirmation did not include a build identity, steps, screenshots, or log, so this report records the user's confirmation without assigning it to a particular revision or claiming reproducible artifact evidence. Gameplay damage/blink/fall/crush and save-failure popup-to-menu-to-gameplay walkthroughs remain open; this confirmation does not establish those paths.

## Approved localization application — 2026-09-25

The user approved application of the revised 11 four-locale campaign strings. All 44 translations now match `Docs/Localization/Campaign-Modes-Translation-Review.md` in production UI String Tables. The existing 142 CSV rows and existing table values/key IDs are preserved; 11 Approved CSV rows and Shared Data keys were added. HP is Smart in all four locales with selectors `{0}` and `{1}`. No Korean-column/CSV schema redesign was introduced.

Asset purpose: the five UI table/shared-data assets register the approved mode selection, HP, return destination, restart and save-status copy. Six existing CJK Static font assets were rebuilt from the current corpus so the new characters render natively. English font assets already cover these strings and remain unchanged. All `.meta`, font local IDs and Addressables bytes were preserved. No Scene or Prefab was changed in this follow-up. The KBO pair now has a 313-scalar corpus; `run_tests.sh` pins both resulting hashes without weakening its integrity checks.

`ApprovedLocalizationDraftApplyUtility.ApplyAndQuit` passed in Unity, updating the two Japanese/Chinese UI tables and six CJK fonts after the English/Korean authoring step. Generated YAML trailing whitespace was normalized without changing semantics. `UiLocalizationRequirementProjection` now governs all 11 campaign keys, including the HP placeholder signature. Two existing table/glyph tests update their exact inventory expectations for the additional approved text; their content, glyph and locale checks remain intact.

Final validation on the same source/assets:

- `python3 .agents/skills/j2m-localization-atlas/scripts/validate_translation_draft.py --project-root . --require-approved`: PASS, 153 keys / 153 Approved.
- `./run_tests.sh ui`: Windows UI build PASS; Unity EditMode **1629 passed / 0 failed / 0 skipped**. The two earlier missing-mode-translation failures now pass.
- Data audit: all 44 texts match approval; HP Smart metadata, all 11 Theme fonts' new-character coverage, existing entries, font identities, `.meta` and Addressables preservation PASS. Final validation input hashes remained unchanged after the lane.
- `bash -n run_tests.sh` and `git diff --check`: PASS.

The first lane attempt stopped at the old pinned KBO hash before tests; the next run exposed four failures (missing campaign governance entries and old exact inventory counts). Those were corrected and the full UI lane rerun successfully; these were relevant application failures, not unrelated baseline failures. Evidence is `/mnt/d/J2M/evidence/campaign-modes-implementation/localization-apply-20260925`, with final XML under `ui-final/test-results`, `application-audit.json`, `validation-summary.json` and `final-validation-inputs.json`.

Core, unfiltered full, Player builds and manual Player checks were not rerun for this localization follow-up. At this checkpoint, headless Editor validation did not establish visual wrapping/clipping of the longer mode descriptions or save-error title, and Player layout and gameplay/save-failure walkthroughs remained open. The later user-confirmed layout check is recorded above; gameplay/save-failure walkthroughs and overall feature sign-off remain open. The existing initial save guarantee is unchanged: the latest unsaved HP/death/clear result may be lost, and previous commands are not replayed.

## Test boundary reinforcement — 2026-09-25

The follow-up strengthens four existing test files without changing production code, Scene/Prefab/assets, localization, schema, or save policy. The contract is StrongContract for saved HP at launch, read-only HUD projection, preservation of authored non-Casual state, and unconditional DestroyTile removal independent of receiver cooldown. No new gameplay definition of player crush was introduced.

- `StageBackedGameplaySceneInstallerTests`: the existing launch test now initializes the actual host from the installer-produced configuration. Casual, Hardcore and noncampaign cases inspect the first WorldSnapshot and HUD query before any tick, authored entity/other-entity preservation, shared timing preset preservation, and resolved cooldown ticks.
- `HUDControllerTests`: two new cases pass the initial HP/Chance read model through the production UI source, mapper and presenters to the authored HUD prefab. They inspect initial HP2, HP3 refresh, localization descriptor arguments and Hardcore Chance/health separation. The test resolver does not validate shipping translations or atlases.
- `TileFeatureEffectResolverTests`: two cases execute real movement into DestroyTile while receiver cooldown is active, either with HP3 or after an actual attack produces HP2 and cooldown. They verify the target event, one PlayerDeathSignal, authoritative removal, no world respawn and no repeated death on the next tick.
- `MovementPhaseScenarioTests`: an additional case activates DestroyTile under an HP3 Player through an actual topology transition and checks the same cooldown-independent death boundary.

Three sub-agents implemented separate test files and cross-reviewed the startup and hazard boundaries. The first focused runner selected 13 EditMode cases and passed all 13, including all eight strengthened cases; its PlayMode selection was zero, not PlayMode pass evidence. The expanded selected run passed 970 EditMode cases with 14 existing deleted-stage-fixture skips, and eight PlayMode cases with one graphics-dependent skip. XML comparison confirms all 316 cases missing from the previous Option A validation were selected: 302 passed and 14 remained skipped. Core passed 293 EditMode and 108 PlayMode cases with four graphics-dependent skips. UI passed 1627 cases and retained the same two `MainMenuSaveSlotLocalizationTests` missing-mode-translation failures. These are unfinished feature failures, not unrelated baseline failures. No new failure was observed in the executed scope, and no previous assertions were weakened.

Evidence: `/mnt/d/J2M/evidence/campaign-modes-implementation/test-gap-hardening-20260925`. `final-source-manifest.json` records 157 source/asset/meta/assembly/runner/package files; `validation-summary.json` records exact case selection, failures and skips. The selected filter includes the prior Option A fixtures, the ten previously omitted fixture/method groups, the complete modified hazard/HUD fixtures and timing preset tests. It is not an unfiltered full-lane run.

All runs used this worktree and `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/campaign-modes-implementation/test-gap-hardening-20260925/<directory>`, with `PLAYER_BUILD_ROOT=/mnt/d/J2M/builds/campaign-test-gap-hardening`. No Player build was produced. All 157 hashes matched after the final lanes.

| Command | EditMode | PlayMode | Directory |
| --- | --- | --- | --- |
| `./run_tests.sh full --filter 'CampaignLaunch_PreparesPlayerHpBeforeFirstSnapshotAndAppliesCooldownAfterPreset,CampaignCasualHud_FirstQuerySnapshot,CampaignHardcoreHud_FirstQuerySnapshot,ReceiverCooldown'` | 13 passed | 0 selected; no pass claim | `focused` |
| `./run_tests.sh full --filter <expanded filter below>` | 970 passed / 14 skipped | 8 passed / 1 skipped | `targeted-final` |
| `./run_tests.sh core` | 293 passed | 108 passed / 4 skipped | `core-final` |
| `./run_tests.sh ui` | 1627 passed / **2 failed** | Not part of lane | `ui-final` |

Expanded filter:

```text
CampaignHudReadStoreTests,CampaignSaveServiceTests,CampaignSaveSlotStoreAdapterTests,CampaignSlotTransitionCharacterizationTests,CampaignSlotStateTests,CampaignSlotTransitionEngineTests,NormalCampaignCompletionReceiptTests,SaveSlotValidationAndDirectPlayTests,StageBackedGameplaySceneInstallerTests,CampaignCasualDamage,CampaignStageFlowTests,GameplayTerminalTransitionPortTests,TerminalSessionAuthorityTests,CampaignSaveArchitectureV2Tests,CampaignProductionEntryTests,CampaignLaunchHandoffPlayModeTests,CampaignSaveFailure_StopsCatchUp,TileFeatureEffectResolverTests,MovementPhaseScenarioTests,GameplayTimingPresetTests,HUDControllerTests
```

The verified hazard is DestroyTile contact or activation under an occupant. Its mapping to the product term “fall” still requires an authored gameplay path; reviewed Barricade/Jump crush paths target Boxes, so these tests do not establish the player-crush requirement. The startup test validates actual simulation/HUD query and the UI test validates a separate read-model-to-prefab boundary; neither is a full scene/Player rendering walkthrough. Shipping localization, actual Player checks, and the initial unsaved-result-loss limit remain open or unchanged.

## Option A — terminal notification correction, 2026-09-25

The user selected option A after the source review: immediate run abandonment, then completion of exact-token cleanup and failure notification at the existing synchronous claim/handler boundary. The diagnostic UI's file-read policy (option B) is unchanged.

Runtime changes are limited to four files:

- `CampaignGameplayFlowController.cs`: retain the exact accepted claim before notification; handle claim-notification exceptions; separate the run failure from failure reporting; finish Claimed cleanup and publish the error after the original callback stack returns; recheck abandonment before clear commits and terminal/route work.
- `TerminalTransitionTypes.cs`: a narrow internal claim handoff records the existing token in the gameplay owner before Changed. Public claim behavior and token generation are retained; no new IDs or durable state are added.
- `GameplayHostPresentationFeed.cs`: stop after claim failure before constructing pending clear; forced clear returns no success result when the run has been abandoned.
- `GameplayTerminalTransitionPort.cs`: own the Iris candidate before phase notification, include that notification in setup exception cleanup, and verify candidate/token/phase ownership before Show and registry publication. Existing matching local setup cancellation bounds remain.

Two existing fixtures gained eight regression cases. `CampaignStageFlowTests` uses an isolated real profile/backup and the same read-then-later-UI-listener ordering as production to exercise claim and destination recovery, plus claim observer IO exceptions for death and forced clear. It verifies no old-result persistence, one surviving failure notice, no active terminal hold, and no pending clear. `GameplayTerminalTransitionPortTests` exercises both a throwing phase observer and a nonthrowing local cancellation, asserting candidate disposal and zero Show calls. These tests do not claim a full actual Player or production UI navigation walkthrough.

The red run (`option-a-red`) selected eight tests and failed all eight on the pre-fix runtime. The selected post-fix run (`option-a-targeted`) passed all 316 EditMode tests. It selected **zero PlayMode tests**, which is not PlayMode pass evidence. Final core validation passed 293 EditMode and 108 PlayMode tests, with four graphics-dependent skips (`option-a-core`). UI passed 1625 tests and retained the same two missing-translation failures (`option-a-ui`); these are unfinished feature failures, not unrelated baseline failures. Selected launch/catch-up PlayMode passed eight tests and skipped one graphics-dependent test (`option-a-playmode`); that command selected zero EditMode tests. No previous expectations were relaxed. Source identity is recorded in `option-a-source-manifest.json`; six source/test files changed relative to the prior menu-correction manifest. No Scene/Prefab/shared asset, localization, schema, writer, commit, push or PR changes are included.

Contract classification remains StrongContract for exact token ownership, no replay and no persistence from an abandoned run. The short-lived claim/handler flags and one run failure are synchronous execution state, not a queue, recovery generation or new persistent receipt.

### Option A final commands and limits

All commands ran from this worktree. `CODEX_VALIDATION_ROOT` points to `/mnt/d/J2M/evidence/campaign-modes-implementation/<directory>`; no Player build was produced. The 79 recorded source/asset/meta hashes were checked after all lanes and remained unchanged. Windows dotnet builds and `git diff --check` passed.

| Command | EditMode | PlayMode | Directory |
| --- | --- | --- | --- |
| `./run_tests.sh full --filter 'TerminalNotificationFailure_,IrisNotificationFailure_'` (pre-fix runtime) | 8 failed as expected | Not reached | `option-a-red` |
| `./run_tests.sh full --filter 'CampaignStageFlowTests,GameplayTerminalTransitionPortTests,TerminalSessionAuthorityTests,CampaignSaveArchitectureV2Tests,CampaignProductionEntryTests'` | 316 passed | 0 selected; no pass claim | `option-a-targeted` |
| `./run_tests.sh core` | 293 passed | 108 passed / 4 skipped | `option-a-core` |
| `./run_tests.sh ui` | 1625 passed / **2 failed** | Not part of lane | `option-a-ui` |
| `./run_tests.sh full --filter 'CampaignLaunchHandoffPlayModeTests,CampaignSaveFailure_StopsCatchUp'` | 0 selected; no pass claim | 8 passed / 1 skipped | `option-a-playmode` |

Exact failure/skip identities and messages are in `option-a-validation-summary.json`. The two UI failures are the same `MainMenuSaveSlotLocalizationTests` missing-mode-name cases, with `□` instead of translated Hardcore. Graphics rendering tests were skipped because these lanes ran without a graphics device. Unfiltered full/broad lanes and an actual Player fault→popup→menu→Continue/completed-card walkthrough were not run. There is no interactive Player/fault-driver evidence for that flow; these automated fixtures do not replace it. Existing translation approval/application and Player verification remain open for the overall feature.

The original initial-guarantee limit is unchanged: the latest unsaved HP/death/clear can be lost, and backup recovery may rewind further. Previous commands are not replayed. Option A does not add a rollback/menu guarantee for transitions already owned by the coordinator or late cover callbacks.

## Menu error correction — 2026-09-25, runtime applied; UI text and Player verification pending

The user chose Menu/Quit after a save error, existing Continue for an in-progress slot, and the existing Completed card for a persisted final completion. Dedicated Reload and recovery GameClear/outro entry have been removed. Normal successful completion remains unchanged. The [concrete modification plan](./Campaign-Casual-Hardcore-Save-Error-Menu-Plan.md) covers affected files, matching terminal-token cleanup, BackupRecovered handling, case-by-case outcomes, trade-offs, and required validation.

The correction adds exact unbound Claimed cancellation, local Iris cleanup without fallback route/presentation, and root-level BackupRecovered observation that blocks gameplay mutations until notifications complete. The popup reuses the existing Confirm view: Menu is the default selection; explicit Quit uses the destructive button; non-user closure only clears presentation state. No Scene/Prefab/shared asset changes were required for this correction. Historical validation below predates this correction; the current results are recorded next. The recovery event is injected by the installer through `ICampaignRecoveryObservation`; Flow does not cast its save query to another capability.

Contract classification: typed save ports, exact token ownership, no replay, immutable state and persistence boundaries are StrongContract. Iris-setup fallback routing was CurrentPolicy explicitly replaced by the approved menu-error plan; only those failure expectations changed.

## Previous menu correction validation — historical source state

All runs below use the same final source state: `menu-final-source-manifest.json` records 79 source/asset/meta hashes, verified unchanged after the lanes. It includes the gateway contract restored to HEAD by deleting the obsolete Reload API. The correction changes 18 source files relative to the previous review snapshot; `menu-correction-changed-files.txt` lists them. No new Scene/Prefab/asset edits, commit, push or PR were made in this correction.

| Command | EditMode | PlayMode | Evidence directory |
| --- | --- | --- | --- |
| `./run_tests.sh core` | 293 passed, 0 failed | 108 passed, 4 skipped, 0 failed | `menu-final-core` |
| `./run_tests.sh ui` | 1623 passed, **2 failed** | Not part of this lane | `menu-final-ui` |
| `./run_tests.sh full --filter <below>` | 426 passed, 0 failed | 8 passed, 1 skipped, 0 failed | `menu-final-targeted` |

Exact selected filter:

```text
CampaignStageFlowTests,CampaignHudReadStoreTests,TerminalSessionAuthorityTests,CampaignProductionEntryTests,GameplayTerminalTransitionPortTests,CampaignSlotTransitionCharacterizationTests,CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests,CampaignSaveSlotStoreAdapterTests,CampaignLaunchHandoffPlayModeTests,CampaignSaveFailure_StopsCatchUp
```

Each command was run from this worktree with `CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/campaign-modes-implementation/<evidence directory>` and `PLAYER_BUILD_ROOT=/mnt/d/J2M/builds/campaign-menu`. All three Windows dotnet builds passed. This is selected `full` evidence, not an unfiltered broad/full pass. The four core skips and one selected PlayMode skip require graphics-enabled render evidence; their full identities/reasons are in `menu-validation-summary.json`.

The two UI failures remain `MainMenuSaveSlotLocalizationTests.SlotMapper_ResolvesEmptyInProgressAndCompletedCardsInEnglishAndKorean` and `SlotMapper_ResolvesRepresentativeOfficialStageNamesWithoutChangingSlotFacts`: Hardcore mode text resolves to `□` while the eleven new translations await explicit approval. These are unfinished feature failures already present before this correction, **not unrelated baseline failures**. No expectations were changed to accept missing translations. New production-popup default Submit/explicit Quit tests and programmatic-close/rejected-menu tests pass.

New failure coverage includes HP/death/clear/final-clear before-write and after-write-response faults; persisted state read without command replay; final completion retained after response failure; matching Claimed cancellation and the next scene's claim; local Iris cleanup without fallback route/presentation; recovery by a preceding read stopping the bound Flow; equal-value backup rejection; callbacks outside the root gate, concurrent/reentrant gameplay mutation rejection during notification, handler exception isolation, and no duplicate cached recovery event. Existing catch-up pause-release and launch handoff tests pass. These are automated service/Flow/UI tests; they do not establish a complete actual Player error-popup → menu → gameplay walkthrough.

Intermediate correction runs are retained: `menu-s1` was 201 passed / 1 failed because its observer used a raw service query that intentionally invalidates the HUD gate; the test now uses the real slot-query path. `menu-s2` was 425 passed / 1 failed because Flow cast its save query to the HUD provider; production was corrected to inject the narrow observation port, preserving the architecture test. Both runs stopped before PlayMode and are not final pass evidence.

Localization draft validation passed for the **existing 142 governed keys**. That does not admit or validate production coverage of the eleven proposed campaign strings in the separate review document. No production table, approval CSV inventory or shipping atlas was changed. Actual Player interaction, damage/blink/fall/crush and error-recovery walkthroughs remain unrun; there is no interactive Player evidence or dedicated Player fault-injection driver for these cases in this execution, and product text approval is pending.

The initial durability limit remains: the latest unsaved HP/death/clear result can be lost; backup recovery may rewind further. No previous command is replayed. Late BlackReached/UI-observer failures and coordinator-owned transition failures still do not have an automatic menu/cover-release guarantee.

## Implementation

- Stages campaign policy/state/document/mapper/service/ports/transient/seed paths: fixed mode, schema 3, strict survival validation, narrow CommitSurvival, shared synchronous operation gate. Local-state remains schema 1. Old profile schemas are blocked without conversion or backup overwrite.
- StageRetryChanceTracker and CampaignSlotTransitionEngine: Casual level-first death/HP 3; Hardcore 3→2→1 retries then sequence-first/Chance 3. Clear restores Casual HP; existing Hardcore clear rules remain. Records, comic flags, receipts, processed IDs, other slots and cumulative deaths are preserved.
- StageBackedGameplaySceneInstallerBase and existing preset pipeline: validated launch state supplies HP; Casual copies only the initial array's Player, then applies 2-second cooldown after presets. Shared presets/assets and noncampaign paths are preserved.
- CampaignGameplayFlowController/InputHost/feed: death > surviving clear > HP change; terminal arbitration/last survival tick suppress duplicates. Save failure abandons the run, including catch-up/input/forced clear. The error popup routes to Main Menu or Quit. Existing Continue uses persisted state and completed saves show the existing Completed card; no old command is replayed.
- MainMenuController/Confirm popup: common mode selection for creation, cancellation and confirmation generations retained. Continue uses stored mode. Health has a separate read model/presenter; Hardcore Chance presentation remains.
- GameplayHudRoot.prefab: references its existing inactive LabelText from the HP view and locale typography binding. This is the sole modified prefab, needed for visible HP; no Scene/shared timing asset was modified.
- CasualPlayerDamageBlink: reads authoritative damage state and changes renderer visibility only. Common achievement facts retain normal-clear/DirectPlay exclusions.

## Verification before the menu correction

Baseline before implementation: CampaignSlotTransitionCharacterizationTests 29/29 passed in `/mnt/d/J2M/evidence/campaign-modes-baseline-20260924-plan`.

Intermediate results are retained under s1–s8. Pipe-separated filters in s5/s6 selected no tests and are **not** pass evidence. s7 broad Campaign selected 930 EditMode tests (910 passed, 20 failed); policy expectations, test doubles and missing current-schema JSON fields were corrected. Two diagnostic-report failures have pre-existing expectation conflicts (schema 14 / last-played slot absent); they were not repaired as part of mode implementation and were not independently rerun at pristine HEAD.

s8 targeted run: EditMode 448 passed / 14 skipped / 0 failed; PlayMode 8 passed / 1 skipped / 0 failed. Later HP prefab/test additions require the final review lanes below; s8 is not final-revision evidence.

Pre-correction source validation (same uncommitted source state, recorded in `review-source-manifest.json`, 73 source/meta/prefab hashes):

| Command / evidence directory | EditMode | PlayMode |
| --- | --- | --- |
| `./run_tests.sh core` / `review-core` | 293 passed, 0 failed | 108 passed, 4 skipped, 0 failed |
| `./run_tests.sh ui` / `review-ui` | 1616 passed, 2 failed | Not part of this lane |
| `./run_tests.sh full --filter <campaign fixtures plus AttackPhaseScenarioTests>` / `review-filtered` | 521 passed, 14 skipped, 1 failed | Runner stops after EditMode failure |
| `./run_tests.sh full --filter <campaign fixtures plus CampaignCasualDamage>` / `review-targeted` | 494 passed, 14 skipped, 0 failed | 8 passed, 1 skipped, 0 failed |

All four Windows dotnet builds passed. `git diff --check` passed. Source hashes were unchanged across these review runs; `review-validation-summary.json` records the results and failed test identities. The two UI failures are in MainMenuSaveSlotLocalizationTests: the new mode name resolves to the missing-text sentinel while the Draft translations await approval. These are current implementation failures, not unrelated baseline failures.

The broader selected attack fixture fails `PlayerInvincible_PassiveContactOverlap_DoesNotRetryInvincibleEveryTick`: expected one enemy execution signal, observed zero. That existing test and the attack implementation are unchanged by this feature; the neighboring existing `PlayerInvincible_PassiveContactReject_DoesNotProduceCombatPresentationSource` test expects no such executed combat signal. This is a source-level expectation conflict, **not a failure independently reproduced at pristine HEAD**. The narrower rerun retains all four new Casual damage cases and does not erase the broader fixture's red result.

The final targeted filter is:

```bash
CODEX_VALIDATION_ROOT=/mnt/d/J2M/evidence/campaign-modes-implementation/review-targeted \
./run_tests.sh full --filter 'CampaignSlotStateTests,CampaignSlotTransitionCharacterizationTests,CampaignSlotTransitionEngineTests,CampaignStageFlowTests,CampaignSaveArchitectureV2Tests,CampaignSaveServiceTests,CampaignSaveSlotStoreAdapterTests,NormalCampaignCompletionReceiptTests,StageBackedGameplaySceneInstallerTests,CampaignLaunchHandoffPlayModeTests,CampaignSaveFailure_StopsCatchUp,CampaignCasualDamage,SaveSlotValidationAndDirectPlayTests'
```

The `review-filtered` command used the same filter with `AttackPhaseScenarioTests` replacing `CampaignCasualDamage`. The core/UI commands used their respective evidence directory as `CODEX_VALIDATION_ROOT`. XML and Unity/dotnet logs are retained under each directory's `test-results`.

Covered additions include actual JsonUtility round-trip, mixed-mode disk reload and other-slot preservation, schema 1/2 canonical and backup byte preservation, shared-root nested mutation rejection, Casual damage amounts/multiple attacks/cooldown boundary, survival response failure without replay, catch-up stop across pause release, validated startup HP/timing, mode selection/cancellation, and the authored HP label. The unconditional destruction unit case validates the damage boundary; it is not an actual fall/crush Player walkthrough.

## Historical pending items before approved localization application

- At this earlier snapshot, eleven new localized UI strings (the obsolete Reload label was removed) remained Draft in [translation review](../Localization/Campaign-Modes-Translation-Review.md). They were subsequently approved and applied as recorded above; this paragraph preserves the earlier validation boundary.
- Player build/walkthrough of Casual damage/blink/fall/crush/return, Hardcore Chance/campaign return and mixed-slot Continue was not performed at this earlier snapshot. Product text/atlas integration was pending then and has since been completed as recorded above. The later user-confirmed slot-switching and layout checks are recorded above; the remaining gameplay and save-failure paths are still open. Automated EditMode/PlayMode results do not substitute for those walkthroughs. Actual Player synchronous I/O latency and persistent I/O-error recovery UX remain unmeasured; test-double failures do not establish those properties.
- Broad unfiltered full lane is not run. Relevant selected fixtures and default lanes are reported separately.
- Initial durability intentionally permits losing the latest unsaved damage/death/clear and reverting to an older backup. No in-scene command retry, new persistent operation IDs/receipts, slot incarnation IDs, hashes, recovery generations, async save framework or low-level writer redesign was added.

## Mode-button audio and icon-only Casual HUD — 2026-09-25

New Game no longer plays `StageLaunch` before mode selection. The common popup still plays its open cue; choosing either Casual or Hardcore plays one `StageLaunch` in place of the generic confirm cue. Occupied Continue retains its existing click cue. For overwrite and completed-slot Restart, the mode cue precedes the later destructive confirmation by product choice, so cancelling that confirmation does not retract the sound.

The Casual HUD now binds numeric HP to the three authored Chance-slot icon views without passing HP through the Chance view model or its audio/last-Chance animation. The inactive `LabelText` TMP object and both serialized HP-text references were removed from `GameplayHudRoot.prefab`; the save-slot card's localized HP copy remains. No Scene, string table, font atlas, or save schema changed in this follow-up. The Prefab change reuses the authored icon geometry for Casual HP while preserving Hardcore's Chance presentation.

`./run_tests.sh ui` passed twice after the change; the final run selected 1,634 EditMode tests with 0 failures, including the authored Prefab HP 3/2/1/0 and audio-choice cases. Final runner log: `/mnt/d/J2M/evidence/campaign-modes-implementation/ui-mode-audio-hp-icons-final-20260925.log`. At this UI-only follow-up, the Unity prefab instantiation tests passed but a manual Editor/Player visual and audible walkthrough had not yet been performed. The later user-confirmed Player layout check is recorded above; an audible walkthrough is still unrecorded. Core and broad full lanes were not rerun for this UI-only follow-up.
