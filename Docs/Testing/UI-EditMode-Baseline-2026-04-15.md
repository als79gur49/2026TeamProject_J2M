# UI EditMode Baseline 2026-04-15

## Scope
- Purpose: Stage 5 HUD freeze evidence before Stage 6+ HUD expansion.
- Command: `./run_tests.sh ui`
- Runner path: governance -> Windows `dotnet build Game.Feature.UI.Tests.csproj -c Debug` -> Unity `TestRunnerCliBootstrap.RunEditMode -codexSelection ui`

## Result
- Status: green
- Unity UI EditMode: `46 total / 0 failed`
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

## Companion Smoke Check
- `./run_tests.sh core`: green on the same working tree after the UI run
- Core EditMode: `13 total / 0 failed`
- Core PlayMode: `2 total / 0 failed`
