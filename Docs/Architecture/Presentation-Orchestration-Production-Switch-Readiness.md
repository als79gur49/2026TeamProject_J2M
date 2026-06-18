# Presentation Orchestration Production Switch Readiness

## Overview

Phase 9J hardens the Box motion production telemetry after the Phase 9H production default switch to `OrchestrationMotionExecutor`. Box slide, box flip, and box flip impact continue to use the orchestration motion executor by default, while explicit `LegacyTrackPlanner` remains the rollback mode. Damage/death VFX still uses explicit orchestration execution ownership by default, and Core gameplay SFX keeps the Phase 9D production default, hardened telemetry, and PlayMode smoke coverage status. The readiness matrix continues to define the criteria for moving one presentation domain at a time from legacy execution ownership to explicit orchestration execution ownership.

The current production policy remains:

- Core gameplay SFX default execution mode is `OrchestrationSfxBridgeExecutor`
- Damage/death VFX default execution mode is `OrchestrationExecutor`
- Box motion default execution mode is `OrchestrationMotionExecutor`
- every other known presentation orchestration domain default execution mode is still its legacy owner
- non-Core-SFX, non-Damage/death-VFX, and non-Box-motion orchestration execution is explicit configuration only
- duplicate guards remain enabled
- diagnostics are hardened for Core gameplay SFX default owner, request, fallback, deferred, suppression, and duplicate review
- Damage/death VFX now has hardened production telemetry and PlayMode smoke for default owner, legacy skip, semantic damage/death planning and playback, duplicate suppression, same-tick death suppression, missing diagnostics, rollback, non-authoritative behavior, and lifecycle cleanup review
- Box motion now has Phase 9J hardened production telemetry for default owner metadata, legacy source suppression, semantic slide/flip/flip-impact lifecycle, active/pending/completed state, duplicate and missing diagnostics, visualRoot/flip-driver reset, lifecycle cleanup, explicit legacy rollback, non-blocking/input-lock neutrality, and determinism neutrality
- PlayMode smoke now covers the actual host/audio lifecycle for Core gameplay SFX default ownership, fallback, topology deferral, suppression, rollback, and non-authoritative behavior
- legacy rollback paths remain available

## Current default policy

All known presentation orchestration domains except Core gameplay SFX, Damage/death VFX, and Box motion must keep legacy as the current default until a dedicated production-switch PR changes exactly one additional domain. Invalid or unset execution mode values must normalize back to the legacy owner, including Core gameplay SFX, Damage/death VFX, and Box motion, so rollback remains safe when stale or corrupt config is encountered.

Production scenes, stage content, and host authoring configuration must not serialize orchestration execution modes as default values except for the Core gameplay SFX, Damage/death VFX, and Box motion production default tokens. Explicit orchestration mode remains allowed in unit tests, targeted fixtures, and controlled integration tests.

## Domain readiness matrix

| Domain | LegacyOwner | OrchestrationOwner | CurrentDefault | ControlledMode | DefaultIsLegacy | InvalidModeNormalizesToLegacy | DuplicateGuardEvidence | DeterminismEvidence | LifecycleCleanupEvidence | BoundaryEvidence | InputLockRisk | AudioUiRisk | RollbackPath | RecommendedStatus |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Topology transition | LegacyCoordinator | ExecutorBridge | LegacyCoordinator | ExecutorBridge | Yes | Yes | `TopologyExecution_ExecutorBridgeMode_UsesExecutorPortOnceAndSkipsLegacyDirectPath` | `TopologyExecutor_DoesNotMutateAuthoritativeTickResult` | `TopologyExecution_ExecutorBridgeMode_CleanupResetsPortAndDiagnostics` | `TopologyAndInputLockExistingPath_RemainsOwnedByCoordinator` | High, input lock observes coordinator presentation phase | Low | Set `TopologyPresentationExecutionMode.LegacyCoordinator` | KeepLegacy |
| Damage/death VFX | LegacyExtension | OrchestrationExecutor | OrchestrationExecutor | OrchestrationExecutor | No | Yes | `DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback` | `DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative` | `DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics` | `VfxPlanningBoundary_StaysPresentationOnly` | Low | Low | Set `DamageDeathVfxExecutionMode.LegacyExtension` | ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered |
| Box motion | LegacyTrackPlanner | OrchestrationMotionExecutor | OrchestrationMotionExecutor | OrchestrationMotionExecutor | No | Yes | `BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced` | `BoxMotionProductionDefault_PlayMode_IsNonAuthoritative` | `BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose` | `ArchitectureBoundary_AfterBoxMotionReadiness_RemainsSeparated` | Medium, motion can affect perceived input timing | Low | Set `BoxMotionPresentationExecutionMode.LegacyTrackPlanner` | ProductionDefaultOnTelemetryHardened |
| Player action animation | LegacyAnimationSync | OrchestrationAnimationExecutor | LegacyAnimationSync | OrchestrationAnimationExecutor | Yes | Yes | `PlayerActionAnimation_OrchestrationMode_RoutesPushFlipAndFakeAttempts` | `PlayerActionAnimationPlanning_DoesNotMutateAuthoritativeTickResult` | `PlayerActionAnimation_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `PlayerActionAnimationBoundary_RemainsHostOnly` | Medium, action holds can affect input feel | Low | Set `PlayerActionAnimationExecutionMode.LegacyAnimationSync` | KeepLegacy |
| Enemy presentation | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | LegacyEnemyPresentationMapper | OrchestrationEnemyPresentationExecutor | Yes | Yes | `EnemyPresentation_OrchestrationMode_RoutesJumpChargeAndDeathRequests` | `EnemyPresentationPlanning_DoesNotMutateAuthoritativeTickResult` | `EnemyPresentation_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `EnemyPresentationPlanningBoundary_StaysPresentationOnly` | Medium | Low | Set `EnemyPresentationExecutionMode.LegacyEnemyPresentationMapper` | KeepLegacy |
| Core gameplay SFX | LegacyGameplayAudioController | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | OrchestrationSfxBridgeExecutor | No | Yes | `CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback` | `CoreSfx_PlayModeSmoke_IsNonAuthoritative` | `CoreGameplaySfx_ResetSessionHardCleanupAndPresentInitial_ClearExecutorPortAndGuardState` | `AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated` | Low, non-blocking one-shot | Medium, audio ownership must stay separated | Set `CoreGameplaySfxExecutionMode.LegacyGameplayAudioController` | ProductionDefaultOnTelemetryHardenedAndPlayModeSmokeCovered |
| Action audio | LegacyActionAudioController | OrchestrationActionAudioBridge | LegacyActionAudioController | OrchestrationActionAudioBridge | Yes | Yes | `ActionAudio_OrchestrationMode_RoutesProfileMoments` | `ActionAudioPlanning_DoesNotMutateAuthoritativeTickResult` | `ActionAudio_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `ActionAudioPlanningBoundary_StaysActionAudioOwned` | Low | Medium, profile/authoring edge cases remain | Set `ActionAudioExecutionMode.LegacyActionAudioController` | KeepLegacy |
| Enemy audio | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | LegacyEnemyAudioController | OrchestrationEnemyAudioBridge | Yes | Yes | `EnemyAudio_OrchestrationMode_RoutesEnemyMoments` | `EnemyAudioPlanning_DoesNotMutateAuthoritativeTickResult` | `EnemyAudio_OrchestrationMode_CleanupResetsPortAndDiagnostics` | `EnemyAudioPlanningBoundary_StaysPlanningOnlyAndDoesNotAbsorbPlaybackOwnership` | Low | Medium, latest audio integration | Set `EnemyAudioExecutionMode.LegacyEnemyAudioController` | KeepLegacy |

## Production switch candidate recommendation

Box motion is `ProductionDefaultOnTelemetryHardened` after Phase 9J. The current production default is `OrchestrationMotionExecutor`, invalid/unset values still normalize to `LegacyTrackPlanner`, and explicit `LegacyTrackPlanner` rollback remains required. There is no `CandidateForNextPR` domain immediately after this telemetry hardening. The next recommended PR is Phase 9K Player Action Animation Production Readiness Assessment; it should evaluate readiness without switching the player action animation default.

Core gameplay SFX remains switched because:

- non-blocking one-shot playback
- small closed semantic set
- legacy/orchestration equivalence coverage exists
- attached and 2D fallback parity exists
- duplicate guard coverage exists
- audio ownership tests already separate core SFX, action audio, enemy audio, BGM, and UI audio
- rollback is a single execution mode switch back to `LegacyGameplayAudioController`
- topology-lock deferral and unlock drain parity are now covered by telemetry tests
- enemy death profile suppression and lethal enemy damage suppression parity are now covered by telemetry tests
- actual host/audio lifecycle smoke covers production default owner telemetry, no duplicate playback, attached-owner playback, missing-owner 2D fallback, topology lock defer/drain, enemy death/lethal damage suppression, explicit legacy rollback, and non-authoritative tick result preservation

Damage/death VFX is now switched because:

- non-blocking one-shot playback
- small closed semantic set: `EnemyDamage -> DamageHit` and `EnemyDeath` / `Killed` exit -> `EnemyDeath`
- legacy/orchestration duplicate guard coverage exists
- same-tick death suppresses damage hit coverage exists
- missing target, anchor, binding, and port diagnostics remain no-op diagnostic paths
- lifecycle cleanup resets guard, executor diagnostics, and controlled port state
- rollback is a single execution mode switch back to `LegacyExtension`
- the legacy `GameplayVfxProductionRuntime` path remains available and suppresses only `EnemyVfxCue.Damage` and `EnemyVfxCue.Death` when orchestration owns Damage/death VFX
- actual host lifecycle PlayMode smoke now covers default owner telemetry, damage/death request routing, same-tick death suppression, explicit legacy rollback, missing port/binding diagnostics, lifecycle cleanup, non-authoritative behavior, and Core SFX/audio boundary stability

Phase 9H switches only Box motion. Player action animation, enemy presentation, action audio, enemy audio, and topology transition remain on their legacy production defaults.

## Phase 9J Box motion production telemetry hardening

Phase 9J keeps the Phase 9H production switch in place and adds reviewable host/runtime diagnostics. The telemetry snapshot remains outside `WorldState`, `TickResult`, save/replay state, and determinism hashing. It records only values such as modes, enum semantics, entity ids, cue keys, dedupe keys, tick indexes, counts, and cleanup reasons.

Phase 9J Box motion telemetry evidence includes:

- `BoxMotion_DefaultOrchestration_TelemetryCoversSlideFlipImpact`
- `BoxMotionProductionDefault_PlayMode_RoutesSlideFlipImpact`
- `BoxMotionProductionDefault_PlayMode_ConcreteAdapterStartsAndCompletesTracks`
- `BoxMotionProductionDefault_PlayMode_FlipPoseAndVisualRootReset`
- `BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced`
- `BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp`
- `BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose`
- `BoxMotionReadiness_PlayMode_ExplicitLegacyRollbackRemains`
- `ProductionSwitchReadiness_ReflectsBoxMotionTelemetryHardening`

Phase 9J does not nominate an immediate next production switch candidate. Player action animation and enemy presentation must remain legacy defaults until a separate readiness pass evaluates lifecycle, rollback, input feel, and boundary evidence.

## Phase 9H Box motion production default switch

Phase 9H uses the Phase 9I hybrid PlayMode evidence as production-default coverage. Synthetic host smoke runs through a real `GameplaySceneHost`, `GameplayTickViewPresenter`, and `GameplayTickPresentationCoordinator` lifecycle while feeding deterministic presentation ticks for Box motion scenarios. The fixture covers both recording-port routing and the concrete `GameplayMotionTrackPlannerPlaybackPort -> GameplayTrackPlanner -> PresentationMotionTrack -> GameplayEntityPresentationApplier / BoxFlipInteractionDriver` adapter path. Actual scene bootstrap smoke verifies the Box motion production default is `OrchestrationMotionExecutor` and unapproved domains remain legacy.

Phase 9H Box motion PlayMode scenarios:

- `BoxMotionProductionDefault_PlayMode_UsesOrchestrationOwner`
- `BoxMotionProductionDefault_PlayMode_RoutesSlideFlipImpact`
- `BoxMotionProductionDefault_PlayMode_ConcreteAdapterStartsAndCompletesTracks`
- `BoxMotionProductionDefault_PlayMode_SlidePoseRemainsEquivalent`
- `BoxMotionProductionDefault_PlayMode_FlipPoseAndVisualRootReset`
- `BoxMotionProductionDefault_PlayMode_FlipImpactRemainsSeparateSemantic`
- `BoxMotionProductionDefault_PlayMode_DuplicateGuardNormalAndForced`
- `BoxMotionProductionDefault_PlayMode_MissingDiagnosticsAreNoOp`
- `BoxMotionProductionDefault_PlayMode_LifecycleCleanupClearsTrackAndPose`
- `BoxMotionReadiness_PlayMode_ExplicitLegacyRollbackRemains`
- `BoxMotionProductionDefault_PlayMode_IsNonBlockingAndInputLockNeutral`
- `BoxMotionProductionDefault_PlayMode_IsNonAuthoritative`
- `CoreSfxAndDamageDeathVfx_RemainStableAfterBoxMotionSwitch`

## Phase 9G Box motion readiness hardening

Phase 9G uses EditMode host/presenter evidence plus existing targeted PlayMode movement smoke. The production default remains `LegacyTrackPlanner`; `OrchestrationMotionExecutor` remains explicit controlled mode only.

Box motion production switch risks covered in Phase 9G:

- pose drift between legacy tracks and orchestration playback
- visualRoot offset and rotation cleanup after flip interaction
- track lifecycle cleanup across completion, `ResetSession`, `HardCleanup`, and `PresentInitial`
- duplicate owner attempts for the same tick/source/entity/cue/source-destination/action key
- non-blocking, scheduler blocking, topology active, and input lock regression

Phase 9G Box motion scenarios:

- `BoxMotion_DefaultMode_IsOrchestrationMotionExecutor`
- `BoxMotion_DefaultOrchestration_RoutesSlideFlipImpact`
- `BoxMotion_Readiness_LegacyAndOrchestrationSemanticEquivalence`
- `BoxMotion_Readiness_PoseEquivalenceAndVisualRootReset`
- `BoxMotion_Readiness_DuplicateGuardNormalAndForced`
- `BoxMotion_Readiness_MissingDiagnosticsSeparated`
- `BoxMotion_Readiness_LifecycleCleanupClearsState`
- `BoxMotion_Readiness_IsNonBlockingAndInputLockNeutral`
- `BoxMotion_Readiness_IsDeterminismNeutral`
- `ProductionSwitchReadiness_ReflectsBoxMotionPlayModeEvidence`
- `CoreSfxAndDamageDeathVfx_ProductionDefaultsRemainStableAfterBoxMotionSwitch`
- `ArchitectureBoundary_AfterBoxMotionReadiness_RemainsSeparated`

## Phase 9D PlayMode smoke coverage

Phase 9D uses Hybrid PlayMode evidence. The synthetic host/audio smoke runs through a real `GameplaySceneHost`, `GameplayTickViewPresenter`, `GameplayTickPresentationCoordinator`, `AudioRuntimeInstaller`, and `AudioManager` lifecycle while feeding deterministic presentation ticks for Core gameplay SFX scenarios. This avoids depending on brittle stage content for damage/death setup while still validating the production runtime path.

The actual `UIAudioScene` bootstrap smoke remains intentionally lightweight. It verifies the scene-host lifecycle boots with `CoreGameplaySfxExecutionMode.OrchestrationSfxBridgeExecutor`, keeps action audio and enemy audio on legacy owners, and preserves existing audio/BGM/UI bootstrap checks. It does not synthesize scene-specific combat or death content from the scene asset.

Phase 9D PlayMode smoke scenarios:

- `CoreSfxProductionDefault_PlayMode_UsesOrchestrationOwner`
- `CoreSfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback`
- `CoreSfxProductionDefault_PlayMode_AttachedAndFallbackSmoke`
- `CoreSfxProductionDefault_PlayMode_TopologyLockDefersAndDrains`
- `CoreSfxProductionDefault_PlayMode_EnemyDeathSuppressionSmoke`
- `CoreSfx_ExplicitLegacyRollback_RemainsAvailableAfterPlayModeSmoke`
- `CoreSfx_PlayModeSmoke_IsNonAuthoritative`
- `AudioOwnership_AfterCoreSfxPlayModeSmoke_RemainsSeparated`

## Phase 9E Damage/death VFX production telemetry coverage

Phase 9E uses EditMode production-default and migration coverage for the Damage/death VFX switch telemetry. The switch remains Phase 9B behavior; Phase 9E validates production owner telemetry, semantic damage/death counters, explicit legacy rollback, same-tick death suppression, duplicate blocking, missing diagnostics, lifecycle cleanup, non-blocking behavior, determinism neutrality, and legacy suppress policy.

Phase 9E Damage/death VFX scenarios:

- `DamageDeathVfx_DefaultOrchestration_TelemetryReportsProductionOwner`
- `DamageDeathVfx_ExplicitLegacyRollback_TelemetryConfirmsNoExecutorPlayback`
- `DamageDeathVfx_DefaultOrchestration_TelemetryCoversDamageAndDeath`
- `DamageDeathVfx_SameTickDeathSuppression_TelemetryIsRecorded`
- `DamageDeathVfx_DefaultOrchestration_DoesNotDuplicateLegacyPlayback`
- `DamageDeathVfx_ForcedDuplicateStillBlocksSecondOwner`
- `DamageDeathVfx_LegacySuppressPolicy_TelemetryCoversOnlyDamageDeath`
- `DamageDeathVfx_MissingDiagnostics_TelemetryRemainsSeparated`
- `DamageDeathVfx_LifecycleCleanup_TelemetryClearsState`
- `DamageDeathVfx_ProductionTelemetry_IsNonAuthoritative`

## Phase 9F Damage/death VFX PlayMode smoke coverage

Phase 9F uses Hybrid PlayMode evidence. Synthetic host smoke runs through a real `GameplaySceneHost`, `GameplayTickViewPresenter`, and `GameplayTickPresentationCoordinator` lifecycle while feeding deterministic presentation ticks for Damage/death VFX scenarios. This avoids brittle combat-scene authoring while validating the production default owner path. The actual `UIAudioScene` bootstrap smoke verifies that the scene-host lifecycle boots with `DamageDeathVfxExecutionMode.OrchestrationExecutor`, keeps `GameplayVfxProductionRuntime` present, keeps Core SFX production ownership, and keeps action/enemy audio on legacy owners.

Phase 9F Damage/death VFX PlayMode smoke scenarios:

- `DamageDeathVfxProductionDefault_PlayMode_UsesOrchestrationOwner`
- `DamageDeathVfxProductionDefault_PlayMode_TelemetryHasNoDuplicatePlayback`
- `DamageDeathVfxProductionDefault_PlayMode_RoutesDamageAndDeath`
- `DamageDeathVfxProductionDefault_PlayMode_SameTickDeathSuppressesDamage`
- `DamageDeathVfxProductionDefault_PlayMode_LegacySuppressPolicyOnlyDamageDeath`
- `DamageDeathVfx_ExplicitLegacyRollback_RemainsAvailableAfterPlayModeSmoke`
- `DamageDeathVfx_PlayModeSmoke_IsNonAuthoritative`
- `DamageDeathVfx_PlayModeSmoke_KeepsCoreSfxAndAudioBoundaries`
- `DamageDeathVfx_PlayModeSmoke_ReportsMissingPortAndBindingWithoutThrowing`
- `DamageDeathVfx_PlayModeSmoke_LifecycleCleanupClearsGuardAndDiagnostics`

## Required validation lanes by candidate

Core gameplay SFX and Damage/death VFX production switch validation must include:

- `GameplayAudioHostOrchestrationTests`
- `GameplayPresentationOrchestrationArchitectureTests`
- `PresentationOrchestrationProductionSwitchGovernanceTests`
- `GameplayTickPresentationCoordinatorTests`
- `GameplayVfxEnemyDamageMigrationTests`
- `GameplayVfxEnemyDeathMigrationTests`
- `AudioArchitectureTests`
- `GameplayActionAudioRuntimeTests`
- `EnemyAudioRuntimeTests`
- `BgmFlowArchitectureTests`
- `UiAudioSfxContractTests`
- `SettingsAudioRuntimeContractTests`
- `./run_tests.sh core`

Optional escalation includes targeted audio, VFX, and PlayMode tests when the touched diff changes runtime playback behavior.

Phase 9D Core gameplay SFX validation should include the targeted PlayMode smoke filters listed above in addition to the existing governance, orchestration, architecture, UI audio, and core lanes. Phase 9F Damage/death VFX validation should include the targeted PlayMode smoke filters listed above, the existing Phase 9E coordinator telemetry filters, architecture, governance, and VFX migration filters, plus Core SFX/audio regression guards.

Phase 9G Box motion validation should include:

- `GameplayBoxMotionOrchestrationTests`
- `GameplayTickPresentationCoordinatorTests`
- `GameplayPresentationOrchestrationArchitectureTests`
- `PresentationOrchestrationProductionSwitchGovernanceTests`
- `GameplayInputHost_BoxSlidePresentation_DoesNotBlockSimulationTicks`
- `GameplayInputHost_FlipPresentation_DoesNotBlockSubsequentTicks`
- `./run_tests.sh core`

Phase 9H Box motion validation should include the targeted PlayMode smoke filters listed above, actual scene bootstrap default checks, the Phase 9G EditMode readiness/governance filters updated for production default ownership, the two existing non-blocking PlayMode filters, and Core SFX / Damage-death VFX regression filters. If these do not run or do not pass on the same revision, revert Box motion to `CandidateForNextPR` and do not claim the production switch is validated.

If the full lane is not executed and passing on the same revision, do not claim full-lane green, project-wide green, full regression closure, or all regressions fixed. A no-test-match result is not validation evidence. `obj` or `dll` file locks are runner/build concurrency issues and must be reported separately from test failures.

## Rollback strategy

Rollback remains configuration-first. For Damage/death VFX:

1. Set `DamageDeathVfxExecutionMode` back to `LegacyExtension`.
2. Remove the Damage/death VFX orchestration default allowance from the production config drift guard.
3. Remove or skip the Phase 9F PlayMode smoke tests.
4. Set the Damage/death VFX readiness matrix status back to `ProductionDefaultOnTelemetryHardened`.
5. Restore default orchestration tests to controlled integration expectations.
6. Leave controlled orchestration integration code in place unless a domain-specific regression requires a separate rollback.

For Core gameplay SFX:

1. Set `CoreGameplaySfxExecutionMode` back to `LegacyGameplayAudioController`.
2. Keep duplicate guards and diagnostics enabled.
3. Remove the Core gameplay SFX orchestration default allowance from the production config drift guard.
4. Remove or skip the Phase 9D PlayMode smoke and set the readiness matrix status back to `ProductionDefaultOnTelemetryHardened`.
5. Restore default orchestration tests to their legacy default expectations.
6. Leave controlled integration code in place unless a domain-specific regression requires a separate rollback.

For Box motion Phase 9H production default switch:

1. Set the normal production Box motion execution mode back to `LegacyTrackPlanner`.
2. Remove the Box motion orchestration default allowance from the production config drift guard.
3. Set the Box motion readiness matrix status back to `CandidateForNextPR`.
4. Restore default orchestration tests to Phase 9I controlled-mode expectations.
5. Restore actual scene bootstrap expected default to `LegacyTrackPlanner`.
6. Keep `OrchestrationMotionExecutor`, Phase 9I smoke fixtures, `GameplayTrackPlanner`, `PresentationMotionTrack`, and `BoxFlipInteractionDriver` paths available.
7. Keep Core gameplay SFX and Damage/death VFX production defaults unless they have a separate regression.

## Explicit non-goals

- Do not switch any additional production default beyond Box motion in Phase 9H.
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
