# UIAudioScene Scene Removal Follow-up Validation

Date: 2026-06-06 KST

## Scope
- Follow-up validation after removing the two legacy gameplay scene assets.
- Stage ids and stage content remain intentionally retained: `mechanics-showcase`, `onboarding`, `stage-0-1`, and `stage-1-1`.
- This note records the validation performed from WSL. GUI-only manual editor smoke was not executed in this non-interactive session.

## Static Validation
- Deleted scene path search: clean.
  - Checked both full legacy scene paths and legacy scene filenames across `Assets`, `Docs`, `ProjectSettings`, and `Packages`.
- Deleted scene GUID search: clean.
  - Checked both removed scene GUIDs across `Assets`, `Docs`, `ProjectSettings`, and `Packages`.
- `ProjectSettings/EditorBuildSettings.asset` contains only:
  - `Assets/Scenes/MainMenuScene.unity`
  - `Assets/Scenes/UIAudioScene.unity`
- `ProjectSettings/ProjectSettings.asset` has `templateDefaultScene: Assets/Scenes/UIAudioScene.unity`.
- `StageEditorDirectPlayCatalog.asset` uses `canonicalShellScenePath: Assets/Scenes/UIAudioScene.unity`.
- `StageEditorDirectPlayCatalog.asset` supported stage ids include:
  - `mechanics-showcase`
  - `onboarding`
  - `stage-0-1`
  - `stage-1-1`

## Manual Smoke Status
- Unity Editor missing script / missing scene reference console check: not run in this non-interactive WSL session.
- Direct-play smoke:
  - `mechanics-showcase`: not run manually; covered by UIAudioScene launch-context PlayMode tests.
  - `onboarding`: not run manually; covered by UIAudioScene launch-context PlayMode and UI contract tests.
  - `stage-0-1`: not run manually; direct-play catalog support verified statically.
  - `stage-1-1`: not run manually; direct-play catalog support verified statically.
- Replay last stage id smoke: not run manually.
- Stage clear/navigation smoke: not run manually.
- UI/audio smoke: not run manually; UIAudioScene UI/audio shell contract validated by UI EditMode lane.
- PlayerProfilerCaptureCli smoke: covered by `StageDefaultStageIdPolicyTests`; capture stage resolves to `UIAudioScene`, and direct shell capture without `--capture-stage` is rejected.

## Automated Validation
- `./run_tests.sh core --filter StageDefaultStageIdPolicyTests`: passed.
  - `core-editmode`: `160 total / 0 failed`
  - `core-playmode`: `32 total / 0 failed`
- `./run_tests.sh core --filter StageSceneBootstrapValidatorTests`: passed.
  - `core-editmode`: `160 total / 0 failed`
  - `core-playmode`: `32 total / 0 failed`
- `./run_tests.sh core --filter ActualSceneBootstrapSmokePlayModeTests`: passed.
  - `core-editmode`: `160 total / 0 failed`
  - `core-playmode`: `32 total / 0 failed`
- `./run_tests.sh core --filter GameplayVfxSceneRuntimeRootPlayModeTests`: passed.
  - `core-editmode`: `160 total / 0 failed`
  - `core-playmode`: `32 total / 0 failed`
- `./run_tests.sh ui --filter pre-cleanup gameplay shell UI/audio contract test`: passed.
  - `ui-editmode`: `651 total / 0 failed`

## Full Lane
- `./run_tests.sh full` was not run.
- full lane은 실행하지 않았습니다. 따라서 project-wide green으로 주장하지 않습니다.
- 검증 범위는 static search, project settings inspection, core/ui runner evidence, and non-GUI automated smoke 기준입니다.
