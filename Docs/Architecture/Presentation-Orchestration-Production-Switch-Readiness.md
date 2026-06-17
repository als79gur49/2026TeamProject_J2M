# Presentation Orchestration Production Switch Readiness

## Overview

Phase 9A switches exactly one production default: Core gameplay SFX now uses explicit orchestration execution ownership by default. The readiness matrix continues to define the criteria for moving one presentation domain at a time from legacy execution ownership to explicit orchestration execution ownership.

The current production policy remains:

- Core gameplay SFX default execution mode is `OrchestrationSfxBridgeExecutor`
- every other known presentation orchestration domain default execution mode is still its legacy owner
- non-Core-SFX orchestration execution is explicit configuration only
- duplicate guards remain enabled
- diagnostics remain enabled
- legacy rollback paths remain available

## Current default policy

All known presentation orchestration domains except Core gameplay SFX must keep legacy as the current default until a dedicated production-switch PR changes exactly one additional domain. Invalid or unset execution mode values must normalize back to the legacy owner, including Core gameplay SFX, so rollback remains safe when stale or corrupt config is encountered.

Production scenes, stage content, and host authoring configuration must not serialize orchestration execution modes as default values except for the Core gameplay SFX production default token. Explicit orchestration mode remains allowed in unit tests, targeted fixtures, and controlled integration tests.

## Domain readiness matrix

| Domain | LegacyOwner | OrchestrationOwner | CurrentDefault | ControlledMode | DefaultIsLegacy | InvalidModeNormalizesToLegacy | DuplicateGuardEvidence | DeterminismEvidence | LifecycleCleanupEvidence | BoundaryEvidence | InputLockRisk | AudioUiRisk | RollbackPath | RecommendedStatus |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Topology transition | LegacyCoordinator | ExecutorBridge | LegacyCoordinator | ExecutorBridge | Yes | Yes | `TopologyExecution_ExecutorBridgeMode_UsesExecutorPortOnceAndSkipsLegacyDirectPath` | `TopologyExecutor_DoesNotMutateAuthoritativeTickResult` | `TopologyExecution_ExecutorBridgeMode_CleanupResetsPortAndDiagnostics` | `TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator` | High, input lock observes coordinator presentation phase | Low | Set `TopologyPresentationExecutionMode.LegacyCoordinator` | KeepLegacy |
| Damage/death VFX | LegacyExtension | OrchestrationExecutor | LegacyExtension | OrchestrationExecutor | Yes | Yes | `DamageDeathVfx_OrchestrationMode_RoutesDamageAndDeathRequests` | `VfxPlanning_DoesNotMutateAuthoritativeTickResult` | `DamageDeathVfx_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `VfxPlanningBoundary_StaysPresentationOnly` | Low | Low | Set `DamageDeathVfxExecutionMode.LegacyExtension` | NeedsMoreCoverage |
| Box motion | LegacyTrackPlanner | OrchestrationMotionExecutor | LegacyTrackPlanner | OrchestrationMotionExecutor | Yes | Yes | `BoxMotion_OrchestrationMotionExecutorMode_RoutesSlideFlipAndImpactRequests` | `BoxMotionPlanning_DoesNotMutateAuthoritativeTickResult` | `BoxMotion_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `BoxMotionExecutionSwitch_DoesNotLeakIntoInputOrVfxContracts` | Medium, motion can affect perceived input timing | Low | Set `BoxMotionPresentationExecutionMode.LegacyTrackPlanner` | NeedsMoreCoverage |
| Player action animation | LegacyAnimationSync | OrchestrationAnimationExecutor | LegacyAnimationSync | OrchestrationAnimationExecutor | Yes | Yes | `PlayerActionAnimation_OrchestrationMode_RoutesPushFlipAndFakeAttempts` | `PlayerActionAnimationPlanning_DoesNotMutateAuthoritativeTickResult` | `PlayerActionAnimation_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `PlayerActionAnimationBoundary_RemainsHostOnly` | Medium, action holds can affect input feel | Low | Set `PlayerActionAnimationExecutionMode.LegacyAnimationSync` | KeepLegacy |
| Enemy presentation | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | Yes | Yes | `EnemyPresentation_OrchestrationMode_RoutesJumpChargeAndDeathRequests` | `EnemyPresentationPlanning_DoesNotMutateAuthoritativeTickResult` | `EnemyPresentation_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `EnemyPresentationPlanningBoundary_StaysPresentationOnly` | Medium | Low | Set `EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper` | KeepLegacy |
| Core gameplay SFX | LegacyGameplayAudioController | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | No | Yes | `CoreSfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback` | `Determinism_NonContamination_AfterCoreSfxDefaultSwitch` | `CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState` | `AudioOwnership_RemainsSeparatedAfterCoreSfxSwitch` | Low, non-blocking one-shot | Medium, audio ownership must stay separated | Set `CoreGameplaySfxExecutionMode.LegacyGameplayAudioController` | ProductionDefaultOn |
| Action audio | LegacyActionAudioController | OrchestrationActionAudioBridge | LegacyActionAudioController | OrchestrationActionAudioBridge | Yes | Yes | `ActionAudio_OrchestrationMode_RoutesProfileMoments` | `ActionAudioPlanning_DoesNotMutateAuthoritativeTickResult` | `ActionAudio_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `ActionAudioPlanningBoundary_StaysActionAudioOwned` | Low | Medium, profile/authoring edge cases remain | Set `ActionAudioExecutionMode.LegacyActionAudioController` | KeepLegacy |
| Enemy audio | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | Yes | Yes | `EnemyAudio_OrchestrationMode_RoutesEnemyMoments` | `EnemyAudioPlanning_DoesNotMutateAuthoritativeTickResult` | `EnemyAudio_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership` | Low | Medium, latest audio integration | Set `EnemyAudioExecutionMode.LegacyEnemyAudioController` | KeepLegacy |

## Production switch candidate recommendation

There is no next `CandidateForNextPR` in this matrix after the Phase 9A switch. The next PR should harden telemetry and validation for Core gameplay SFX before another domain becomes a production default candidate.

Core gameplay SFX was switched first because:

- non-blocking one-shot playback
- small closed semantic set
- legacy/orchestration equivalence coverage exists
- attached and 2D fallback parity exists
- duplicate guard coverage exists
- audio ownership tests already separate core SFX, action audio, enemy audio, BGM, and UI audio
- rollback is a single execution mode switch back to `LegacyGameplayAudioController`

No other domain is a Phase 9A production default candidate.

## Required validation lanes by candidate

Core gameplay SFX production switch validation must include:

- `GameplayAudioHostOrchestrationTests`
- `GameplayPresentationOrchestrationArchitectureTests`
- `PresentationOrchestrationProductionSwitchGovernanceTests`
- `GameplayTickPresentationCoordinatorTests`
- `AudioArchitectureTests`
- `GameplayActionAudioRuntimeTests`
- `EnemyAudioRuntimeTests`
- `BgmFlowArchitectureTests`
- `UiAudioSfxContractTests`
- `SettingsAudioRuntimeContractTests`
- `./run_tests.sh core`

Optional escalation includes targeted audio, VFX, and PlayMode tests when the touched diff changes runtime playback behavior.

If the full lane is not executed and passing on the same revision, do not claim full-lane green, project-wide green, full regression closure, or all regressions fixed. A no-test-match result is not validation evidence. `obj` or `dll` file locks are runner/build concurrency issues and must be reported separately from test failures.

## Rollback strategy

Rollback remains configuration-first:

1. Set `CoreGameplaySfxExecutionMode` back to `LegacyGameplayAudioController`.
2. Keep duplicate guards and diagnostics enabled.
3. Remove the Core gameplay SFX orchestration default allowance from the production config drift guard.
4. Set the readiness matrix status back to `CandidateForNextPR`.
5. Restore default orchestration tests to their legacy default expectations.
6. Leave controlled integration code in place unless a domain-specific regression requires a separate rollback.

## Explicit non-goals

- Do not switch any additional production default in this PR.
- Do not remove legacy paths or coordinator direct-call paths.
- Do not promote scheduler blocking state to input lock ownership.
- Do not let `GameplayInputHost` read scheduler, pipeline, or plan internals.
- Do not let UI consume raw cue, plan, scheduler, executor diagnostics, or raw presentation frames.
- Do not introduce direct `AudioManager` dependencies into presentation contracts, planning, playback, or host executors.
- Do not merge core SFX, action audio, enemy audio, BGM, or UI audio ownership.
- Do not absorb BGM or UI audio into gameplay presentation orchestration.
- Do not touch `WorldState`, `TickPipeline`, entity logic, or `FinalizationBatch`.
- Do not add scene-global lookup, singleton search, or lazy runtime root creation.

## Full-lane evidence policy

Phase 9 evidence is bounded to the lanes that actually run on the current revision. Report targeted governance results separately from the documented red full-lane baseline. Do not combine older artifacts with current targeted runs to claim broad closure.
