# UI EditMode Baseline 2026-04-15

## Scope
- Purpose: preserve the Stage 4 mapped seam, Stage 5 HUD shell boundaries, Stage 6 popup stack ownership, Stage 7 screen runtime ownership, and the remaining screen presenter boundaries while Stage 9 adds test and diagnostic hardening.
- Command: `./run_tests.sh ui`
- Runner path: governance -> Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` -> Unity `TestRunnerCliBootstrap.RunEditMode -codexSelection ui`
- Companion smoke lane: `./run_tests.sh core` on the same worktree
- Recorded rerun: April 16, 2026 on the current Stage 9 worktree
- Result note: future updates to `## Result` and `## Companion Smoke Check` must not be bumped without updating the structural delta below.

## Result
- Status: green
- Unity UI EditMode: `155 total / 0 failed`
- Result XML: `TestResults/wsl-unity-ui-editmode.xml`
- Unity log: `TestResults/wsl-unity-ui-editmode.log`
- Build log: `TestResults/wsl-dotnet-ui.log`

## Structural Delta
- Added tests:
  - controller/coordinator public-surface freeze tests for `UIFlowCoordinator`, `ScreenController`, `PopupController`, and `UIBlockPolicy`
  - deterministic controller/policy guards for `PopTo`, runtime action relay, close-all ordering, backdrop routing, and older-frame refresh behavior
  - diagnostics boundary tests proving the Stage 9 overlay remains read-only, bounded, and opt-in for drill-down details
  - governance documentation tests for baseline structure, stale wording removal, PlayMode escalation-marker enforcement, and `TutorialScene` manual runtime smoke-plan governance
  - structural drift guards for root-owned state, child public surfaces, and input-bag/non-flow leakage
  - `TutorialScene` scene contract guard proving one canonical gameplay/bootstrap root path, one serialized installer/host binding, and no serialized duplicate UI residue
  - canonical stage-clear integration guard proving gameplay host + installer flow transitions into the Stage 7 `StageResult` screen without relying on the legacy overlay path
  - canonical root-shell prefab structure guards proving the runtime shell contains only infrastructure children and no serialized feature views
  - HUD prefab migration guards proving the installer mounts one authored HUD prefab under `HudLayer`, the shell remains HUD-markup free, and the legacy HUD builder symbols are absent from code and docs
  - HUD view boundary guards proving HUD views no longer expose runtime `Configure(...)` entrypoints and stay free of flow/gameplay-access/diagnostics dependencies
  - popup prefab migration guards proving the installer mounts one fixed-shape popup catalog, the popup factory instantiates one canonical authored prefab per popup kind under `PopupLayer`, and popup legacy builder symbols are absent from code and docs
  - per-kind popup prefab contract and boundary guards proving `Pause`, `ObjectiveInfo`, `Confirm`, `Tooltip`, and `Reward` stay visual/local only, tooltip keeps bounded anchor/clamp behavior, and popup callbacks/timers do not acquire lifecycle ownership
  - screen prefab migration guards proving the installer mounts one fixed-shape screen-only catalog, the screen factory instantiates one canonical authored prefab per screen id under `ScreenLayer`, and screen legacy builder symbols are absent from code and docs
  - screen checkpoint guards proving the simple-shell, terminal-screen, and complex-screen checkpoints stay mechanically inspectable instead of hiding risk inside one broad migration phase
  - stronger screen-view ownership guards proving screen views do not surface navigation, popup, back-stack, controller, gameplay-access, or diagnostics shortcuts
- Test count delta:
  - previous pinned UI EditMode baseline: `64 total / 0 failed`
  - current rerun: `155 total / 0 failed`
  - delta: `+91` tests, targeted at seam hardening, diagnostics boundary checks, governance evidence, canonical `TutorialScene` adoption, canonical root-shell migration, HUD prefab sunset proof, popup prefab sunset proof, screen prefab sunset proof, checkpoint coverage for simple-shell/terminal/complex screens, mixed-mode drift detection, and manual smoke-plan governance
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

## Prefab Migration Mixed-Mode Status
- Root shell status:
  - canonical root shell is now migrated to the installer-instantiated prefab path
  - no mixed-mode allowlist row remains for the root shell
- HUD status:
  - canonical HUD is now migrated to one installer-assigned prefab-authored path under `HudLayer`
  - HUD legacy runtime builder path was removed in the same phase
  - this remains a bounded HUD proof and must not be treated as precedent for screen migration
- Popup status:
  - canonical popup layer is now migrated to one popup-catalog-backed prefab-authored path under `PopupLayer`
  - popup legacy runtime builder paths were removed in the same phase
  - the popup catalog remains fixed-shape and popup-only; it must not drift into a cross-layer asset registry or policy store
  - tooltip remains a bounded special case for local anchor/clamp presentation only; auto-hide and timer-owned lifetime remain out of scope
  - this remains a bounded popup proof and must not be treated as precedent for screen migration
- Screen status:
  - canonical screen layer is now migrated to one screen-catalog-backed prefab-authored path under `ScreenLayer`
  - screen legacy runtime builder paths were removed in the same phase
  - the screen catalog remains fixed-shape and screen-only; it must not drift into a theme registry, variant registry, child-section catalog, or cross-layer asset registry
  - simple-shell checkpoint is complete for `Help` and `ObjectiveStatus`
  - `GameplayScreen` remains a gameplay-root-adjacent special case and must not be treated as the ordinary migration template
  - terminal-screen checkpoint is complete for `StageResult`, which remains a runtime-owned terminal special case rather than a generic screen model
  - complex-screen checkpoint is complete for bounded `Settings`, which remains one screen shell with nested authored child views and bounded child presenters while `Settings` accessibility remains root-shell-owned
- Hybrid allowlist status:
  - root shell, HUD, popup, and screen migration allowlists are now empty
  - screen hybrid allowlist is now empty
- Allowlisted hybrid entries:
  - none
- Governance rule:
  - any reintroduced legacy builder, child-section runtime unit, or mixed-mode entry is drift and blocks freeze

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
- HUD prefab migration is a bounded HUD proof and must not be treated as precedent for screen migration without fresh review
- popup legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored popup creation path beneath `PopupLayer` via a fixed-shape popup-only catalog
- popup prefab views remain visual/local only; popup callbacks, timers, and animation completions do not own lifecycle, stack mutation, or dismissibility policy
- tooltip remains a bounded popup special case for local anchor/clamp presentation only and does not own auto-hide, backdrop, or timer-driven lifetime policy
- screen legacy runtime builder paths were removed in the same phase, leaving one canonical prefab-authored screen creation path beneath `ScreenLayer` via a fixed-shape screen-only catalog
- the screen catalog remains fixed-shape and screen-only and does not widen into a variant/theme/child-section registry
- screen prefab migration is guarded by simple-shell checkpoint, terminal-screen checkpoint, and complex-screen checkpoint evidence so `GameplayScreen`, `StageResultScreen`, and `SettingsScreen` cannot distort the general migration model
- `GameplayScreen` remains gameplay-root-adjacent and does not acquire gameplay-access shortcuts, pause ownership, or history shortcuts
- `StageResultScreen` remains a runtime-owned terminal special case; its continue action stays intent-only and does not locally decide root replacement policy
- `SettingsScreen` now remains one runtime-managed shell with authored `SettingsAudioSection` and `SettingsDisplaySection` children; audio/display fallback rebuilding is removed while preview/session ownership remains in `SettingsRuntime`
- Settings authored child-view canonicalization and migration helper cleanup are closed here; no UI mixed-mode allowlist remains as runtime or editor code
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
