# UI EditMode Baseline 2026-04-15

## Scope
- Purpose: preserve the Stage 4 mapped seam, Stage 5 HUD shell boundaries, Stage 6 popup stack ownership, Stage 7 screen runtime ownership, and the remaining screen presenter boundaries while Stage 9 adds test and diagnostic hardening.
- Command: `./run_tests.sh ui`
- Runner path: governance -> Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` -> Unity `TestRunnerCliBootstrap.RunEditMode -codexSelection ui`
- Companion smoke lane: `./run_tests.sh core` on the same worktree
- Recorded rerun: April 16, 2026 on the current Stage 9 worktree
- Result note: future updates to `## Result` and `## Companion Smoke Check` must not be bumped without updating the structural delta below.

## Result
- Pinned Stage 9 status: green
- Pinned Unity UI EditMode: `155 total / 0 failed`
- Prior Phase 1 drift-correction rerun: green on 2026-06-06 KST
- Current PR-A Objective UI removal baseline rerun: green on 2026-06-11 KST
- Current PR-T3 transition content base contract rerun: green on 2026-06-12 KST
- Current PR-T5 ChanceLost slot root explicit binding rerun: green on 2026-06-12 KST
- Current StageResult result text schema cleanup rerun: green on 2026-06-12 KST
- Current transition Title/Message payload decommission rerun: green on 2026-07-31 KST
- Historical Climate Crisis KR PR2 typography contract rerun: green on 2026-07-26 KST with code-head `CommandLine-20260726-052954`, Settings 38 applied / 13 skipped, six canonical PNGs, and three separate Climate diagnostic PNGs; superseded by the KBO Dia Gothic migration
- Current KBO Dia Gothic typography migration rerun: green on 2026-09-05 KST, Windows UI build passed and Unity UI EditMode `1348 total / 0 failed`; Medium owns the existing 10 large/emphasis Display/UI/Utility roles and Light owns the remaining 9 Heading/Body roles, TTF/SDF GUID and material/atlas local IDs are preserved, and both atlases were regenerated from the supplied unmodified TTFs with native managed glyph coverage and no fallback
- Current Settings movement-key toggle rerun: green on 2026-08-08 KST, Windows UI build passed and Unity UI EditMode `1357 total / 0 failed`; Settings now has 37 locale-themed bindings / 10 invariant bindings over 47 TMP targets
- Current Settings Push/Flip keycap rebind rerun: green on 2026-08-08 KST, Windows UI build passed and Unity UI EditMode `1361 total / 0 failed`; Settings now has 35 locale-themed bindings / 10 invariant bindings over 45 TMP targets, with the obsolete Change localization entry removed
- Current fullscreen cursor confinement rerun: green on 2026-08-15 KST, Windows UI build passed and Unity UI EditMode `1386 total / 0 failed`
- Comic-sequence terminology rerun: green on 2026-08-19 KST, Windows UI build passed and Unity UI EditMode `1324 total / 0 failed`; the MP4/VideoPlayer path was retired and the current runtime remains sprite-sequence only. The 2026-08-21 product follow-up temporarily removed the validation-only outro Definition and left the Gameplay scene reference explicitly null; that scene-wiring state is superseded by the production activation row below.
- Historical production-outro removal rerun: green on 2026-08-21 KST, Windows UI build passed, Unity UI EditMode `1341 total / 0 failed`, and filtered actual-scene PlayMode `1 total / 0 failed`; at that revision an active completed campaign skipped comic presentation, completed the regular Main Menu lifecycle, and did not mark absent outro content complete
- Current production-outro activation rerun: green on 2026-09-03 KST, Windows UI build passed, Unity UI EditMode `1340 total / 0 failed`, filtered Full EditMode matched `0`, and filtered actual-scene PlayMode passed `1 total / 0 failed`; the authored one-page sequence cumulatively reveals six independent panels in fixed order, keeps panels 3 and 4 as separate advance steps, completes the opaque Main Menu handoff, and marks outro progress once
- Current SurfaceBelt center remainder badge rerun: green on 2026-09-04 KST, Windows UI build passed and Unity UI EditMode `1344 total / 0 failed`; only centered `Cell_0` authors one number-free 32x32 `NormalBadge` on the visual's left, its frame/fill use the Objective completion gold, and its active/inactive alpha is selected by the current sector's `HasAnyRemaining` value. Initial binding is immediate; later state changes use DOTween color/scale transitions, and new active sectors receive one runtime-isolated All In 1 Shine without replaying on identical binds
- Current comic-sequence audio-settings follow-up rerun: green on 2026-08-22 KST, Windows UI build passed and Unity UI EditMode `1345 total / 0 failed`; authored intro audio follows both Master and BGM mute/volume settings while retaining the comic-sequence fade gain
- Current comic-sequence BGM-focus recovery rerun: green on 2026-08-22 KST, Windows UI build passed and Unity UI EditMode `1347 total / 0 failed`; cancellation and unsuccessful handoff restore the router's current BGM selection, while synchronously accepted scene routes keep the source-scene BGM stopped for destination takeover
- Current comic-sequence enter-fade follow-up rerun: green on 2026-08-22 KST, Windows UI build passed and Unity UI EditMode `1348 total / 0 failed`; entry keeps the overlay background transparent while the dedicated fade layer transitions the visible source scene to opaque black, then fixes the background to black before the first comic page reveal
- Historical NanumGothic retirement rerun: green on 2026-08-23 KST, Windows UI build passed and Unity UI EditMode `1336 total / 0 failed`; the unused Nanum TTF/SDF/SyntheticBold assets and preservation-only validation/runner contracts were removed after confirming the then-current Climate 2000/2019 mapping
- Current blocked-save recovery fail-closed rerun: green on 2026-08-20 KST, Windows UI build passed and Unity UI EditMode `1338 total / 0 failed`; incomplete resets remain globally blocked, Retry resumes the pending transaction, and destructive reset remains limited to incompatible/corrupt profile states
- Current blocked-save typography follow-up rerun: green on 2026-08-20 KST, Windows UI build passed and Unity UI EditMode `1341 total / 0 failed`; the recovery title, detail, and two actions use authored semantic bindings, while ordinal fallback remains card-only
- Current Gameplay Stage Name typography follow-up rerun: green on 2026-08-20 KST, Windows UI build passed and Unity UI EditMode `1341 total / 0 failed`; Stage Name resolves `HeaderLarge` through the theme for both locales and adds target-local TMP `UpperCase` presentation without changing World Guide or transition-label default-locale restoration
- Historical Pause progression stepper rerun: green on 2026-08-20 KST, Windows UI build passed and Unity UI EditMode `1341 total / 0 failed`; this predates the selectable stage-image browser contract introduced on 2026-09-05 KST
- Current Pause stage-image browser rerun: green on 2026-09-05 KST, Windows UI build passed and Unity UI EditMode `1346 total / 0 failed`; the prefab-authored horizontal ScrollRect renders one undecorated image per stage, uses the current stage only as the initial selection, reserves double width for the selected image so adjacent images move without overlap, exposes the localized stage name, and opens a pause-owned full-canvas preview from selected click or Submit
- Current campaign MainMenu separated-launch-result rerun: green on 2026-08-25 KST, Windows UI build passed and Unity UI EditMode `1352 total / 0 failed`; slot cards consume immutable entry/evaluation/action inputs, profile blocked recovery remains a separate global path, and the combined validation facade/corrected clone is retired
- Current localization string-governance Phase 3 review-hardening rerun: green on 2026-09-14 KST, focused governance `36 total / 0 failed`, Windows UI build passed, and Unity UI EditMode `1435 total / 0 failed`; package-free lifecycle validation and the read-only Unity adapter cover governed UI/Stage exact-set drift, structured registration/table/key/value/Smart/placeholder diagnostics, immutable public dictionary views, a data-only third ShipReady locale, and the explicit retained legacy Stage key without replacing exact en-US/ko-KR copy contracts
- Current localization/typography CJK Phase 4 runtime-selection rerun: green on 2026-09-14 KST, focused policy/Unity adapter/production registration fixtures `17/17`, `50/50`, and `7/7`, Windows UI build passed, and Unity UI EditMode `1445 total / 0 failed`; all four locale ingress paths use one package-free selection policy, Unity-origin aliases fail exact-canonical admission, rejected post-start events restore the last approved Locale, and the active production provider contains only exact catalogued ShipReady en-US/ko-KR registrations
- Current localization/typography CJK Phase 5 ordered-autonym option rerun: green on 2026-09-14 KST, focused catalog/foundation/production/integration/architecture/governance fixtures `21/21`, `74/74`, `42/42`, `53/53`, `62/62`, and `12/12`, Windows UI build passed, and Unity UI EditMode `1464 total / 0 failed`; the user explicitly approved the recorded tests-first chronology deviation for closeout, while the execution record retains the initial `61 total / 1 failed` structural red, `20 total / 1 failed` behavior red, and retrospective supplemental limitation
- Current localization/typography CJK Phase 6 unified persistence/fallback rerun: green on 2026-09-14 KST, focused catalog/foundation/production/integration/architecture/governance fixtures `29/29`, `82/82`, `42/42`, `65/65`, `66/66`, and `12/12`, Windows UI build passed, and Unity UI EditMode `1496 total / 0 failed`; structural red was `65 total / 3 failed`, while behavior red was `57 total / 5 failed` with four intended persistence gaps plus one disclosed factory-count companion failure
- Current four-locale Slice A2 selected-startup-health rerun: green on 2026-09-16 KST, structural assertion-red `67 total / 1 failed`, valid behavior assertion-red `70 total / 4 failed`, focused integration `70/70`, architecture `67/67`, and real Unity string-governance `7/7`, Windows UI build passed, and Unity UI EditMode `1504 total / 0 failed`; startup now checks only the selected canonical locale's Settings title and canonical Stage display-name descriptors through exact-locale lookup, while registered ShipReady completeness, Draft intersection zero, and uncatalogued-code zero remain governance-owned
- Current terminal-completion HUD Pause recovery rerun: green on 2026-09-20 KST, Windows UI build passed and Unity UI EditMode `1424 total / 0 failed`; the tests-first regression failed before implementation, then the final touched cluster passed `4 total / 0 failed`. `UIFlowCoordinator` now publishes one immutable flow-presentation snapshot through an explicitly implemented interface after policy evaluation, `UIFlowShellPresenter` owns projection, and composition-owned pull synchronization is removed. Manual Player mouse validation was not run.
- Current DemoStageControl authored assist popup rerun: green on 2026-09-21 KST, Windows UI build passed, focused authored-popup and destroyed-navigation-lifetime contracts passed, and Unity UI EditMode `1459 total / 0 failed`; the repository catalog owns the authored prefab, all six actions have directly tested pointer/Submit and navigation parity, coordinator-owned F10 admission blocks terminal/scene transitions without queueing, the feature-local immutable presentation source refreshes open panels without polling, and navigation routing discards destroyed Unity targets before a later popup receives keyboard input.
- Current approved-localization Apply transaction hardening rerun: green on 2026-09-22 KST, Windows UI build passed and Unity UI EditMode `1528 total / 0 failed`; frozen Shared Data/en-US/ko-KR/approved-target semantic manifests, managed `.meta` byte fences, Addressables root `.meta`, whole-managed-graph post-import clean checks, and one-pass best-effort rollback now guard the production mutation boundary
- Current four-locale representative layout-boundary rerun: green on 2026-09-22 KST, Windows UI build passed and Unity UI EditMode `1528 total / 0 failed`; the existing 12-capture Settings/Pause/Main Menu fixture now rejects localized TMP overflow, glyph meshes outside authored text rectangles beyond a bounded two-unit bearing tolerance, and glyph meshes outside the capture frame for en-US/ko-KR/ja-JP/zh-CN. The clean-revision `127bc453770f5fddb9ebbc11bc69b3c17f7bdfd5` visual lane passed 12 canonical captures, 16 M2B dynamic captures, and four Stage save-slot diagnostics at 1920x1080 with no guarded-asset mutation; evidence is `/mnt/d/J2M/evidence/typography-visual/CommandLine-20260922-042424`.
- Current main-integration rerun: green on 2026-09-22 KST after the localization branch was merged with current `origin/main`; Windows UI build passed and the second Unity UI EditMode run passed `1609 total / 0 failed`. The first cold-worktree run completed package and asset import but reported `1609 total / 59 failed` because Unity Localization initialization had not completed; no source or guarded font asset mutation occurred, and the same worktree rerun closed that initialization-only failure.
- Current Windows build result: `dotnet build Game.Feature.UI.Tests.csproj -c Debug` passed with `0` errors
- Current Unity UI EditMode: `1624 total / 0 failed` (ChanceLost production content, 2026-09-26; one UI presentation case added)
- Baseline test result: command `./run_tests.sh ui`, result `1624 total / 0 failed`, failed tests `none`, failure category `none`, PR change pre-existing failure `no`
- Current visual result: automated `./run_tests.sh typography-visual` passed on clean revision `127bc453770f5fddb9ebbc11bc69b3c17f7bdfd5` on 2026-09-22 KST for four-locale representative 1920x1080 coverage; the prior user-performed manual visual validation remains the 2026-09-05 KST result and does not independently cover the later ja-JP/zh-CN addition
- Current KBO interpretation: 19/19 ko-KR roles use KBO Dia Gothic Medium/Light with Normal style and authored sizing, managed glyph fallback is 0, and the Pause/audio/display layout contracts remain guarded by focused production fixtures
- Prior 2차 UI canonical correction report red reason: Windows `dotnet build` missing compile symbols `SurfaceBeltButtonBadgeStyleProfile`, `SurfaceBeltButtonBadgeGroupView`, `EnemyTargetEligibilityResult`, `PendingEnemyBlockedReaction`
- Current interpretation: the prior red reason was not reproduced by the 2026-06-06 KST rerun; retired HUD proof residue was removed after product option B was selected
- Result XML: `/mnt/d/J2M/evidence/pr222-followup-20260926/ui-pass/test-results/wsl-unity-ui-editmode.xml`
- Unity log: `/mnt/d/J2M/evidence/pr222-followup-20260926/ui-pass/test-results/wsl-unity-ui-editmode.log`
- Build log: `/mnt/d/J2M/evidence/pr222-followup-20260926/ui-pass/test-results/wsl-dotnet-ui.log`

## Structural Delta

### ChanceLost production fracture and debris (2026-09-26)

- Added `ChanceLostProductionContent_CracksBeforeDebrisPreservesFramesAndRestoresOnRebind`, taking the UI EditMode inventory from `1623` to `1624`. It guards the authored three-slot prefab, crack-before-debris order, frame retention, icon fall, survivor pulse, material restoration, and rebind. No UI tests were removed, renamed, merged, or split.
- The existing `CampaignLaunchHandoffPlayModeTests` graphics capture case was extended, not added. It checks the dedicated shader and captures 19 frames at each of four resolutions; the authored prefab and materials support this presentation only. UI/runtime ownership did not move between layers.
- `./run_tests.sh ui` passed the Windows UI build and EditMode `1624/0`. Focused graphics `full --filter CampaignLaunchHandoffPlayModeTests` selected `0` EditMode and passed PlayMode `8/0`; it produced 76 PNGs. Evidence: `/mnt/d/J2M/evidence/pr222-followup-20260926/ui-pass/`, `playmode-pass/`, and `frames/20260925-175918/`. The first UI run exposed a stale 1623 documentation guard, and the first graphics run exposed missing WSL-to-Windows environment forwarding; both failed artifacts are preserved separately under `ui/` and `playmode/`. The PlayMode runner removed two generated `InitTestScene` artifacts after its passing run; no generated-scene residue remains. Core, unfiltered full, Player build, and manual campaign E2E were not run; the earlier user manual visual review found no issue.

### UI EventSystem navigation action direct access (2026-09-25)

- The Main Menu and Gameplay EventSystem installers now obtain `InputSystemUIInputModule` directly and clear its public `move`, `submit`, and `cancel` action references. The nine reflection-name probes and module type lookup are removed. `UiNavigationInputRouter` still dispatches navigation while the Input System module retains pointer input.
- One UI lifecycle case was added for module reactivation and reapplication; the existing Gameplay UI composition case now checks that navigation actions are null and point/left-click/scroll references remain connected. No tests were removed, renamed, merged or split; no prefab migration or new product ownership. The selected input actions and UI navigation router were not changed.
- UI Windows build and EditMode passed `1623/0`. Navigation action cleanup is repeated when either installer ensures the EventSystem; the test verifies the module's default actions can return on reactivation and be cleared again without dropping pointer references. Evidence: `/mnt/d/J2M/evidence/ui-navigation-module-20260925-015324/validation-summary.md`. Manual Editor/Player interaction and broad full were not run.

### Demo hotkey keyboard bridge simplification (2026-09-25)

- Replaced F10/BackQuote reflection reads with null-safe `Keyboard.current` access. Removed keyboard/key property metadata, including the Escape metadata used only to discover `wasPressedThisFrame`. Key selection, rebind/transition gates, panel toggling and the separate navigation utility are unchanged.
- Added two UI input cases for the selected key, ignored alternate/Escape keys, press/hold/repress, missing keyboard and reconnection. They use the Input System package's isolated `InputTestFixture`; only the Editor UI test assembly adds `Unity.InputSystem.TestFramework`. No cases removed, renamed, merged or split; no prefab migration or product ownership shift. The diagnostics guard now checks the retained BackQuote entrypoint instead of the removed reflection field name.
- Windows builds and UI EditMode passed `1622/0`; core EditMode passed `293/0`, PlayMode `108 passed / 4 graphics skips / 0 failed`. The initial UI run passed the existing 1620 cases but failed both new press-edge cases in the unisolated Editor input environment; the isolated rerun passed both. Core's runner detected and removed two generated InitTestScene files, then completed successfully. Existing category/compiler warnings remain; font guards reported `NO_MUTATION`.
- Evidence: `/mnt/d/J2M/evidence/keyboard-bridge-20260925-012711/validation-summary.md`. Manual hardware/Player checks and broad full were not run; virtual-device input and existing UI flow coverage bound this change. Baseline count literals are synchronized after measurement and checked by the UI documentation filter.

### Player action and UI movement retirement (2026-09-25)

- Removed seven unused Player actions and their 21 bindings while preserving all remaining action/binding IDs, the UI map and input asset meta. A fixed saved JSON fixture was captured through the production rebind service before deletion; both movement schemes restore it after import without clearing settings.
- Moved shared admission-policy disposal to the Host-owned UIAccess context, including repeated-dispose and failed-composition cleanup. Retired the UI movement gateway, held-direction override and unused acceptance DTO; retained query gates, snapshot windows, physical movement buffering and presentation direction DTOs. Pause does not acquire a new physical-input reset.
- UI inventory: four production binding save/load cases and one installer recreation/lifetime case added; the obsolete HUD-to-gateway dependency test was renamed to a gateway-absence guard. No UI tests removed, merged or split; no prefab migration. Gameplay gateway-driven tests now exercise raw input/query contracts, one UI-held Core case was retired and a physical-held Extended case added, with new disposal and Pause characterization coverage. Four existing PlayMode cases now load the production input asset.
- Executed: pre-removal binding capture `2/0`; post-removal keyboard fixture `22/0`; B1 ownership/Pause `14/0`; B2 query/lifecycle/campaign/architecture `184/0`; input selection EditMode `7/0`, PlayMode `38/0`; core EditMode `293/0`, PlayMode `108 passed / 4 graphics skips / 0 failed`; UI Windows build and EditMode `1620/0`. The B1/B2 filters selected zero PlayMode cases and do not establish PlayMode coverage.
- Initial fixture-constant and PlayMode assembly-reference compile failures were corrected before successful reruns; logs are retained. All invoked font guards reported `NO_MUTATION`; existing category and untouched-code warnings remain. Manual Editor/Player interaction, broad unfiltered full and dedicated graphics evidence lanes were not run. Evidence: `/mnt/d/J2M/evidence/input-retirement-20260925-000826/validation-summary.md`.

### Input reset cleanup follow-up (2026-09-24)

- Removed the uncalled Escape bridge method; F10/BackQuote metadata and behavior remain. Gameplay UI clearing now delegates to one held-direction implementation, and redundant terminal/unbind resets were removed.
- No UI tests were added, removed, renamed, merged, or split; no ownership or prefab migration occurred. Initial UI validation failed one documentation test because the preceding cleanup updated this baseline after validation while leaving two expected count literals at `1609`. Those assertions now match the measured `1615` baseline; runtime assertions are unchanged.
- Final results: UI rerun `1615/0`; core EditMode `293/0`, PlayMode `108 passed / 4 graphics-related skips / 0 failed`; focused input-lifecycle EditMode `83/0`, PlayMode `1/0`. All Windows builds and SDF integrity guards passed. Initial failure and rerun evidence are retained under `/mnt/d/J2M/evidence/input-reset-cleanup-20260924-141032-f86c0f/`; warning and not-run limitations are recorded in the matching `Gameplay-Test-Automation-Guide.md` entry.

### Input unused-surface cleanup (2026-09-24)

- Removed the unconsumed movement display string from Shared/UI keyboard snapshots and updated production, NoOp, world-guide, preview, and test constructors. Removed the unused required-action arrays and service path forwarding properties; canonical action paths and binding persistence remain unchanged.
- UI tests added/removed/renamed/merged/split: none. Removed five obsolete assertions for the retired display string and forwarding properties; canonical path, required-action, movement-scheme, rebind, and default-label assertions remain.
- Gameplay companion: removed the ineffective direction-change delay option through scene/configuration/host wiring and three unused methods. Renamed the existing Flip-after-direction-change PlayMode test to describe its projected-view assertions, retaining its scenario and assertions.
- Responsibility shifts and prefab migrations: none. Validation and warning evidence for this slice is recorded in `Gameplay-Test-Automation-Guide.md` under the matching cleanup entry.
- Same-implementation result: UI Windows build and EditMode `1615/0` passed; core passed EditMode `293/0`, PlayMode `108 passed / 4 graphics-related skips / 0 failed`. PlayerMovement PlayMode escalation ran `83 total / 76 passed / 7 failed`: the seven camera visual cases require graphics/output arguments absent from the ordinary headless invocation. The renamed input case passed; fixture-wide green is not claimed. Dedicated visual lanes, broad unfiltered full, and manual smoke were not run.
- Evidence root: `/mnt/d/J2M/evidence/input-unused-cleanup-20260924-135019-a7d26a/`; all invoked SDF guards reported `NO_MUTATION`. Source-category mismatch and untouched-code compiler warnings remain recorded in the logs.

### Prior structural delta

- Added tests:
  - four-locale representative layout-boundary coverage over the existing 12 Settings/Pause/Main Menu captures, including explicit ja-JP/zh-CN filename expectations and common localized TMP overflow, authored-rect glyph-mesh, and capture-frame glyph-mesh guards
  - approved-localization Apply rollback guard rejecting changed managed `.meta` bytes; existing rollback and directory-fence fixtures now also prove exact managed `.meta` restoration and Addressables root `.meta` restoration without widening production mutation ownership
  - four-locale Slice A2 guards for exact-locale injection shape, selected-only two-descriptor startup lookup, locale/table/key-aware missing-entry failure, Phase 6 failure cardinality and subscription absence, ordinary selected-to-emergency fallback, and lookup exception propagation; the existing SmartFormat and nested-descriptor production tests remain green
  - localization/typography CJK Phase 6 guards for closed preference result/reporter shapes, complete parameterized startup classification, persisted/selected/default convergence, health-gated canonical rewrite and read diagnostics, explicit/external/same/rejected write-event-audio cardinality, null Load/Save contract propagation, reporter/subscriber exceptions, transient subscription cleanup, PlayerPrefs missing/blank/round-trip behavior, and source-ordered subscription with startup custom-event zero
  - localization/typography CJK Phase 5 guards for validated immutable canonical/autonym option snapshots, catalog-order versus supported-membership projection, final deduplication and Draft/alias/unknown exclusion, zero/one/three/missing/duplicate current behavior, rejected and true-target-mismatch outcomes, external approved/rejected Unity changes, apply/save/event/audio cardinality, en-US/ko-KR text/font round trips, and source-proven precomputed no-resolve/no-preload projection
  - localization/typography CJK Phase 4 guards for Draft persisted/selected/default precedence, missing-default failure, duplicate registration deduplication, stable selectable order, production-factory ownership, explicit Change/NoOp/Rejected side-effect cardinality, canonical alias registration, external approved zero-write behavior, Draft/unknown/null/Unity-origin-alias event restoration, and active-provider exact canonical ShipReady registration
  - localization string-governance guards covering lifecycle-driven registration, required UI/Stage tables and values, governed/Shared Data exact-set drift, SmartFormat parse and normalized selector-multiset parity, input-order-independent requirement conflicts, non-Smart literal braces, deterministic structured diagnostics, immutable downcast-resistant dictionary views, data-only `loc-C`, real en-US/ko-KR adapter state, explicit legacy Stage compatibility, and the adapter's read-only/package boundary
  - SurfaceBelt center remainder badge guards proving the canonical HUD authors exactly one `ButtonBadgeGroup` to the left of the `Cell_0` visual, contains exactly one number-free 32x32 `NormalBadge` whose frame/fill share the Objective completion gold, leaves neighboring cells unbound, uses active style for normal-only, MoonBlock-only, or combined remainder, and uses inactive style only when `HasAnyRemaining` is false; focused transition guards additionally cover immediate first bind, active/inactive tween lifecycle, active-sector confirmation without identical-bind replay, disable-time stabilization, and runtime-isolated All In 1 Shine material wiring
  - blocked-save recovery typography guards proving four authored semantic bindings, en-US/ko-KR font/material round-trip with authored sizing preserved, and fail-fast behavior when a non-card binding is missing instead of applying a card-ordinal fallback
  - blocked-save state classification, retry-only IO/permission policy, destructive reset confirmation/cancel flow, status revalidation, locale refresh, startup reset resumption, and incompatible/corrupt profile archive-and-empty-profile recovery guards
  - comic-sequence import/layout guards, exact intro progression, authored six-panel production-outro scene wiring and order, shared intro/outro routing contracts including missing-content direct return, current comic-sequence component presence, opaque-owner cleanup on disable, claim-conflict audio-focus ordering, Master/BGM/fade audio-setting composition, setup-failure cleanup, and pointer-only background click ownership
  - comic-sequence BGM-focus guards proving cancellation restores current router selection after the terminal callback, synchronously accepted routing commits without source-scene BGM restart, and unsuccessful/stale/duplicate routing never commits the audio handoff
  - comic-sequence enter-fade guard proving the source scene remains visible at entry start, the dedicated black layer gains opacity during the authored duration, and the persistent black background is enabled only after full cover before initial content reveal
  - fullscreen cursor confinement policy guards covering focused borderless fullscreen, windowed/unfocused release, unsupported-platform no-op, idempotent writes, shared-display ownership, authored default-cursor hotspot/dimensions, and installer focus/pause/update lifecycle reconciliation
  - Settings movement-key production guards proving the authored `WASDKeyDisplay` button toggles WASD/arrow visuals in both directions with click feedback, and `Input.Movement.Toggle` shows its `SelectionFrame` and submits once on Enter without Slider edit mode
  - Settings language-cycle navigation guards proving `Display.Language.Button` is reachable after the resolution and fullscreen controls, pointer click and keyboard Submit share the same semantic action exactly once, the authored `SelectionFrame` reveals focus, and unavailable language selection is skipped
  - KBO Dia Gothic Medium/Light committed TTF/SDF Git-blob, GUID, and material/atlas-localID preflight separated from Unity runtime font/material reference, 19-role completeness, en-US identity preservation, dynamic managed-table glyph/fallback, license notice, representative Main Menu brand-image separation, and approved Pause/audio/display layout guards; canonical working hashes, calculated ScaleRatio values `0.9/1/0.73125`, and byte convergence are required; the historical migration rerun above does not validate the later serialization correction
  - Climate ko-KR diagnostic screenshot coverage for ConfirmPopup, Settings Audio muted, and Settings Display status, kept outside the exact canonical six-file root
  - locale-independent typography P2 guards for invalid invariant style enums, null-theme invariant/themed preview parity, parent/child Selection normalization, independent roots, repeated preview calls, unique restore counts, live Settings 38-count capture, and schema-v1 manifest rejection of Settings count 51
  - production Settings typography composition tests that open Settings through the actual Main Menu scene installer/overlay path and the actual UIAudioScene installer/coordinator path, then verify the shared catalog/builder, exact closure across all 51 TMP targets / binding targets / manifest entries, 36 governed-target font/material/fontStyle parity, open dropdown live-item restyling, and prefab-authored `en-US -> ko-KR -> en-US` restoration
  - one-event external locale refresh coverage proving shell, Audio muted value, Display status/countdown/language, and Input status update without replacing active ViewModels
  - production scene serialization guards proving Main Menu and Gameplay reference the same Settings catalog/theme and that `_koreanSettingsFont` / `_settingsScreenPrefab` YAML residue is absent
  - controller/coordinator public-surface freeze tests for `UIFlowCoordinator`, `ScreenController`, `PopupController`, and `UIBlockPolicy`
  - deterministic controller/policy guards for `PopTo`, runtime action relay, close-all ordering, backdrop routing, and older-frame refresh behavior
  - diagnostics overlay residue absence guards proving the removed runtime feature does not remain in production UI code, installer hotkeys, or the canonical root shell
  - governance documentation tests for baseline structure, stale wording removal, PlayMode escalation-marker enforcement, and gameplay shell manual runtime smoke-plan governance
  - structural drift guards for root-owned state, child public surfaces, and input-bag/non-flow leakage
  - gameplay shell UI/audio contract guard proving one canonical gameplay/bootstrap root path, one serialized installer/host binding, and no serialized duplicate UI residue
  - canonical stage-clear integration guard proving gameplay host + installer flow transitions into the Stage 7 `StageResult` screen without relying on the legacy overlay path
  - canonical root-shell prefab structure guards proving the runtime shell contains only infrastructure children and no serialized feature views
  - HUD prefab migration guards proving the installer mounts one authored HUD prefab under `HudLayer`, the shell remains HUD-markup free, and the legacy HUD builder symbols are absent from code and docs
  - HUD view boundary guards proving HUD views no longer expose runtime `Configure(...)` entrypoints and stay free of flow/gameplay-access dependencies
  - popup prefab migration guards proving the installer mounts one fixed-shape popup catalog, the popup factory instantiates one canonical authored prefab per popup kind under `PopupLayer`, and popup legacy builder symbols are absent from code and docs
  - per-kind popup prefab contract and boundary guards proving `Pause` and `Confirm` stay visual/local only, and popup callbacks/timers do not acquire lifecycle ownership
  - screen prefab migration guards proving the installer mounts one fixed-shape screen-only catalog, the screen factory instantiates one canonical authored prefab per screen id under `ScreenLayer`, and screen legacy builder symbols are absent from code and docs
  - screen checkpoint guards proving the simple-shell, terminal-screen, and complex-screen checkpoints stay mechanically inspectable instead of hiding risk inside one broad migration phase
  - stronger screen-view ownership guards proving screen views do not surface navigation, popup, back-stack, controller, or gameplay-access shortcuts
  - transition overlay shell/content catalog guards proving scene transitions use the canonical shell asset and authored catalog, with missing setup reported as a defect
  - PR-T2 transition content guards proving `StageClearNext` maps exactly to authored `GenericLoadingOverlayContent`, `DeathRetryChanceLost` maps exactly to dedicated `ChanceLostOverlayContent`, unmatched transition kinds fail closed, stale LevelFailed-only transition message/text residue is removed, and deleted duplicate content prefab GUID references are absent
  - PR-T3 transition content base contract guards proving base content requires only root group and progress text bindings, while retired title/message/progress bar/animator base bindings stay absent from source and prefabs
  - transition payload decommission guard proving chance-loss payload and overlay model no longer expose generic `Title` / `Message`, the coordinator owns no display-copy resolvers, and typed progress/chance-loss plus canonical content routing remain intact
  - PR-T5 ChanceLost slot root binding guards proving `_chanceSlotRoots` remains the explicit inspector binding contract, current prefab slots are bound in `ChanceSlotView 0/1/2` order, and fallback name lookup is safety-only
  - canonical UI navigation resolver guards proving `UiNavigationInputRouter` exposes only the `IUiNavigationTargetResolver` setup path and does not reassemble popup/menu concrete targets
  - external structure-source regeneration guard proving root `UI-Current-Structure-Source.md` mirrors the current 3-layer shell, identity lists, DemoStageControl classification, retired ActionBar status, removed diagnostics status, canonical transition path, and resolver-only navigation state
  - PR-1 stage completion guards proving StageResult + Continue, final-stage GameClear, retry payload, next-stage/no-next-stage mapping, terminal back consume, Reward popup absence, and screen/popup/HUD separation before UI refactor scaffolding begins
  - PR-A Objective UI removal guards proving `ObjectiveStatus` screen, `ObjectiveInfo` popup, pause objective action semantics, deleted prefab files, and deleted prefab GUID references are absent from production UI vocabulary
  - StageResult result text schema cleanup guards proving `ResultTitle`, `ResultSummaryText`, `ResultDetailText`, `ResultContinueLabel`, StageResult text payload members, hidden title/detail prefab labels, and production YAML residue are absent
- Test count delta:
  - previous documented current UI EditMode result: `1074 total / 0 failed`
  - pre-slice exact-head expectation: `1149 total / 0 failed` or higher
  - current rerun: `1150 total / 0 failed`
  - slice-local delta against the exact-head expectation: `+1` transition payload decommission guard
  - observed delta against the previously documented result: `+76`; the remaining `+75` predates this slice on the current branch
  - cursor-confinement slice pre-change observed result: `1383 total / 0 failed`
  - cursor-confinement slice current rerun: `1386 total / 0 failed`
  - cursor-confinement slice-local delta: `+3`; the `+22` between the prior documented `1361` result and this slice's pre-change result predates this change
  - comic-sequence slice pre-change observed result: `1391 total / 0 failed`
  - comic-sequence slice current rerun: `1324 total / 0 failed`
  - comic-sequence slice-local executed-case delta: `-67`; the 70-method legacy mixed suite was replaced by 13 shared-routing methods plus expanded comic guards, with parameterized cases accounting for the executed-case total
  - blocked-save recovery slice pre-change observed result: `1324 total / 0 failed`
  - blocked-save recovery slice current fail-closed rerun: `1338 total / 0 failed`
  - blocked-save recovery slice-local delta: `+6` executed UI cases, alongside expanded Stages editor coverage outside the UI lane
  - blocked-save typography follow-up pre-change observed result: `1338 total / 0 failed`
  - blocked-save typography follow-up current rerun: `1341 total / 0 failed`
  - blocked-save typography follow-up slice-local delta: `+3` executed UI cases covering authored bindings, locale round-trip/sizing preservation, and missing-binding fail-fast behavior
  - Gameplay Stage Name typography follow-up pre-change and current rerun: `1341 total / 0 failed`
  - Gameplay Stage Name typography follow-up slice-local delta: `+0`; the existing locale round-trip guard now asserts en-US and ko-KR `HeaderLarge` theme identity plus target-local TMP `UpperCase` while retaining authored sizing
  - Pause stage-image browser pre-change observed result: `1344 total / 0 failed`
  - Pause stage-image browser current rerun: `1346 total / 0 failed`
  - Pause stage-image browser slice-local delta: `+2`; new guards cover the prefab-authored shell/panel/overlay/marker dependencies and selected-stage name locale refresh while the existing mapper, navigation, and screenshot-preview guards were updated to the image-browser contract
  - production-outro removal pre-change and current UI rerun: `1341 total / 0 failed`
  - production-outro removal slice-local UI delta: `+0`; the temporary Definition parity test was replaced one-for-one by explicit null scene wiring coverage, with the renamed actual-scene PlayMode smoke validated separately as `1 total / 0 failed`
  - production-outro activation slice-local UI delta: `+0`; the explicit-null scene guard was replaced one-for-one by authored six-panel order/layout/import/scene-wiring coverage, while the renamed actual-scene PlayMode smoke separately passed `1 total / 0 failed`
  - SurfaceBelt center remainder badge pre-change observed result: `1340 total / 0 failed`
  - SurfaceBelt center remainder badge current rerun: `1344 total / 0 failed`
  - SurfaceBelt center remainder badge slice-local delta: `+4`; one residue-contract test and three focused transition/lifecycle tests were added while existing presenter and canonical prefab guards were updated to the single-center, number-free `HasAnyRemaining` contract and its DOTween/All In 1 presentation behavior
  - comic-sequence audio-settings follow-up pre-change observed result: `1343 total / 0 failed`
  - comic-sequence audio-settings follow-up current rerun: `1345 total / 0 failed`
  - comic-sequence audio-settings follow-up slice-local delta: `+2`; focused guards cover Master/BGM/fade volume composition and Master-or-BGM mute behavior
  - comic-sequence BGM-focus recovery pre-change observed result: `1345 total / 0 failed`
  - comic-sequence BGM-focus recovery current rerun: `1347 total / 0 failed`
  - comic-sequence BGM-focus recovery slice-local UI delta: `+2`; focused coordinator guards cover source-scene restore and accepted-transition no-restore settlement, while existing routing cases gained handoff-commit assertions without adding executed cases
  - comic-sequence enter-fade follow-up pre-change observed result: `1347 total / 0 failed`
  - comic-sequence enter-fade follow-up current rerun: `1348 total / 0 failed`
  - comic-sequence enter-fade follow-up slice-local UI delta: `+1`; the focused overlay guard covers transparent entry, in-progress black opacity, and full-cover background settlement before initial reveal
  - Settings language-cycle navigation pre-change focused result: `108 total / 2 failed`; only the missing focus slot and skipped language node failed
  - Settings language-cycle navigation implemented focused result: `108 total / 0 failed`
  - Settings language-cycle navigation slice-local delta: `+2`; focused guards cover pointer/Submit exactly-once parity, authored focus reveal, and unavailable-node exclusion
  - localization string-governance Phase 3 pre-change observed result: `1399 total / 0 failed`
  - localization string-governance Phase 3 review-hardening focused rerun: `36 total / 0 failed`
  - localization string-governance Phase 3 final rerun: `1435 total / 0 failed`
  - localization string-governance Phase 3 slice-local delta: `+36`; synthetic validator fixtures, public dictionary immutability coverage, and Unity integration guards were added without removing, merging, or weakening existing exact-copy, Smart-flag, or Stage compatibility tests
  - localization/typography CJK Phase 4 pre-change observed result: `1435 total / 0 failed`
  - localization/typography CJK Phase 4 focused reruns: policy `17/17`, Unity adapter `50/50`, production registration `7/7`
  - localization/typography CJK Phase 4 final rerun: `1445 total / 0 failed`
  - localization/typography CJK Phase 4 slice-local delta: `+10`; two pure-policy cases and eight production-adapter cases cover runtime selection admission and side-effect contracts without changing production assets
  - localization/typography CJK Phase 5 focused reruns: catalog `21/21`, foundation `74/74`, production `42/42`, Unity integration `53/53`, architecture `62/62`, documentation governance `12/12`
  - localization/typography CJK Phase 5 final rerun: `1464 total / 0 failed`
  - localization/typography CJK Phase 5 tests-first deviation: initial structural red `61 total / 1 failed` and behavior red `20 total / 1 failed`; supplemental entry replays were retrospective, and the user explicitly approved that retained chronology deviation for closeout
- Removed tests:
  - 12 NanumGothic-specific source/SDF/glyph/fallback validation cases and one legacy Climate migration retention case were removed with the retired assets; the locale-independent lower-layer boundary remains covered by the stronger existing theme-model guard, and the Settings localization-key coverage contract was preserved as a font-independent test, for a net UI executed-case delta of `-12` (`1348 -> 1336`)
  - the temporary outro validation-copy parity guard was replaced by an explicit null production-scene wiring guard after the duplicate Definition asset was removed; shared outro routing behavior remains covered
  - the later production-outro activation replaced that explicit-null guard with authored asset/order/layout/import and production-scene wiring coverage; the generic missing-content direct-return routing test remains intentionally covered
  - the 70-method mixed legacy suite containing `CinematicVideoOverlayView`, `VideoClip`, viewport/aspect, skip-policy, video coordinator, and routing tests was removed with the retired MP4 runtime; shared intro/outro routing coverage was retained in `ComicIntroOutroRoutingTests`, and comic overlay/coordinator behavior is covered in `ComicSequenceFlowTests`
  - ActionBar presenter behavior tests were removed with the retired proof residue presenter.
  - The inactive product-decision prefab guard was replaced by a proof-residue absence and missing-script guard.
  - Diagnostics overlay behavior tests were removed with the unused runtime feature.
  - Obsolete transition overlay component, old prefab, and recovery-route behavior tests were removed after the canonical shell/content catalog route became the only supported path.
  - Duplicate common transition content prefab files and stale common-only content view types were removed after PR-T2 collapsed the shared physical content mapping.
  - ObjectiveStatus screen controller tests were removed with the retired ObjectiveStatus production screen.
- Renamed / merged / split tests:
  - replaced the former informational Pause stepper contract with a prefab-authored, selectable stage-image browser and pause-owned full-canvas preview; the current stage now determines only the initial selection and owns no separate decoration
  - renamed the installer HUD migration guard from the allowlisted legacy-bridge wording to canonical HUD prefab wording so the test name matches the surviving runtime path
  - renamed the transition content catalog guard to cover shared semantic mapping instead of one physical prefab per semantic
- Replaced weak guards:
  - color-only current-marker and movable viewed-frame assertions are replaced by full sequence-state mapping, a persistent current ring, rail/node geometry checks, and screenshot-preview payload closure
  - Stage Name's generic authored-English assertion is replaced by exact en-US Orbitron `HeaderLarge`, ko-KR KBO Dia Gothic Medium theme font/material/style, and target-local TMP `UpperCase` assertions; World Guide remains independently fixed to KBO Dia Gothic Light
  - retired video playback/skip/aspect guards are replaced by sprite import-resolution, normalized panel-layout, click sequencing, final-transition fade, ownership cleanup, audio-focus ordering, and current comic-sequence component presence coverage
  - retired movement Slider, separate Arrow/WASD display-group alpha/Light checks, `Use Arrow Keys` localized label, and movement-current text expectations are replaced by one state-driven visual toggle contract
  - title-only and injected Korean font-resolver evidence is replaced by production Scene/Catalog composition coverage over all 36 governed Settings TMP targets, while a separate exact 51-target closure guard catches new unbound TMP or unclassified binding additions
  - Main Menu-only duplicated Settings assembly assertions are replaced by a thin-adapter guard plus common `SettingsScreenRuntimeBuilder` behavior coverage
  - Stage 5-only freeze language is replaced with Stage 4–8 seam-preservation language
  - ad hoc “UI test count” bookkeeping is replaced with structural delta, guard evolution, and warning interpretation
  - legacy overlay-dependent stage-clear assumptions are replaced with canonical Stage 7 terminal-screen coverage and scene-bootstrap contract coverage
  - diagnostics read-only boundary checks are replaced with absence guards for removed runtime surface
  - transition overlay recovery-route protection is replaced with canonical shell/content catalog setup-defect guards
  - legacy navigation router setup coverage is replaced with `IUiNavigationTargetResolver` fixture coverage plus public-surface absence guards
- Obsolete guards:
  - none removed by default
  - if a guard becomes obsolete, record which stronger guard now protects the same seam
- Runner warning changes:
  - governance warnings remain non-blocking unless the runner exit code changes
  - the current soft governance warning state must be recorded separately from UI regressions

## Guard Evolution
- Expected architectural evolution:
  - legitimate public-surface evolution is allowed only when it is durable, architecture-relevant, and lands with the functional change, updated freeze expectation, matching behavior guard, and baseline/doc rationale in the same change
- Stale baseline wording correction:
  - the baseline note and `Docs/Testing/Gameplay-Test-Automation-Guide.md` must be updated together when the UI lane scope, interpretation, or counts change
  - KBO Dia Gothic committed source identity must remain a pre-Unity Git-object gate; UI tests must not reinterpret a known importer-derived working serialization as source corruption
- Weak-to-strong guard replacement:
  - replacing a weak guard is acceptable only when this note records the old seam, the stronger replacement guard, and the reason the replacement is stronger
- Accidental seam erosion:
  - any new public surface, cross-layer shortcut, or Stage 4–8 contract growth without matching guard updates is a regression, even if behavior tests still pass
- Public-surface change governance:
  - freeze-test updates land alongside the functional change, never as later cleanup
  - temporary exceptions are not part of the Stage 9 freeze; unresolved needs become blockers instead of exemptions
  - `UiNavigationInputRouter` remains an input router only; popup-first, screen, overlay, and HUD target assembly belongs to canonical resolver implementations

## Runner Warning Status
- Governance mode: `soft`
- Non-blocking governance warnings:
  - `Core candidate debt is above threshold: candidates=8, threshold=5, streak=0`
  - this warning remains non-blocking and separate from popup migration pass/fail interpretation
  - do not collapse non-blocking warnings into the pass/fail summary
- Regression distinction:
  - a new UI seam failure is blocking
  - an unchanged non-blocking governance warning is advisory and must not hide a new seam regression

## PlayMode Escalation
- Stage 9 default: no additional UI PlayMode coverage unless EditMode cannot credibly verify the protected ownership behavior.
- Escalation triggers:
  - runtime-only input routing that depends on the real play loop
  - scene lifecycle ordering or activation timing that materially changes screen/popup/HUD ownership behavior
  - domain reload or play-loop behavior that invalidates an EditMode-only result
- Non-triggers:
  - mapper/policy/controller tests
  - reflection guards
  - presenter interaction tests
  - EditMode-composed UI hierarchy checks that can be driven directly
- PlayMode escalation status:
  - ChanceLost adds one UI EditMode production-content test and extends the existing graphics PlayMode capture case for shader compilation and four-aspect-ratio frames; it adds no new PlayMode test. Focused PlayMode `8/0` passed, while unfiltered full and manual campaign E2E remain unrun.
  - no additional UI PlayMode tests were added in Stage 9
  - EditMode remained sufficient for mapper/policy/controller hardening and UI hierarchy ownership verification
  - the SurfaceBelt center remainder badge change required no additional PlayMode escalation because its prefab hierarchy, serialized references, remainder-state binding, active/inactive distinction, tween replay rules, disable cleanup, and isolated Shine material are covered by the canonical HUD and focused EditMode tests; manual in-game visual inspection remains not run
  - the Settings language-cycle navigation change required no additional PlayMode escalation because the focus graph, serialized `SelectionFrame`, pointer/Submit semantic parity, and unavailable-node exclusion are covered by focused EditMode tests; manual Editor navigation inspection remains not run
  - localization string-governance Phase 3 required no PlayMode escalation because its package-free fixtures, editor-only Unity asset snapshot, parser integration, and source-level mutation boundary are fully exercised in EditMode; Player, CJK content/font, performance, and memory evidence remain not run
  - comic-sequence production scene bootstrap continues to be covered by actual-scene PlayMode smoke: intro presentation exercises actual Submit and blocked Cancel input, while the Gameplay outro case uses Input System mouse state plus EventSystem raycasts to prove six cumulative panel advances, opaque handoff, Main Menu lifecycle completion, and one-time progress persistence with authored production content

## Covered Freeze Evidence
- architectural seams are guarded by tests, not only by convention
- `Gameplay.UIAccess` remains bounded and gameplay-owned
- `UIStateMapper` remains the only durable mapped presentation reduction path into UI-facing snapshot state
- no raw gameplay feed or gameplay-authoritative type is reintroduced into presenters, views, or HUD/screen/popup contracts
- `HUDRootPresenter` remains the sole mapped-state HUD subscriber and shell-level fan-out owner
- `HUDController` remains lifecycle, child binding, and bounded input relay only
- popup stack identity, topmost ownership, close ordering, and popup-first back handling remain centralized in `PopupController` and `UIFlowCoordinator`
- screen runtime ownership remains centralized in `ScreenController` with deterministic show/push/replace/pop/pop-to/clear semantics
- `UIFlowCoordinator` remains routing, popup-first-back, and cross-layer sequencing only
- Historical Stage 8 inventory decomposition remains representative rather than contractual and is not a current gameplay screen:
  - root presenter stays bounded to source items plus canonical selection
  - child presenters stay mesh-free and responsibility-specific
  - no popup/flow ownership or global child input-bag convenience is added to the action child
- Help is also not a current gameplay screen; any older Help-as-screen wording is documentation drift or historical context only
- UI diagnostics overlay was removed as an unused runtime feature; it is not hidden, dev-only retained, or a protected runtime path
- `UIAudioScene` now preserves one canonical `GameplaySceneHost -> GameplayUiFlowInstaller` bootstrap path with no serialized duplicate UI roots, duplicate input-routing roots, or pre-authored popup/screen lifecycle trees
- canonical UI bootstrap now instantiates one prefab-authored root shell named `GameplayUiCanvasRoot`, and that shell remains infrastructure-only at the top level with `HudLayer`, `ScreenLayer`, and `PopupLayer`
- root `UI-Current-Structure-Source.md` is the external current-structure source for docs regeneration and stale-token audits; it mirrors the 3-layer root shell, current `ScreenId` / `PopupId` lists, HUD membership, display-only HUD responsibility, removed UI Push/Flip command-route vocabulary, DemoStageControl preservation, diagnostics removal, scene transition shell/catalog path, resolver-only navigation state, and the shadow-only locale catalog/string-governance boundaries without implying production runtime migration
- scene transition overlay runtime uses the canonical shell asset and authored content catalog only; missing shell/catalog setup is surfaced as a defect instead of rebuilding UI at runtime
- scene transition content mapping is exact: `StageClearNext` uses authored `GenericLoadingOverlayContent`, `DeathRetryChanceLost` uses slot/effect-driven `ChanceLostOverlayContent`, unmatched kinds fail closed, and no overlay-kind or generic catalog fallback participates in production routing
- scene transition content base views expose only root group and progress text as required inspector bindings; title/message/progress bar/animator base bindings are not current contract
- scene transition payload/model composition carries semantic identity, input/progress state, and typed chance-loss numeric state only; generic `Title` / `Message` members and coordinator display-copy resolvers are retired because no transition renderer consumes them
- `LevelFailedRestart` has no current dedicated transition content message/text contract; the old LevelFailed-only message field was removed as stale residue
- HUD legacy runtime builder path was removed in the same phase, leaving one canonical prefab-authored HUD creation path beneath `HudLayer`
- HUD prefab authoring remains a bounded HUD proof and must not be treated as precedent for screen changes without fresh review
- canonical HUD composition is `Pause`, `StageInfo`, `ObjectiveHud`, `ChancePanel`, and `SurfaceBeltIndicator`
- `PlayerStatus` Presenter/VM/View and its permanently hidden prefab subtree are retired; HUD construction retains only the five visible child slices
- the unused legacy `ObjectiveConditionRowView` is retired while `ObjectiveHudRowView` remains the canonical authored objective-row view
- ActionBar retired proof residue was removed from runtime types, tests, and `GameplayHudRoot.prefab`; no HUD responsibility was moved into notification, screen stack, or gameplay command ownership
- HUD remains a display consumer of mapped UI presentation state. It may raise bounded UI-owned requests such as pause flow, but `RequestPush`, `RequestFlip`, `BufferUiPush`, and `BufferUiFlip` are removed UI command-route vocabulary and are not current gameplay command paths.
- Settings/rebind Push/Flip UI remains active for binding display, override, save, and restore.
- popup legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored popup creation path beneath `PopupLayer` via a fixed-shape popup-only catalog
- Main Menu popup debt classification is now explicit: `EnsurePopupLayerView()` creates only the technical Backdrop/Content mount as runtime infrastructure, while the player-visible Confirm hierarchy is the catalog-authored `ConfirmPopup.prefab`; this documentation correction changes no runtime path or executed test count
- popup prefab views remain visual/local only; popup callbacks, timers, and animation completions do not own lifecycle, stack mutation, or dismissibility policy
- tooltip remains a bounded popup special case for local anchor/clamp presentation only and does not own auto-hide, backdrop, or timer-driven lifetime policy
- screen legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored screen creation path beneath `ScreenLayer` via a fixed-shape screen-only catalog
- the screen catalog remains fixed-shape and screen-only and does not widen into a variant/theme/child-section registry
- screen prefab authoring is guarded by simple-shell checkpoint, terminal-screen checkpoint, and complex-screen checkpoint evidence so the logical gameplay root, `StageResultScreen`, and `SettingsScreen` cannot distort the general screen model
- current canonical `ScreenId` values are `None`, `Gameplay`, `Settings`, `StageResult`, `LevelFailed`, and `GameClear`
- `ScreenId.Gameplay` remains gameplay-root-adjacent with no visible screen prefab/view and does not acquire gameplay-access shortcuts, pause ownership, or history shortcuts
- `StageResultScreen`, `LevelFailedScreen`, and `GameClearScreen` remain runtime-owned terminal result screens; their actions stay intent-only and do not locally decide root replacement policy
- terminal result copy is descriptor-backed in UI.Application: StageResult owns only the localized Continue action, LevelFailed receives a typed gameplay failure reason and maps it to title/detail/restart/main descriptors, and GameClear owns localized title/main descriptors; Gameplay Host/UIAccess own no resolved terminal display strings or localization dependencies
- `GameClearScreen` is a result-only terminal screen with title and main label bindings only; retired authored `RestartLevelButton` and `Detail` compatibility objects were removed from its runtime view and prefab
- `SettingsScreen` is one authored prefab hierarchy managed at runtime, with authored `SettingsAudioSection` and `SettingsDisplaySection` children; audio/display fallback rebuilding is removed while preview/session ownership remains in `SettingsRuntime`
- Main Menu and Gameplay now compose that Settings shell through the same `GameplayScreenPrefabCatalog -> SettingsScreenRuntimeBuilder` path. The catalog theme is mandatory, Main Menu owns only overlay Back/popup translation, external locale refresh preserves child state, and dropdown live item typography follows the same locale theme.
- Main Menu Settings overlay debt classification is now explicit: its runtime-created ordering root, technical pointer blocker, and Content mount are infrastructure around the authored `SettingsScreen.prefab`, not a second runtime-built Settings hierarchy; this documentation correction changes no runtime path or executed test count
- Settings authored child-view canonicalization is closed here; future changes should update runtime contracts and focused behavior tests directly
- stage clear reaches only the canonical Stage 7 terminal `StageResult` screen path through `MinimalStageCompletionReadModel`; the legacy host-owned clear overlay no longer survives as a parallel runtime UI system
- current canonical `PopupId` values are `None`, `Pause`, `Confirm`, and `DemoStageControl`
- StageResult screen is a minimal stage-completion navigation endpoint. It no longer carries or displays title/summary/detail result text; it consumes `MinimalStageCompletionReadModel`-derived navigation payloads and emits intent-only `StageNavigationRequest` values for continue, retry, and next-stage paths.
- Reward popup is not a stage-clear presentation endpoint or reward commit owner. Reward/progression commit remains owned by the stage subsystem before UI consumes the read model.
- UI remains non-authoritative: it does not mutate `WorldState`, does not receive raw `TickResult` or raw gameplay frames in views, and consumes snapshots/viewmodels/read models instead.
- Stage completion back handling is fixed as current behavior: terminal result screens consume back, and screen/popup/HUD remain separate stacks/layers with input blocking derived from `UIBlockPolicy`.
- Final-stage clear currently selects `GameClear` instead of the regular StageResult next-stage flow. This is protected as current behavior, not extracted into a new policy in PR-1.
- `DemoStageControl` is a catalog-authored, English operator-assist popup; `UIFlowCoordinator` owns popup flow/lifetime routing, while feature-local sources and command ports own presentation and commands
- `DemoStageControl` is a build-included tester/demo/showcase assist feature for tester assist clear, hard-section bypass, showcase navigation, and stage browsing; it is not a deletion candidate or dev-only compile exclusion target
- future product/build availability for `DemoStageControl` is supplied through a separate access-provider decision that gates new opens only; no provider is implemented in this migration
- `Pause` and `Confirm` remain protected canonical popup paths; Reward popup is absent from current popup vocabulary and is not the canonical stage-clear result path
- `TooltipPopup` was retired from the current popup vocabulary after PR-TT1 found no production caller. Settings display hover hint remains as a local inline pointer-hover affordance and does not use `PopupId.Tooltip`.
- protected UI paths for drift correction include `LevelFailed`, `GameClear`, `StageResult`, Pause/Confirm popup paths, `UI_Composition` adapters, UI audio/display/settings bridges, and `StageNavigationRequest`
- no Stage 4–8 contract is widened merely for test/debug convenience
- Policy extraction candidates for later PR: terminal screen selection, terminal back handling, pause return decision, audio transaction outcome mapping, and final-stage routing. Do not extract in PR-1.
- Remaining PR-1 gaps: builder registry completeness belongs to PR-2; popup completion audio mapping, settings adapter lifecycle, HUD module completeness, and broader stage completion end-to-end/manual runtime evidence remain follow-up work.

## Companion Smoke Check
- Command: `./run_tests.sh core`
- Status: green
- Core EditMode: `217 total / 0 failed`
- Core PlayMode: `111 total / 0 failed`
- Interpretation:
  - this remains a companion smoke lane, not a replacement for `./run_tests.sh ui`
  - Stage 9 evidence is incomplete if the UI lane passes on a worktree where the companion core lane is not rerun

## Campaign PlayerPrefs Retirement and JSON Save Integration — 2026-08-23

### Structural Delta
- Campaign progression, active-slot, and launch-state production truth now flows through the Stages-owned JSON composition. UI consumes `ICampaignSaveQuery`, `ICampaignSlotLifecyclePort`, `ICampaignContinuePreparationPort`, typed comic progress, and recovery ports instead of owning serialization or arbitrary slot mutation policy. Continue preparation carries only expected slot/stage/persisted-group/target-group identity; stale preparation clears only the caller-owned handoff, refreshes the panel, and never routes.
- Comic fixed-shell runtime-generation debt was closed on 2026-09-13 KST. Both production installers now reference the same canonical authored `ComicSequenceOverlay.prefab`; missing or malformed authored references fail fast, and `EnsureHierarchy()` no longer creates or repairs fixed visual objects. The Definition-sized, non-interactive panel pool remains bounded runtime infrastructure and is reused across presentations. Direct pointer/Submit exactly-once, Cancel/lower-layer blocking, prefab/scene wiring, failure, and pool-reuse contracts are covered. Same-working-tree evidence passed the Windows UI build and Unity UI EditMode `1384/0`, filtered `full` actual-scene PlayMode `3/0`, and `core` EditMode `282/0` plus PlayMode `111 total / 107 passed / 4 skipped / 0 failed`; broad unfiltered `full` and manual Editor/Player visual validation were not run.
- PlayerPrefs-backed campaign fallback, legacy import, and rollback-selection production paths are retired. Readiness probes remain diagnostics only and do not participate in production read, write, delete, or launch routing.
- Main Menu pending-launch ownership is lifecycle-bound. Stale confirmations, disposed controllers, and commands that arrive after an accepted launch cannot delete or replace the accepted slot/handoff owner.
- Direct-play temporary-state clearing is an explicit non-recovering delete operation. It removes the canonical file, backup, rollback residue, and both write-temp families for profile, local launch state, and pending reset without deleting quarantine/rejection evidence.

### Guard Evolution and Responsibility Shift
- New UI guards cover delete confirmation after disposal, reset confirmation after disposal, public commands after disposal, stale failure ownership, pending-launch delete rejection, continue/delete ordering, and preservation of an existing restart confirmation for invalid intent.
- Continue guards now cover reservation-before-preparation, committed stage/group recheck, stale/no-route handling, and exact-token preservation when a newer handoff replaces the original reservation during preparation. This is an EditMode policy/composition contract; it does not trigger PlayMode escalation.
- The former default-PlayerPrefs constructor assertion and the coarse `MainMenu_Delete_ClearsOnlyMatchingPendingHandoff` case were removed with their retired ownership model. Exact launch reservation and lifecycle cases now carry that responsibility.
- Stage save guards cover absence of production PlayerPrefs writes, the UI-to-Stages adapter boundary, and explicit temporary-state deletion without rollback resurrection.
- UI owns intent, confirmation, recovery presentation, and view-model publication. Stages owns campaign persistence, active-slot/local launch state, recovery state, and atomic file lifecycle.

### Same-Revision Validation
- `./run_tests.sh ui`: Windows UI build passed; Unity UI EditMode `1353 total / 0 failed`.
- `./run_tests.sh core`: Core EditMode `217 total / 0 failed`; Core PlayMode `109 total / 0 failed`.
- `./run_tests.sh full --filter CampaignSaveArchitectureV2Tests`: EditMode `54 total / 0 failed`; PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter CampaignSaveSlotStoreAdapterTests`: EditMode `12 total / 0 failed`; PlayMode `0 total / 0 failed`.
- `./run_tests.sh full --filter CampaignPlayerPrefsWriteRemovalTests`: EditMode `6 total / 0 failed`; PlayMode `0 total / 0 failed`.
- The UI count moves from the 2026-08-22 follow-up's `1348` to `1353` for this slice. This is a slice-local delta, not a replacement for the pinned snapshot or a broad full-lane claim.

### Runner and PlayMode Status
- Existing soft governance warnings remain advisory and separate from the touched-slice pass/fail result.
- The filtered EditMode fixtures contain no PlayMode tests; the companion core PlayMode lane passed on the same revision.
- Generated `InitTestScene` artifacts were removed automatically by the runner and left no residual worktree mutation.
- Broad `./run_tests.sh full` was not run, so no project-wide or full-regression-green claim is made.

## Campaign MainMenu Separated Launch Results — 2026-08-25

### Structural Delta
- `MainMenuController` evaluates each immutable `CampaignSlotEntry` through the Stages-owned launch evaluator and derives `CampaignSlotActionPolicy` separately.
- `MainMenuSlotViewModelMapper` receives `MainMenuSlotPresentationInput` and validates entry/evaluation state identity plus policy consistency before publishing card state. It no longer receives a sequence resolver or combined validation service/result.
- Profile corruption, unsupported schema, IO, authorization, and recovery-pending states remain on the global blocked-report screen. Sequence/catalog launch failures remain occupied per-slot cards with restart/delete actions. Completed slots remain Completed even when their persisted level group requires synchronization.
- Restart/delete/new-game confirmations and exact handoff-token ownership remain controller application policy. UI does not repair raw save evidence or submit complete slot replacement.

### Guard Evolution and Responsibility Shift
- Architecture guards require the legacy combined validation source to be absent and prevent its result/status/service vocabulary from returning to the controller or mapper.
- Mapper behavior guards cover separated launch-failure results and completed stale-group presentation. Production composition guards prove that installer, controller, and launch evaluator share the same campaign sequence authority.
- Existing MainMenu lifecycle, localization, blocked recovery, preparation-stale, and newer-handoff preservation fixtures remain in the touched cluster. These are pure EditMode policy/composition paths and do not require separate PlayMode escalation.

### Same-Revision Validation
- Tests-first focused run: Windows build stopped with the intended two `CS1503` errors while the mapper still required the legacy signature.
- First implemented focused gate: EditMode `113 total / 0 failed`; matching PlayMode `0`.
- Nine-fixture filtered `full`: EditMode `403 total / 0 failed`; matching PlayMode `0`.
- `./run_tests.sh core`: Core EditMode `217 total / 0 failed`; Core PlayMode `109 total / 0 failed`.
- `./run_tests.sh ui`: Windows UI build passed; Unity UI EditMode `1352 total / 0 failed`.
- Broad unfiltered `full` and manual Player/build smoke were not run; no broad-green claim is made.

## 2026-09-06 KBO serialization correction

- Same working-tree `./run_tests.sh ui`: Windows build passed; EditMode 1355 passed, 0 failed.
- Added four TMP canonical-ratio recalculation/rejection cases; changed two importer-drift allowance cases to rejection.
- Both KBO SDF files remained byte-identical after Unity; no automatic restore was needed.
- This run also includes the pending HUD badge split and authored-position regression cases.
- Evidence: `/mnt/d/J2M/evidence/20260906-kbo-canonical-commit/ui/`.
- Broad full, visual capture, and manual Player checks were not run; the visual runner requires clean tracked HEAD inputs and unrelated Addressables edits remain preserved.

## Pause Preview Close Keyboard Accessibility — 2026-09-13

### Structural Delta
- `StagePreviewOverlay/CloseButton` now owns an authored, non-raycast `SelectionFrame` through a one-slot `UiSelectableButtonGroup`; its existing `Button` and `UiHoverScaleEffect` remain the pointer and feedback components.
- `PauseStagePreviewOverlayView` is the local single-action navigation target. Focused Submit plays the shared button feedback and emits the same close request as pointer click, while Cancel remains a nested-state exit shortcut.
- `PausePopupView` retains router-facing popup ownership and delegates only the open preview state to the `PreviewClose` domain. Every preview open starts a new hidden-focus cycle, even when the Pause target already has revealed keyboard focus, and close returns to the existing progression selection.

### Guard Evolution and Responsibility Shift
- Prefab guards require the one-slot group, Close ownership, inactive authored frame, non-raycast selection image, and existing hover feedback.
- Behavior guards cover hidden keyboard-open and pointer-open state, first subsequent Submit or Navigate reveal, following-Submit close, Cancel close, exactly-once closure, keyboard-selected scale cleanup, and preserved progression selection.
- The Close action no longer relies on Pause-level unconditional Submit handling. Overlay-local navigation now owns its focus and submit feedback without becoming a new popup-stack entry or reusing the Settings-oriented focus graph.

### Same-Working-Tree Validation
- `./run_tests.sh ui`: Windows UI build passed; Unity UI EditMode `1376 total / 0 failed` on the main integration working tree.
- `./run_tests.sh core`: Core EditMode `282 total / 0 failed`; Core PlayMode `111 total / 0 failed`.
- The shared KBO Dia Gothic Medium/Light integrity guard reported `NO_MUTATION` for both assets and required no restore.
- Existing soft governance warnings remained advisory and did not change the touched UI result.
- The runner detected and removed its two generated `InitTestScene` artifacts after Core PlayMode; no generated scene residue remains.
- Separate manual Editor/Player visual validation and broad `./run_tests.sh full` were not run; no claim is made for those scopes.
