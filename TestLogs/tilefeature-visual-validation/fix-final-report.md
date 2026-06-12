# TileFeatureVisual Split Follow-up Fix Report

Date: 2026-06-13 KST
HEAD: 16132cd447aac4bd5744746a6ba1ce44561a72b5

## Summary

Fixed the Conditional owner lanes without reopening TileFeatureVisual implementation:

- Gameplay_Vfx runtime policy focused failures are green.
- TileFeatureAudio Exit duplicate binding identity failure is green.
- PresentationCatalog enemy binding focused and broad lanes are green.
- Button targeted VFX/no-profile lanes remain green.
- Button broad still times out in Unity EditMode before XML output; it is recorded as infra timeout, not an assertion failure.

Merge readiness remains Conditional unless broad Button is declared non-gating and GameplayVfx governance/authoring/architecture residual failures are accepted as separate non-runtime-policy work.

## Changes by owner

Gameplay_Vfx:

- Suppressed old fallback playback for migrated cue flag-off cases by treating an empty configured cue map as explicit "no playback".
- Preserved partial cue-map diagnostics for canonical VFX while suppressing optional sibling box destroy cue fallbacks.
- Counted direct runtime playback only when an actual playback or intended diagnostic owner exists.
- Kept box destroy and item consume flags independent.
- Updated migration tests to make flag-off/no-fallback policy explicit with empty cue maps.

TileFeatureAudio:

- Preserved pending audio request order in `TileFeatureAudioPresentationController`.
- Duplicate Exit audio requests now resolve/play the expected exact `AudioDefinition` asset identity in order.

Button test infra:

- Hardened `run_tests.sh` timeout artifact capture.
- Added current-project Unity process guard before Unity stages.
- Added Windows-side current-project Unity process discovery/termination for timeout cleanup.
- Added timeout artifacts for log tail, process snapshots, lock status, XML presence, stage key, and exit code.

PresentationCatalog:

- Fixed the enemy presentation fixture so the bound prefab exposes the expected child `Animator`.
- Kept the fix in presentation/test fixture scope; no TileFeatureVisual migration path was used.

## Test Results

Focused required lanes:

- `./run_tests.sh full --filter GameplayVfxBoxExitRuntimePolicyTests`
  - EditMode 31/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/GameplayVfxBoxExitRuntimePolicyTests.log`
- `./run_tests.sh full --filter GameplayVfxReservedCueRuntimePolicyTests`
  - EditMode 17/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/GameplayVfxReservedCueRuntimePolicyTests.log`
- `./run_tests.sh full --filter TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent`
  - EditMode 1/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/TileFeatureAudioPresentationController_ExitBindings_PlayDuplicatesWhenPresent.log`
- `./run_tests.sh full --filter ButtonProductionVfxLoops_StartStopAndCleanupByCatalogStyle`
  - EditMode 1/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/ButtonProductionVfxLoops_StartStopAndCleanupByCatalogStyle.log`
- `./run_tests.sh full --filter ButtonAndEntranceProductionPrefabs_NoAnimatorNoProfile_AreExplicitVfxOnlyNoOp`
  - EditMode 1/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/ButtonAndEntranceProductionPrefabs_NoAnimatorNoProfile_AreExplicitVfxOnlyNoOp.log`
- `./run_tests.sh full --filter GameplaySceneHost_Initialize_WithEnemyPresentationCatalog_UsesBoundPrefabForConfiguredEnemy`
  - EditMode 1/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/GameplaySceneHost_Initialize_WithEnemyPresentationCatalog_UsesBoundPrefabForConfiguredEnemy.log`

Non-regression lanes:

- `./run_tests.sh full --filter TileFeatureVisual`
  - EditMode 97/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/TileFeatureVisual.rerun.log`
- `./run_tests.sh full --filter TileFeatureVfx`
  - EditMode 6/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/TileFeatureVfx.rerun.log`
- `./run_tests.sh core`
  - EditMode 184/0, PlayMode 34/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-final-validation/core.rerun.log`
- `git diff --check`
  - passed

Broad/optional lanes:

- `./run_tests.sh full --filter GameplayVfx`
  - 779 total / 5 failed
  - Runtime policy failures from the requested focused set are closed.
  - Remaining failures are architecture/governance/authoring category:
    - `GameplayHostAssembly_DoesNotReferenceVfxOrVfxHost`
    - `HostAnchorAssembly_DoesNotReferenceAuthoringOrAuthorityRuntime`
    - `Authoring_ButtonActiveLoopPrefabs_AreLoopingPlayOnAwakeAndNotPrefabLocalOnButtons`
    - `GameplayHost_DoesNotDirectlyDependOnVfxCoreOrAuthoring`
    - `Governance_DocumentsTileFeatureAndGravityFieldNoLegacyFallbackPolicy`
  - Log: `TestLogs/tilefeature-visual-validation/fix-gameplay-vfx/GameplayVfx.broad.final.log`
- `./run_tests.sh full --filter TileFeatureAudioRuntimeTests`
  - EditMode 42/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-audio/TileFeatureAudioRuntimeTests.log`
- `./run_tests.sh full --filter PresentationCatalog`
  - EditMode 115/0, PlayMode 0/0, passed
  - Log: `TestLogs/tilefeature-visual-validation/fix-presentation-catalog/PresentationCatalog.log`
- `./run_tests.sh full --filter Button`
  - Timed out in Unity full EditMode with exit code 124 before result XML.
  - Timeout artifact: `TestResults/timeout-artifacts/full-editmode-20260613-015955`
  - Log: `TestLogs/tilefeature-visual-validation/fix-button-infra/Button.broad.after-windows-cleanup-verified.log`
  - Final manual post-cleanup check: no current-project Windows Unity process, no current-project WSL Unity process, `Temp/UnityLockfile` missing.

## Non-regression Checks

- No `TileFeatureVisualTargetView` changes are present in `git diff --name-only`.
- No `PlayButton`/`PlayDestroy`/`PlaySlide`/`PlayBarricade`/`PlayExit`/`PlayMoon` legacy surface was restored.
- No serialized string, UnityEvent, or ParticleSystem legacy TileFeatureVisual surface was restored.
- Gameplay_Vfx remained a VFX runtime/policy owner, not a gameplay semantic dispatcher.
- Audio fix stayed in TileFeatureAudio runtime presentation code.
- PresentationCatalog fix stayed in presentation fixture/binding scope and did not move gameplay authority.

## Remaining Issues

- Owner: Gameplay_Vfx governance/architecture/authoring
  - Priority: depends on merge gate policy
  - Repro: `./run_tests.sh full --filter GameplayVfx`
  - Gate: Blocked if broad GameplayVfx is required; otherwise separate follow-up.
- Owner: Button test infra / Unity PerformanceTesting prebuild
  - Priority: infra follow-up
  - Repro: `./run_tests.sh full --filter Button`
  - Gate: Conditional if targeted Button lanes are accepted; Blocked if broad Button must pass.

Full project-wide green was not run and is not claimed.
