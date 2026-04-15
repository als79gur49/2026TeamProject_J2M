# UI EditMode Baseline 2026-04-15

## Scope
- Purpose: preserve the Stage 4 mapped seam, Stage 5 HUD shell boundaries, Stage 6 popup stack ownership, Stage 7 screen runtime ownership, and the Stage 8 inventory presenter decomposition while Stage 9 adds test and diagnostic hardening.
- Command: `./run_tests.sh ui`
- Runner path: governance -> Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` -> Unity `TestRunnerCliBootstrap.RunEditMode -codexSelection ui`
- Companion smoke lane: `./run_tests.sh core` on the same worktree
- Recorded rerun: April 16, 2026 on the current Stage 9 worktree
- Result note: future updates to `## Result` and `## Companion Smoke Check` must not be bumped without updating the structural delta below.

## Result
- Status: green
- Unity UI EditMode: `110 total / 0 failed`
- Result XML: `TestResults/wsl-unity-ui-editmode.xml`
- Unity log: `TestResults/wsl-unity-ui-editmode.log`
- Build log: `TestResults/wsl-dotnet-ui.log`

## Structural Delta
- Added tests:
  - controller/coordinator public-surface freeze tests for `UIFlowCoordinator`, `ScreenController`, `PopupController`, and `UIBlockPolicy`
  - deterministic controller/policy guards for `PopTo`, runtime action relay, close-all ordering, backdrop routing, and older-frame refresh behavior
  - diagnostics boundary tests proving the Stage 9 overlay remains read-only, bounded, and opt-in for drill-down details
  - governance documentation tests for baseline structure, stale wording removal, and PlayMode escalation-marker enforcement
  - Stage 8 structural drift guards for root-owned state, child public surfaces, and input-bag/non-flow leakage
  - `TutorialScene` scene contract guard proving one canonical gameplay/bootstrap root path, one serialized installer/host binding, and no serialized duplicate UI residue
  - canonical stage-clear integration guard proving gameplay host + installer flow transitions into the Stage 7 `StageResult` screen without relying on the legacy overlay path
  - canonical root-shell prefab structure guards proving the runtime shell contains only infrastructure children and no serialized feature views
  - HUD prefab migration guards proving the installer mounts one authored HUD prefab under `HudLayer`, the shell remains HUD-markup free, and the legacy HUD builder symbols are absent from code and docs
  - HUD view boundary guards proving HUD views no longer expose runtime `Configure(...)` entrypoints and stay free of flow/gameplay-access/diagnostics dependencies
  - mixed-mode inventory guards proving the current hybrid entries are explicit, root shell is no longer hybrid, and the allowlist remains mechanically inspectable
  - stronger screen-view ownership guards proving screen and inventory child views do not surface navigation, popup, back-stack, or controller shortcuts
- Test count delta:
  - previous pinned UI EditMode baseline: `64 total / 0 failed`
  - current rerun: `110 total / 0 failed`
  - delta: `+46` tests, targeted at seam hardening, diagnostics boundary checks, governance evidence, canonical `TutorialScene` adoption, canonical root-shell migration, HUD prefab sunset proof, and mixed-mode drift detection
- Removed tests: none expected for Stage 9; if any are removed, the replacement guard must be named here explicitly.
- Renamed / merged / split tests:
  - renamed the installer HUD migration guard from the allowlisted legacy-bridge wording to canonical HUD prefab wording so the test name matches the surviving runtime path
- Replaced weak guards:
  - Stage 5-only freeze language is replaced with Stage 4–8 seam-preservation language
  - ad hoc “UI test count” bookkeeping is replaced with structural delta, guard evolution, and warning interpretation
  - legacy overlay-dependent stage-clear assumptions are replaced with canonical Stage 7 terminal-screen coverage and scene-bootstrap contract coverage
- Obsolete guards:
  - none removed by default
  - if a guard becomes obsolete, record which stronger guard now protects the same seam
- Runner warning changes:
  - governance warnings remain non-blocking unless the runner exit code changes
  - the current soft governance warning state must be recorded separately from UI regressions

## Prefab Migration Mixed-Mode Inventory
- Root shell status:
  - canonical root shell is now migrated to the installer-instantiated prefab path
  - no mixed-mode allowlist row remains for the root shell
- HUD status:
  - canonical HUD is now migrated to one installer-assigned prefab-authored path under `HudLayer`
  - HUD legacy runtime builder path was removed in the same phase
  - this remains a bounded HUD proof and must not be treated as precedent for popup/screen migration
- Hybrid allowlist status:
  - mixed mode remains temporary and explicitly allowlisted only for the entries below
  - freeze evidence stays blocked while this allowlist remains non-empty
- Allowlisted hybrid entries:
  - `Popup:Pause -> GameplayPopupRuntimeFactory.CreatePausePopup`
  - `Popup:ObjectiveInfo -> GameplayPopupRuntimeFactory.CreateObjectiveInfoPopup`
  - `Popup:Confirm -> GameplayPopupRuntimeFactory.CreateConfirmPopup`
  - `Popup:Tooltip -> GameplayPopupRuntimeFactory.CreateTooltipPopup`
  - `Popup:Reward -> GameplayPopupRuntimeFactory.CreateRewardPopup`
  - `Screen:Gameplay -> GameplayScreenRuntimeFactory.CreateGameplayScreen`
  - `Screen:Help -> GameplayScreenRuntimeFactory.CreateHelpScreen`
  - `Screen:ObjectiveStatus -> GameplayScreenRuntimeFactory.CreateObjectiveStatusScreen`
  - `Screen:Inventory -> GameplayScreenRuntimeFactory.CreateInventoryScreen`
  - `Screen:Settings -> GameplayScreenRuntimeFactory.CreateSettingsScreen`
  - `Screen:StageResult -> GameplayScreenRuntimeFactory.CreateStageResultScreen`
  - `ScreenInternal:InventoryScreen.Sections -> InventoryScreenView authored child sections remain runtime-built`
- Governance rule:
  - this section must shrink monotonically as entries migrate
  - any reintroduced legacy builder for a non-listed entry is drift and blocks freeze

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

## Runner Warning Status
- Governance mode: `soft`
- Non-blocking governance warnings:
  - `Core candidate debt is above threshold: candidates=7, threshold=5, streak=0`
  - this warning was unchanged during the Stage 9 reruns
  - do not collapse non-blocking warnings into the pass/fail summary
- Regression distinction:
  - a new UI seam failure is blocking
  - an unchanged non-blocking governance warning is advisory and must not hide a new seam regression

## PlayMode Escalation
- Stage 9 default: no additional UI PlayMode coverage unless EditMode cannot credibly verify the protected ownership behavior.
- Escalation triggers:
  - runtime-only input routing that depends on the real play loop
  - scene lifecycle ordering or activation timing that materially changes screen/popup/HUD ownership behavior
  - diagnostics visibility/toggle behavior that depends on runtime-only execution
  - domain reload or play-loop behavior that invalidates an EditMode-only result
- Non-triggers:
  - mapper/policy/controller tests
  - reflection guards
  - presenter interaction tests
  - EditMode-composed UI hierarchy checks that can be driven directly
- PlayMode escalation status:
  - no additional UI PlayMode tests were added in Stage 9
  - EditMode remained sufficient for mapper/policy/controller hardening, diagnostics toggles, and UI hierarchy ownership verification

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
- Stage 8 inventory decomposition remains representative rather than contractual:
  - root presenter stays bounded to source items plus canonical selection
  - child presenters stay mesh-free and responsibility-specific
  - no popup/flow ownership or global child input-bag convenience is added to the action child
- Stage 9 diagnostics remain read-only, bounded, editor/development-only, and non-reusable as runtime state aggregation
- `TutorialScene` now preserves one canonical `GameplaySceneHost -> GameplayUiFlowInstaller` bootstrap path with no serialized duplicate UI roots, duplicate input-routing roots, or pre-authored popup/screen lifecycle trees
- canonical UI bootstrap now instantiates one prefab-authored root shell named `GameplayUiCanvasRoot`, and that shell remains infrastructure-only at the top level
- HUD legacy runtime builder path was removed in the same phase, leaving one canonical prefab-authored HUD creation path beneath `HudLayer`
- HUD prefab migration is a bounded HUD proof and must not be treated as precedent for popup/screen migration without fresh review
- mixed mode is now explicitly inventoried: root shell and HUD are migrated, while popup/screen legacy builders remain temporary allowlisted entries rather than implicit permanent hybrids
- stage clear reaches only the canonical Stage 7 terminal `StageResult` screen path; the legacy host-owned clear overlay no longer survives as a parallel runtime UI system
- no Stage 4–8 contract is widened merely for test/debug convenience

## Companion Smoke Check
- Command: `./run_tests.sh core`
- Status: green
- Core EditMode: `13 total / 0 failed`
- Core PlayMode: `2 total / 0 failed`
- Interpretation:
  - this remains a companion smoke lane, not a replacement for `./run_tests.sh ui`
  - Stage 9 evidence is incomplete if the UI lane passes on a worktree where the companion core lane is not rerun
