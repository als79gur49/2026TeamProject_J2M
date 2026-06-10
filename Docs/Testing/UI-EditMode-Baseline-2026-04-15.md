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
- Current PR-1 stage-completion baseline rerun: green on 2026-06-10 KST
- Current Windows build result: `dotnet build Game.Feature.UI.Tests.csproj -c Debug` passed with `0` errors
- Current Unity UI EditMode: `706 total / 0 failed`
- Baseline test result: command `./run_tests.sh ui`, result `701 total / 0 failed`, failed tests `none`, failure category `none`, PR change pre-existing failure `no`
- Prior 2차 UI canonical correction report red reason: Windows `dotnet build` missing compile symbols `SurfaceBeltButtonBadgeStyleProfile`, `SurfaceBeltButtonBadgeGroupView`, `EnemyTargetEligibilityResult`, `PendingEnemyBlockedReaction`
- Current interpretation: the prior red reason was not reproduced by the 2026-06-06 KST rerun; retired HUD proof residue was removed after product option B was selected
- Result XML: `TestResults/wsl-unity-ui-editmode.xml`
- Unity log: `TestResults/wsl-unity-ui-editmode.log`
- Build log: `TestResults/wsl-dotnet-ui.log`

## Structural Delta
- Added tests:
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
  - per-kind popup prefab contract and boundary guards proving `Pause`, `ObjectiveInfo`, `Confirm`, `Tooltip`, and `Reward` stay visual/local only, tooltip keeps bounded anchor/clamp behavior, and popup callbacks/timers do not acquire lifecycle ownership
  - screen prefab migration guards proving the installer mounts one fixed-shape screen-only catalog, the screen factory instantiates one canonical authored prefab per screen id under `ScreenLayer`, and screen legacy builder symbols are absent from code and docs
  - screen checkpoint guards proving the simple-shell, terminal-screen, and complex-screen checkpoints stay mechanically inspectable instead of hiding risk inside one broad migration phase
  - stronger screen-view ownership guards proving screen views do not surface navigation, popup, back-stack, controller, or gameplay-access shortcuts
  - transition overlay shell/content catalog guards proving scene transitions use the canonical shell asset and authored catalog, with missing setup reported as a defect
  - canonical UI navigation resolver guards proving `UiNavigationInputRouter` exposes only the `IUiNavigationTargetResolver` setup path and does not reassemble popup/menu concrete targets
  - external structure-source regeneration guard proving root `UI-Current-Structure-Source.md` mirrors the current 3-layer shell, identity lists, DemoStageControl classification, retired ActionBar status, removed diagnostics status, canonical transition path, and resolver-only navigation state
  - PR-1 stage completion guards proving StageResult + Reward + Continue, rewardless clear, final-stage GameClear, retry payload, next-stage/no-next-stage mapping, terminal back consume, Reward popup back consume, and screen/popup/HUD separation before UI refactor scaffolding begins
- Test count delta:
  - previous pinned UI EditMode baseline: `64 total / 0 failed`
  - current rerun: `706 total / 0 failed`
  - delta: `+642` tests, targeted at seam hardening, removed diagnostics overlay absence, governance evidence, canonical gameplay shell adoption, canonical root-shell migration, HUD prefab sunset proof, popup prefab sunset proof, screen prefab sunset proof, transition overlay shell/catalog closure, checkpoint coverage for simple-shell/terminal/complex screens, mixed-mode drift detection, manual smoke-plan governance, canonical navigation resolver-only enforcement, current-structure source regeneration, and PR-1 stage completion protection
- Removed tests:
  - ActionBar presenter behavior tests were removed with the retired proof residue presenter.
  - The inactive product-decision prefab guard was replaced by a proof-residue absence and missing-script guard.
  - Diagnostics overlay behavior tests were removed with the unused runtime feature.
  - Obsolete transition overlay component, old prefab, and recovery-route behavior tests were removed after the canonical shell/content catalog route became the only supported path.
- Renamed / merged / split tests:
  - renamed the installer HUD migration guard from the allowlisted legacy-bridge wording to canonical HUD prefab wording so the test name matches the surviving runtime path
- Replaced weak guards:
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
  - no additional UI PlayMode tests were added in Stage 9
  - EditMode remained sufficient for mapper/policy/controller hardening and UI hierarchy ownership verification

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
- root `UI-Current-Structure-Source.md` is the external current-structure source for docs regeneration and stale-token audits; it mirrors the 3-layer root shell, current `ScreenId` / `PopupId` lists, HUD membership, display-only HUD responsibility, removed UI Push/Flip command-route vocabulary, DemoStageControl preservation, diagnostics removal, scene transition shell/catalog path, and resolver-only navigation state
- scene transition overlay runtime uses the canonical shell asset and authored content catalog only; missing shell/catalog setup is surfaced as a defect instead of rebuilding UI at runtime
- HUD legacy runtime builder path was removed in the same phase, leaving one canonical prefab-authored HUD creation path beneath `HudLayer`
- HUD prefab authoring remains a bounded HUD proof and must not be treated as precedent for screen changes without fresh review
- canonical HUD composition is `Pause`, `StageInfo`, `ObjectiveHud`, `ChancePanel`, `SurfaceBeltIndicator`, and `PlayerStatus`
- ActionBar retired proof residue was removed from runtime types, tests, and `GameplayHudRoot.prefab`; no HUD responsibility was moved into PlayerStatus, notification, screen stack, or gameplay command ownership
- HUD remains a display consumer of mapped UI presentation state. It may raise bounded UI-owned requests such as pause flow, but `RequestPush`, `RequestFlip`, `BufferUiPush`, and `BufferUiFlip` are removed UI command-route vocabulary and are not current gameplay command paths.
- Settings/rebind Push/Flip UI remains active for binding display, override, save, and restore.
- popup legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored popup creation path beneath `PopupLayer` via a fixed-shape popup-only catalog
- popup prefab views remain visual/local only; popup callbacks, timers, and animation completions do not own lifecycle, stack mutation, or dismissibility policy
- tooltip remains a bounded popup special case for local anchor/clamp presentation only and does not own auto-hide, backdrop, or timer-driven lifetime policy
- screen legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored screen creation path beneath `ScreenLayer` via a fixed-shape screen-only catalog
- the screen catalog remains fixed-shape and screen-only and does not widen into a variant/theme/child-section registry
- screen prefab authoring is guarded by simple-shell checkpoint, terminal-screen checkpoint, and complex-screen checkpoint evidence so the logical gameplay root, `StageResultScreen`, and `SettingsScreen` cannot distort the general screen model
- current canonical `ScreenId` values are `None`, `Gameplay`, `ObjectiveStatus`, `Settings`, `StageResult`, `LevelFailed`, and `GameClear`
- `ScreenId.Gameplay` remains gameplay-root-adjacent with no visible screen prefab/view and does not acquire gameplay-access shortcuts, pause ownership, or history shortcuts
- `StageResultScreen`, `LevelFailedScreen`, and `GameClearScreen` remain runtime-owned terminal result screens; their actions stay intent-only and do not locally decide root replacement policy
- `SettingsScreen` now remains one runtime-managed shell with authored `SettingsAudioSection` and `SettingsDisplaySection` children; audio/display fallback rebuilding is removed while preview/session ownership remains in `SettingsRuntime`
- Settings authored child-view canonicalization is closed here; future changes should update runtime contracts and focused behavior tests directly
- stage clear reaches only the canonical Stage 7 terminal `StageResult` screen path; the legacy host-owned clear overlay no longer survives as a parallel runtime UI system
- StageResult screen is a stage completion presentation endpoint. It consumes `StageCompletionReadModel`-derived payloads and emits intent-only `StageNavigationRequest` values for continue, retry, and next-stage paths.
- Reward popup is a reward presentation endpoint, not the reward commit owner. Reward/progression commit remains owned by the stage subsystem before UI consumes the read model.
- UI remains non-authoritative: it does not mutate `WorldState`, does not receive raw `TickResult` or raw gameplay frames in views, and consumes snapshots/viewmodels/read models instead.
- Stage completion back handling is fixed as current behavior: terminal result screens consume back, Reward popup consumes back through popup policy until acknowledged, and screen/popup/HUD remain separate stacks/layers with input blocking derived from `UIBlockPolicy`.
- Final-stage clear currently selects `GameClear` instead of the regular StageResult next-stage flow. This is protected as current behavior, not extracted into a new policy in PR-1.
- current canonical `PopupId` values are `None`, `Pause`, `ObjectiveInfo`, `Confirm`, `Tooltip`, `Reward`, and `DemoStageControl`
- `DemoStageControl` is a catalog-less runtime assist popup created through the factory/runtime/hotkey path and not a gameplay popup catalog entry
- `DemoStageControl` is a build-included tester/demo/showcase assist feature for tester assist clear, hard-section bypass, showcase navigation, and stage browsing; it is not a deletion candidate or dev-only compile exclusion target
- future public-release hiding or disabling for `DemoStageControl` requires a separate product/build configuration decision, not a simple `DEVELOPMENT_BUILD` or `UNITY_EDITOR` compile gate
- `Reward` and `Confirm` remain protected canonical popup paths
- protected UI paths for drift correction include `LevelFailed`, `GameClear`, `StageResult`, `Reward` popup, `Confirm` popup, `UI_Composition` adapters, UI audio/display/settings bridges, and `StageNavigationRequest`
- no Stage 4–8 contract is widened merely for test/debug convenience
- Policy extraction candidates for later PR: terminal screen selection, reward popup open condition, terminal back handling, pause return decision, audio transaction outcome mapping, and final-stage routing. Do not extract in PR-1.
- Remaining PR-1 gaps: builder registry completeness belongs to PR-2; popup completion audio mapping, settings adapter lifecycle, HUD module completeness, and broader stage completion end-to-end/manual runtime evidence remain follow-up work.

## Companion Smoke Check
- Command: `./run_tests.sh core`
- Status: green
- Core EditMode: `13 total / 0 failed`
- Core PlayMode: `2 total / 0 failed`
- Interpretation:
  - this remains a companion smoke lane, not a replacement for `./run_tests.sh ui`
  - Stage 9 evidence is incomplete if the UI lane passes on a worktree where the companion core lane is not rerun
