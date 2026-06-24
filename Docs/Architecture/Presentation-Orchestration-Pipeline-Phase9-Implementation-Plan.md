# Presentation Orchestration Pipeline Phase 9 Lane Integration Plan

## Summary

Phase 9 completion is scoped to Presentation Orchestration lane integration only. The closeout gate is the lane-integration matrix below, not repository-wide full-lane recovery.

Do not use the following as required evidence or blockers for this phase:

- `./run_tests.sh full`
- unfiltered full EditMode
- unfiltered full PlayMode
- full-lane failure provenance
- BASE/TIP/CURRENT worktree comparison
- existing full-lane unrelated failures
- repository-wide UI, Stage, simulation, reservation, occupancy, traversal, or Enemy AI suite recovery

Existing full-lane failures are out of scope unless a selected Phase 9 lane-integration filter fails on the current worktree.

## Lane Scope

The remaining implementation and validation scope is limited to coordinator route extraction and related Presentation Orchestration integration defects.

Production owners:

- Core gameplay SFX: orchestration SFX bridge executor
- Damage/death VFX: orchestration VFX executor
- Box motion: orchestration motion executor
- Player action animation: orchestration animation executor
- Enemy presentation: orchestration enemy presentation executor
- Action audio: orchestration action audio bridge
- Enemy audio: one-shot orchestration enemy audio bridge; `ChargeActiveLoop` remains owned by the legacy loop controller
- Topology: executor bridge for production topology playback; topology transition controller keeps input-lock and visual lifecycle ownership

Fallback and rollback:

- Invalid or unset execution modes normalize to the matching legacy owner.
- Explicit rollback modes remain available for every Phase 9 domain.
- Legacy suppression, duplicate prevention, missing-dependency diagnostics, reset, hard cleanup, and ignored-result accounting remain domain-owned.

## Required Validation

Run every command from the current worktree and run Unity commands sequentially.

```bash
git diff --check
"/mnt/c/Program Files/dotnet/dotnet.exe" build Game.Feature.Gameplay.Host.csproj -c Debug
"/mnt/c/Program Files/dotnet/dotnet.exe" build Game.Feature.Gameplay.Tests.csproj -c Debug
"/mnt/c/Program Files/dotnet/dotnet.exe" build Game.Feature.Gameplay.PlayModeTests.csproj -c Debug
```

Use direct Unity bootstrap commands for targeted lane filters. Do not use a combined command with a zero-match lane as final evidence.

EditMode command shape:

```bash
timeout --kill-after=10 300 /mnt/c/Users/user/Desktop/6000.3.11f1/Editor/Unity.exe \
  -batchmode -nographics \
  -projectPath "$(wslpath -w "$PWD")" \
  -logFile "$(wslpath -w "$LOG_PATH")" \
  -executeMethod TestRunnerCliBootstrap.RunEditMode \
  -codexSelection full \
  -codexResultPath "$(wslpath -w "$XML_PATH")" \
  -codexTestFilter "<single EditMode filter>"
```

PlayMode uses the same command shape with `TestRunnerCliBootstrap.RunPlayMode`.

Required EditMode filters:

- `PresentationOrchestrationProductionSwitchGovernanceTests`
- `GameplayTickPresentationCoordinatorTests`
- `GameplayPresentationOrchestrationArchitectureTests`
- `TopologyPresentationExecutorTests`
- `GameplayPlayerActionAnimationOrchestrationTests`
- `GameplayEnemyPresentationOrchestrationTests`
- `GameplayActionAudioRuntimeTests`
- `EnemyAudioRuntimeTests`
- `GameplayBoxMotionOrchestrationTests`

Required PlayMode filters:

- `PlayerActionAnimationReadinessPlayModeTests`
- `EnemyPresentationReadinessPlayModeTests`
- `GameplayAudioIntegrationPlayModeTests`
- `ActualSceneBootstrapSmokePlayModeTests`
- `PlayerMovementPlayModeTests`
- `DamageDeathVfxProductionDefaultPlayModeTests`
- `BoxMotionReadinessPlayModeTests`

## Completion Gate

`LANE-INTEGRATION-PASS` requires:

- `git diff --check` passes.
- All three project builds pass.
- Every selected EditMode and PlayMode filter has `matched > 0` and `failed = 0`.
- Production default, invalid/unset fallback, and explicit rollback checks pass.
- Player Push/Flip semantic telemetry passes for Windup, Execute, Recovery, Blocked, ImpactContact, and Failed.
- PlannedCount, ObservedCount, RequestedCount, AppliedCount, ignored result separation, Execute-to-Recovery lowering, and Recovery counter isolation pass.
- Enemy presentation, action audio, enemy audio, topology actual-runtime integration, and coordinator architecture/routing pass.
- Core SFX, Damage/death VFX, and Box motion targeted production regressions pass.

`FAIL-CONTINUE` applies when any lane-integration compile failure, zero-match, test failure, owner mismatch, duplicate playback, cleanup failure, fallback failure, or rollback failure remains.

## Final Report Requirements

Final evidence must include:

- `HEAD` and tracked diff hash
- changed files
- lane-by-lane production owner
- fallback and rollback owner
- exact test command
- matched, passed, failed, skipped
- exit code
- log and XML path
- lane-integration completion status
- remaining coordinator responsibilities

Allowed closeout wording: `Phase 9 Presentation Orchestration Lane Integration PASS`.

Do not claim `project-wide green`, `full regression green`, `unfiltered full green`, or any equivalent broad regression closure.
