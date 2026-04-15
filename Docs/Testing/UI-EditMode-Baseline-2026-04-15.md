# UI EditMode Baseline 2026-04-15

## Scope
- Purpose: preserve the Stage 5 HUD freeze while validating the Stage 6 popup stack and Stage 7 screen-flow architecture on the same runner path.
- Command: `./run_tests.sh ui`
- Runner path: governance -> Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` -> Unity `TestRunnerCliBootstrap.RunEditMode -codexSelection ui`

## Result
- Status: green
- Unity UI EditMode: `57 total / 0 failed`
- Result XML: `TestResults/wsl-unity-ui-editmode.xml`
- Unity log: `TestResults/wsl-unity-ui-editmode.log`
- Build log: `TestResults/wsl-dotnet-ui.log`

## Covered Freeze Evidence
- persistent HUD subtree composition
- `HUDRootPresenter` mapped-source subscription lifecycle
- child-view binding integrity
- pause and blocking refresh propagation through mapped presentation state
- action-bar input relay correctness
- architecture guard tests for root growth, root binding, and raw gameplay boundary leakage
- popup stack ownership centralized in flow and `PopupController`
- top-popup-only interaction, dim, and blocking policy centralized outside popup prefabs
- popup-first back handling across tooltip, confirm, reward, and migrated legacy popup paths
- screen-transition cleanup remaining deterministic under the Stage 6 default without expanding Stage 4 mapped contracts
- classification-sensitive popup behavior differences validated on one shared stack
- explicit screen runtime ownership in `ScreenController` with current-screen plus history semantics
- Stage 7 default retention and reuse policies validated without hard-coding them as permanent invariants
- popup-first back order preserved while screens add push, pop, replace, and terminal result behavior
- terminal `StageResultScreen` auto-opened from the existing tick-event seam without widening gameplay contracts
- HUD shell visibility and HUD read-only policy kept on centralized flow/policy seams instead of screen-prefab control

## Companion Smoke Check
- `./run_tests.sh core`: green on the same working tree after the UI run
- Core EditMode: `13 total / 0 failed`
- Core PlayMode: `2 total / 0 failed`
